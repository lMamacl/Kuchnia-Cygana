namespace KuchniaUCygana.Infrastructure.ExternalServices.Maps;

public interface IGeocodeService
{
    Task<(double Latitude, double Longitude)> GeocodeAsync(string address);
}
