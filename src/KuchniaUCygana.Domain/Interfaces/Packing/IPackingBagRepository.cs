using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Interfaces.Packing;

public interface IPackingBagRepository : IRepository<PackingBag>
{
    Task<IEnumerable<PackingBag>> GetBySessionIdAsync(int packingSessionId);

    Task<IReadOnlyList<PackingBag>> GetBySessionIdsAsync(IEnumerable<int> packingSessionIds);

    Task<PackingBag?> GetByCodeAsync(string bagCode);

    Task<PackingBag?> GetDefaultForSessionAsync(int packingSessionId);

    Task<PackingBagSearchResult> SearchBoardBagsAsync(PackingBagQuery query);
}

public sealed class PackingBagQuery
{
    public DateOnly PackingDate { get; init; }

    public string? Search { get; init; }

    public int? RouteId { get; init; }

    public PackingBagStatus? Status { get; init; }

    public string? LabelState { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }
}

public sealed class PackingBagSearchResult
{
    public IReadOnlyList<PackingBagSearchRow> Bags { get; init; } = Array.Empty<PackingBagSearchRow>();

    public IReadOnlyList<PackingRouteSearchSummary> Routes { get; init; } = Array.Empty<PackingRouteSearchSummary>();

    public int TotalCount { get; init; }

    public int TotalBags { get; init; }

    public int PackedBags { get; init; }

    public int LoadedBags { get; init; }
}

public sealed class PackingRouteSearchSummary
{
    public int RouteId { get; set; }

    public string RouteName { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    public string VehicleRegistration { get; set; } = string.Empty;

    public int TotalBags { get; set; }

    public int PackedBags { get; set; }

    public int LoadedBags { get; set; }

    public int DispatchedBags { get; set; }

    public int MissingLabelBags { get; set; }

    public int UnattachedLabelBags { get; set; }
}

public sealed class PackingBagSearchRow
{
    public int PackingBagId { get; set; }

    public int PackingSessionId { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public int BagNumber { get; set; }

    public string BagCode { get; set; } = string.Empty;

    public PackingBagStatus Status { get; set; }

    public PackingStatus SessionStatus { get; set; }

    public int OrderId { get; set; }

    public string ClientName { get; set; } = string.Empty;

    public string? ClientPublicId { get; set; }

    public int DietVariantId { get; set; }

    public string Address { get; set; } = string.Empty;

    public int RouteId { get; set; }

    public string RouteName { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    public string VehicleRegistration { get; set; } = string.Empty;

    public int StopNumber { get; set; }

    public string DeliveryWindow { get; set; } = string.Empty;

    public int TotalBoxes { get; set; }

    public int PackedBoxes { get; set; }

    public int? TransportLabelId { get; set; }

    public int TransportLabelPrintNumber { get; set; }

    public DateTimeOffset? TransportLabelAttachedAt { get; set; }

    public string? TransportLabelAttachedBy { get; set; }
}
