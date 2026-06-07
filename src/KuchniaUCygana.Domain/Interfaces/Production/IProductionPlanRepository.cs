using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Interfaces.Production;

public interface IProductionPlanRepository : IRepository<ProductionPlan>
{
    /// <summary>
    /// Pobiera plan produkcji na konkretną datę.
    /// </summary>
    Task<ProductionPlan?> GetByDateAsync(DateOnly date);

    /// <summary>
    /// Pobiera plan z załadowanymi pozycjami (eager loading).
    /// </summary>
    Task<ProductionPlan?> GetWithItemsAsync(int planId);

    /// <summary>
    /// Pobiera pozycje planu.
    /// </summary>
    Task<IEnumerable<ProductionPlanItem>> GetPlanItemsAsync(int planId);

    Task<(IReadOnlyList<ProductionPlanItem> Items, int TotalCount)> SearchPlanItemsAsync(
        ProductionPlanItemQuery query);

    Task<ProductionPlanItemSummary> GetPlanItemSummaryAsync(int planId);

    Task ReplacePlanItemsAsync(int planId, IReadOnlyList<ProductionPlanItem> items, string requestedBy);
}

public sealed class ProductionPlanItemQuery
{
    public int PlanId { get; init; }

    public string? Search { get; init; }

    public ProductionItemStatus? Status { get; init; }

    public int? ProductionGroup { get; init; }

    public bool? FefoDeducted { get; init; }

    public bool? PackagingDeducted { get; init; }

    public bool? HasSnapshot { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }
}

public sealed class ProductionPlanItemSummary
{
    public int TotalItems { get; set; }

    public int TotalPlannedQuantity { get; set; }

    public int TotalCookedQuantity { get; set; }

    public int PlannedItems { get; set; }

    public int CookingItems { get; set; }

    public int CookedItems { get; set; }

    public int FailedItems { get; set; }

    public int PendingFefoItems { get; set; }

    public int FefoDeductedItems { get; set; }

    public int PendingPackagingItems { get; set; }

    public int PackagingDeductedItems { get; set; }

    public int SnapshotItems { get; set; }
}
