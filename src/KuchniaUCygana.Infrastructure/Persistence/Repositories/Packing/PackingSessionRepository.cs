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
}
