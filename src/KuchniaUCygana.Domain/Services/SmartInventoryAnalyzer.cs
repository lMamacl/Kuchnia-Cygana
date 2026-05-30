using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    public SmartInventoryAnalyzer(IStockItemRepository stockItemRepository)
    {
        _stockItemRepository = stockItemRepository;
    }

    /// <summary>
    /// Generuje pełną listę alertów: poniżej minimum + przeterminowane + bliskie przeterminowania.
    /// </summary>
    public async Task<IReadOnlyList<InventoryAlert>> AnalyzeAsync()
    {
        var rows = await _stockItemRepository.GetSmartInventoryAlertRowsAsync(DateTimeOffset.UtcNow);
        var alerts = new List<InventoryAlert>();

        foreach (var row in rows)
        {
            alerts.Add(CreateAlert(row));
        }

        return alerts;
    }

    private static InventoryAlert CreateAlert(SmartInventoryAlertRow row)
    {
        var alertType = row.AlertCode switch
        {
            "NoStock" => InventoryAlertType.NoStock,
            "Expired" => InventoryAlertType.Expired,
            "ExpiringWithin3Days" => InventoryAlertType.ExpiringWithin3Days,
            "ExpiringWithin7Days" => InventoryAlertType.ExpiringWithin7Days,
            _ => InventoryAlertType.BelowMinimum,
        };

        return new InventoryAlert
        {
            StockItemId = row.StockItemId,
            StockItemName = row.StockItemName,
            SupplierBatchNumber = row.SupplierBatchNumber,
            AlertType = alertType,
            Message = BuildMessage(row, alertType),
            CurrentQuantity = row.CurrentQuantity,
            MinimumLevel = row.MinimumLevel,
            EarliestExpiry = row.EarliestExpiry,
            DaysUntilExpiry = row.DaysUntilExpiry,
        };
    }

    private static string BuildMessage(SmartInventoryAlertRow row, InventoryAlertType alertType)
    {
        return alertType switch
        {
            InventoryAlertType.NoStock => $"BRAK W MAGAZYNIE: {row.StockItemName}",
            InventoryAlertType.BelowMinimum => $"Stan {row.CurrentQuantity:F1} poniżej minimum {row.MinimumLevel:F1} — zamów uzupełnienie",
            InventoryAlertType.Expired => $"{row.StockItemName} (Partia {row.SupplierBatchNumber}, {row.CurrentQuantity:F1} szt.) — PRZETERMINOWANA",
            InventoryAlertType.ExpiringWithin3Days => $"{row.StockItemName} (Partia {row.SupplierBatchNumber}, {row.CurrentQuantity:F1} szt.) — przeterminuje się w ciągu 3 dni",
            InventoryAlertType.ExpiringWithin7Days => $"{row.StockItemName} (Partia {row.SupplierBatchNumber}, {row.CurrentQuantity:F1} szt.) — przeterminuje się w ciągu 7 dni",
            _ => row.StockItemName,
        };
    }
}
