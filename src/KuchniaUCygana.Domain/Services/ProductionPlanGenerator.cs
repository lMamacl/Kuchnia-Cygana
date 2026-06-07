using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Production;

namespace KuchniaUCygana.Domain.Services;

/// <summary>
/// Wynik generowania planu produkcji.
/// </summary>
public sealed class PlanGenerationResult
{
    public ProductionPlan Plan { get; set; } = null!;

    public List<ProductionPlanItem> Items { get; set; } = new();

    public FoodCostReport? FoodCostReport { get; set; }

    public bool HasShortages => FoodCostReport?.Shortages.Count > 0;
}

public readonly record struct ProductionMealKey(
    int MealId,
    int DietVariantId,
    int? DietMenuPlanItemId,
    int? MealVariantId);

/// <summary>
/// Generator Planu Produkcji — tworzy dzienny plan na podstawie:
/// - zamówień z M1 (IOrderDataProvider)
/// - planu diet z M2 (IDietDataProvider)
/// 
/// Grupuje posiłki wg MealId+DietVariantId, oblicza Food Cost,
/// przypisuje grupy produkcyjne i szacuje ETA.
/// </summary>
public sealed class ProductionPlanGenerator
{
    private readonly IOrderDataProvider _orderDataProvider;
    private readonly IDietDataProvider _dietDataProvider;
    private readonly FoodCostCalculator _foodCostCalculator;
    private readonly IProductionPlanRepository _planRepository;

    public ProductionPlanGenerator(
        IOrderDataProvider orderDataProvider,
        IDietDataProvider dietDataProvider,
        FoodCostCalculator foodCostCalculator,
        IProductionPlanRepository planRepository)
    {
        _orderDataProvider = orderDataProvider;
        _dietDataProvider = dietDataProvider;
        _foodCostCalculator = foodCostCalculator;
        _planRepository = planRepository;
    }

