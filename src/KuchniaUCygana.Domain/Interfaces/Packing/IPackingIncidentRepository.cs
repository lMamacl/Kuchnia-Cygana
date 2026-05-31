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
        int? deliveryCalendarId);

    Task<IReadOnlyList<PackingIncident>> GetKitchenReworkAsync(DateOnly? date);
}
