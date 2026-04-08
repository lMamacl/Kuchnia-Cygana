namespace KuchniaUCygana.Infrastructure.ExternalServices.Maps;

public sealed class OpenStreetMapService : IGeocodeService
{
    public Task<(double Latitude, double Longitude)> GeocodeAsync(string address)
    {
        return Task.FromResult((0.0d, 0.0d));
    }
}
