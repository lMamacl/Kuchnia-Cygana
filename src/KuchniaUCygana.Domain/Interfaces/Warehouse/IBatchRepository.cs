using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;

namespace KuchniaUCygana.Domain.Interfaces.Warehouse;

public interface IBatchRepository : IRepository<Batch>
{
    /// <summary>
    /// Zwraca aktywne (nie wyczerpane) partie dla danego składnika,
    /// posortowane FEFO (First Expired, First Out).
    /// </summary>
    Task<IEnumerable<Batch>> GetActiveBatchesByStockItemAsync(int stockItemId);

    Task<IEnumerable<Batch>> GetActiveBatchesByWarehouseCategoryAsync(int warehouseCategoryId);

    Task<IEnumerable<Batch>> GetBatchesByStockItemAsync(int stockItemId);

    /// <summary>
    /// Zwraca partie, których data ważności upływa przed podaną datą.
    /// Używane do alertów o zbliżającym się przeterminowaniu.
    /// </summary>
    Task<IEnumerable<Batch>> GetExpiringBeforeAsync(DateTimeOffset date);

    Task<(IEnumerable<FefoReportRow> Items, int TotalCount)> GetFefoReportPageAsync(FefoReportQuery query);

    Task<IEnumerable<FefoReportRow>> GetFefoReportAsync(FefoReportQuery query);

    Task<(IEnumerable<BatchInventoryRow> Items, int TotalCount)> GetBatchInventoryPageAsync(
        BatchInventoryQuery query);
}

public sealed record FefoReportQuery(
    string? Search,
    string? Status,
    int Page,
    int PageSize,
    bool UsePaging);

public sealed class FefoReportRow
{
    public int StockItemId { get; set; }

    public string StockItemName { get; set; } = string.Empty;

    public int BatchId { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public DateTimeOffset? ExpiryDate { get; set; }

    public decimal Quantity { get; set; }

    public int? DaysToExpiry { get; set; }

    public string Status { get; set; } = string.Empty;
}

public sealed record BatchInventoryQuery(
    string? Search,
    int? CategoryId,
    string? LegacyCategory,
    int Page,
    int PageSize);

public sealed class BatchInventoryRow
{
    public int BatchId { get; set; }

    public int StockItemId { get; set; }

    public string StockItemName { get; set; } = string.Empty;

    public string BatchNumber { get; set; } = string.Empty;

    public int? CategoryId { get; set; }

    public string Category { get; set; } = string.Empty;

    public decimal CurrentQuantity { get; set; }

    public string UnitSymbol { get; set; } = string.Empty;

    public DateTimeOffset? ExpiryDate { get; set; }

    public DateTimeOffset ReceivedDate { get; set; }
}
