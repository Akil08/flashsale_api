using FlashSale.Api.Data;
using FlashSale.Api.Models;
using FlashSale.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FlashSale.Api.Controllers;

[ApiController]
[Route("sales")]
public class SalesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly IRedisService _redisService;
    private readonly ILogger<SalesController> _logger;

    public SalesController(AppDbContext dbContext, IRedisService redisService, ILogger<SalesController> logger)
    {
        _dbContext = dbContext;
        _redisService = redisService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSales([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var cacheKey = string.IsNullOrEmpty(search) ? "sales:all" : $"sales:search:{search.ToLower()}";

        var cachedData = await _redisService.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedData))
        {
            _logger.LogInformation("Cache HIT for {CacheKey}", cacheKey);
            return Content(cachedData, "application/json");
        }

        _logger.LogInformation("Cache MISS for {CacheKey}. Fetching from DB.", cacheKey);

        var query = _dbContext.FlashSales.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // FUZZY SEARCH: Use trigram similarity. A threshold of 0.3 allows for slight misspellings.
            // PostgreSQL will automatically use the GIN index we created in Step 28 for this.
            // how to solve this eoror form vscode 
            /*
            'DbFunctions' does not contain a definition for 'TrigramSimilarity' and no accessible extension method 'TrigramSimilarity' accepting a first argument of type 'DbFunctions' could be found (are you missing a using directive or an assembly reference?)
            tell me :
            // EF
            */
           query = query.Where(s => 
                EF.Functions.TrigramsSimilarity(s.Name, search) > 0.3 || 
                EF.Functions.TrigramsSimilarity(s.Description, search) > 0.3);
        }

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

        var serializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var jsonData = JsonSerializer.Serialize(sales, serializerOptions);
        
        await _redisService.SetStringAsync(cacheKey, jsonData, TimeSpan.FromSeconds(10), cancellationToken);

        return Ok(sales);
    }

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

        await _redisService.DeleteAsync("sales:all", cancellationToken);

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