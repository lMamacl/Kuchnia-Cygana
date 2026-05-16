using Microsoft.AspNetCore.Mvc;
using KuchniaUCygana.Domain.Interfaces.Logistics;

namespace KuchniaUCygana.Web.Controllers;

public class LogisticsController : Controller
{
    private readonly IGeocodeService _geocode;

    public LogisticsController(IGeocodeService geocode)
    {
        _geocode = geocode;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> TestGeocode(string address)
    {
        var result = await _geocode.GeocodeAsync(address);

        // Mapujemy wynik z ValueTuple na typ anonimowy, aby serializator JSON zadziałał prawidłowo
        return Json(new { 
            latitude = result.Latitude, 
            longitude = result.Longitude 
        });
    }
}

/*
@{ Layout = "~/Views/Shared/_Layout.cshtml"; }

<div class="text-center">
    <h1 class="display-4">Test geokodowania</h1>
</div>

<form hx-post="/Logistics/TestGeocode" hx-target="#result" hx-swap="innerHTML">
    <input type="text" name="address" placeholder="Wpisz adres" class="form-control" />
    <button type="submit" class="btn btn-primary mt-2">Geokoduj</button>
</form>

<div id="result" class="mt-3"></div>

@section Scripts {
    <script>
        document.addEventListener('htmx:responseError', function(event) {
            const errorMessage = event.detail.xhr.responseText;

            // Wyświetlanie błędu w globalnym kontenerze
            const alertsContainer = document.getElementById('htmx-alerts');
            alertsContainer.innerHTML = `
                <div class="alert alert-danger mt-3" role="alert">
                    <strong>Błąd serwera:</strong> ${errorMessage}
                </div>
            `;

            console.error('HTMX response error:', errorMessage);
        });
    </script>
}
*/