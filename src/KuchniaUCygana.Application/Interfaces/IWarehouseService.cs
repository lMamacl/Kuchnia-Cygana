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
}
