
namespace KuchniaUCygana.Domain.Interfaces.Logistics;

public interface IGeocodeService
{
    Task<(double Latitude, double Longitude)> GeocodeAsync(string address);
}
