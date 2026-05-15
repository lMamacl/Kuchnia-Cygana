using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using ServiceStack.AI;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Logistics;

/// <summary>
/// Rejestrowanie każdego "przekazania" torby 
/// (np. Magazyn -> Kierowca -> Klient -> Kierowca)
/// </summary>

public sealed class BagMovementLog : BaseEntity
{
    public int ThermalBagId { get; set; }

    public BagStatus FromStatus { get; set; }

    public BagStatus ToStatus { get; set; }

    private int? DriverId;

    private int? RouteStopId;

    public void UpdateStatus(BagStatus newStatus)
    {
        this.FromStatus = this.ToStatus;
        this.ToStatus = newStatus;
    }
}

