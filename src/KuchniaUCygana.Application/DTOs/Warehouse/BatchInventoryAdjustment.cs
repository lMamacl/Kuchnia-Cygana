namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// Pojedyncza korekta inwentaryzacyjna dla konkretnej partii magazynowej.
/// </summary>
public sealed class BatchInventoryAdjustment
{
    /// <summary>
    /// ID partii magazynowej.
    /// </summary>
    public int BatchId { get; set; }

    /// <summary>
    /// Rzeczywista ilość policzona dla tej partii.
    /// </summary>
    public decimal ActualQuantity { get; set; }

    /// <summary>
    /// Powód korekty.
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}
