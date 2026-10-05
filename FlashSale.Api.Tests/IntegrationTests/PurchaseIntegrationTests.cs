using System.Net;
using System.Net.Http.Json;
using FlashSale.Api.Data;
using FlashSale.Api.Models;
using FlashSale.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FlashSale.Api.Tests.IntegrationTests;

[Collection("Integration Tests")]
public class PurchaseIntegrationTests
{
    private readonly HttpClient _client;

    // what this whole fixture does is it allows us to share the same test server and 
    // database across all the tests in this class, so we can test things like concurrency and idempotency
    // and also allows us to set up and tear down the database before and after the tests run 
    // so we can have a clean slate for each test 
    // and also allows us to override services like Redis and Hangfire with fakes for testing purposes 
    private readonly FlashSaleApiFixture _fixture;

    public PurchaseIntegrationTests(FlashSaleApiFixture fixture)
    {
        _fixture = fixture;
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task PurchaseItem_Success()
    {
        // Arrange
        var sale = await CreateTestSaleAsync(stock: 5);
        var buyerToken = await GetBuyerTokenAsync("buyer1@test.com");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/sales/{sale.Id}/purchase");
        request.Headers.Add("Authorization", $"Bearer {buyerToken}");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify stock decreased in the real database
        var updatedSale = await GetSaleFromDbAsync(sale.Id);
        updatedSale.Stock.Should().Be(4);
        updatedSale.SoldCount.Should().Be(1);
    }

    [Fact]
    public async Task PurchaseItem_DuplicateBuyer_Rejected()
    {
        // Arrange
        var sale = await CreateTestSaleAsync(stock: 5);
        var buyerToken = await GetBuyerTokenAsync("buyer2@test.com");

        // First purchase succeeds
        var request1 = new HttpRequestMessage(HttpMethod.Post, $"/sales/{sale.Id}/purchase");
        request1.Headers.Add("Authorization", $"Bearer {buyerToken}");
        request1.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        await _client.SendAsync(request1);

        // Second purchase with a DIFFERENT idempotency key should fail due to DB unique constraint
        var request2 = new HttpRequestMessage(HttpMethod.Post, $"/sales/{sale.Id}/purchase");
        request2.Headers.Add("Authorization", $"Bearer {buyerToken}");
        request2.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        // Act
        var response = await _client.SendAsync(request2);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict); // 409
        
        var content = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        content.Should().ContainKey("error").WhoseValue.Should().Be("You have already purchased this item.");
    }

    [Fact]
    public async Task PurchaseItem_ConcurrentRequests_PreventDoublePurchase()
    {
        // Arrange: Create a sale with ONLY 1 in stock
        var sale = await CreateTestSaleAsync(stock: 1);
        var buyerToken = await GetBuyerTokenAsync("buyer3@test.com");

        // Fire 3 concurrent requests simultaneously
        var tasks = Enumerable.Range(1, 3).Select(async i =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/sales/{sale.Id}/purchase");
            request.Headers.Add("Authorization", $"Bearer {buyerToken}");
            // Different keys to bypass the idempotency cache and force the DB constraint check
            request.Headers.Add("Idempotency-Key", $"concurrent-key-{i}"); 
            return await _client.SendAsync(request);
        });

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        var statusCodes = responses.Select(r => r.StatusCode).ToList();
        
        // Exactly ONE request should succeed (200 OK)
        statusCodes.Count(c => c == HttpStatusCode.OK).Should().Be(1);
        
        // The other TWO should be rejected by the database unique constraint (409 Conflict)
        statusCodes.Count(c => c == HttpStatusCode.Conflict).Should().Be(2);

        // CRITICAL: Verify final stock is exactly 0, NOT -1 or -2
        var updatedSale = await GetSaleFromDbAsync(sale.Id);
        updatedSale.Stock.Should().Be(0);
        updatedSale.SoldCount.Should().Be(1);
    }

    // --- Test Helpers ---

    private async Task<FlashSale.Api.Models.FlashSale> CreateTestSaleAsync(int stock)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var sale = new FlashSale.Api.Models.FlashSale 
        {
            Name = "Test Sale",
            Description = "Test",
            Price = 10.00m,
            Stock = stock,
            SoldCount = 0,
            StartsAt = DateTime.UtcNow.AddDays(-1), // Already started
            EndsAt = DateTime.UtcNow.AddDays(1),    // Not ended
            Status = SaleStatus.Open
        };

        db.FlashSales.Add(sale);
        await db.SaveChangesAsync();
        return sale;
    }

    private async Task<string> GetBuyerTokenAsync(string email)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure user exists in the test database
        if (!await db.Users.AnyAsync(u => u.Email == email))
        {
            db.Users.Add(new User
            {
                Email = email,
                PasswordHash = "test_hash", // Doesn't matter, we generate token directly
                Role = UserRole.Buyer
            });
            await db.SaveChangesAsync();
        }

        // Generate a valid JWT using the real IJwtService registered in the app
        var user = await db.Users.FirstAsync(u => u.Email == email);
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtService>();
        return jwtService.GenerateToken(user);
    }

    private async Task<FlashSale.Api.Models.FlashSale> GetSaleFromDbAsync(Guid id)
    {
        using var scope = _fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.FlashSales.FirstAsync(s => s.Id == id);
    }
}