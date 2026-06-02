namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class MealVariantDto
{
    public int Id { get; set; }

    public int MealId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string VariantType { get; set; } = "Standard";

    public string Status { get; set; } = "Draft";

    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public decimal? CaloriesPer100g { get; set; }

    public decimal? ProteinPer100g { get; set; }

    public decimal? CarbohydratesPer100g { get; set; }

    public decimal? FatPer100g { get; set; }

    public decimal? FiberPer100g { get; set; }

    public string NutritionSource { get; set; } = "Aggregated";

    public string? NutritionOverrideReason { get; set; }

    public bool AllergensApproved { get; set; }

    public string? AllergenOverrideReason { get; set; }

    public bool IsComplete { get; set; }

    public List<string> ValidationWarnings { get; set; } = new();

    public List<MealVariantComponentDto> Components { get; set; } = new();

    public List<string> Allergens { get; set; } = new();
}

public sealed class MealVariantComponentDto
{
    public int Id { get; set; }

    public int MealVariantId { get; set; }

    public int RecipeComponentId { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public string VersionStatus { get; set; } = string.Empty;

    public string? Role { get; set; }

    public decimal QuantityPerServing { get; set; }

    public string Unit { get; set; } = "portion";

    public int SortOrder { get; set; }

    public bool IsOptional { get; set; }
}

public sealed class CreateMealVariantRequest
{
    public int MealId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string VariantType { get; set; } = "Standard";

    public int? SourceMealVariantId { get; set; }
}