    /// <summary>
    /// Generuje plan produkcji na podany dzień.
    /// </summary>
    public async Task<PlanGenerationResult> GeneratePlanAsync(DateOnly productionDate, string? createdBy = null)
    {
        // 1. Sprawdź czy plan na ten dzień już istnieje
        var existing = await _planRepository.GetByDateAsync(productionDate);
        if (existing != null)
            throw new InvalidOperationException(
                $"Plan produkcji na dzień {productionDate} już istnieje (ID: {existing.Id}, Status: {existing.Status}).");

        // 2. Pobierz zamówienia na ten dzień (M1)
        var orders = (await _orderDataProvider.GetActiveOrdersAsync(productionDate)).ToList();
        if (orders.Count == 0)
            throw new InvalidOperationException($"Brak aktywnych zamówień na dzień {productionDate}.");

        // 3. Pobierz opublikowany snapshot planu M2; legacy plan zostaje fallbackiem kompatybilności.
        var snapshot = await _dietDataProvider.GetPublishedPlanSnapshotAsync(productionDate);
        if (snapshot is null)
        {
            throw new InvalidOperationException(
                $"Brak opublikowanego snapshotu M2 na dzien {productionDate:yyyy-MM-dd}. " +
                "M3 nie generuje operacyjnego planu produkcji z legacy GetPlanForDateAsync.");
        }

        var dietPlan = snapshot.Items.Select(MapSnapshotItemToDietPlanEntry).ToList();
        if (dietPlan.Count == 0)
        {
            throw new InvalidOperationException(
                $"Brak opublikowanego planu diet z M2 na dzien {productionDate:yyyy-MM-dd}. " +
                "M2 musi publikowac plan codziennie z co najmniej 7-dniowym wyprzedzeniem.");
        }

        // 4. Oblicz ilości per posiłek — agregacja zamówień × diety
        var mealQuantities = await CalculateMealQuantitiesAsync(productionDate, orders, dietPlan);
        if (mealQuantities.Count == 0)
        {
            throw new InvalidOperationException(
                $"Opublikowany plan M2 na dzien {productionDate:yyyy-MM-dd} nie pasuje do aktywnych zamowien.");
        }

        // 5. Utwórz plan
        var plan = new ProductionPlan
        {
            ProductionDate = productionDate,
            Status = ProductionPlanStatus.Draft,
            CreatedBy = createdBy ?? "System",
        };

        var planId = await _planRepository.InsertAsync(plan);
        plan.Id = planId;

        // 6. Utwórz pozycje planu z grupami produkcyjnymi i ETA
        var items = new List<ProductionPlanItem>();
        foreach (var (key, quantity) in mealQuantities
            .OrderBy(kv => GetPlanSortOrder(kv.Key, dietPlan))
            .ThenBy(kv => kv.Key.DietVariantId)
            .ThenBy(kv => kv.Key.MealId)
            .ThenBy(kv => kv.Key.MealVariantId ?? 0))
        {
            var dietEntry = FindDietPlanEntry(key, dietPlan);
            var snapshotItem = FindSnapshotItem(key, snapshot.Items);
            var snapshotPayload = snapshotItem is null ? null : ProductionSnapshotPayloadFactory.Create(snapshotItem);
            var productionGroup = AssignProductionGroup(key, dietPlan);

            items.Add(new ProductionPlanItem
            {
                ProductionPlanId = planId,
                MealId = key.MealId,
                MealName = snapshotItem?.MealName ?? dietEntry?.MealName ?? $"Posiłek #{key.MealId}",
                DietVariantId = key.DietVariantId,
                DietMenuPlanItemId = snapshotItem?.DietMenuPlanItemId,
                RecipeComponentVersionIds = snapshotItem is null
                    ? null
                    : string.Join(
                        ",",
                        (snapshotItem.RecipeComponentVersionIds.Count > 0
                            ? snapshotItem.RecipeComponentVersionIds
                            : snapshotItem.Components.Select(c => c.RecipeComponentVersionId))
                        .Where(id => id > 0)
                        .Distinct()),
                M2SnapshotJson = snapshotPayload?.Json,
                M2SnapshotHash = snapshotPayload?.Hash,
                PlannedQuantity = quantity,
                CookedQuantity = 0,
                Status = ProductionItemStatus.Planned,
                // Model B-lite: grupa produkcyjna i ETA
                ProductionGroup = productionGroup,
                EstimatedReadyTime = EstimateReadyTime(productionGroup, quantity),
            });
        }

        // 7. Oblicz Food Cost
        var foodCostReport = _foodCostCalculator.CalculateFromSnapshot(mealQuantities, snapshot);

        return new PlanGenerationResult
        {
            Plan = plan,
            Items = items,
            FoodCostReport = foodCostReport,
        };
    }

