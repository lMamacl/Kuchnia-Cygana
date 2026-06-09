namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status pojazdu w systemie.
/// </summary>
public enum VehicleStatus
{
    Active = 1,                // Pojazd w użytku
    Maintenance = 2,           // W serwisie
    Retired = 3,              // Wycofany
}
