using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services.Logistics;

/// <summary>
/// Pobiera dane, przesyła do API i zleca zapis
/// </summary>

//używa IAddressRepository i IGeocodeService
public class GeocodingOrchestrator
{

    // pola IAddressRepository i IGeocodeService

    // wstrzykiwanie w konstruktorze (DI)


    public async Task<(double Latitude, double Longitude)> GeocodeAsync(string address)
    {
        // 1. Zapytaj repozytorium, czy adres ma już koordynaty
        // 2. Jeśli tak → zwróć od razu (cache)
        // 3. Jeśli nie → wywołaj IGeocodeService
        // 4. Zapisz wynik w repozytorium
        // 5. Zwróć koordynaty

        return (0,0);
    }
}