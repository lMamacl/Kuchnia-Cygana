using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using ServiceStack.AI;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Logistics;

/// <summary>
/// Reprezentacja fizycznej torby w systemie.
/// Każda torba ma swój unikalny identyfikator
/// </summary>

public sealed class ThermalBag : AuditableEntity
{
    public string SerialNumber { get; set; } = string.Empty;

    public BagStatus Status { get; set; } = BagStatus.Available;

    public int? LastCustomerId { get; set; }

    public void UpdateStatus(BagStatus newStatus)
    {
        this.Status = newStatus;
    }
}
