using KuchniaUCygana.Application.DTOs.Packing;

namespace KuchniaUCygana.Application.Interfaces;

public interface IPackingIncidentService
{
    Task<PackingIncidentDto> CreateItemIssueAsync(CreatePackingItemIssueRequest request);

    Task<PackingIncidentDto> CreateBagIssueAsync(CreatePackingBagIssueRequest request);

    Task<IReadOnlyList<PackingIncidentDto>> SearchAsync(PackingIncidentFilterDto filter);

    Task<PackingIncidentPageDto> SearchPageAsync(PackingIncidentFilterDto filter);

    Task<IReadOnlyList<PackingIncidentDto>> SearchByDeliveryCalendarIdsAsync(IEnumerable<int> deliveryCalendarIds);

    Task<IReadOnlyList<PackingIncidentDto>> GetKitchenReworkAsync(DateOnly? date);

    Task AssignToCurrentUserAsync(int incidentId);

    Task AddAdminNoteAsync(HandlePackingIncidentRequest request);

    Task RegisterWasteAsync(RegisterIncidentWasteRequest request);

    Task RequestKitchenReworkAsync(HandlePackingIncidentRequest request);

    Task MarkKitchenInProgressAsync(HandlePackingIncidentRequest request);

    Task MarkKitchenPreparedAsync(HandlePackingIncidentRequest request);

    Task ResolveAsync(ResolvePackingIncidentRequest request);
}
