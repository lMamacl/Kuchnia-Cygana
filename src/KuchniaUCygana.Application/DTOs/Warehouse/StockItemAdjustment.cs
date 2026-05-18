namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// Pojedyncza korekta inwentaryzacyjna — ilość rzeczywista dla danego składnika.
/// </summary>
public sealed class StockItemAdjustment
{
    /// <summary>
    /// ID składnika magazynowego.
    /// </summary>
    public int StockItemId { get; set; }

    /// <summary>
    /// Rzeczywista ilość (policzony stan).
    /// </summary>
    public decimal ActualQuantity { get; set; }

    /// <summary>
    /// Powód korekty.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
