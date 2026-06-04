using System;
using System.Threading.Tasks;
using KuchniaUCygana.Application.DTOs.Packing;

namespace KuchniaUCygana.Application.Interfaces;

/// <summary>
/// Serwis aplikacyjny kompletacji/załadunku — obsługa załadunku toreb do aut i manifestów dostaw.
/// Wyodrębniony z PackingService w ramach Sprintu 5.4.
/// </summary>
public interface ILoadingService
{
    /// <summary>
    /// Ładuje torbę o podanym ID sesji pakowania do auta.
    /// </summary>
    Task LoadOrderBagAsync(int packingSessionId);

    /// <summary>
    /// Generuje manifest dostawy dla wybranej trasy i dnia.
    /// </summary>
    Task<PackingManifestDto> GenerateManifestAsync(
        DateOnly date,
        int routeId,
        string generatedBy,
        string? changeReason = null);

    /// <summary>
    /// Pobiera najnowszy manifest dostawy dla wybranej trasy i dnia.
    /// </summary>
    Task<PackingManifestDto?> GetManifestAsync(DateOnly date, int routeId);

    /// <summary>
    /// Weryfikuje manifest dostawy przed załadunkiem.
    /// </summary>
    Task<PackingManifestDto> VerifyManifestAsync(DateOnly date, int routeId, string verifiedBy);

    /// <summary>
    /// Wysyła dostawę (ustawia status wszystkich toreb trasy na Dispatched).
    /// </summary>
    Task DispatchAsync(DateOnly date, int routeId);

    /// <summary>
    /// Cofa stan zaladunku dla dnia albo trasy, bez usuwania tras M4 ani etykiet transportowych.
    /// </summary>
    Task<int> ResetLoadingAsync(DateOnly date, int? routeId = null);

    /// <summary>
    /// Skanuje i ładuje torbę po jej kodzie transportowym.
    /// </summary>
    Task<PackingBagDto> LoadBagByCodeAsync(int routeId, string transportCode);

    /// <summary>
    /// Pobiera szczegóły trasy w celu kontroli statusu załadunku.
    /// </summary>
    Task<PackingRouteDto?> GetRouteDetailsAsync(DateOnly date, int routeId);
}
