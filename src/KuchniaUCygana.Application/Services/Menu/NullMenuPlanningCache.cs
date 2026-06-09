using KuchniaUCygana.Application.Interfaces.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class NullMenuPlanningCache : IMenuPlanningCache
{
    public Task<T?> GetAsync<T>(string key)
    {
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix)
    {
        return Task.CompletedTask;
    }
}
