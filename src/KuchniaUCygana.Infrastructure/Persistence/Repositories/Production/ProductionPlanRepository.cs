using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Production;

/// <summary>
/// Repozytorium ProductionPlan z eager loading pozycji.
/// </summary>
public sealed class ProductionPlanRepository : BaseRepository<ProductionPlan>, IProductionPlanRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public ProductionPlanRepository(IDbConnectionFactory connectionFactory, ICurrentUserService? currentUserService = null) : base(connectionFactory, currentUserService)
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
}


