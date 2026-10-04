using FlashSale.Api.Data;
using FlashSale.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api.Services;

public class SaleStatusUpdateService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SaleStatusUpdateService> _logger;

    public SaleStatusUpdateService(IServiceProvider serviceProvider, ILogger<SaleStatusUpdateService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // This method will be called by Hangfire on a schedule
    public async Task UpdateSaleStatusesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;

        try
        {
            // 1. Find all sales that should be opened
            var toOpen = await dbContext.FlashSales
                .Where(s => s.Status == SaleStatus.Scheduled && s.StartsAt <= now)
                .ToListAsync();

            foreach (var sale in toOpen)
            {
                sale.Status = SaleStatus.Open;
            }

            // 2. Find all sales that should be closed
            var toClose = await dbContext.FlashSales
                .Where(s => s.Status == SaleStatus.Open && s.EndsAt <= now)
                .ToListAsync();

            foreach (var sale in toClose)
            {
                sale.Status = SaleStatus.Closed;
            }

            // 3. Save changes if any were made
            if (toOpen.Count > 0 || toClose.Count > 0)
            {
                await dbContext.SaveChangesAsync();
                _logger.LogInformation("Updated status for {OpenCount} sales to Open, and {CloseCount} sales to Closed.", toOpen.Count, toClose.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating sale statuses.");
            throw; // Let Hangfire know the job failed so it can retry
        }
    }
}