using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Interfaces.Packing;

public interface IPackingSessionRepository : IRepository<PackingSession>
{
    /// <summary>
    /// Pobiera aktywne sesje pakowania na konkretną datę.
    /// </summary>
    Task<IEnumerable<PackingSession>> GetActiveByDateAsync(DateOnly date);

    /// <summary>
    /// Pobiera sesję z załadowanymi pozycjami (eager loading).
    /// </summary>
    Task<PackingSession?> GetWithItemsAsync(int sessionId);

    /// <summary>
    /// Pobiera pozycje sesji pakowania.
    /// </summary>
    Task<PackingSession?> GetByDateAndOrderAsync(DateOnly date, int orderId);

    Task<IEnumerable<PackingSession>> GetByDateWithItemsAsync(DateOnly date);

    Task<IEnumerable<PackingItem>> GetSessionItemsAsync(int sessionId);

    Task<IEnumerable<PackingLabel>> GetLabelsBySessionAsync(int sessionId);

    Task<PackingLabel?> GetShippingLabelAsync(int sessionId);

    Task<PackingLabel?> GetProductLabelAsync(int packingItemId);

    Task<IEnumerable<string>> GetMealIngredientsAsync(int mealId);

    Task<IEnumerable<string>> GetMealAllergensAsync(int mealId);

    Task<int?> GetMealCaloriesAsync(int mealId);

    Task<(IReadOnlyList<PackingItemSearchRow> Items, int TotalCount)> SearchPackingItemsAsync(
        PackingItemQuery query);

    Task<FoilLabelSummary> GetFoilLabelSummaryAsync(DateOnly date);
}

public sealed class PackingItemQuery
{
    public DateOnly PackingDate { get; init; }

    public string? Search { get; init; }

    public PackingItemStatus? Status { get; init; }

    public string? LabelState { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;

    public string? SortBy { get; init; }

    public bool SortDescending { get; init; }
}

public sealed class PackingItemSearchRow
{
    public int Id { get; set; }

    public int PackingSessionId { get; set; }

    public int? PackingBagId { get; set; }

    public int? ProductionPlanItemId { get; set; }

    public int MealId { get; set; }

    public string MealName { get; set; } = string.Empty;

    public int DietVariantId { get; set; }

    public string? BoxCode { get; set; }

    public PackingItemStatus Status { get; set; }

    public DateTimeOffset? FoilPrintedAt { get; set; }

    public DateTimeOffset? PackedAt { get; set; }

    public bool IsDamaged { get; set; }

    public string? Remarks { get; set; }

    public int? OrderId { get; set; }

    public int? DeliveryCalendarId { get; set; }

    public string? ClientName { get; set; }

    public string? ClientPublicId { get; set; }

    public int? RouteId { get; set; }

    public int? StopNumber { get; set; }

    public int ProductLabelPrintCount { get; set; }

    public DateTimeOffset? LatestProductLabelPrintedAt { get; set; }
}

public sealed class FoilLabelSummary
{
    public int TotalBoxes { get; set; }

    public int PendingCount { get; set; }

    public int PrintedCount { get; set; }

    public int ReprintCount { get; set; }

    public int BlockedCount { get; set; }

    public int PackedCount { get; set; }
}
