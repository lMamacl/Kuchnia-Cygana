namespace KuchniaUCygana.Domain.Interfaces.Logistics;

/// <summary>
/// Abstrakcja dla algorytmu optymalizacji trasy.
/// </summary>
public interface IRouteOptimizer
{
    Task<IReadOnlyList<int>> OptimizeSequenceAsync(
        IReadOnlyList<RouteOptimizationPoint> stops,
        RouteOptimizationPoint? origin = null);
}
