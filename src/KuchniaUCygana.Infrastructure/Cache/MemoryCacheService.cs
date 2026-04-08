using Microsoft.Extensions.Caching.Memory;

namespace KuchniaUCygana.Infrastructure.Cache;

public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache cache;
    private readonly HashSet<string> keys = new();

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
        keys.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix)
    {
        foreach (var key in keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            cache.Remove(key);
        }

        return Task.CompletedTask;
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        keys.Add(key);
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(10),
        };
        cache.Set(key, value, options);
        return Task.CompletedTask;
    }
}
