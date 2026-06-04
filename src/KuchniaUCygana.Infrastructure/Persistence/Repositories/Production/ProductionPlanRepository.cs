using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Production;

/// <summary>
/// Repozytorium ProductionPlan z eager loading pozycji.
/// </summary>
public sealed class ProductionPlanRepository : BaseRepository<ProductionPlan>, IProductionPlanRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductionPlanRepository(IDbConnectionFactory connectionFactory)
        : base(connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    /// <inheritdoc />
    public async Task<ProductionPlan?> GetByDateAsync(DateOnly date)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<ProductionPlan>(
            """
            SELECT TOP 1 *
            FROM [ProductionPlans]
            WHERE [ProductionDate] = @date
              AND [IsDeleted] = 0;
            """,
            new { date });
    }

    /// <inheritdoc />
    public async Task<ProductionPlan?> GetWithItemsAsync(int planId)
    {
        using var db = _connectionFactory.CreateConnection();
        var plan = await db.QuerySingleOrDefaultAsync<ProductionPlan>(
            """
            SELECT TOP 1 *
            FROM [ProductionPlans]
            WHERE [Id] = @planId;
            """,
            new { planId });

        if (plan == null || plan.IsDeleted)
            return null;

        return plan;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProductionPlanItem>> GetPlanItemsAsync(int planId)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.QueryAsync<ProductionPlanItem>(
            """
            SELECT *
            FROM [ProductionPlanItems]
            WHERE [ProductionPlanId] = @planId
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { planId });
    }

    public async Task<(IReadOnlyList<ProductionPlanItem> Items, int TotalCount)> SearchPlanItemsAsync(
        ProductionPlanItemQuery query)
    {
        using var db = _connectionFactory.CreateConnection();
        var parameters = new DynamicParameters();
        var where = BuildWhereClause(query, parameters);
        var orderBy = ResolveOrderBy(query.SortBy, query.SortDescending);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var sql = $"""
        SELECT COUNT(1)
        FROM [ProductionPlanItems]
        WHERE {where};

        SELECT *
        FROM [ProductionPlanItems]
        WHERE {where}
        ORDER BY {orderBy}
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        """;

        using var multi = await db.QueryMultipleAsync(sql, parameters);
        var totalCount = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<ProductionPlanItem>()).ToList();
        return (items, totalCount);
    }

    public async Task<ProductionPlanItemSummary> GetPlanItemSummaryAsync(int planId)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.QuerySingleAsync<ProductionPlanItemSummary>(
            """
            SELECT
                COUNT(1) AS TotalItems,
                COALESCE(SUM(PlannedQuantity), 0) AS TotalPlannedQuantity,
                COALESCE(SUM(CookedQuantity), 0) AS TotalCookedQuantity,
                COALESCE(SUM(CASE WHEN [Status] = 0 THEN 1 ELSE 0 END), 0) AS PlannedItems,
                COALESCE(SUM(CASE WHEN [Status] = 1 THEN 1 ELSE 0 END), 0) AS CookingItems,
                COALESCE(SUM(CASE WHEN [Status] = 2 THEN 1 ELSE 0 END), 0) AS CookedItems,
                COALESCE(SUM(CASE WHEN [Status] = 3 THEN 1 ELSE 0 END), 0) AS FailedItems,
                COALESCE(SUM(CASE WHEN FefoDeductedAt IS NULL THEN 1 ELSE 0 END), 0) AS PendingFefoItems,
                COALESCE(SUM(CASE WHEN FefoDeductedAt IS NOT NULL THEN 1 ELSE 0 END), 0) AS FefoDeductedItems,
                COALESCE(SUM(CASE WHEN PackagingDeductedAt IS NULL THEN 1 ELSE 0 END), 0) AS PendingPackagingItems,
                COALESCE(SUM(CASE WHEN PackagingDeductedAt IS NOT NULL THEN 1 ELSE 0 END), 0) AS PackagingDeductedItems,
                COALESCE(SUM(CASE WHEN M2SnapshotJson IS NOT NULL AND LTRIM(RTRIM(M2SnapshotJson)) <> '' THEN 1 ELSE 0 END), 0) AS SnapshotItems
            FROM [ProductionPlanItems]
            WHERE ProductionPlanId = @planId
              AND IsDeleted = 0;
            """,
            new { planId });
    }

    private static string BuildWhereClause(ProductionPlanItemQuery query, DynamicParameters parameters)
    {
        var clauses = new List<string>
        {
            "[ProductionPlanId] = @PlanId",
            "[IsDeleted] = 0",
        };
        parameters.Add("PlanId", query.PlanId);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            clauses.Add(
                "([MealName] LIKE @SearchLike OR CONVERT(varchar(20), [Id]) = @SearchExact OR CONVERT(varchar(20), [MealId]) = @SearchExact OR CONVERT(varchar(20), [DietMenuPlanItemId]) = @SearchExact OR [M2SnapshotHash] = @SearchExact)");
            parameters.Add("SearchLike", $"%{search}%");
            parameters.Add("SearchExact", search);
        }

        if (query.Status.HasValue)
        {
            clauses.Add("[Status] = @Status");
            parameters.Add("Status", (int)query.Status.Value);
        }

        if (query.ProductionGroup.HasValue)
        {
            clauses.Add("[ProductionGroup] = @ProductionGroup");
            parameters.Add("ProductionGroup", query.ProductionGroup.Value);
        }

        AddNullableDateFilter(clauses, "FefoDeductedAt", query.FefoDeducted);
        AddNullableDateFilter(clauses, "PackagingDeductedAt", query.PackagingDeducted);

        if (query.HasSnapshot.HasValue)
        {
            clauses.Add(query.HasSnapshot.Value
                ? "(M2SnapshotJson IS NOT NULL AND LTRIM(RTRIM(M2SnapshotJson)) <> '')"
                : "(M2SnapshotJson IS NULL OR LTRIM(RTRIM(M2SnapshotJson)) = '')");
        }

        return string.Join(" AND ", clauses);
    }

    private static void AddNullableDateFilter(List<string> clauses, string columnName, bool? hasValue)
    {
        if (!hasValue.HasValue)
        {
            return;
        }

        clauses.Add(hasValue.Value
            ? $"[{columnName}] IS NOT NULL"
            : $"[{columnName}] IS NULL");
    }

    private static string ResolveOrderBy(string? sortBy, bool descending)
    {
        var column = sortBy?.Trim().ToLowerInvariant() switch
        {
            "meal" or "mealname" => "[MealName]",
            "planned" or "plannedquantity" => "[PlannedQuantity]",
            "cooked" or "cookedquantity" => "[CookedQuantity]",
            "status" => "[Status]",
            "group" or "productiongroup" => "[ProductionGroup]",
            "ready" or "estimatedreadytime" => "[EstimatedReadyTime]",
            _ => "[Id]",
        };

        var direction = descending ? "DESC" : "ASC";
        return column == "[Id]"
            ? $"{column} {direction}"
            : $"{column} {direction}, [Id] ASC";
    }
}
