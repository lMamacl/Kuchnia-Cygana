using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Logistics;

/// <summary>
/// Reprezentuje pojazd w firmowej flocie.
/// Encja audytowana — śledzi twórcę i autora zmian dla celów operacyjnych.
/// </summary>

[Table("Vehicles")]
public sealed class Vehicle : AuditableEntity
{
    /// <summary>
    /// Numer rejestracyjny pojazdu (np. DWX1234).
    /// </summary>
    public string RegistrationNumber { get; set; } = string.Empty;

    /// <summary>
    /// Marka i model pojazdu (np. Fiat Ducato).
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Maksymalna ładowność pojazdu w kilogramach.
    /// </summary>
    public decimal MaxLoadKg { get; set; }

    /// <summary>
    /// Status pojazdu (aktywny, w serwisie, wycofany).
    /// </summary>
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
