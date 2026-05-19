using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using ServiceStack.AI;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Logistics;

/// <summary>
/// Reprezentuje pojazd w firmowej flocie.
/// Encja audytowana — śledzi twórcę i autora zmian dla celów operacyjnych.
/// </summary>

[Alias("Vehicles")]
public sealed class Vehicle : AuditableEntity
{
    /// <summary>
    /// Numer rejestracyjny pojazdu (np. DWX1234).
    /// </summary>
    [Required]
    public string RegistrationNumber { get; set; } = string.Empty;

    /// <summary>
    /// Marka i model pojazdu (np. Fiat Ducato).
    /// </summary>
    [Required]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Maksymalna ładowność pojazdu w kilogramach.
    /// </summary>
    [Required]
    public decimal MaxLoadKg { get; set; }

    /// <summary>
    /// Status pojazdu (aktywny, w serwisie, wycofany).
    /// </summary>
    [Required]
    public VehicleStatus Status { get; set; } = VehicleStatus.Active;

    public bool IsOperational()
    {
        return this.Status == VehicleStatus.Active;
    }

    public bool CanCarry(decimal weightKg)
    {
        return this.IsOperational() && weightKg <= this.MaxLoadKg;
    }
}
