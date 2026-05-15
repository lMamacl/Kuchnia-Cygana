namespace KuchniaUCygana.Infrastructure.ExternalServices.Maps;

/// <summary>
/// Abstrakcja dla algorytmu optymalizacji trasy
/// </summary>

public interface IRouteOptimizer
{
    Task<List<int>> OptimizeSequenceAsync(List<Address> stops)
}