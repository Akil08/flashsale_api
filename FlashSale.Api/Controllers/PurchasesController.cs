using FlashSale.Api.Data;
using FlashSale.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Security.Claims;
using System.Text.Json;

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

        var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            return BadRequest(new { error = "Idempotency-Key header is required." });
        }

        var existingRecord = await _dbContext.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Key == idempotencyKey && r.UserId == buyerId, cancellationToken);

        if (existingRecord != null)
        {
            _logger.LogInformation("Idempotent replay for key {Key} by buyer {BuyerId}", idempotencyKey, buyerId);
            // what is wrong ? can u give the right code ? jsut the code plz, no talk plz 
            // Return the stored response body and status code for idempotent requests
            return StatusCode(existingRecord.StatusCode, existingRecord.ResponseBody);
            // return Content(existingRecord.ResponseBody, "application/json", (System.Net.HttpStatusCode)existingRecord.StatusCode);
        }

        var sale = await _dbContext.FlashSales
            .Select(s => new { s.Id, s.StartsAt, s.EndsAt, s.Status })
            .FirstOrDefaultAsync(s => s.Id == saleId, cancellationToken);

        if (sale == null) return NotFound(new { error = "Flash sale not found." });

        var now = DateTime.UtcNow;
        if (now < sale.StartsAt) return BadRequest(new { error = "Sale has not started yet." });
        if (now > sale.EndsAt || sale.Status == SaleStatus.Closed) return BadRequest(new { error = "Sale has ended or is closed." });

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var rowsAffected = await _dbContext.FlashSales
                .Where(s => s.Id == saleId && s.Stock > 0)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(s => s.Stock, s => s.Stock - 1)
                    .SetProperty(s => s.SoldCount, s => s.SoldCount + 1), 
                    cancellationToken);

            if (rowsAffected == 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning("Purchase rejected: Out of stock for sale {SaleId}", saleId);
                return Conflict(new { error = "Item is out of stock." });
            }

            var purchase = new Purchase
            {
                FlashSaleId = saleId,
                BuyerId = buyerId,
                CreatedAt = now
            };
            _dbContext.Purchases.Add(purchase);

            var responseObj = new { message = "Purchase successful", purchaseId = purchase.Id };
            var responseJson = JsonSerializer.Serialize(responseObj);

            var idempotencyRecord = new IdempotencyRecord
            {
                Key = idempotencyKey,
                UserId = buyerId,
                ResponseBody = responseJson,
                StatusCode = 200,
                CreatedAt = now
            };
            
            // FIX: Add BOTH entities to the context BEFORE calling SaveChangesAsync
            _dbContext.IdempotencyRecords.Add(idempotencyRecord);

            // Save BOTH entities in a single database round-trip
            await _dbContext.SaveChangesAsync(cancellationToken);

            // Commit transaction only after both are successfully saved
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation("Purchase successful for sale {SaleId} by buyer {BuyerId}", saleId, buyerId);
            return Ok(responseObj);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning("Concurrency conflict detected for sale {SaleId}", saleId);
            return Conflict(new { error = "The sale was modified by another process. Please try again." });
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == "23505")
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning("Duplicate purchase attempt blocked by DB for sale {SaleId} by buyer {BuyerId}", saleId, buyerId);
            return Conflict(new { error = "You have already purchased this item." });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}