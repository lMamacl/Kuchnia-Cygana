using ServiceStack.DataAnnotations;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

/// <summary>
/// Korekta inwentaryzacyjna — rejestracja różnic stanu rzeczywistego vs. systemowego.
/// </summary>
[Alias("InventoryAdjustments")]
public class InventoryAdjustment : AuditableEntity<int>
{
    [References(typeof(StockItem))]
    public int StockItemId { get; set; }

    /// <summary>
    /// Stan systemowy przed korektą.
    /// </summary>
    public decimal QuantityBefore { get; set; }

    /// <summary>
    /// Stan rzeczywisty (policzony).
    /// </summary>
    public decimal QuantityAfter { get; set; }

    /// <summary>
    /// Różnica (QuantityAfter - QuantityBefore).
    /// </summary>
    public decimal Difference { get; set; }

    /// <summary>
    /// Uzasadnienie korekty.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Kto przeprowadził inwentaryzację.
    /// </summary>
    [StringLength(50)]
    public string? AdjustedBy { get; set; }
}
