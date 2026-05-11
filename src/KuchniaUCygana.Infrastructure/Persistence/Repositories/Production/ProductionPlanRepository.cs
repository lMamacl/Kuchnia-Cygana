using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

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
        return await db.SingleAsync<ProductionPlan>(
            pp => pp.ProductionDate == date && !pp.IsDeleted);
    }

    /// <inheritdoc />
    public async Task<ProductionPlan?> GetWithItemsAsync(int planId)
    {
        using var db = _connectionFactory.CreateConnection();
        var plan = await db.SingleByIdAsync<ProductionPlan>(planId);

        if (plan == null || plan.IsDeleted)
            return null;

        return plan;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<ProductionPlanItem>> GetPlanItemsAsync(int planId)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.SelectAsync<ProductionPlanItem>(
            i => i.ProductionPlanId == planId && !i.IsDeleted);
    }
}
