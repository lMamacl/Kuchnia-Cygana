using System;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Infrastructure.Pdf;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

/// <summary>
/// Kontroler magazynu — stany, przyjęcia, odpisy, inwentaryzacja, temperatury, alerty.
/// TASK-M3-025 | Stanowisko: Warehouse | Szef: WarehouseManager
/// </summary>
[Authorize(Roles = "Warehouse,WarehouseManager,Admin")]
[Route("warehouse")]
public sealed class WarehouseController : Controller
{
    private readonly IWarehouseService warehouseService;
    private readonly ITemperatureService temperatureService;
    private readonly IPdfGenerator pdfGenerator;

    public WarehouseController(
        IWarehouseService warehouseService,
        ITemperatureService temperatureService,
        IPdfGenerator pdfGenerator)
    {
        this.warehouseService = warehouseService;
        this.temperatureService = temperatureService;
        this.pdfGenerator = pdfGenerator;
    }

    // ── Stany magazynowe ─────────────────────────────────────

    /// <summary>
    /// Widok główny magazynu — przegląd stanów z alertami.
    /// GET /warehouse
    /// </summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var alerts = await warehouseService.GetSmartAlertsAsync();
        
        var filter = new StockTableFilterDto { Page = 1, PageSize = 15 };
        var stockItems = await warehouseService.GetStockTableAsync(filter);

        ViewData["CurrentPage"] = filter.Page;
        ViewData["PageSize"] = filter.PageSize;
        ViewData["TotalCount"] = filter.TotalCount;
        ViewData["TotalPages"] = (int)Math.Ceiling((double)filter.TotalCount / filter.PageSize);

        var viewModel = new WarehouseDashboardViewModel
        {
            Alerts = alerts,
            StockItems = stockItems
        };