    public async Task<PlanGenerationResult> GeneratePlanItemsForExistingPlanAsync(
        DateOnly productionDate,
        int planId)
    {
        if (planId <= 0)
        {
            throw new ArgumentException("Plan produkcji musi miec poprawny identyfikator.", nameof(planId));
        }

        var orders = (await _orderDataProvider.GetActiveOrdersAsync(productionDate)).ToList();
        if (orders.Count == 0)
        {
            throw new InvalidOperationException($"Brak aktywnych zamowien na dzien {productionDate}.");
        }

        var snapshot = await _dietDataProvider.GetPublishedPlanSnapshotAsync(productionDate);
        if (snapshot is null)
        {
            throw new InvalidOperationException(
                $"Brak opublikowanego snapshotu M2 na dzien {productionDate:yyyy-MM-dd}. " +
                "M3 nie odswieza operacyjnego planu produkcji z legacy GetPlanForDateAsync.");
        }

        var dietPlan = snapshot.Items.Select(MapSnapshotItemToDietPlanEntry).ToList();
        if (dietPlan.Count == 0)
        {
            throw new InvalidOperationException(
                $"Brak opublikowanego planu diet z M2 na dzien {productionDate:yyyy-MM-dd}.");
        }

        var mealQuantities = await CalculateMealQuantitiesAsync(productionDate, orders, dietPlan);
        if (mealQuantities.Count == 0)
        {
            throw new InvalidOperationException(
                $"Opublikowany plan M2 na dzien {productionDate:yyyy-MM-dd} nie pasuje do aktywnych zamowien.");
        }

        var items = new List<ProductionPlanItem>();
        foreach (var (key, quantity) in mealQuantities
            .OrderBy(kv => GetPlanSortOrder(kv.Key, dietPlan))
            .ThenBy(kv => kv.Key.DietVariantId)
            .ThenBy(kv => kv.Key.MealId)
            .ThenBy(kv => kv.Key.MealVariantId ?? 0))
        {
            var dietEntry = FindDietPlanEntry(key, dietPlan);
            var snapshotItem = FindSnapshotItem(key, snapshot.Items);
            var snapshotPayload = snapshotItem is null ? null : ProductionSnapshotPayloadFactory.Create(snapshotItem);
            var productionGroup = AssignProductionGroup(key, dietPlan);

            items.Add(new ProductionPlanItem
            {
                ProductionPlanId = planId,
                MealId = key.MealId,
                MealName = snapshotItem?.MealName ?? dietEntry?.MealName ?? $"Posilek #{key.MealId}",
                DietVariantId = key.DietVariantId,
                DietMenuPlanItemId = snapshotItem?.DietMenuPlanItemId,
                RecipeComponentVersionIds = snapshotItem is null
                    ? null
                    : string.Join(
                        ",",
                        (snapshotItem.RecipeComponentVersionIds.Count > 0
                            ? snapshotItem.RecipeComponentVersionIds
                            : snapshotItem.Components.Select(c => c.RecipeComponentVersionId))
                        .Where(id => id > 0)
                        .Distinct()),
                M2SnapshotJson = snapshotPayload?.Json,
                M2SnapshotHash = snapshotPayload?.Hash,
                PlannedQuantity = quantity,
                CookedQuantity = 0,
                Status = ProductionItemStatus.Planned,
                ProductionGroup = productionGroup,
                EstimatedReadyTime = EstimateReadyTime(productionGroup, quantity),
            });
        }

        return new PlanGenerationResult
        {
            Plan = new ProductionPlan
            {
                Id = planId,
                ProductionDate = productionDate,
                Status = ProductionPlanStatus.Draft,
            },
            Items = items,
            FoodCostReport = _foodCostCalculator.CalculateFromSnapshot(mealQuantities, snapshot),
        };
    }

    private static DietPlanEntry MapSnapshotItemToDietPlanEntry(PublishedDietPlanItemDto item)
        => new()
        {
            PlanDate = item.PlanDate,
            PlanStatus = "Published",
            DietMenuPlanItemId = item.DietMenuPlanItemId,
            MealId = item.MealId,
            MealVariantId = item.MealVariantId,
            MealName = item.MealName,
            CategoryId = item.CategoryId,
            CategoryName = item.CategoryName,
            DietVariantId = item.DietVariantId,
            MealSlot = item.MealSlot,
            SortOrder = item.SortOrder,
            ServingMultiplier = item.ServingMultiplier,
            ServingWeightGrams = item.FinalWeightAfterMultiplierGrams
                ?? item.FinalWeightGrams
                ?? item.CookedWeightGrams
                ?? item.RawWeightGrams
                ?? item.ServingMultiplier * 100m,
        };

    /// <summary>
    /// Agreguje: ile porcji każdego posiłku (MealId+DietVariantId) potrzeba.
    /// </summary>
    private async Task<Dictionary<ProductionMealKey, int>> CalculateMealQuantitiesAsync(
        DateOnly productionDate,
        List<ActiveOrderEntry> orders,
        List<DietPlanEntry> dietPlan)
    {
        var explicitQuantities = await CalculateExplicitOrderItemQuantitiesAsync(productionDate, dietPlan);
        return explicitQuantities.Count > 0
            ? explicitQuantities
            : CalculateLegacyDietVariantQuantities(orders, dietPlan);
    }

