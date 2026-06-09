using KuchniaUCygana.Domain.Entities.Warehouse;

namespace KuchniaUCygana.Domain.Interfaces.Warehouse;

public interface IWarehouseCategoryRepository : IRepository<WarehouseCategory>
{
    Task<IReadOnlyList<WarehouseCategory>> GetActiveOrderedAsync();

    Task<IReadOnlyList<WarehouseCategory>> GetAllOrderedAsync();

    Task<WarehouseCategory?> GetByCodeAsync(string code);

    Task<int?> ResolveActiveCategoryIdAsync(int? categoryId, string? legacyCategoryName);
}
