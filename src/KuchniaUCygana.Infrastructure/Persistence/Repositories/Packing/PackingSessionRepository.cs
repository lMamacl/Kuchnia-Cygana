using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;

public sealed class PackingSessionRepository : BaseRepository<PackingSession>, IPackingSessionRepository
{
    public PackingSessionRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<IEnumerable<PackingSession>> GetActiveByDateAsync(DateOnly date)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<PackingSession>(
            """
            SELECT *
            FROM PackingSessions
            WHERE PackingDate = @date
              AND IsDeleted = 0
              AND Status <> @dispatchedStatus
            ORDER BY Id;
            """,
            new
            {
                date = date.ToDateTime(TimeOnly.MinValue),
                dispatchedStatus = (int)PackingStatus.Dispatched,
            });
    }

    public async Task<PackingSession?> GetWithItemsAsync(int sessionId)
    {
        using var db = Factory.CreateConnection();
        var session = await db.QuerySingleOrDefaultAsync<PackingSession>(
            """
            SELECT *
            FROM PackingSessions
            WHERE Id = @sessionId
              AND IsDeleted = 0;
            """,
            new { sessionId });

        if (session is null)
        {
            return null;
        }

        session.Items = (await db.QueryAsync<PackingItem>(
            """
            SELECT *
            FROM PackingItems
            WHERE PackingSessionId = @sessionId
              AND IsDeleted = 0
            ORDER BY Id;
            """,
            new { sessionId })).ToList();

        return session;
    }

    public async Task<PackingSession?> GetByDateAndOrderAsync(DateOnly date, int orderId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<PackingSession>(
            """
            SELECT *
            FROM PackingSessions
            WHERE PackingDate = @date
              AND OrderId = @orderId
              AND IsDeleted = 0;
            """,
            new
            {
                date = date.ToDateTime(TimeOnly.MinValue),
                orderId,
            });
    }

    public async Task<IEnumerable<PackingSession>> GetByDateWithItemsAsync(DateOnly date)
    {
        using var db = Factory.CreateConnection();
        var sessions = (await db.QueryAsync<PackingSession>(
            """
            SELECT ps.*
            FROM PackingSessions ps
            LEFT JOIN DeliveryRouteStops drs ON ps.DeliveryCalendarId = drs.DeliveryCalendarId AND drs.IsDeleted = 0
            WHERE ps.PackingDate = @date
              AND ps.IsDeleted = 0
            ORDER BY COALESCE(drs.RouteId, 2147483647), COALESCE(drs.SequenceNumber, 2147483647), ps.Id;
            """,
            new { date = date.ToDateTime(TimeOnly.MinValue) })).ToList();

        if (sessions.Count == 0)
        {
            return sessions;
        }

        var items = (await db.QueryAsync<PackingItem>(
            """
            SELECT *
            FROM PackingItems
            WHERE PackingSessionId IN @sessionIds
              AND IsDeleted = 0
            ORDER BY PackingSessionId, Id;
            """,
            new { sessionIds = sessions.Select(s => s.Id).ToArray() })).ToList();

        var itemsBySession = items
            .GroupBy(i => i.PackingSessionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var session in sessions)
        {
            session.Items = itemsBySession.TryGetValue(session.Id, out var sessionItems)
                ? sessionItems
                : new List<PackingItem>();
        }

        return sessions;
    }

    public async Task<IEnumerable<PackingItem>> GetSessionItemsAsync(int sessionId)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<PackingItem>(
            """
            SELECT *
            FROM PackingItems
            WHERE PackingSessionId = @sessionId
              AND IsDeleted = 0
            ORDER BY Id;
            """,
            new { sessionId });
    }

    public async Task<IEnumerable<PackingLabel>> GetLabelsBySessionAsync(int sessionId)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<PackingLabel>(
            """
            SELECT l.*
            FROM PackingLabels l
            LEFT JOIN PackingItems i ON i.Id = l.PackingItemId
            WHERE l.PackingSessionId = @sessionId
               OR i.PackingSessionId = @sessionId
            ORDER BY l.LabelType DESC, l.Id;
            """,
            new { sessionId });
    }

    public async Task<PackingLabel?> GetShippingLabelAsync(int sessionId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<PackingLabel>(
            """
            SELECT TOP 1 *
            FROM PackingLabels
            WHERE PackingSessionId = @sessionId
              AND LabelType = @labelType
            ORDER BY PrintNumber DESC, Id DESC;
            """,
            new
            {
                sessionId,
                labelType = (int)LabelType.Shipping,
            });
    }

    public async Task<PackingLabel?> GetProductLabelAsync(int packingItemId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<PackingLabel>(
            """
            SELECT TOP 1 *
            FROM PackingLabels
            WHERE PackingItemId = @packingItemId
              AND LabelType = @labelType
            ORDER BY Id DESC;
            """,
            new
            {
                packingItemId,
                labelType = (int)LabelType.Product,
            });
    }

    public async Task<IEnumerable<string>> GetMealIngredientsAsync(int mealId)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<string>(
            """
            SELECT i.Name
            FROM [Recipes] r
            INNER JOIN [Ingredients] i ON i.Id = r.IngredientId
            WHERE r.MealId = @mealId;
            """,
            new { mealId });
    }

    public async Task<IEnumerable<string>> GetMealAllergensAsync(int mealId)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<string>(
            """
            SELECT a.Name
            FROM [MealAllergens] ma
            INNER JOIN [Allergens] a ON a.Id = ma.AllergenId
            WHERE ma.MealId = @mealId;
            """,
            new { mealId });
    }

    public async Task<int?> GetMealCaloriesAsync(int mealId)
    {
        using var db = Factory.CreateConnection();
        var cal = await db.QuerySingleOrDefaultAsync<decimal?>(
            """
            SELECT TOP 1 CaloriesPer100g
            FROM [NutritionFacts]
            WHERE MealId = @mealId;
            """,
            new { mealId });
        return cal.HasValue ? (int)Math.Round(cal.Value) : null;
    }

    public async Task<(IReadOnlyList<PackingItemSearchRow> Items, int TotalCount)> SearchPackingItemsAsync(
        PackingItemQuery query)
    {
        using var db = Factory.CreateConnection();
        var parameters = new DynamicParameters();
        var where = BuildPackingItemsWhereClause(query, parameters);
        var orderBy = ResolvePackingItemsOrderBy(query.SortBy, query.SortDescending);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var sql = $"""
        WITH LabelCounts AS (
            SELECT
                PackingItemId,
                COUNT(1) AS ProductLabelPrintCount,
                MAX(PrintedAt) AS LatestProductLabelPrintedAt
            FROM BoxLabels
            GROUP BY PackingItemId
        )
        SELECT COUNT(1)
        FROM PackingItems pi
        INNER JOIN PackingSessions ps ON ps.Id = pi.PackingSessionId
        LEFT JOIN DeliveryRouteStops drs ON ps.DeliveryCalendarId = drs.DeliveryCalendarId AND drs.IsDeleted = 0
        LEFT JOIN LabelCounts labels ON labels.PackingItemId = pi.Id
        WHERE {where};

        WITH LabelCounts AS (
            SELECT
                PackingItemId,
                COUNT(1) AS ProductLabelPrintCount,
                MAX(PrintedAt) AS LatestProductLabelPrintedAt
            FROM BoxLabels
            GROUP BY PackingItemId
        )
        SELECT
            pi.Id,
            pi.PackingSessionId,
            pi.PackingBagId,
            pi.ProductionPlanItemId,
            pi.MealId,
            pi.MealName,
            pi.DietVariantId,
            pi.BoxCode,
            pi.Status,
            pi.FoilPrintedAt,
            pi.PackedAt,
            pi.IsDamaged,
            pi.Remarks,
            ps.OrderId,
            ps.DeliveryCalendarId,
            ps.ClientName,
            ps.ClientPublicId,
            drs.RouteId,
            drs.SequenceNumber AS StopNumber,
            COALESCE(labels.ProductLabelPrintCount, 0) AS ProductLabelPrintCount,
            labels.LatestProductLabelPrintedAt
        FROM PackingItems pi
        INNER JOIN PackingSessions ps ON ps.Id = pi.PackingSessionId
        LEFT JOIN DeliveryRouteStops drs ON ps.DeliveryCalendarId = drs.DeliveryCalendarId AND drs.IsDeleted = 0
        LEFT JOIN LabelCounts labels ON labels.PackingItemId = pi.Id
        WHERE {where}
        ORDER BY {orderBy}
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        """;

        using var multi = await db.QueryMultipleAsync(sql, parameters);
        var totalCount = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<PackingItemSearchRow>()).ToList();
        return (items, totalCount);
    }

    public async Task<FoilLabelSummary> GetFoilLabelSummaryAsync(DateOnly date)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleAsync<FoilLabelSummary>(
            """
            WITH LabelCounts AS (
                SELECT
                    PackingItemId,
                    COUNT(1) AS ProductLabelPrintCount
                FROM BoxLabels
                GROUP BY PackingItemId
            )
            SELECT
                COUNT(1) AS TotalBoxes,
                COALESCE(SUM(CASE WHEN pi.Status = 0 THEN 1 ELSE 0 END), 0) AS PendingCount,
                COALESCE(SUM(CASE WHEN pi.Status IN (1, 2) OR pi.FoilPrintedAt IS NOT NULL OR COALESCE(labels.ProductLabelPrintCount, 0) > 0 THEN 1 ELSE 0 END), 0) AS PrintedCount,
                COALESCE(SUM(CASE WHEN COALESCE(labels.ProductLabelPrintCount, 0) > 1 THEN 1 ELSE 0 END), 0) AS ReprintCount,
                COALESCE(SUM(CASE WHEN pi.Status IN (3, 4) OR pi.IsDamaged = 1 THEN 1 ELSE 0 END), 0) AS BlockedCount,
                COALESCE(SUM(CASE WHEN pi.Status = 2 THEN 1 ELSE 0 END), 0) AS PackedCount
            FROM PackingItems pi
            INNER JOIN PackingSessions ps ON ps.Id = pi.PackingSessionId
            LEFT JOIN LabelCounts labels ON labels.PackingItemId = pi.Id
            WHERE ps.PackingDate = @date
              AND ps.IsDeleted = 0
              AND pi.IsDeleted = 0;
            """,
            new { date = date.ToDateTime(TimeOnly.MinValue) });
    }

    private static string BuildPackingItemsWhereClause(PackingItemQuery query, DynamicParameters parameters)
    {
        var clauses = new List<string>
        {
            "ps.PackingDate = @PackingDate",
            "ps.IsDeleted = 0",
            "pi.IsDeleted = 0",
        };
        parameters.Add("PackingDate", query.PackingDate.ToDateTime(TimeOnly.MinValue));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            clauses.Add(
                "(pi.MealName LIKE @SearchLike OR pi.BoxCode LIKE @SearchLike OR ps.ClientName LIKE @SearchLike OR ps.ClientPublicId LIKE @SearchLike OR CONVERT(varchar(20), pi.Id) = @SearchExact OR CONVERT(varchar(20), ps.OrderId) = @SearchExact)");
            parameters.Add("SearchLike", $"%{search}%");
            parameters.Add("SearchExact", search);
        }

        if (query.Status.HasValue)
        {
            clauses.Add("pi.Status = @Status");
            parameters.Add("Status", (int)query.Status.Value);
        }

        switch (NormalizeLabelState(query.LabelState))
        {
            case "missing":
                clauses.Add("pi.Status = 0 AND pi.FoilPrintedAt IS NULL AND COALESCE(labels.ProductLabelPrintCount, 0) = 0");
                break;
            case "printed":
                clauses.Add("(pi.FoilPrintedAt IS NOT NULL OR COALESCE(labels.ProductLabelPrintCount, 0) > 0)");
                break;
            case "reprint":
                clauses.Add("COALESCE(labels.ProductLabelPrintCount, 0) > 1");
                break;
            case "blocked":
                clauses.Add("(pi.Status IN (3, 4) OR pi.IsDamaged = 1)");
                break;
        }

        return string.Join(" AND ", clauses);
    }

    private static string? NormalizeLabelState(string? labelState)
    {
        if (string.IsNullOrWhiteSpace(labelState) || labelState.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return labelState.Trim().ToLowerInvariant() switch
        {
            "missing" or "pending" => "missing",
            "printed" or "done" => "printed",
            "reprint" or "reprinted" => "reprint",
            "blocked" or "problem" => "blocked",
            _ => null,
        };
    }

    private static string ResolvePackingItemsOrderBy(string? sortBy, bool descending)
    {
        var column = sortBy?.Trim().ToLowerInvariant() switch
        {
            "meal" or "mealname" => "pi.MealName",
            "box" or "boxcode" => "pi.BoxCode",
            "status" => "pi.Status",
            "client" => "ps.ClientName",
            "order" => "ps.OrderId",
            "printed" => "pi.FoilPrintedAt",
            "route" => "COALESCE(drs.RouteId, 2147483647), COALESCE(drs.SequenceNumber, 2147483647)",
            _ => "pi.Id",
        };

        var direction = descending ? "DESC" : "ASC";
        return column == "pi.Id"
            ? $"{column} {direction}"
            : $"{column} {direction}, pi.Id ASC";
    }
}