    private async Task<Dictionary<ProductionMealKey, int>> CalculateExplicitOrderItemQuantitiesAsync(
        DateOnly productionDate,
        List<DietPlanEntry> dietPlan)
    {
        var deliveries = await _orderDataProvider.GetDeliveriesForDateAsync(productionDate.ToDateTime(TimeOnly.MinValue));
        var quantities = new Dictionary<ProductionMealKey, int>();

        foreach (var item in deliveries.SelectMany(delivery => delivery.Items))
        {
            if (!item.MealId.HasValue || item.MealId.Value <= 0)
            {
                continue;
            }

            var matchedPlanItem = item.DietMenuPlanItemId.HasValue
                ? dietPlan.FirstOrDefault(planItem =>
                    planItem.DietMenuPlanItemId == item.DietMenuPlanItemId.Value)
                : null;

            if (matchedPlanItem is null && item.DietMenuPlanItemId.HasValue)
            {
                throw CreateM2SnapshotMismatchException(item, productionDate);
            }

            if (matchedPlanItem is null && item.MealVariantId.HasValue)
            {
                matchedPlanItem = dietPlan.FirstOrDefault(planItem =>
                    planItem.MealId == item.MealId.Value &&
                    planItem.DietVariantId == item.DietVariantId &&
                    planItem.MealVariantId == item.MealVariantId.Value);
            }

            matchedPlanItem ??= dietPlan.FirstOrDefault(planItem =>
                planItem.MealId == item.MealId.Value &&
                planItem.DietVariantId == item.DietVariantId);

            if (matchedPlanItem is null)
            {
                throw CreateM2SnapshotMismatchException(item, productionDate);
            }

            var key = CreateKey(matchedPlanItem);
            quantities[key] = quantities.GetValueOrDefault(key) + 1;
        }

        return quantities;
    }

    private static InvalidOperationException CreateM2SnapshotMismatchException(
        OrderItemInfo item,
        DateOnly productionDate)
    {
        var mealVariantPart = item.MealVariantId.HasValue
            ? item.MealVariantId.Value.ToString(CultureInfo.InvariantCulture)
            : "brak";
        var snapshotKey = item.DietMenuPlanItemId.HasValue
            ? $"DietMenuPlanItemId {item.DietMenuPlanItemId.Value}"
            : $"MealVariantId {mealVariantPart}";

        return new InvalidOperationException(
            $"Pozycja zamowienia z M1 nie pasuje do opublikowanego snapshotu M2 na dzien {productionDate:yyyy-MM-dd}: MealId {item.MealId.GetValueOrDefault()}, DietVariantId {item.DietVariantId}, {snapshotKey}.");
    }

    private static Dictionary<ProductionMealKey, int> CalculateLegacyDietVariantQuantities(
        List<ActiveOrderEntry> orders,
        List<DietPlanEntry> dietPlan)
    {
        var quantities = new Dictionary<ProductionMealKey, int>();

        foreach (var order in orders)
        {
            // Znajdź posiłki przypisane do wariantu kalorycznego zamówienia
            var mealsForVariant = dietPlan
                .Where(d => d.DietVariantId == order.DietVariantId)
                .ToList();

            foreach (var meal in mealsForVariant)
            {
                var key = CreateKey(meal);
                quantities[key] = quantities.GetValueOrDefault(key) + 1;
            }
        }

        return quantities;
    }

