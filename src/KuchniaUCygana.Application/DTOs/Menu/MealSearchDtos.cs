namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class MealSearchFilterDto
{
    public string? Search { get; set; }

    public int? CategoryId { get; set; }

    public string? Status { get; set; }

    public int? AllergenId { get; set; }

    public bool MissingPublicationData { get; set; }

    public bool? HasVariants { get; set; }

    public bool MissingPackaging { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 25;
}

public sealed class MealListItemDto
{
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public string? CategoryName { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public string Status { get; set; } = string.Empty;

    public int PreparationTimeMinutes { get; set; }

    public bool IsActive { get; set; }

    public decimal? RawWeightGrams { get; set; }

    public decimal? CookedWeightGrams { get; set; }

    public decimal? CaloriesPer100g { get; set; }

    public decimal? ProteinPer100g { get; set; }

    public decimal? CarbohydratesPer100g { get; set; }

    public decimal? FatPer100g { get; set; }

    public decimal? FiberPer100g { get; set; }

    public int VariantCount { get; set; }

    public int PublishedVariantCount { get; set; }

    public int ComponentCount { get; set; }

    public int PackagingRequirementCount { get; set; }

    public string? AllergenNames { get; set; }

    public bool HasVariants => this.VariantCount > 0;

    public bool HasNutrition { get; set; }

    public bool MissingPackaging { get; set; }

    public bool MissingPublicationData { get; set; }

    public bool IsComplete => !this.MissingPublicationData && !this.MissingPackaging;
}
