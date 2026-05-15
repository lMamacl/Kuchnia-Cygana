using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using ServiceStack.AI;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Logistics;

/// <summary>
/// Reprezentuje przystanek na trasie do którego musi dojechać kierowca
/// Encja audytowana — śledzi twórcę i autora zmian dla celów operacyjnych.
/// </summary>

public sealed class DeliveryRouteStop : AuditableEntity
{
    public int RouteId { get; set; }

    public int DeliveryCalendarId { get; set; }

    public int SequenceNumber { get; set; }

    public DateTimeOffset? PlannedArrivalTime { get; set; }

    public DateTimeOffset? ActualArrivalTime { get; set; }

    public StopStatus Status { get; set; } = StopStatus.Created;
}
