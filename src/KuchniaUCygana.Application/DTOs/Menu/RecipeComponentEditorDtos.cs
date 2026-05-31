namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class RecipeComponentListItemDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public int VersionCount { get; set; }

    public int? LatestVersionId { get; set; }

    public int? LatestVersionNumber { get; set; }

    public string? LatestVersionStatus { get; set; }
}

public sealed class RecipeComponentDetailDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; }

    public List<RecipeComponentVersionSummaryDto> Versions { get; set; } = new();
}

public sealed class RecipeComponentVersionSummaryDto
{
    public int Id { get; set; }

    public int VersionNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? ChangeSummary { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public bool IsComplete { get; set; }
}

public sealed class RecipeComponentVersionDetailDto
{
    public int Id { get; set; }

    public int RecipeComponentId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Instructions { get; set; }

    public decimal YieldQuantity { get; set; } = 1.0m;

    public string YieldUnit { get; set; } = "portion";

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public decimal? CaloriesPer100g { get; set; }

    public decimal? ProteinPer100g { get; set; }

    public decimal? CarbohydratesPer100g { get; set; }

    public decimal? FatPer100g { get; set; }

    public decimal? FiberPer100g { get; set; }

    public int? ShelfLifeHours { get; set; }

    public bool UseEarliestIngredientExpiry { get; set; }

    public string? ChangeSummary { get; set; }

    public bool IsTechnologyChange { get; set; } = true;

    public string? NonTechnologyChangeReason { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }

    public bool CanEditDraft => Status == "Draft";

    public List<RecipeComponentIngredientEditDto> Ingredients { get; set; } = new();

    public List<PackagingRequirementEditDto> PackagingRequirements { get; set; } = new();

    public List<string> ValidationWarnings { get; set; } = new();

    public bool IsComplete => ValidationWarnings.Count == 0;
}

public sealed class RecipeComponentIngredientEditDto
{
    public int Id { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public int IngredientId { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string? WarehouseCategoryName { get; set; }

    public decimal WeightInGrams { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool IsOptional { get; set; }

    public string? Notes { get; set; }
}

public sealed class PackagingRequirementEditDto
{
    public int Id { get; set; }

    public string OwnerType { get; set; } = "RecipeComponentVersion";

    public int? MealId { get; set; }

    public int? RecipeComponentVersionId { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string ResourceName { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1.0m;

    public string Unit { get; set; } = "pcs";

    public string? ContainerRole { get; set; }

    public bool IsCustomerFacing { get; set; } = true;
}

public sealed class RecipeComponentVersionOptionDto
{
    public int RecipeComponentVersionId { get; set; }

    public int RecipeComponentId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public int VersionNumber { get; set; }
}

public sealed class CreateRecipeComponentRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public sealed class CreateRecipeComponentVersionRequest
{
    public int RecipeComponentId { get; set; }

    public int? SourceVersionId { get; set; }

    public string? ChangeSummary { get; set; }
}

public sealed class UpdateRecipeComponentVersionRequest
{
    public string? Instructions { get; set; }

    public decimal YieldQuantity { get; set; } = 1.0m;

    public string YieldUnit { get; set; } = "portion";

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public decimal? CaloriesPer100g { get; set; }

    public decimal? ProteinPer100g { get; set; }

    public decimal? CarbohydratesPer100g { get; set; }

    public decimal? FatPer100g { get; set; }

    public decimal? FiberPer100g { get; set; }

    public int? ShelfLifeHours { get; set; }

    public bool UseEarliestIngredientExpiry { get; set; }

    public string? ChangeSummary { get; set; }

    public bool IsTechnologyChange { get; set; } = true;

    public string? NonTechnologyChangeReason { get; set; }
}

public sealed class SaveComponentIngredientRequest
{
    public int Id { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public int IngredientId { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public decimal WeightInGrams { get; set; }

    public decimal YieldFactor { get; set; } = 1.0m;

    public bool IsOptional { get; set; }

    public string? Notes { get; set; }
}

public sealed class SavePackagingRequirementRequest
{
    public int Id { get; set; }

    public int? RecipeComponentVersionId { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string ResourceName { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1.0m;

    public string Unit { get; set; } = "pcs";

    public string? ContainerRole { get; set; }

    public bool IsCustomerFacing { get; set; } = true;
}

public sealed class AttachComponentToMealRequest
{
    public int MealId { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public string? Role { get; set; }

    public decimal QuantityPerServing { get; set; } = 1.0m;

    public string Unit { get; set; } = "portion";

    public int SortOrder { get; set; }
}
