using System.Collections.Generic;

namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// DTO zawierające szczegóły składnika, powiązane z nim aktywne partie oraz log zmian dat ważności.
/// </summary>
public sealed class StockItemDetailsDto
{
    public StockItemDto StockItem { get; set; } = null!;
    public List<BatchDto> Batches { get; set; } = new();
    public List<BatchExpiryChangeLogDto> ExpiryChangeLogs { get; set; } = new();
}
