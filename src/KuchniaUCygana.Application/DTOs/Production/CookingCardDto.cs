namespace KuchniaUCygana.Application.DTOs.Production;

/// <summary>
/// Karta produkcyjna (Cooking Card) — instrukcja gotowania z przeliczonymi gramaturami.
/// Złożony DTO agregujący dane z ProductionPlanItem + receptura z M2.
/// </summary>
public sealed class CookingCardDto
{
    public int PlanItemId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int DietVariantId { get; set; }

    public int PlannedQuantity { get; set; }

    public int? ProductionGroup { get; set; }

    public string? EstimatedReadyTime { get; set; }

    /// <summary>
    /// Lista składników z przeliczonymi gramaturami (na całą ilość porcji).
    /// </summary>
    public List<CookingCardIngredientDto> Ingredients { get; set; } = new();
}

/// <summary>
/// Składnik na karcie produkcyjnej — gramatura przeliczona na liczbę porcji.
/// </summary>
public sealed class CookingCardIngredientDto
{
    public int IngredientId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    /// <summary>
    /// Gramatura na 1 porcję (z receptury M2).
    /// </summary>
    public decimal WeightPerServing { get; set; }

    /// <summary>
    /// Gramatura łączna (WeightPerServing × PlannedQuantity).
    /// </summary>
    public decimal TotalWeight { get; set; }

    public bool IsOptional { get; set; }
}
