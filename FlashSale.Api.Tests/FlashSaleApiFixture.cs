using FlashSale.Api.Data;
using FlashSale.Api.Services;
using FlashSale.Api.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FlashSale.Api.Tests;

public class FlashSaleApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Uses your EXISTING local Postgres, just a different database name
    private const string TestDbConnectionString = 
        "Host=localhost;Database=flashsale_test_db;Username=postgres;Password=postgres";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestDbConnectionString);

        builder.ConfigureServices(services =>
        {
            // Replace real Redis with our thread-safe in-memory fake
            var redisDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IRedisService));
            if (redisDescriptor != null)
            {
                services.Remove(redisDescriptor);
            }
            services.AddScoped<IRedisService, FakeRedisService>();

            // Remove Hangfire to prevent background workers from mutating state during tests
            var hangfireDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("Hangfire") == true)
                .ToList();
            foreach (var descriptor in hangfireDescriptors)
            {
                services.Remove(descriptor);
            }
        });
    }

    public async Task InitializeAsync()
    {
        // FIX: EnsureDeleted then Migrate to guarantee real migrations run from scratch
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public async new Task DisposeAsync()
    {
        // Clean up: drop the test database after all tests finish
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
    }
}
