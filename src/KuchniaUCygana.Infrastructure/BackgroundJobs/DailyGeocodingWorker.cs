using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using KuchniaUCygana.Application.Services.Logistics;

namespace KuchniaUCygana.Infrastructure.BackgroundJobs;

/// <summary>
/// Usługa tła odpowiedzialna za codzienne geokodowanie adresów.
/// Działa w pętli, wykonując zadanie co określony interwał (domyślnie 24 godziny).
/// </summary>
public sealed class DailyGeocodingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DailyGeocodingWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(24); // interwał wykonywania

    /// <param name="scopeFactory">Fabryka zakresów DI – pozwala tworzyć oddzielny zakres dla każdego wykonania zadania.</param>
    /// <param name="logger">Logger do rejestrowania zdarzeń (rozpoczęcie, błędy, zakończenie).</param>
    public DailyGeocodingWorker(IServiceScopeFactory scopeFactory, ILogger<DailyGeocodingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Główna metoda wykonywana przez środowisko hostingu po uruchomieniu usługi.
    /// Zawiera nieskończoną pętlę, która cyklicznie wywołuje logikę geokodowania.
    /// </summary>
    /// <param name="stoppingToken">Token anulowania – sygnalizuje zatrzymanie aplikacji.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DailyGeocodingWorker się uruchomił");

        // Nieskończona pętla – będzie działać dopóki aplikacja nie zostanie zatrzymana
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wykonaj zadanie geokodowania
                await DoWorkAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                // Logujemy błąd, ale nie przerywamy pętli – usługa ma dalej działać
                _logger.LogError(ex, "Wystąpił nieoczekiwany błąd w DailyGeocodingWorker");
            }

            // Odczekaj zadany interwał (24h) lub do momentu anulowania
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Normalne zachowanie przy anulowaniu – wychodzimy z pętli
                break;
            }
        }

        _logger.LogInformation("DailyGeocodingWorker zakończył zadanie");
    }

    /// <summary>
    /// Właściwa logika geokodowania. Tworzy oddzielny zakres DI, aby uniezależnić obiekty
    /// (np. repozytoria, serwisy) od zakresu głównego. Dzięki temu nie ma wycieków pamięci
    /// i wszystkie zależności są prawidłowo zwalniane po wykonaniu zadania.
    /// </summary>
    private async Task DoWorkAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Rozpoczynam proces geokodowania");

        // Utworzenie nowego zakresu usług – pozwala na użycie scoped services
        using (var scope = _scopeFactory.CreateScope())
        {
            // Pobranie potrzebnych serwisów z zakresu
            var orchestrator = scope.ServiceProvider.GetRequiredService<GeocodingOrchestrator>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<GeocodingOrchestrator>>();

            // Wywołanie logiki biznesowej – geokodowanie oczekujących adresów
            var processedCount = await orchestrator.ProcessPendingAddressesAsync(cancellationToken);

            logger.LogInformation("Geocoding completed. Processed {Count} addresses.", processedCount);
        } // Po opuszczeniu bloku using zakres i wszystkie utworzone w nim obiekty są zwalniane
    }
}
