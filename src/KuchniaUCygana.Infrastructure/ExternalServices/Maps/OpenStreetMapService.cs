using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Infrastructure.ExternalServices.Maps;

public sealed class OpenStreetMapService : IGeocodeService
{
    private readonly HttpClient _httpClient;

    public OpenStreetMapService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<(double Latitude, double Longitude)> GeocodeAsync(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return (0.0d, 0.0d);
        }

        // Endpoint Nominatim API dla wyszukiwania adresów
        var requestUri = $"search?q={Uri.EscapeDataString(address)}&format=json&limit=1";
        
        try
        {
            // Pobieramy dane jako tablicę obiektów NominatimResponse
            var results = await _httpClient.GetFromJsonAsync<NominatimResponse[]>(requestUri);

            if (results is not null && results.Length > 0)
            {
                var firstMatch = results[0];
                
                // Nominatim zwraca lat i lon jako stringi, musimy je przeparsować (kropka jako separator dziesiętny)
                if (double.TryParse(firstMatch.Lat, NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) &&
                    double.TryParse(firstMatch.Lon, NumberStyles.Any, CultureInfo.InvariantCulture, out var lon))
                {
                    return (lat, lon);
                }
            }
        }
        catch (Exception)
        {
            // W środowisku produkcyjnym powinniśmy tutaj użyć ILogger, 
            // ale dla niezawodności logistyki w razie błędu zwracamy 0,0
            return (0.0d, 0.0d);
        }

        return (0.0d, 0.0d);
    }

    // Wewnętrzna klasa do deserializacji JSONa z OpenStreetMap
    private class NominatimResponse
    {
        [JsonPropertyName("lat")]
        public string Lat { get; set; } = string.Empty;

        [JsonPropertyName("lon")]
        public string Lon { get; set; } = string.Empty;
    }
}
