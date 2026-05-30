namespace KuchniaUCygana.Application.DTOs.Production;

/// <summary>
/// Raport Food Cost — zagregowane zapotrzebowanie materiałowe.
/// </summary>
public sealed class FoodCostReportDto
{
    public List<FoodCostEntryDto> Entries { get; set; } = new();

    public List<FoodCostEntryDto> Shortages { get; set; } = new();

    public bool AllAvailable { get; set; }
}

/// <summary>
/// Pozycja raportu Food Cost.
/// </summary>
public sealed class FoodCostEntryDto
{
    public int IngredientId { get; set; }

    public int? StockItemId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public decimal TotalWeightGrams { get; set; }

    public decimal? AvailableInStock { get; set; }

    public decimal? Shortage { get; set; }
}
