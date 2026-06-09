using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Web.Models;

public sealed class MenuCatalogViewModel
{
    public string? SearchTerm { get; set; }

    public decimal BasePricePerDay { get; set; }

    public IReadOnlyList<MenuDietViewModel> Diets { get; set; } = Array.Empty<MenuDietViewModel>();

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;

    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int FirstItem => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItem => Math.Min(Page * PageSize, TotalCount);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

public sealed class MenuDietViewModel
{
    public int DietId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public string? ThumbnailUrl { get; set; }

    public IReadOnlyList<MenuDietVariantViewModel> Variants { get; set; } = Array.Empty<MenuDietVariantViewModel>();

    public MenuDietVariantViewModel? DefaultVariant
        => Variants.FirstOrDefault(v => v.IsDefault && v.IsAvailable)
           ?? Variants.FirstOrDefault(v => v.IsAvailable)
           ?? Variants.FirstOrDefault();
}

public sealed class MenuDietVariantViewModel
{
    public int DietVariantId { get; set; }

    public int DietId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TargetCalories { get; set; }

    public decimal PriceMultiplier { get; set; }

    public decimal PricePerDay { get; set; }

    public bool IsDefault { get; set; }

    public bool IsAvailable { get; set; }
}

public sealed class MenuDietDetailsViewModel
{
    public MenuDietViewModel Diet { get; set; } = new();

    public MenuDietVariantViewModel? SelectedVariant { get; set; }

    public DateOnly WeekStartDate { get; set; }

    public IReadOnlyList<DietPlanEntry> WeekPlan { get; set; } = Array.Empty<DietPlanEntry>();
}
