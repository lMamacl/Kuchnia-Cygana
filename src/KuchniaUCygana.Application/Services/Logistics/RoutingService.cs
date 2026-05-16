using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Application.Services.Logistics;

/// <summary>
/// Koordynacja procesu tworzenia tras. Łączy zamówienia w paczki (batches)
/// i zleca ich optymalizację.
/// </summary>

// używa IRouteOptimizer i IDeliveryRouteRepository