    /// <summary>
    /// Przypisuje grupę produkcyjną na podstawie kolejności posiłku w planie.
    /// Grupy: 1=zimne/śniadania (pierwsze), 2=zupy, 3=dania główne, 4=sałatki/desery.
    /// W przyszłości: inteligentne przypisanie na podstawie kategorii posiłku z M2.
    /// </summary>
    private static int AssignProductionGroup(ProductionMealKey key, List<DietPlanEntry> dietPlan)
    {
        // Prosty algorytm: podział wg SortOrder/pozycji w planie diet
        var entry = FindDietPlanEntry(key, dietPlan);
        var slot = entry?.MealSlot ?? string.Empty;

        if (slot.Contains("breakfast", StringComparison.OrdinalIgnoreCase)
            || slot.Contains("snack", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (slot.Contains("soup", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (slot.Contains("lunch", StringComparison.OrdinalIgnoreCase)
            || slot.Contains("dinner", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        var index = entry?.SortOrder - 1 ?? dietPlan.FindIndex(d => d.MealId == key.MealId);

        return index switch
        {
            0 or 1 => 1,   // Śniadanie, II śniadanie → grupa 1 (zimne)
            2 => 2,         // Obiad/zupa → grupa 2
            3 => 3,         // Danie główne → grupa 3
            _ => 4,         // Podwieczorek, kolacja → grupa 4
        };
    }

    /// <summary>
    /// Szacuje czas gotowości grupy na podstawie numeru grupy i ilości porcji.
    /// </summary>
    private static TimeOnly EstimateReadyTime(int? group, int portions)
    {
        // Bazowy czas per grupa + dodatkowy czas za duże ilości
        var extraMinutes = portions > 30 ? 30 : portions > 15 ? 15 : 0;

        return group switch
        {
            1 => new TimeOnly(6, 30).AddMinutes(extraMinutes),  // Zimne → 6:30 + extra
            2 => new TimeOnly(7, 30).AddMinutes(extraMinutes),  // Zupy → 7:30 + extra
            3 => new TimeOnly(8, 30).AddMinutes(extraMinutes),  // Główne → 8:30 + extra
            4 => new TimeOnly(9, 0).AddMinutes(extraMinutes),   // Sałatki → 9:00 + extra
            _ => new TimeOnly(9, 30),
        };
    }

    private static int GetPlanSortOrder(ProductionMealKey key, List<DietPlanEntry> dietPlan)
        => FindDietPlanEntry(key, dietPlan)?.SortOrder ?? int.MaxValue;

    private static ProductionMealKey CreateKey(DietPlanEntry entry)
        => new(entry.MealId, entry.DietVariantId, entry.DietMenuPlanItemId, entry.MealVariantId);

    private static DietPlanEntry? FindDietPlanEntry(ProductionMealKey key, List<DietPlanEntry> dietPlan)
    {
        if (key.DietMenuPlanItemId.HasValue)
        {
            var planItemMatch = dietPlan.FirstOrDefault(item =>
                item.DietMenuPlanItemId == key.DietMenuPlanItemId.Value);
            if (planItemMatch is not null)
            {
                return planItemMatch;
            }
        }

        var variantMatch = dietPlan.FirstOrDefault(item =>
            item.MealId == key.MealId &&
            item.DietVariantId == key.DietVariantId &&
            item.MealVariantId == key.MealVariantId);

        return variantMatch
            ?? dietPlan.FirstOrDefault(item => item.MealId == key.MealId && item.DietVariantId == key.DietVariantId)
            ?? dietPlan.FirstOrDefault(item => item.MealId == key.MealId);
    }

    private static PublishedDietPlanItemDto? FindSnapshotItem(
        ProductionMealKey key,
        IReadOnlyList<PublishedDietPlanItemDto> snapshotItems)
    {
        if (key.DietMenuPlanItemId.HasValue)
        {
            var planItemMatch = snapshotItems.FirstOrDefault(item =>
                item.DietMenuPlanItemId == key.DietMenuPlanItemId.Value);
            if (planItemMatch is not null)
            {
                return planItemMatch;
            }
        }

        var variantMatch = snapshotItems.FirstOrDefault(item =>
            item.MealId == key.MealId &&
            item.DietVariantId == key.DietVariantId &&
            item.MealVariantId == key.MealVariantId);

        return variantMatch
            ?? snapshotItems.FirstOrDefault(item => item.MealId == key.MealId && item.DietVariantId == key.DietVariantId)
            ?? snapshotItems.FirstOrDefault(item => item.MealId == key.MealId);
    }

}
