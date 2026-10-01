using FlashSale.Api.Data;
using FlashSale.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Controllers;

[ApiController]
[Route("sales")]
public class SalesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SalesController> _logger;

    public SalesController(AppDbContext dbContext, ILogger<SalesController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // --- PUBLIC ENDPOINT (No Auth Required) ---
    [HttpGet]
    public async Task<IActionResult> GetSales([FromQuery] string? search, CancellationToken cancellationToken)
    {   
       
        //    

        var query = _dbContext.FlashSales.AsQueryable();

        // Optional fuzzy search (will be upgraded to pg_trgm in Step 29)
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(s => s.Name.Contains(search) || s.Description.Contains(search));
        }

        // For now, just return all matching sales ordered by start date
        var sales = await query
            .OrderBy(s => s.StartsAt)
            .Select(s => new 
            {
                s.Id,
                s.Name,
                s.Description,
                s.Price,
                s.Stock,
                s.SoldCount,
                s.StartsAt,
                s.EndsAt,
                s.Status
            })
            .ToListAsync(cancellationToken);

        return Ok(sales);
    }

    // --- ADMIN ONLY ENDPOINTS ---
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateSale([FromBody] CreateSaleRequest request, CancellationToken cancellationToken)
    {
        if (request.Price <= 0) return BadRequest(new { error = "Price must be greater than zero." });
        if (request.Stock <= 0) return BadRequest(new { error = "Stock must be greater than zero." });
        if (request.StartsAt >= request.EndsAt) return BadRequest(new { error = "StartsAt must be before EndsAt." });

        var newSale = new Models.FlashSale
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Stock = request.Stock,
            SoldCount = 0,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Status = SaleStatus.Scheduled
        };

        _dbContext.FlashSales.Add(newSale);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Admin created new flash sale: {SaleId} - {Name}", newSale.Id, newSale.Name);

        return CreatedAtAction(nameof(GetSaleById), new { id = newSale.Id }, new 
        { 
            id = newSale.Id,
            name = newSale.Name,
            price = newSale.Price,
            stock = newSale.Stock,
            status = newSale.Status
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSaleById(Guid id, CancellationToken cancellationToken)
    {
        var sale = await _dbContext.FlashSales.FindAsync(new object[] { id }, cancellationToken);
        if (sale == null) return NotFound();
        
        return Ok(new 
        {
            sale.Id,
            sale.Name,
            sale.Description,
            sale.Price,
            sale.Stock,
            sale.SoldCount,
            sale.StartsAt,
            sale.EndsAt,
            sale.Status
        });
    }
}

public record CreateSaleRequest(
    string Name,
    string Description,
    decimal Price,
    int Stock,
    DateTime StartsAt,
    DateTime EndsAt
);