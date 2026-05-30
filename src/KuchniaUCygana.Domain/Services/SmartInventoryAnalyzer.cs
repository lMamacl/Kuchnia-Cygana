using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.Warehouse;

namespace KuchniaUCygana.Domain.Services;

/// <summary>
/// Alert Smart Inventory — informacja o składniku wymagającym uwagi.
/// </summary>
public sealed class InventoryAlert
{
    public int StockItemId { get; set; }

    public string StockItemName { get; set; } = string.Empty;

    public string? SupplierBatchNumber { get; set; }

    public InventoryAlertType AlertType { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>Aktualny stan.</summary>
    public decimal CurrentQuantity { get; set; }

    /// <summary>Poziom minimum (jeśli dotyczy).</summary>
    public decimal? MinimumLevel { get; set; }

    /// <summary>Data ważności najstarszej partii (jeśli dotyczy).</summary>
    public DateTimeOffset? EarliestExpiry { get; set; }

    /// <summary>Ile dni do przeterminowania.</summary>
    public int? DaysUntilExpiry { get; set; }
}

public enum InventoryAlertType
{
    BelowMinimum,      // Stan poniżej poziomu minimum
    ExpiringWithin3Days, // Partia przeterminowuje się w ciągu 3 dni
    ExpiringWithin7Days, // Partia przeterminowuje się w ciągu 7 dni
    Expired,            // Partia przeterminowana
    NoStock,            // Brak w magazynie
}

/// <summary>
/// Smart Inventory Analyzer — codzienny audyt stanów i dat ważności.
/// Generuje alerty dla zaopatrzeniowca i Szefa Kuchni.
/// </summary>
public sealed class SmartInventoryAnalyzer
{
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IBatchRepository _batchRepository;

    public SmartInventoryAnalyzer(
        IStockItemRepository stockItemRepository,
        IBatchRepository batchRepository)
    {
        _stockItemRepository = stockItemRepository;
        _batchRepository = batchRepository;
    }

    /// <summary>
    /// Generuje pełną listę alertów: poniżej minimum + przeterminowane + bliskie przeterminowania.
    /// </summary>
    public async Task<IReadOnlyList<InventoryAlert>> AnalyzeAsync()
    {
        var alerts = new List<InventoryAlert>();

        // 1. Składniki poniżej poziomu minimum
        var belowMinimum = await _stockItemRepository.GetBelowMinimumAsync();
        foreach (var item in belowMinimum)
        {
            var available = (await _batchRepository.GetActiveBatchesByStockItemAsync(item.Id))
                .Sum(b => b.CurrentQuantity);

            alerts.Add(new InventoryAlert
            {
                StockItemId = item.Id,
                StockItemName = item.Name,
                AlertType = available <= 0 ? InventoryAlertType.NoStock : InventoryAlertType.BelowMinimum,
                Message = available <= 0
                    ? $"BRAK W MAGAZYNIE: {item.Name}"
                    : $"Stan {available:F1} poniżej minimum {item.MinimumLevel:F1} — zamów uzupełnienie (lead time: {item.LeadTimeDays} dni)",
                CurrentQuantity = available,
                MinimumLevel = item.MinimumLevel,
            });
        }

        // Pobieramy wszystkie składniki magazynowe, by znać ich nazwy
        var stockItems = (await _stockItemRepository.GetAllAsync())
            .ToDictionary(item => item.Id);

        // 2. Partie przeterminowane
        var expired = await _batchRepository.GetExpiringBeforeAsync(DateTimeOffset.UtcNow);
        foreach (var batch in expired)
        {
            stockItems.TryGetValue(batch.StockItemId, out var stockItem);
            var itemName = stockItem?.Name ?? $"Składnik #{batch.StockItemId}";
            alerts.Add(CreateExpiryAlert(batch, itemName, InventoryAlertType.Expired, "PRZETERMINOWANA"));
        }

        // 3. Partie przeterminowujące się w ciągu 3 dni
        var expiring3 = await _batchRepository.GetExpiringBeforeAsync(DateTimeOffset.UtcNow.AddDays(3));
        foreach (var batch in expiring3.Where(b => b.ExpiryDate > DateTimeOffset.UtcNow))
        {
            stockItems.TryGetValue(batch.StockItemId, out var stockItem);
            var itemName = stockItem?.Name ?? $"Składnik #{batch.StockItemId}";
            alerts.Add(CreateExpiryAlert(batch, itemName, InventoryAlertType.ExpiringWithin3Days, "przeterminuje się w ciągu 3 dni"));
        }

        // 4. Partie przeterminowujące się w ciągu 7 dni
        var expiring7 = await _batchRepository.GetExpiringBeforeAsync(DateTimeOffset.UtcNow.AddDays(7));
        foreach (var batch in expiring7.Where(b => b.ExpiryDate > DateTimeOffset.UtcNow.AddDays(3)))
        {
            stockItems.TryGetValue(batch.StockItemId, out var stockItem);
            var itemName = stockItem?.Name ?? $"Składnik #{batch.StockItemId}";
            alerts.Add(CreateExpiryAlert(batch, itemName, InventoryAlertType.ExpiringWithin7Days, "przeterminuje się w ciągu 7 dni"));
        }

        return alerts.OrderByDescending(a => a.AlertType).ToList();
    }

    private static InventoryAlert CreateExpiryAlert(Batch batch, string stockItemName, InventoryAlertType type, string desc)
    {
        var daysLeft = batch.ExpiryDate.HasValue
            ? (int)(batch.ExpiryDate.Value - DateTimeOffset.UtcNow).TotalDays
            : 0;

        return new InventoryAlert
        {
            StockItemId = batch.StockItemId,
            StockItemName = stockItemName,
            SupplierBatchNumber = batch.SupplierBatchNumber,
            AlertType = type,
            Message = $"{stockItemName} (Partia {batch.SupplierBatchNumber}, {batch.CurrentQuantity:F1} szt.) — {desc}",
            CurrentQuantity = batch.CurrentQuantity,
            EarliestExpiry = batch.ExpiryDate,
            DaysUntilExpiry = daysLeft,
        };
    }
}
