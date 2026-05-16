using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Domain.Interfaces.Logistics;

/// <summary>
/// Abstrakcja dla algorytmu optymalizacji trasy
/// </summary>

public interface IRouteOptimizer
{
    // adres pobiorę z modułu 1; odkomentuję jak złączę 1 z 4
    //Task<List<int>> OptimizeSequenceAsync(List<Address> stops);
}