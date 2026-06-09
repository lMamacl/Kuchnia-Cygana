using System.Collections.Concurrent;
using KuchniaUCygana.Application.Interfaces.Menu;
using Microsoft.Extensions.Caching.Memory;

namespace KuchniaUCygana.Infrastructure.Cache;

public sealed class MemoryCacheService : ICacheService, IMenuPlanningCache
{
    private readonly IMemoryCache cache;
    private readonly ConcurrentDictionary<string, byte> keys = new();

    public MemoryCacheService(IMemoryCache cache)
    {
        this.cache = cache;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        return Task.FromResult(cache.TryGetValue(key, out T? value) ? value : default);
    }

    public Task RemoveAsync(string key)
    {
        cache.Remove(key);
        keys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix)
    {
        foreach (var key in keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            cache.Remove(key);
            keys.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        keys.TryAdd(key, 0);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(10),
        };
        cache.Set(key, value, options);
        return Task.CompletedTask;
    }
}
