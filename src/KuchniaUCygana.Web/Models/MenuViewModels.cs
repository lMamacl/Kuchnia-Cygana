using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Web.Models;

public sealed class MenuCatalogViewModel
{
    public string? SearchTerm { get; set; }

    public decimal BasePricePerDay { get; set; }

    public IReadOnlyList<MenuDietViewModel> Diets { get; set; } = Array.Empty<MenuDietViewModel>();
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
