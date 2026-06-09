using Dapper;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Production;

public sealed class ProductionAdjustmentApprovalRepository
    : BaseRepository<ProductionAdjustmentApproval>, IProductionAdjustmentApprovalRepository
{
    private readonly IDbConnectionFactory connectionFactory;

    public ProductionAdjustmentApprovalRepository(
        IDbConnectionFactory connectionFactory,
        ICurrentUserService? currentUserService = null)
        : base(connectionFactory, currentUserService)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<ProductionAdjustmentApproval>> GetByPlanItemAsync(int productionPlanItemId)
    {
        using var db = this.connectionFactory.CreateConnection();
        var approvals = await db.QueryAsync<ProductionAdjustmentApproval>(
            """
            SELECT *
            FROM [ProductionAdjustmentApprovals]
            WHERE [ProductionPlanItemId] = @productionPlanItemId
              AND [IsDeleted] = 0
            ORDER BY [RequestedAt] DESC, [Id] DESC;
            """,
            new { productionPlanItemId });

        return approvals.ToList();
    }

    public async Task<ProductionAdjustmentApproval?> GetLatestApprovedAsync(
        int productionPlanItemId,
        IReadOnlyCollection<string> adjustmentTypes,
        decimal requestedValue)
    {
        if (adjustmentTypes.Count == 0)
        {
            return null;
        }

        using var db = this.connectionFactory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<ProductionAdjustmentApproval>(
            """
            SELECT TOP 1 *
            FROM [ProductionAdjustmentApprovals]
            WHERE [ProductionPlanItemId] = @productionPlanItemId
              AND [AdjustmentType] IN @adjustmentTypes
              AND [Status] = N'Approved'
              AND [RequestedValue] = @requestedValue
              AND [IsDeleted] = 0
            ORDER BY COALESCE([ApprovedAt], [RequestedAt]) DESC, [Id] DESC;
            """,
            new
            {
                productionPlanItemId,
                adjustmentTypes,
                requestedValue,
            });
    }
}
