using FlashSale.Api.Data;
using FlashSale.Api.Services;
using FlashSale.Api.Tests.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FlashSale.Api.Tests;

public class FlashSaleApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string TestDbConnectionString = 
        "Host=localhost;Database=flashsale_test_db;Username=postgres;Password=postgres";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // 1. Force environment to "Testing"
        builder.UseEnvironment("Testing");

        // 2. Configuration overrides
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestDbConnectionString);
        builder.UseSetting("ConnectionStrings:Redis", "localhost:6379"); 
        builder.UseSetting("Jwt:SecretKey", "SuperSecretKeyForTestingPurposesOnly123456789!");
        builder.UseSetting("Jwt:Issuer", "FlashSaleApi");
        builder.UseSetting("Jwt:Audience", "FlashSaleUsers");

        // 3. SILENCE TERMINAL LOG NOISE: Remove console loggers and filter out framework chatter
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders(); // Clears ASP.NET Core default console loggers
            logging.SetMinimumLevel(LogLevel.Error); // Captures only critical errors
        });

        // 4. Service Overrides
        builder.ConfigureServices(services =>
        {
            // Swap real Redis with in-memory FakeRedisService
            var redisDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IRedisService));
            if (redisDescriptor != null)
            {
                services.Remove(redisDescriptor);
            }
            services.AddScoped<IRedisService, FakeRedisService>();
            
            // NOTE: Do NOT manually remove Hangfire descriptors here to prevent DI corruption
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            await dbContext.Database.EnsureDeletedAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "3D000")
        {
            // Expected on first run when database does not exist
        }

        await dbContext.Database.MigrateAsync();
    }

    public async new Task DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            await dbContext.Database.EnsureDeletedAsync();
        }
        catch (PostgresException ex) when (ex.SqlState == "3D000")
        {
            // Ignore if already deleted
        }
    }
}