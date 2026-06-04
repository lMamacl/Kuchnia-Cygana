using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Entities.Logistics;

/// <summary>
/// Reprezentacja pracownika biurowego odpowiedzialnego
/// za nadzór nad flotą, planowanie tras i reagowanie na
/// sytuacje awaryjne.
/// </summary>


[System.ComponentModel.DataAnnotations.Schema.Table("Dispatchers")]
public sealed class Dispatcher : AuditableEntity
{
    public int UserId { get; set; }

    public string DeskPhoneNumber { get; set; } = string.Empty;

    public bool IsOnDuty { get; set; }

    public void Createroute(DateTimeOffset date)
    {
        // logika tworzenia trasy dla danego dnia
    }

    public void AssignDriver(Driver driver, DeliveryRoute route)
    {
        // logika przypisania kierowcy do trasy
    }

    public void ReassignVehicle(DeliveryRoute route, Vehicle newVehicle)
    {
        // logika przypisywania nowego pojazdu do trasy
    }
}

