namespace KuchniaUCygana.Domain.Interfaces.External;

public sealed class DietCatalogQuery
{
    public string? SearchTerm { get; set; }

    public bool IncludeInactive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;
}

public sealed class DietCatalogDto
{
    public IReadOnlyList<DietCatalogItemDto> Diets { get; set; } = Array.Empty<DietCatalogItemDto>();

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 12;

    public int TotalCount { get; set; }

    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class DietCatalogItemDto
{
    public int DietId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? ThumbnailUrl { get; set; }

    public IReadOnlyList<DietCatalogVariantDto> Variants { get; set; } = Array.Empty<DietCatalogVariantDto>();
}

public sealed class DietCatalogVariantDto
{
    public int DietVariantId { get; set; }

    public int DietId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TargetCalories { get; set; }

    public decimal PriceMultiplier { get; set; }

    public bool IsDefault { get; set; }

    public bool IsAvailable { get; set; }
}

public interface IDietCatalogProvider
{
    Task<DietCatalogDto> GetCurrentCatalogAsync(DietCatalogQuery? query = null);

    Task<DietCatalogItemDto?> GetDietAsync(int dietId);

    Task<DietCatalogVariantDto?> GetVariantAsync(int dietVariantId);
}