        return View(viewModel);
    }

    /// <summary>
    /// Pobiera listę składników z filtrowaniem i paginacją (HTMX).
    /// GET /warehouse/stock-table
    /// </summary>
    [HttpGet("stock-table")]
    public async Task<IActionResult> StockTable(StockTableFilterDto filter)
    {
        var items = await warehouseService.GetStockTableAsync(filter);

        ViewData["CurrentPage"] = filter.Page;
        ViewData["PageSize"] = filter.PageSize;
        ViewData["TotalCount"] = filter.TotalCount;
        ViewData["TotalPages"] = (int)Math.Ceiling((double)filter.TotalCount / filter.PageSize);

        return PartialView("_StockTablePartial", items);
    }

    /// <summary>
    /// Endpoint HTMX — lista alertów (partial).
    /// GET /warehouse/alerts
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts()
    {
        var alerts = await warehouseService.GetSmartAlertsAsync();
        return PartialView("_AlertsPartial", alerts);
    }

    // ── Przyjęcie dostawy ────────────────────────────────────

    /// <summary>
    /// Formularz przyjęcia dostawy.
    /// GET /warehouse/receive
    /// </summary>
    [HttpGet("receive")]
    public async Task<IActionResult> Receive()
    {
        ViewBag.StockItems = await warehouseService.GetStockOverviewAsync();
        return View(new ReceiveDeliveryRequest());
    }

    /// <summary>
    /// Przetwarza przyjęcie dostawy — tworzy nową partię.
    /// POST /warehouse/receive
    /// </summary>
    [HttpPost("receive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(ReceiveDeliveryRequest request, string actionType)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.StockItems = await warehouseService.GetStockOverviewAsync();
            return View(request);
        }

        var batch = await warehouseService.ReceiveDeliveryAsync(request);
        TempData["Success"] = $"Dostawa przyjęta. Partia #{batch.Id} ({batch.CurrentQuantity}) zarejestrowana.";

        if (actionType == "addAnother")
        {
            return RedirectToAction(nameof(Receive));
        }

        return RedirectToAction(nameof(Index));
    }

    // ── Wydanie ręczne ───────────────────────────────────────

    /// <summary>
    /// Formularz ręcznego wydania składnika.
    /// GET /warehouse/issue
    /// </summary>
    [HttpGet("issue")]
    public async Task<IActionResult> Issue()
    {
        ViewBag.StockItems = await warehouseService.GetStockOverviewAsync();
        return View(new ManualIssueRequest());
    }

    /// <summary>
    /// Przetwarza ręczne wydanie składnika.
    /// POST /warehouse/issue
    /// </summary>
    [HttpPost("issue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Issue(ManualIssueRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.StockItems = await warehouseService.GetStockOverviewAsync();
            return View(request);
        }

        try
        {
            await warehouseService.IssueManualAsync(request);
            TempData["Success"] = "Wydano składnik z magazynu (zdjęto wg FEFO).";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.StockItems = await warehouseService.GetStockOverviewAsync();
            return View(request);
        }
    }

    // ── Rejestracja odpadu ───────────────────────────────────

    /// <summary>
    /// Formularz rejestracji straty/odpadu.
    /// GET /warehouse/waste
    /// </summary>
    [HttpGet("waste")]
    public async Task<IActionResult> Waste()
    {
        ViewBag.StockItems = await warehouseService.GetStockOverviewAsync();
        return View(new RegisterWasteRequest());
    }

    /// <summary>
    /// Przetwarza rejestrację odpadu — zdejmuje ze stanu wg FEFO.
    /// POST /warehouse/waste
    /// </summary>
    [HttpPost("waste")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Waste(RegisterWasteRequest request)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.StockItems = await warehouseService.GetStockOverviewAsync();
            return View(request);
        }

        await warehouseService.RegisterWasteAsync(request);
        TempData["Success"] = "Odpad zarejestrowany i zdjęty z magazynu.";
        return RedirectToAction(nameof(Index));
    }

    // ── Inwentaryzacja ───────────────────────────────────────

    /// <summary>
    /// Formularz inwentaryzacyjny — lista składników do przeliczenia.
    /// GET /warehouse/inventory
    /// </summary>
    [HttpGet("inventory")]
    public async Task<IActionResult> Inventory()
    {
        var stockItems = await warehouseService.GetStockOverviewAsync();
        return View(stockItems);
    }

    /// <summary>
    /// Przetwarza wyniki inwentaryzacji — korekty stanów.
    /// POST /warehouse/inventory
    /// </summary>
    [HttpPost("inventory")]
    [Authorize(Roles = "WarehouseManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inventory(List<StockItemAdjustment> adjustments)
    {
        if (!ModelState.IsValid)
        {
            var stockItems = await warehouseService.GetStockOverviewAsync();
            return View(stockItems);
        }

        await warehouseService.PerformInventoryAsync(adjustments);
        TempData["Success"] = $"Inwentaryzacja zakończona. Skorygowano {adjustments.Count} pozycji.";
        return RedirectToAction(nameof(Index));
    }

    // ── Temperatury / HACCP ──────────────────────────────────

    /// <summary>
    /// Widok logowania temperatur i historii.
    /// GET /warehouse/temperatures
    /// </summary>
    [HttpGet("temperatures")]
    public async Task<IActionResult> Temperatures()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var report = await temperatureService.GetHaccpReportAsync(today.AddDays(-1), today);
        ViewBag.RecentLogs = report.Readings;
        return View(new LogTemperatureRequest());
    }

    /// <summary>
    /// Loguje odczyt temperatury.
    /// POST /warehouse/temperatures
    /// </summary>
    [HttpPost("temperatures")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogTemperature(LogTemperatureRequest request, string actionType)
    {
        if (!ModelState.IsValid)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var report = await temperatureService.GetHaccpReportAsync(today.AddDays(-1), today);
            ViewBag.RecentLogs = report.Readings;
            return View("Temperatures", request);
        }

        var log = await temperatureService.LogTemperatureAsync(request);
        TempData["Success"] = $"Temperatura {log.RecordedTemperatureCelsius}°C zapisana ({log.DeviceNameOrLocation}).";

        if (actionType == "save")
        {
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(Temperatures));
    }

    /// <summary>
    /// Generuje raport HACCP za wybrany zakres dat.
    /// GET /warehouse/haccp-report?from=2026-05-01&to=2026-05-18
    /// </summary>
    [HttpGet("haccp-report")]
    [Authorize(Roles = "WarehouseManager,Admin")]
    public async Task<IActionResult> HaccpReport(DateOnly? from, DateOnly? to)
    {
        var dateFrom = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.Today);

        var report = await temperatureService.GetHaccpReportAsync(dateFrom, dateTo);

        ViewBag.DateFrom = dateFrom;
        ViewBag.DateTo = dateTo;
        return View(report);
    }

    // ── Szczegóły partii i zmiana daty ważności ─────────────────

    /// <summary>
    /// Szczegóły składnika, aktywne partie i historia zmian dat ważności.
    /// GET /warehouse/batch-details/{stockItemId}
    /// </summary>
    [HttpGet("batch-details/{stockItemId}")]
    public async Task<IActionResult> BatchDetails(int stockItemId)
    {
        var details = await warehouseService.GetStockItemDetailsWithBatchesAsync(stockItemId);
        var viewModel = new BatchDetailsViewModel { Details = details };
        return View(viewModel);
    }

    /// <summary>
    /// Pobiera historię transakcji dla składnika (HTMX lazy load).
    /// GET /warehouse/stock-item-transactions/{stockItemId}
    /// </summary>
    [HttpGet("stock-item-transactions/{stockItemId}")]
    public async Task<IActionResult> StockItemTransactions(int stockItemId)
    {
        var filter = new TransactionHistoryFilterDto { StockItemId = stockItemId };
        var transactions = await warehouseService.GetTransactionHistoryAsync(filter);
        return PartialView("_StockTransactionsPartial", transactions);
    }

    /// <summary>
    /// Pobiera modal edycji ważności partii (HTMX).
    /// GET /warehouse/edit-batch-expiry/{batchId}
    /// </summary>
    [HttpGet("edit-batch-expiry/{batchId}")]
    public async Task<IActionResult> EditBatchExpiry(int batchId)
    {
        var batchDetails = await warehouseService.GetBatchDetailsAsync(batchId);
        var request = new EditBatchExpiryRequest
        {
            BatchId = batchId,
            NewExpiryDate = batchDetails.ExpiryDate ?? DateTimeOffset.UtcNow,
            Reason = string.Empty
        };

        return PartialView("_EditBatchExpiryModal", request);
    }

    /// <summary>
    /// Zapisuje nową datę ważności partii.
    /// POST /warehouse/edit-batch-expiry/{batchId}
    /// </summary>
    [HttpPost("edit-batch-expiry/{batchId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBatchExpiry(int batchId, EditBatchExpiryRequest request)
    {
        if (request.BatchId == 0)
        {
            request.BatchId = batchId;
        }

        if (!ModelState.IsValid)
        {
            return PartialView("_EditBatchExpiryModal", request);
        }

        try
         {
             var batchDetails = await warehouseService.GetBatchDetailsAsync(request.BatchId);
             await warehouseService.EditBatchExpiryAsync(request);
             TempData["Success"] = $"Zmieniono datę ważności partii #{request.BatchId}.";
             
             if (Request.Headers.ContainsKey("HX-Request"))
             {
                 Response.Headers.Add("HX-Redirect", Url.Action("BatchDetails", new { stockItemId = batchDetails.StockItemId }));
                 return Ok();
             }

             return RedirectToAction(nameof(BatchDetails), new { stockItemId = batchDetails.StockItemId });
         }
         catch (Exception ex)
         {
             ModelState.AddModelError(string.Empty, ex.Message);
             return PartialView("_EditBatchExpiryModal", request);
         }
    }

    /// <summary>
    /// Generuje raport partii magazynowych wg zasady FEFO.
    /// GET /warehouse/fefo-report
    /// </summary>
    [HttpGet("fefo-report")]
    public async Task<IActionResult> FefoReport()
    {
        var report = await warehouseService.GetFefoReportAsync();
        return View(report);
    }

    /// <summary>
    /// Pobiera historię transakcji z filtrowaniem (HTMX friendly).
    /// GET /warehouse/transaction-history
    /// </summary>
    [HttpGet("transaction-history")]
    public async Task<IActionResult> TransactionHistory(TransactionHistoryFilterDto filter)
    {
        var transactions = await warehouseService.GetTransactionHistoryAsync(filter);
        
        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return PartialView("_StockTransactionsPartial", transactions);
        }

        ViewBag.StockItems = await warehouseService.GetStockOverviewAsync();
        ViewBag.Filter = filter;
        return View(transactions);
    }

    /// <summary>
    /// Pobiera listę aktywnych partii dla składnika w postaci opcji dropdown (HTMX).
    /// GET /warehouse/batches-for-item?stockItemId=X
    /// </summary>
    [HttpGet("batches-for-item")]
    public async Task<IActionResult> BatchesForItem(int stockItemId)
    {
        var details = await warehouseService.GetStockItemDetailsWithBatchesAsync(stockItemId);
        return PartialView("_BatchesDropdownPartial", details.Batches);
    }

    /// <summary>
    /// Pobiera dane temperatur do wykresu Chart.js (JSON).
    /// GET /warehouse/temperature-chart-data
    /// </summary>
    [HttpGet("temperature-chart-data")]
    public async Task<IActionResult> GetTemperatureChartData(string device, int days = 7)
    {
        var data = await temperatureService.GetChartDataAsync(device, days);
        return Json(data);
    }

    /// <summary>
    /// Eksportuje logi HACCP do pliku CSV.
    /// GET /warehouse/haccp-report/csv
    /// </summary>
    [HttpGet("haccp-report/csv")]
    [Authorize(Roles = "WarehouseManager,Admin")]
    public async Task<IActionResult> ExportHaccpCsv(DateOnly from, DateOnly to)
    {
        var fromOffset = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toOffset = new DateTimeOffset(to.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
        var csv = await temperatureService.ExportHaccpCsvAsync(fromOffset, toOffset);
        var fileName = $"HACCP_Raport_{from:yyyyMMdd}_{to:yyyyMMdd}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    /// <summary>
    /// Eksportuje logi HACCP do pliku PDF.
    /// GET /warehouse/haccp-report/pdf
    /// </summary>
    [HttpGet("haccp-report/pdf")]
    [Authorize(Roles = "WarehouseManager,Admin")]
    public async Task<IActionResult> ExportHaccpPdf(DateOnly from, DateOnly to)
    {
        var report = await temperatureService.GetHaccpReportAsync(from, to);
        var title = $"Raport HACCP ({from:dd.MM.yyyy} - {to:dd.MM.yyyy})";
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Okres raportu: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}");
        sb.AppendLine($"Liczba pomiarów: {report.TotalReadings}");
        sb.AppendLine($"Pomiary poza zakresem [-25, +8]°C: {report.OutOfRangeReadings}");
        sb.AppendLine();
        sb.AppendLine("Lista pomiarów:");
        sb.AppendLine("--------------------------------------------------------------------------------");
        
        foreach (var l in report.Readings)
        {
            var isAlert = l.IsOutOfRange ? "ALERT! " : "";
            sb.AppendLine($"[{l.RecordedAt:dd.MM.yyyy HH:mm:ss}] {l.DeviceNameOrLocation}: {l.RecordedTemperatureCelsius}°C ({isAlert}{l.Remarks})");
        }
        
        var pdfBytes = pdfGenerator.Generate(title, sb.ToString());
        var fileName = $"HACCP_Raport_{from:yyyyMMdd}_{to:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }

    /// <summary>
    /// Wyszukuje dynamicznie składniki po nazwie do autouzupełniania (HTMX).
    /// GET /warehouse/stock-lookup?q=...
    /// </summary>
    [HttpGet("stock-lookup")]
    public async Task<IActionResult> StockLookup(string q)
    {
        var items = await warehouseService.GetStockOverviewAsync();
        if (!string.IsNullOrEmpty(q))
        {
            items = items.Where(i => i.Name.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        return PartialView("_StockLookupPartial", items);
    }

    /// <summary>
    /// Eksportuje raport FEFO do pliku CSV.
    /// GET /warehouse/fefo-report/csv
    /// </summary>
    [HttpGet("fefo-report/csv")]
    public async Task<IActionResult> ExportFefoCsv()
    {
        var report = await warehouseService.GetFefoReportAsync();
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Skladnik;Partia;Data waznosci;Ilosc;Dni do waznosci;Status");
        
        foreach (var item in report)
        {
            var expiryStr = item.ExpiryDate?.ToString("yyyy-MM-dd") ?? "Brak";
            var daysStr = item.DaysToExpiry?.ToString() ?? "N/A";
            sb.AppendLine($"{item.StockItemName};{item.BatchNumber};{expiryStr};{item.Quantity:F2};{daysStr};{item.Status}");
        }
        
        var fileName = $"FEFO_Raport_{DateTime.Today:yyyyMMdd}.csv";
        return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", fileName);
    }

    /// <summary>
    /// Eksportuje raport FEFO do pliku PDF.
    /// GET /warehouse/fefo-report/pdf
    /// </summary>
    [HttpGet("fefo-report/pdf")]
    public async Task<IActionResult> ExportFefoPdf()
    {
        var report = await warehouseService.GetFefoReportAsync();
        var title = $"Raport FEFO - Ważność Partii ({DateTime.Today:dd.MM.yyyy})";
        
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"Liczba pozycji: {report.Count()}");
        sb.AppendLine();
        
        // Print clean monospace table header
        sb.AppendLine(string.Format("{0,-35} | {1,-15} | {2,-12} | {3,-10} | {4,-15} | {5,-10}", 
            "Skladnik", "Numer Partii", "Waznosc", "Ilosc", "Dni do wazn.", "Status"));
        sb.AppendLine(new string('-', 108));
        
        foreach (var item in report)
        {
            var name = item.StockItemName.Length > 35 ? item.StockItemName.Substring(0, 35) : item.StockItemName;
            var expiryStr = item.ExpiryDate?.ToString("dd.MM.yyyy") ?? "Brak";
            var daysStr = item.DaysToExpiry?.ToString() ?? "N/A";
            sb.AppendLine(string.Format("{0,-35} | {1,-15} | {2,-12} | {3,-10:F2} | {4,-15} | {5,-10}",
                name, item.BatchNumber, expiryStr, item.Quantity, daysStr, item.Status));
        }
        
        var pdfBytes = pdfGenerator.Generate(title, sb.ToString());
        var fileName = $"FEFO_Raport_{DateTime.Today:yyyyMMdd}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}
