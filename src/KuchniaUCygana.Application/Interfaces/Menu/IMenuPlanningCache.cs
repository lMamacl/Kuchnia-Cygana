namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IMenuPlanningCache
{
    Task<T?> GetAsync<T>(string key);

    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);

    Task RemoveByPrefixAsync(string prefix);
}
