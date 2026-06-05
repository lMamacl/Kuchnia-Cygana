using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;

namespace KuchniaUCygana.Domain.Interfaces.Warehouse;

public interface IInventoryTransactionRepository : IRepository<InventoryTransaction, long>
{
    /// <summary>
    /// Historia transakcji dla konkretnej partii.
    /// </summary>
    Task<IEnumerable<InventoryTransaction>> GetByBatchIdAsync(int batchId);

    /// <summary>
    /// Logi z okresu – potrzebne do raportów Sanepidu za dany okres.
    /// </summary>
    Task<IEnumerable<InventoryTransaction>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to);

    /// <summary>
    /// Historia transakcji dla konkretnego składnika magazynowego (przez StockItemId).
    /// Wymaga migracji 010 (backfill StockItemId). Stronicowana, posortowana malejąco po dacie.
    /// </summary>
    Task<IEnumerable<InventoryTransaction>> GetByStockItemIdAsync(int stockItemId, int page = 1, int pageSize = 25);

    Task<(IEnumerable<TransactionHistoryRow> Items, int TotalCount)> GetTransactionHistoryPageAsync(
        TransactionHistoryQuery query);
}

public sealed record TransactionHistoryQuery(
    int? StockItemId,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int? TransactionType,
    int Page,
    int PageSize);

public sealed class TransactionHistoryRow
{
    public long Id { get; set; }

    public int? StockItemId { get; set; }

    public string StockItemName { get; set; } = string.Empty;

    public int BatchId { get; set; }

    public string BatchNumber { get; set; } = string.Empty;

    public int TransactionType { get; set; }

    public decimal Quantity { get; set; }

    public DateTimeOffset PerformedAt { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string ReferenceDocument { get; set; } = string.Empty;

    public string PerformedBy { get; set; } = string.Empty;
}

