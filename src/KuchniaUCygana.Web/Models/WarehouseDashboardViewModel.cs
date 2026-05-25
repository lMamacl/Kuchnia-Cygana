using System.Collections.Generic;
using KuchniaUCygana.Application.DTOs.Warehouse;

namespace KuchniaUCygana.Web.Models;

/// <summary>
/// Model widoku dla pulpitu magazynu.
/// </summary>
public sealed class WarehouseDashboardViewModel
{
    /// <summary>
    /// Lista inteligentnych alertów o stanie zapasów i ważności partii.
    /// </summary>
    public IEnumerable<InventoryAlertDto> Alerts { get; set; } = new List<InventoryAlertDto>();

    /// <summary>
    /// Zestawienie aktualnego asortymentu w magazynie.
    /// </summary>
    public IEnumerable<StockItemDto> StockItems { get; set; } = new List<StockItemDto>();
}
