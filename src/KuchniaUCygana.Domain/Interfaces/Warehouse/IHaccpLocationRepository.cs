using KuchniaUCygana.Domain.Entities.Warehouse;

namespace KuchniaUCygana.Domain.Interfaces.Warehouse;

public interface IHaccpLocationRepository : IRepository<HaccpLocation>
{
    Task<IReadOnlyList<HaccpLocationDetailsRow>> GetAllWithCategoriesAsync(bool activeOnly);

    Task<HaccpLocationDetailsRow?> GetDetailsByIdAsync(int id);

    Task<IReadOnlyList<WarehouseCategory>> GetCategoriesForLocationAsync(int locationId);

    Task ReplaceCategoriesAsync(int locationId, IReadOnlyCollection<int> categoryIds);
}

public sealed class HaccpLocationDetailsRow
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public decimal MinTemperatureCelsius { get; set; }

    public decimal MaxTemperatureCelsius { get; set; }

    public bool IsActive { get; set; }

    public int DisplayOrder { get; set; }

    public string? Notes { get; set; }

    public string CategoryIds { get; set; } = string.Empty;

    public string CategoryNames { get; set; } = string.Empty;
}
