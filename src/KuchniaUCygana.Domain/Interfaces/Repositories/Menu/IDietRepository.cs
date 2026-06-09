using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IDietRepository : IRepository<Diet>
{
    Task<IEnumerable<Diet>> GetActiveWithVariantsAsync();

    Task<IReadOnlyList<ActiveDietVariantRow>> GetActiveDietVariantRowsAsync();
}

public sealed class ActiveDietVariantRow
{
    public int DietId { get; set; }

    public string DietName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? MarketingDescription { get; set; }

    public string Status { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? ThumbnailUrl { get; set; }

    public int? DietVariantId { get; set; }

    public string? VariantName { get; set; }

    public int TargetCalories { get; set; }

    public decimal PriceMultiplier { get; set; }

    public bool IsDefault { get; set; }
}
