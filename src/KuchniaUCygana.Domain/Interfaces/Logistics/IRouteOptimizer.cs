using KuchniaUCygana.Domain.Entities.Logistics;

namespace KuchniaUCygana.Domain.Interfaces.Logistics;

/// <summary>
/// Abstrakcja dla algorytmu optymalizacji trasy
/// </summary>

public interface IRouteOptimizer
{
    Task<List<int>> OptimizeSequenceAsync(List<Address> stops);
}