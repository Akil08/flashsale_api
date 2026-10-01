using FlashSale.Api.Data;
using FlashSale.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FlashSale.Api.Controllers;

[ApiController]
[Route("sales/{saleId}/purchase")]
[Authorize(Roles = "Buyer")]
public class PurchasesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PurchasesController> _logger;

    public PurchasesController(AppDbContext dbContext, ILogger<PurchasesController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> PurchaseItem(Guid saleId, CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var buyerId))
        {
            return Unauthorized(new { error = "Invalid user token." });
        }

        // 1. Fetch sale ONLY to check time rules (we no longer read Stock for the update logic)
        var sale = await _dbContext.FlashSales
            .Select(s => new { s.Id, s.StartsAt, s.EndsAt, s.Status })
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken);

        if (sale == null)
        {
            return NotFound(new { error = "Flash sale not found." });
        }

        var now = DateTime.UtcNow;
        if (now < sale.StartsAt)
        {
            return BadRequest(new { error = "Sale has not started yet." });
        }
        if (now > sale.EndsAt || sale.Status == SaleStatus.Closed)
        {
            return BadRequest(new { error = "Sale has ended or is closed." });
        }

        // 2. ATOMIC UPDATE: Let the database handle the check and decrement in one step.
        // This translates to: UPDATE "FlashSales" SET "Stock" = "Stock" - 1, "SoldCount" = "SoldCount" + 1 
        // WHERE "Id" = @saleId AND "Stock" > 0
        var rowsAffected = await _dbContext.FlashSales
            .Where(s => s.Id == saleId && s.Stock > 0)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Stock, s => s.Stock - 1)
                .SetProperty(s => s.SoldCount, s => s.SoldCount + 1), 
                cancellationToken);

        // 3. If 0 rows were affected, it means Stock was already 0 (or sale was deleted)
        if (rowsAffected == 0)
        {
            _logger.LogWarning("Purchase rejected: Out of stock for sale {SaleId}", saleId);
            return Conflict(new { error = "Item is out of stock." });
        }

        // 4. Create the purchase record
        var purchase = new Purchase
        {
            FlashSaleId = saleId,
            BuyerId = buyerId,
            CreatedAt = now
        };
        _dbContext.Purchases.Add(purchase);

        // 5. Save the purchase record
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Purchase successful for sale {SaleId} by buyer {BuyerId}", saleId, buyerId);

        return Ok(new { message = "Purchase successful", purchaseId = purchase.Id });
    }
}