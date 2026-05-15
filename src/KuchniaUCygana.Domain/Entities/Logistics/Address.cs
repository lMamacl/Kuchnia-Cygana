using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using ServiceStack.AI;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Logistics;

/// <summary>
/// Reprezentacja fizycznego adresu
/// dostawy w bazie danych
/// </summary>

public class Address : AuditableEntity
{
    public string Street {get; set; }

    public string City {get; set; }

    public string PostalCode {get; set;}

    public double? Latitude {get; set;}

    public double? Longitude {get; set;}

    public bool IsGeoCoded {get; set;} = false;

    public void UpdateCoordinates(double lat, double lng)
    {
        this.Latitude = lat;
        this.Longitude = lng;
        this.IsGeoCoded = true;
    }
}