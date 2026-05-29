namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// Request rejestracji straty/odpadu — zdejmuje ze stanu wg FEFO.
/// </summary>
public sealed class RegisterWasteRequest
{
    /// <summary>
    /// ID składnika magazynowego.
    /// </summary>
    public int StockItemId { get; set; }

    /// <summary>
    /// Ilość do odpisu.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Powód odpisu.
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Opcjonalny ID konkretnej partii (jeśli pusty — FEFO).
    /// </summary>
    public int? BatchId { get; set; }

    /// <summary>
    /// Dodatkowe uwagi (wymagane, jeśli powód to "Inny").
    /// </summary>
    public string Notes { get; set; } = string.Empty;
}
