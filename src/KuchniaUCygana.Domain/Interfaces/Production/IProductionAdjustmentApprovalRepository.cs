using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Production;

public interface IProductionAdjustmentApprovalRepository : IRepository<ProductionAdjustmentApproval>
{
    Task<IReadOnlyList<ProductionAdjustmentApproval>> GetByPlanItemAsync(int productionPlanItemId);

    Task<ProductionAdjustmentApproval?> GetLatestApprovedAsync(
        int productionPlanItemId,
        IReadOnlyCollection<string> adjustmentTypes,
        decimal requestedValue);
}
