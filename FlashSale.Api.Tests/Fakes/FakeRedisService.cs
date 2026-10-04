using System.Collections.Concurrent;
using FlashSale.Api.Services;

namespace FlashSale.Api.Tests.Fakes;

public class FakeRedisService : IRedisService
{
    private readonly ConcurrentDictionary<string, (string Value, DateTime? Expiry)> _store = new();
    private readonly object _lock = new();

    public Task<string?> GetStringAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_store.TryGetValue(key, out var entry))
            {
                if (entry.Expiry.HasValue && entry.Expiry.Value < DateTime.UtcNow)
                {
                    _store.TryRemove(key, out _);
                    return Task.FromResult<string?>(null);
                }
                return Task.FromResult<string?>(entry.Value);
            }
            return Task.FromResult<string?>(null);
        }
    }

    public Task SetStringAsync(string key, string value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var exp = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
            _store[key] = (value, exp);
            return Task.CompletedTask;
        }
    }

    public Task<long> IncrementAsync(string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (!_store.TryGetValue(key, out var entry) || (entry.Expiry.HasValue && entry.Expiry.Value < DateTime.UtcNow))
            {
                _store[key] = ("1", DateTime.UtcNow.Add(expiry));
                return Task.FromResult(1L);
            }

            var newValue = long.Parse(entry.Value) + 1;
            _store[key] = (newValue.ToString(), entry.Expiry);
            return Task.FromResult(newValue);
        }
    }

    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            return Task.FromResult(_store.TryRemove(key, out _));
        }
    }
}
