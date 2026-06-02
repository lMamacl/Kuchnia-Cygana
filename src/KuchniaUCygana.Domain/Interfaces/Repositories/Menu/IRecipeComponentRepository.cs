using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IRecipeComponentRepository
{
    Task<IReadOnlyList<RecipeComponentListRow>> SearchComponentsAsync(string? query);

    Task<RecipeComponentDetailRow?> GetComponentAsync(int componentId);

    Task<IReadOnlyList<RecipeComponentVersionRow>> GetComponentVersionsAsync(int componentId);

    Task<RecipeComponentVersionRow?> GetVersionAsync(int versionId);

    Task<IReadOnlyList<RecipeComponentIngredientRow>> GetVersionIngredientsAsync(int versionId);

    Task<IReadOnlyList<PackagingRequirementRow>> GetVersionPackagingAsync(int versionId);

    Task<IReadOnlyList<RecipeComponentInstructionSectionRow>> GetVersionInstructionSectionsAsync(int versionId);

    Task<IReadOnlyList<RecipeComponentVersionOptionRow>> GetPublishedVersionOptionsAsync();

    Task<IEnumerable<MealRecipeComponentDetailsRow>> GetMealComponentDetailsAsync(int mealId);

    Task<bool> HasProductionPackagingAsync(int mealId);

    Task<int> CreateComponentAsync(RecipeComponent component);

    Task<int> CreateVersionAsync(RecipeComponentVersion version, int? sourceVersionId);

    Task UpdateDraftVersionAsync(RecipeComponentVersion version);

    Task UpdatePublishedNonTechnologyAsync(int versionId, string? instructions, string? changeSummary, string reason, string? updatedBy);

    Task<int> SaveIngredientAsync(RecipeComponentIngredient ingredient);

    Task DeleteIngredientAsync(int ingredientId, string? deletedBy);

    Task<int> SavePackagingAsync(PackagingRequirement packaging);

    Task DeletePackagingAsync(int packagingRequirementId, string? deletedBy);

    Task<int> SaveInstructionSectionAsync(RecipeComponentInstructionSection section);

    Task<int> SaveInstructionStepAsync(RecipeComponentInstructionStep step);

    Task DeleteInstructionSectionAsync(int sectionId, string? deletedBy);

    Task DeleteInstructionStepAsync(int stepId, string? deletedBy);

    Task PublishVersionAsync(int versionId, string? publishedBy);

    Task AttachComponentToMealAsync(MealRecipeComponent component);
}

public sealed class RecipeComponentListRow
{
    public int Id { get; set; }

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public int PreparationTimeMinutes { get; set; }

    public bool IsActive { get; set; }

    public int VersionCount { get; set; }

    public int? LatestVersionId { get; set; }

    public int? LatestVersionNumber { get; set; }

    public string? LatestVersionStatus { get; set; }
}

public sealed class RecipeComponentDetailRow
{
    public int Id { get; set; }

    public int? CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ImageUrl { get; set; }

    public int PreparationTimeMinutes { get; set; }

    public bool IsActive { get; set; }
}

public sealed class RecipeComponentVersionRow
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

    public string NutritionSource { get; set; } = "Manual";

    public string? NutritionOverrideReason { get; set; }

    public bool AllergensApproved { get; set; }

    public string? AllergenOverrideReason { get; set; }

    public DateTimeOffset? AllergensApprovedAt { get; set; }

    public string? AllergensApprovedBy { get; set; }

    public string? ChangeSummary { get; set; }

    public bool IsTechnologyChange { get; set; } = true;

    public string? NonTechnologyChangeReason { get; set; }

    public DateTimeOffset? PublishedAt { get; set; }

    public string? PublishedBy { get; set; }
}

public sealed class RecipeComponentIngredientRow
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

public sealed class PackagingRequirementRow
{
    public int Id { get; set; }

    public string OwnerType { get; set; } = string.Empty;

    public int? MealId { get; set; }

    public int? RecipeComponentVersionId { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string ResourceName { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1.0m;

    public string Unit { get; set; } = "pcs";

    public string? ContainerRole { get; set; }

    public bool IsCustomerFacing { get; set; }
}

public sealed class RecipeComponentVersionOptionRow
{
    public int RecipeComponentVersionId { get; set; }

    public int RecipeComponentId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public int VersionNumber { get; set; }
}

public sealed class MealRecipeComponentDetailsRow
{
    public int RecipeComponentId { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public string ComponentName { get; set; } = string.Empty;

    public int VersionNumber { get; set; }

    public string VersionStatus { get; set; } = string.Empty;

    public string? Role { get; set; }

    public decimal QuantityPerServing { get; set; }

    public string Unit { get; set; } = "portion";

    public int SortOrder { get; set; }

    public string? Instructions { get; set; }

    public int? ShelfLifeHours { get; set; }

    public bool UseEarliestIngredientExpiry { get; set; }

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

public sealed class RecipeComponentInstructionSectionRow
{
    public int Id { get; set; }

    public int RecipeComponentVersionId { get; set; }

    public string? Title { get; set; }

    public int SortOrder { get; set; }

    public IReadOnlyList<RecipeComponentInstructionStepRow> Steps { get; set; } = Array.Empty<RecipeComponentInstructionStepRow>();
}

public sealed class RecipeComponentInstructionStepRow
{
    public int Id { get; set; }

    public int RecipeComponentInstructionSectionId { get; set; }

    public string StepText { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool RequiresControl { get; set; }

    public string? ControlType { get; set; }

    public decimal? ExpectedValue { get; set; }

    public string? ExpectedUnit { get; set; }

    public bool IsCritical { get; set; }
}
