using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Application.Services.Logistics;

/// <summary>
/// Pobiera dane, przesyła do API i zleca zapis
/// </summary>
public class GeocodingOrchestrator
{
    private readonly IAddressRepository _addressRepository;
    private readonly IGeocodeService _geocodeService;
    private readonly ILogger<GeocodingOrchestrator> _logger;

    public GeocodingOrchestrator(
        IAddressRepository addressRepository,
        IGeocodeService geocodeService,
        ILogger<GeocodingOrchestrator> logger)
    {
        _addressRepository = addressRepository;
        _geocodeService = geocodeService;
        _logger = logger;
    }

    public async Task<(double Latitude, double Longitude)> GeocodeAsync(string address)
    {
        return await _geocodeService.GeocodeAsync(address);
    }

    /// <summary>
    /// Pobiera niegeokodowane adresy z bazy danych, wysyła je do serwisu geolokalizacyjnego i zapisuje wynik.
    /// </summary>
    public async Task<int> ProcessPendingAddressesAsync(CancellationToken cancellationToken)
    {
        // Używamy generycznej metody FindAsync dostępnej w IAddressRepository (dziedziczonej z IRepository)
        var pendingAddresses = await _addressRepository.FindAsync(a => a.Latitude == null || a.Longitude == null);
        int processedCount = 0;

        foreach (var address in pendingAddresses)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Proces geokodowania został przerwany (Cancellation Requested).");
                break;
            }

            try
            {
                _logger.LogInformation("Geokodowanie adresu: {FullAddress}", address.FullAddress);
                var (latitude, longitude) = await _geocodeService.GeocodeAsync(address.FullAddress);

                // OpenStreetMap w przypadku braku wyników lub błędu zwraca (0,0)
                if (latitude != 0.0d || longitude != 0.0d)
                {
                    address.Latitude = latitude;
                    address.Longitude = longitude;

                    await _addressRepository.UpdateAsync(address);
                    processedCount++;
                    _logger.LogInformation("Pomyślnie zaktualizowano adres ID {Id} (Lat: {Lat}, Lon: {Lon}).", address.Id, latitude, longitude);
                }
                else
                {
                    _logger.LogWarning("Nie udało się odnaleźć koordynatów dla adresu ID {Id}: '{FullAddress}'", address.Id, address.FullAddress);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wystąpił błąd podczas geokodowania adresu ID {Id}", address.Id);
            }

            // Ograniczenie zapytań (Rate Limit) do API Nominatim (OpenStreetMap) - 1 żądanie na sekundę
            try
            {
                await Task.Delay(1010, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Zakończono proces geokodowania. Pomyślnie przetworzono: {Count} adresów.", processedCount);
        return processedCount;
    }
}