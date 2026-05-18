namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// DTO składnika magazynowego — odpowiada StockItem z Domain.
/// </summary>
public sealed class StockItemDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? BaseIngredientId { get; set; }

    public int DefaultUnitOfMeasureId { get; set; }

    public decimal MinimumLevel { get; set; }

    public int LeadTimeDays { get; set; }

    /// <summary>
    /// Sumaryczny stan z aktywnych partii (obliczany w serwisie).
    /// </summary>
    public decimal CurrentStock { get; set; }
}
