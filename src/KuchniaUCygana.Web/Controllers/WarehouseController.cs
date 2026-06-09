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
    private readonly IWarehouseCategoryService warehouseCategoryService;
    private readonly IHaccpLocationService haccpLocationService;
    private readonly IPdfGenerator pdfGenerator;

    public WarehouseController(
        IWarehouseService warehouseService,
        ITemperatureService temperatureService,
        IWarehouseCategoryService warehouseCategoryService,
        IHaccpLocationService haccpLocationService,
        IPdfGenerator pdfGenerator)
    {
        this.warehouseService = warehouseService;
        this.temperatureService = temperatureService;
        this.warehouseCategoryService = warehouseCategoryService;
        this.haccpLocationService = haccpLocationService;
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
        var alerts = await warehouseService.GetSmartAlertsAsync(limit: 15);

        var filter = new StockTableFilterDto { Page = 1, PageSize = 15 };
        var stockPage = await warehouseService.GetStockTablePageAsync(filter);
        await PopulateWarehouseCategoriesAsync();

        ApplyStockTablePagingViewData(stockPage);

        var viewModel = new WarehouseDashboardViewModel
        {
            Alerts = alerts,
            StockItems = stockPage.Items,
            StockTotalCount = stockPage.TotalCount
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
        var items = await warehouseService.GetStockTablePageAsync(filter);
        ApplyStockTablePagingViewData(items);

        return PartialView("_StockTablePartial", items.Items);
    }

    /// <summary>
    /// Endpoint HTMX — lista alertów (partial).
    /// GET /warehouse/alerts
    /// </summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts()
    {
        var alerts = await warehouseService.GetSmartAlertsAsync(limit: 100);
        return PartialView("_AlertsPartial", alerts);
    }

    // ── Przyjęcie dostawy ────────────────────────────────────

    /// <summary>
    /// Formularz przyjęcia dostawy.
    /// GET /warehouse/receive
    /// </summary>
    [HttpGet("receive")]
    public IActionResult Receive()
    {
        return View(new ReceiveDeliveryRequest { OperationKey = CreateWarehouseOperationKey("RCV") });
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
            EnsureOperationKey(request, "RCV");
            await PopulateSelectedStockItemAsync(request.StockItemId);
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
    public IActionResult Issue()
    {
        return View(new ManualIssueRequest { OperationKey = CreateWarehouseOperationKey("ISS") });
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
            EnsureOperationKey(request, "ISS");
            await PopulateSelectedStockItemAsync(request.StockItemId);
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
            EnsureOperationKey(request, "ISS");
            await PopulateSelectedStockItemAsync(request.StockItemId);
            return View(request);
        }
    }

    // ── Rejestracja odpadu ───────────────────────────────────

    /// <summary>
    /// Formularz rejestracji straty/odpadu.
    /// GET /warehouse/waste
    /// </summary>
    [HttpGet("waste")]
    public IActionResult Waste()
    {
        return View(new RegisterWasteRequest { OperationKey = CreateWarehouseOperationKey("WST") });
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
            EnsureOperationKey(request, "WST");
            await PopulateWasteSelectionAsync(request);
            return View(request);
        }

        try
        {
            await warehouseService.RegisterWasteAsync(request);
            TempData["Success"] = "Odpad zarejestrowany i zdjęty z magazynu.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            EnsureOperationKey(request, "WST");
            await PopulateWasteSelectionAsync(request);
            return View(request);
        }
    }

    // ── Inwentaryzacja ───────────────────────────────────────

    /// <summary>
    /// Formularz inwentaryzacyjny — lista składników do przeliczenia.
    /// GET /warehouse/inventory
    /// </summary>
    [HttpGet("inventory")]
    [Authorize(Roles = "WarehouseManager,Admin")]
    public async Task<IActionResult> Inventory(StockTableFilterDto filter)
    {
        EnsureInventoryDefaults(filter);
        var stockItems = await warehouseService.GetBatchInventoryPageAsync(filter);
        await PopulateWarehouseCategoriesAsync();
        ViewBag.Filter = filter;
        return View(stockItems);
    }

    [HttpGet("inventory-table")]
    [Authorize(Roles = "WarehouseManager,Admin")]
    public async Task<IActionResult> InventoryTable(StockTableFilterDto filter)
    {
        EnsureInventoryDefaults(filter);
        var stockItems = await warehouseService.GetBatchInventoryPageAsync(filter);
        ViewBag.Filter = filter;
        return PartialView("_InventoryTablePartial", stockItems);
    }

    /// <summary>
    /// Przetwarza wyniki inwentaryzacji — korekty stanów.
    /// POST /warehouse/inventory
    /// </summary>
    [HttpPost("inventory")]
    [Authorize(Roles = "WarehouseManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Inventory(List<BatchInventoryAdjustment> adjustments)
    {
        if (!ModelState.IsValid)
        {
            var stockItems = await warehouseService.GetBatchInventoryPageAsync(new StockTableFilterDto { Page = 1, PageSize = 50 });
            ViewBag.Filter = new StockTableFilterDto { Page = 1, PageSize = 50 };
            return View(stockItems);
        }

        adjustments = (adjustments ?? new List<BatchInventoryAdjustment>())
            .Where(a => a.BatchId > 0)
            .ToList();

        await warehouseService.PerformBatchInventoryAsync(adjustments);
        TempData["Success"] = $"Inwentaryzacja zakończona. Skorygowano {adjustments.Count} pozycji.";
        return RedirectToAction(nameof(Index));
    }

    // ── Temperatury / HACCP ──────────────────────────────────

    /// <summary>
    /// Przekierowanie ze starego widoku na pełny raport HACCP.
    /// GET /warehouse/temperatures
    /// </summary>
    [HttpGet("temperatures")]
    public IActionResult Temperatures()
    {
        return RedirectToAction(nameof(HaccpReport));
    }

    /// <summary>
    /// Loguje odczyt temperatury.
    /// POST /warehouse/temperatures
    /// </summary>
    [HttpPost("temperatures")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LogTemperature(LogTemperatureRequest request, string? actionType = null)
    {
        var dateFrom = DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        var dateTo = DateOnly.FromDateTime(DateTime.Today);

        if (!ModelState.IsValid)
        {
            return await HaccpReportWithLogRequestAsync(request, dateFrom, dateTo);
        }

        TemperatureLogDto log;
        try
        {
            log = await temperatureService.LogTemperatureAsync(request);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return await HaccpReportWithLogRequestAsync(request, dateFrom, dateTo);
        }
        TempData["Success"] = $"Temperatura {log.RecordedTemperatureCelsius}°C zapisana ({log.DeviceNameOrLocation}).";

        if (actionType == "save")
        {
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction(nameof(HaccpReport), new { activeTab = log.DeviceNameOrLocation });
    }

    private async Task<IActionResult> HaccpReportWithLogRequestAsync(
        LogTemperatureRequest request,
        DateOnly dateFrom,
        DateOnly dateTo)
    {
        var report = await temperatureService.GetHaccpReportAsync(dateFrom, dateTo);
        ViewBag.DateFrom = dateFrom;
        ViewBag.DateTo = dateTo;
        ViewBag.LogRequest = request;
        return View("HaccpReport", report);
    }

    /// <summary>
    /// Generuje raport HACCP za wybrany zakres dat.
    /// GET /warehouse/haccp-report?from=2026-05-01&to=2026-05-18
    /// </summary>
    [HttpGet("haccp-report")]
    public async Task<IActionResult> HaccpReport(DateOnly? from, DateOnly? to, string? activeTab = null)
    {
        var dateFrom = from ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-7));
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.Today);

        var report = await temperatureService.GetHaccpReportAsync(dateFrom, dateTo);

        ViewBag.DateFrom = dateFrom;
        ViewBag.DateTo = dateTo;
        ViewBag.ActiveTab = activeTab;

        if (ViewBag.LogRequest == null)
        {
            ViewBag.LogRequest = new LogTemperatureRequest();
        }

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
    public async Task<IActionResult> StockItemTransactions(int stockItemId, int page = 1, int pageSize = 25)
    {
        var filter = new TransactionHistoryFilterDto
        {
            StockItemId = stockItemId,
            Page = page,
            PageSize = pageSize,
        };
        var transactions = await warehouseService.GetTransactionHistoryPageAsync(filter);
        ViewBag.TransactionsTarget = "#transactions-placeholder";
        ViewBag.TransactionsPagingUrl = Url.Action(nameof(StockItemTransactions), new { stockItemId });
        return PartialView("_StockTransactionsPartial", transactions);
    }

    /// <summary>
    /// Pobiera modal edycji ważności partii (HTMX).
    /// GET /warehouse/edit-batch-expiry/{batchId}
    /// </summary>
    [HttpGet("edit-batch-expiry/{batchId}")]
    [Authorize(Roles = "WarehouseManager,Admin")]
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
    [Authorize(Roles = "WarehouseManager,Admin")]
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
    public async Task<IActionResult> FefoReport(FefoReportFilterDto filter)
    {
        var report = await warehouseService.GetFefoReportPageAsync(filter);
        ViewBag.Filter = filter;

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return PartialView("_FefoReportTablePartial", report);
        }

        return View(report);
    }

    [HttpGet("haccp-locations")]
    [Authorize(Roles = "WarehouseManager,Admin")]
    public async Task<IActionResult> HaccpLocations()
    {
        ViewBag.Categories = await warehouseCategoryService.GetActiveAsync();
        var locations = await haccpLocationService.GetAllAsync();
        return View(locations);
    }

    [HttpPost("haccp-locations")]
    [Authorize(Roles = "WarehouseManager,Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveHaccpLocation(SaveHaccpLocationRequest request)
    {
        try
        {
            await haccpLocationService.SaveAsync(request);
            TempData["Success"] = "Lokalizacja HACCP została zapisana.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(HaccpLocations));
    }

    /// <summary>
    /// Pobiera historię transakcji z filtrowaniem (HTMX friendly).
    /// GET /warehouse/transaction-history
    /// </summary>
    [HttpGet("transaction-history")]
    public async Task<IActionResult> TransactionHistory(TransactionHistoryFilterDto filter)
    {
        var transactions = await warehouseService.GetTransactionHistoryPageAsync(filter);
        ViewBag.Filter = filter;
        ViewBag.SelectedStockItem = filter.StockItemId.HasValue
            ? await warehouseService.GetStockLookupByIdAsync(filter.StockItemId.Value)
            : null;
        ViewBag.TransactionsTarget = "#transactions-table-container";
        ViewBag.TransactionsPagingUrl = Url.Action(nameof(TransactionHistory));

        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return PartialView("_StockTransactionsPartial", transactions);
        }

        return View(transactions);
    }

    /// <summary>
    /// Pobiera listę aktywnych partii dla składnika w postaci opcji dropdown (HTMX).
    /// GET /warehouse/batches-for-item?stockItemId=X
    /// </summary>
    [HttpGet("batches-for-item")]
    public async Task<IActionResult> BatchesForItem(int stockItemId)
    {
        var batches = await warehouseService.GetActiveBatchesForStockItemAsync(stockItemId);
        return PartialView("_BatchesDropdownPartial", batches);
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
    public async Task<IActionResult> StockLookup(string q, bool onlyAvailable = false)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            ViewData["LookupMessage"] = "Wpisz co najmniej 2 znaki";
            return PartialView("_StockLookupPartial", Array.Empty<StockItemDto>());
        }

        var items = await warehouseService.SearchStockLookupAsync(new StockLookupFilterDto
        {
            Query = q,
            Limit = 20,
            OnlyAvailable = onlyAvailable,
        });

        return PartialView("_StockLookupPartial", items);
    }

    /// <summary>
    /// Eksportuje raport FEFO do pliku CSV.
    /// GET /warehouse/fefo-report/csv
    /// </summary>
    [HttpGet("fefo-report/csv")]
    [Authorize(Roles = "WarehouseManager,Admin")]
    public async Task<IActionResult> ExportFefoCsv(FefoReportFilterDto filter)
    {
        var report = await warehouseService.GetFefoReportAsync(filter);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Składnik;Partia;Data ważności;Ilość;Dni do ważności;Status");

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
    [Authorize(Roles = "WarehouseManager,Admin")]
    public async Task<IActionResult> ExportFefoPdf(FefoReportFilterDto filter)
    {
        var report = await warehouseService.GetFefoReportAsync(filter);
        var title = $"Raport FEFO - Ważność Partii ({DateTime.Today:dd.MM.yyyy})";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Wygenerowano: {DateTime.Now:dd.MM.yyyy HH:mm}");
        sb.AppendLine($"Liczba pozycji: {report.Count()}");
        sb.AppendLine();

        // Print clean monospace table header
        sb.AppendLine(string.Format("{0,-35} | {1,-15} | {2,-12} | {3,-10} | {4,-15} | {5,-10}",
            "Składnik", "Numer partii", "Ważność", "Ilość", "Dni do ważn.", "Status"));
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

    private void ApplyStockTablePagingViewData(PagedResultDto<StockItemDto> page)
    {
        ViewData["CurrentPage"] = page.Page;
        ViewData["PageSize"] = page.PageSize;
        ViewData["TotalCount"] = page.TotalCount;
        ViewData["TotalPages"] = page.TotalPages;
    }

    private void EnsureInventoryDefaults(StockTableFilterDto filter)
    {
        if (filter.Page < 1)
        {
            filter.Page = 1;
        }

        if (filter.PageSize < 1 || !Request.Query.ContainsKey(nameof(StockTableFilterDto.PageSize)))
        {
            filter.PageSize = 50;
        }
    }

    private async Task PopulateWarehouseCategoriesAsync()
    {
        ViewBag.WarehouseCategories = await warehouseCategoryService.GetActiveAsync();
    }

    private async Task PopulateSelectedStockItemAsync(int stockItemId)
    {
        if (stockItemId > 0)
        {
            ViewBag.SelectedStockItem = await warehouseService.GetStockLookupByIdAsync(stockItemId);
        }
    }

    private async Task PopulateWasteSelectionAsync(RegisterWasteRequest request)
    {
        await PopulateSelectedStockItemAsync(request.StockItemId);

        if (request.StockItemId > 0)
        {
            ViewBag.SelectedBatches = await warehouseService.GetActiveBatchesForStockItemAsync(request.StockItemId);
            ViewBag.SelectedBatchId = request.BatchId;
        }
    }

    private static void EnsureOperationKey(ReceiveDeliveryRequest request, string scope)
    {
        if (string.IsNullOrWhiteSpace(request.OperationKey))
        {
            request.OperationKey = CreateWarehouseOperationKey(scope);
        }
    }

    private static void EnsureOperationKey(ManualIssueRequest request, string scope)
    {
        if (string.IsNullOrWhiteSpace(request.OperationKey))
        {
            request.OperationKey = CreateWarehouseOperationKey(scope);
        }
    }

    private static void EnsureOperationKey(RegisterWasteRequest request, string scope)
    {
        if (string.IsNullOrWhiteSpace(request.OperationKey))
        {
            request.OperationKey = CreateWarehouseOperationKey(scope);
        }
    }

    private static string CreateWarehouseOperationKey(string scope)
    {
        return $"WH-{scope}-{Guid.NewGuid():N}";
    }
}
