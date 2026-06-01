using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Warehouse;

namespace KuchniaUCygana.Domain.Services;

/// <summary>
/// Wynik operacji zdejmowania ze stanu FEFO.
/// </summary>
public sealed class FefoDeductionResult
{
    /// <summary>Czy udało się zdjąć pełną ilość.</summary>
    public bool IsFullyDeducted { get; set; }

    /// <summary>Ile zdjęto łącznie.</summary>
    public decimal TotalDeducted { get; set; }

    /// <summary>Ile brakowało (0 jeśli pełne).</summary>
    public decimal Shortage { get; set; }

    /// <summary>Lista partii, z których zdjęto (z ilościami).</summary>
    public List<BatchDeduction> Deductions { get; set; } = new();
}

/// <summary>
/// Pojedyncze zdjęcie z partii.
/// </summary>
public sealed class BatchDeduction
{
    public int BatchId { get; set; }

    public string? SupplierBatchNumber { get; set; }

    public DateTimeOffset? ExpiryDate { get; set; }

    public decimal QuantityDeducted { get; set; }

    public decimal RemainingInBatch { get; set; }
}

/// <summary>
/// Serwis FEFO (First Expired, First Out) — algorytm zdejmowania składników ze stanu
/// wg daty ważności. Partia z najkrótszą datą ważności jest zużywana jako pierwsza.
/// </summary>
public sealed class FefoService
{
    private readonly IBatchRepository _batchRepository;
    private readonly IInventoryTransactionRepository _transactionRepository;

    public FefoService(
        IBatchRepository batchRepository,
        IInventoryTransactionRepository transactionRepository)
    {
        _batchRepository = batchRepository;
        _transactionRepository = transactionRepository;
    }

    /// <summary>
    /// Zdejmuje podaną ilość składnika ze stanu wg FEFO.
    /// Partia z najkrótszą datą ważności jest zużywana jako pierwsza.
    /// Jeśli jedna partia nie wystarczy, przechodzi do następnej.
    /// </summary>
    /// <param name="stockItemId">ID składnika magazynowego.</param>
    /// <param name="requiredQuantity">Wymagana ilość do zdjęcia.</param>
    /// <param name="reason">Powód zdjęcia (np. "Produkcja: Zupa pomidorowa").</param>
    /// <param name="referenceDocument">Dokument referencyjny (np. "PLAN-2026-05-12").</param>
    /// <returns>Wynik operacji z listą partii i ilościami.</returns>
    public async Task<FefoDeductionResult> DeductByFefoAsync(
        int stockItemId,
        decimal requiredQuantity,
        string reason,
        string? referenceDocument = null)
    {
        if (requiredQuantity <= 0)
            throw new ArgumentException("Ilość do zdjęcia musi być większa od 0.", nameof(requiredQuantity));

        // Pobierz aktywne partie w kolejności FEFO (repo sortuje po ExpiryDate ASC)
        var activeBatches = (await _batchRepository.GetActiveBatchesByStockItemAsync(stockItemId)).ToList();

        var result = new FefoDeductionResult();
        var remaining = requiredQuantity;

        foreach (var batch in activeBatches)
        {
            if (remaining <= 0)
                break;

            var toDeduct = Math.Min(remaining, batch.CurrentQuantity);

            batch.CurrentQuantity -= toDeduct;

            if (batch.CurrentQuantity <= 0)
            {
                batch.CurrentQuantity = 0;
                batch.IsDepleted = true;
            }

            await _batchRepository.UpdateAsync(batch);

            // Rejestruj transakcję magazynową
            var transaction = new InventoryTransaction
            {
                BatchId = batch.Id,
                StockItemId = stockItemId,
                TransactionType = InventoryTransactionType.ProductionIssue,
                QuantityChanged = -toDeduct, // Ujemna = wydanie
                Reason = reason,
                ReferenceDocument = referenceDocument,
            };

            await _transactionRepository.InsertAsync(transaction);

            result.Deductions.Add(new BatchDeduction
            {
                BatchId = batch.Id,
                SupplierBatchNumber = batch.SupplierBatchNumber,
                ExpiryDate = batch.ExpiryDate,
                QuantityDeducted = toDeduct,
                RemainingInBatch = batch.CurrentQuantity,
            });

            remaining -= toDeduct;
        }

        result.TotalDeducted = requiredQuantity - remaining;
        result.IsFullyDeducted = remaining <= 0;
        result.Shortage = Math.Max(0, remaining);

        return result;
    }

    public async Task<FefoDeductionResult> DeductByFefoCategoryAsync(
        int warehouseCategoryId,
        decimal requiredQuantity,
        string reason,
        string? referenceDocument = null)
    {
        if (requiredQuantity <= 0)
            throw new ArgumentException("Ilosc do zdjecia musi byc wieksza od 0.", nameof(requiredQuantity));

        var activeBatches = (await _batchRepository.GetActiveBatchesByWarehouseCategoryAsync(warehouseCategoryId)).ToList();

        var result = new FefoDeductionResult();
        var remaining = requiredQuantity;

        foreach (var batch in activeBatches)
        {
            if (remaining <= 0)
                break;

            var toDeduct = Math.Min(remaining, batch.CurrentQuantity);

            batch.CurrentQuantity -= toDeduct;
            if (batch.CurrentQuantity <= 0)
            {
                batch.CurrentQuantity = 0;
                batch.IsDepleted = true;
            }

            await _batchRepository.UpdateAsync(batch);

            await _transactionRepository.InsertAsync(new InventoryTransaction
            {
                BatchId = batch.Id,
                StockItemId = batch.StockItemId,
                TransactionType = InventoryTransactionType.ProductionIssue,
                QuantityChanged = -toDeduct,
                Reason = reason,
                ReferenceDocument = referenceDocument,
            });

            result.Deductions.Add(new BatchDeduction
            {
                BatchId = batch.Id,
                SupplierBatchNumber = batch.SupplierBatchNumber,
                ExpiryDate = batch.ExpiryDate,
                QuantityDeducted = toDeduct,
                RemainingInBatch = batch.CurrentQuantity,
            });

            remaining -= toDeduct;
        }

        result.TotalDeducted = requiredQuantity - remaining;
        result.IsFullyDeducted = remaining <= 0;
        result.Shortage = Math.Max(0, remaining);

        return result;
    }

    /// <summary>
    /// Sprawdza dostępność składnika bez zdejmowania.
    /// </summary>
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

    /// <summary>
    /// Pobiera partie, które przeterminowują się w ciągu podanej liczby dni.
    /// </summary>
    public async Task<IEnumerable<Batch>> GetExpiringWithinDaysAsync(int days)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(days);
        return await _batchRepository.GetExpiringBeforeAsync(cutoff);
    }
}
