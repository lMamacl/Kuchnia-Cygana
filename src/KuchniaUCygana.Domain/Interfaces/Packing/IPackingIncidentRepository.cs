using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Interfaces.Packing;

public interface IPackingIncidentRepository : IRepository<PackingIncident>
{
    Task<IReadOnlyList<PackingIncident>> SearchAsync(
        DateOnly? date,
        PackingIncidentStatus? status,
        PackingIncidentType? type,
        string? clientPublicId,
        int? deliveryCalendarId,
        string? search = null);

    Task<PackingIncidentSearchResult> SearchPageAsync(PackingIncidentSearchQuery query);

    Task<IReadOnlyList<PackingIncident>> SearchByDeliveryCalendarIdsAsync(IEnumerable<int> deliveryCalendarIds);

    Task<IReadOnlyList<PackingIncident>> GetKitchenReworkAsync(DateOnly? date);
}

public sealed record PackingIncidentSearchQuery(
    DateOnly? Date,
    PackingIncidentStatus? Status,
    PackingIncidentType? Type,
    string? ClientPublicId,
    int? DeliveryCalendarId,
    string? Search,
    int Page,
    int PageSize);

public sealed class PackingIncidentSearchResult
{
    public IReadOnlyList<PackingIncident> Items { get; init; } = Array.Empty<PackingIncident>();

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}
