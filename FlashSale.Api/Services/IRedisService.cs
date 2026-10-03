namespace FlashSale.Api.Services;

public interface IRedisService
{
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default);
    
    Task SetStringAsync(string key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    
    Task<long> IncrementAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default);
    
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
}