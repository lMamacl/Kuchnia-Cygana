using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Warehouse;

namespace KuchniaUCygana.Domain.Services;

public sealed class FefoDeductionResult
{
    public bool IsFullyDeducted { get; set; }

    public decimal TotalDeducted { get; set; }

    public decimal Shortage { get; set; }

    public List<BatchDeduction> Deductions { get; set; } = new();
}

public sealed class BatchDeduction
{
    public int BatchId { get; set; }

    public string? SupplierBatchNumber { get; set; }

    public DateTimeOffset? ExpiryDate { get; set; }

    public decimal QuantityDeducted { get; set; }

    public decimal RemainingInBatch { get; set; }
}

public sealed class FefoService
{
    private readonly IBatchRepository _batchRepository;
    private readonly IWarehouseCommandRepository _warehouseCommandRepository;

    public FefoService(
        IBatchRepository batchRepository,
        IWarehouseCommandRepository warehouseCommandRepository)
    {
        _batchRepository = batchRepository;
        _warehouseCommandRepository = warehouseCommandRepository;
    }

    public async Task<FefoDeductionResult> DeductByFefoAsync(
        int stockItemId,
        decimal requiredQuantity,
        string reason,
        string? referenceDocument = null)
    {
        if (requiredQuantity <= 0)
        {
            throw new ArgumentException("Ilosc do zdjecia musi byc wieksza od 0.", nameof(requiredQuantity));
        }

        var transactions = await _warehouseCommandRepository.DeductStockAsync(new WarehouseDeductionCommand(
            stockItemId,
            requiredQuantity,
            InventoryTransactionType.ProductionIssue,
            reason,
            referenceDocument,
            BatchId: null,
            ExcludeExpired: false,
            RequireFullQuantity: true,
            PerformedBy: "Production",
            SkipIfReferenceDocumentExists: true));

        return CreateResult(requiredQuantity, transactions, referenceDocument);
    }

    public async Task<FefoDeductionResult> DeductByFefoCategoryAsync(
        int warehouseCategoryId,
        decimal requiredQuantity,
        string reason,
        string? referenceDocument = null)
    {
        if (requiredQuantity <= 0)
        {
            throw new ArgumentException("Ilosc do zdjecia musi byc wieksza od 0.", nameof(requiredQuantity));
        }

        var transactions = await _warehouseCommandRepository.DeductStockByCategoryAsync(new WarehouseCategoryDeductionCommand(
            warehouseCategoryId,
            requiredQuantity,
            InventoryTransactionType.ProductionIssue,
            reason,
            referenceDocument,
            ExcludeExpired: false,
            RequireFullQuantity: true,
            PerformedBy: "Production",
            SkipIfReferenceDocumentExists: true));

        return CreateResult(requiredQuantity, transactions, referenceDocument);
    }

    public async Task<decimal> GetAvailableQuantityAsync(int stockItemId)
    {
        var batches = await _batchRepository.GetActiveBatchesByStockItemAsync(stockItemId);
        return batches.Sum(b => b.CurrentQuantity);
    }

    public async Task<decimal> GetAvailableQuantityByCategoryAsync(int warehouseCategoryId)
    {
        var batches = await _batchRepository.GetActiveBatchesByWarehouseCategoryAsync(warehouseCategoryId);
        return batches.Sum(b => b.CurrentQuantity);
    }

    public async Task<IEnumerable<Batch>> GetExpiringWithinDaysAsync(int days)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(days);
        return await _batchRepository.GetExpiringBeforeAsync(cutoff);
    }

    private static FefoDeductionResult CreateResult(
        decimal requiredQuantity,
        IReadOnlyList<InventoryTransaction> transactions,
        string? referenceDocument)
    {
        if (transactions.Count == 0 && !string.IsNullOrWhiteSpace(referenceDocument))
        {
            return new FefoDeductionResult
            {
                IsFullyDeducted = true,
                TotalDeducted = requiredQuantity,
                Shortage = 0,
            };
        }

        var totalDeducted = transactions.Sum(transaction => Math.Abs(transaction.QuantityChanged));
        return new FefoDeductionResult
        {
            IsFullyDeducted = totalDeducted >= requiredQuantity,
            TotalDeducted = totalDeducted,
            Shortage = Math.Max(0, requiredQuantity - totalDeducted),
            Deductions = transactions
                .Select(transaction => new BatchDeduction
                {
                    BatchId = transaction.BatchId,
                    QuantityDeducted = Math.Abs(transaction.QuantityChanged),
                })
                .ToList(),
        };
    }
}
