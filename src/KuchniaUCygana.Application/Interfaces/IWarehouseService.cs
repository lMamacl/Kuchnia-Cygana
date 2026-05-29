using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Application.Interfaces;

/// <summary>
/// Serwis aplikacyjny magazynu — przyjęcia, odpisy, inwentaryzacja, alerty.
/// </summary>
public interface IWarehouseService
{
    Task<BatchDto> ReceiveDeliveryAsync(ReceiveDeliveryRequest request);

    Task RegisterWasteAsync(RegisterWasteRequest request);

    Task PerformInventoryAsync(IEnumerable<StockItemAdjustment> adjustments);

    Task<IEnumerable<InventoryAlertDto>> GetSmartAlertsAsync();

    Task<IEnumerable<StockItemDto>> GetStockOverviewAsync();

    /// <summary>
    /// Pobiera ręcznie składnik z magazynu (odpis ilościowy).
    /// </summary>
    Task IssueManualAsync(ManualIssueRequest request);

    /// <summary>
    /// Zmienia datę ważności partii składnika i rejestruje log zmian.
    /// </summary>
    Task EditBatchExpiryAsync(EditBatchExpiryRequest request);

    /// <summary>
    /// Pobiera szczegółowe informacje o partii składnika.
    /// </summary>
    Task<BatchDetailsDto> GetBatchDetailsAsync(int batchId);

    /// <summary>
    /// Generuje raport partii wg zasady FEFO (First Expired First Out).
    /// </summary>
    Task<IEnumerable<FefoReportItemDto>> GetFefoReportAsync();

    /// <summary>
    /// Pobiera historię transakcji z możliwością filtrowania.
    /// </summary>
    Task<IEnumerable<TransactionHistoryDto>> GetTransactionHistoryAsync(TransactionHistoryFilterDto filter);

    /// <summary>
    /// Pobiera listę składników z magazynu z filtrowaniem i paginacją pod HTMX.
    /// </summary>
    Task<IEnumerable<StockItemDto>> GetStockTableAsync(StockTableFilterDto filter);

    /// <summary>
    /// Pobiera szczegóły składnika, listę jego partii oraz logi zmian dat ważności.
    /// </summary>
    Task<StockItemDetailsDto> GetStockItemDetailsWithBatchesAsync(int stockItemId);
}
