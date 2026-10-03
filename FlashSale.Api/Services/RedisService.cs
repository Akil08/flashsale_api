using StackExchange.Redis;

namespace FlashSale.Api.Services;

public class RedisService : IRedisService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    public RedisService(IConnectionMultiplexer redis)
    {
        _redis = redis;
        _db = _redis.GetDatabase();
    }

    

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await _db.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task SetStringAsync(string key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
    
        await _db.StringSetAsync(key, value, expiry, StackExchange.Redis.When.Always, StackExchange.Redis.CommandFlags.None);
     
    }

    public async Task<long> IncrementAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        // Increment the value. If it's a new key, it starts at 1.
        var result = await _db.StringIncrementAsync(key);
        
        // FIX: Only set the expiry on the VERY FIRST increment. 
        // This ensures the rate-limit window has a fixed start time and doesn't extend indefinitely.
        if (result == 1)
        {
            await _db.KeyExpireAsync(key, expiry);
        }
        
        return result;
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _db.KeyDeleteAsync(key);
    }
}