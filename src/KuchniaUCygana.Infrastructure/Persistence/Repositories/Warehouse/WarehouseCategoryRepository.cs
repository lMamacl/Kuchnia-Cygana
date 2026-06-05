using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public sealed class WarehouseCategoryRepository : BaseRepository<WarehouseCategory>, IWarehouseCategoryRepository
{
    public WarehouseCategoryRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IReadOnlyList<WarehouseCategory>> GetActiveOrderedAsync()
    {
        using var db = Factory.CreateConnection();
        var items = await db.QueryAsync<WarehouseCategory>(
            """
            SELECT *
            FROM [WarehouseCategories]
            WHERE [IsActive] = 1
            ORDER BY [DisplayOrder], [Name], [Id];
            """);
        return items.ToList();
    }

    public async Task<IReadOnlyList<WarehouseCategory>> GetAllOrderedAsync()
    {
        using var db = Factory.CreateConnection();
        var items = await db.QueryAsync<WarehouseCategory>(
            """
            SELECT *
            FROM [WarehouseCategories]
            ORDER BY [DisplayOrder], [Name], [Id];
            """);
        return items.ToList();
    }

    public async Task<WarehouseCategory?> GetByCodeAsync(string code)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<WarehouseCategory>(
            """
            SELECT TOP 1 *
            FROM [WarehouseCategories]
            WHERE [Code] = @code;
            """,
            new { code });
    }

    public async Task<int?> ResolveActiveCategoryIdAsync(int? categoryId, string? legacyCategoryName)
    {
        using var db = Factory.CreateConnection();

        if (categoryId.HasValue)
        {
            var exists = await db.ExecuteScalarAsync<int>(
                """
                SELECT COUNT(1)
                FROM [WarehouseCategories]
                WHERE [Id] = @categoryId
                  AND [IsActive] = 1;
                """,
                new { categoryId });

            return exists > 0 ? categoryId : null;
        }

        if (string.IsNullOrWhiteSpace(legacyCategoryName))
        {
            return null;
        }

        return await db.ExecuteScalarAsync<int?>(
            """
            SELECT TOP 1 [Id]
            FROM [WarehouseCategories]
            WHERE [Name] = @name
              AND [IsActive] = 1;
            """,
            new { name = legacyCategoryName.Trim() });
    }
}


