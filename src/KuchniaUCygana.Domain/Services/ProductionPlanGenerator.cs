using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
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

/// <summary>
/// Generator Planu Produkcji — tworzy dzienny plan na podstawie:
/// - zamówień z M1 (IOrderDataProvider — mock)
/// - planu diet z M2 (IDietDataProvider — prawdziwy adapter)
/// 
/// Grupuje posiłki wg MealId+DietVariantId, oblicza Food Cost,
/// przypisuje grupy produkcyjne i szacuje ETA.
/// </summary>
public sealed class ProductionPlanGenerator
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

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
        var dietPlan = snapshot?.Items.Select(MapSnapshotItemToDietPlanEntry).ToList()
            ?? (await _dietDataProvider.GetPlanForDateAsync(productionDate)).ToList();
        if (dietPlan.Count == 0)
        {
            throw new InvalidOperationException(
                $"Brak opublikowanego planu diet z M2 na dzien {productionDate:yyyy-MM-dd}. " +
                "M2 musi publikowac plan codziennie z co najmniej 7-dniowym wyprzedzeniem.");
        }

        // 4. Oblicz ilości per posiłek — agregacja zamówień × diety
        var mealQuantities = CalculateMealQuantities(orders, dietPlan);
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
            .OrderBy(kv => GetPlanSortOrder(kv.Key.MealId, kv.Key.DietVariantId, dietPlan))
            .ThenBy(kv => kv.Key.MealId))
        {
            var dietEntry = dietPlan.FirstOrDefault(d =>
                d.MealId == key.MealId && d.DietVariantId == key.DietVariantId);
            var snapshotItem = snapshot?.Items.FirstOrDefault(i =>
                i.MealId == key.MealId && i.DietVariantId == key.DietVariantId);
            var snapshotPayload = snapshotItem is null ? null : CreateSnapshotPayload(snapshotItem);

            items.Add(new ProductionPlanItem
            {
                ProductionPlanId = planId,
                MealId = key.MealId,
                MealName = snapshotItem?.MealName ?? dietEntry?.MealName ?? $"Posiłek #{key.MealId}",
                DietVariantId = key.DietVariantId,
                DietMenuPlanItemId = snapshotItem?.DietMenuPlanItemId,
                RecipeComponentVersionIds = snapshotItem is null
                    ? null
                    : string.Join(",", snapshotItem.Components.Select(c => c.RecipeComponentVersionId).Distinct()),
                M2SnapshotJson = snapshotPayload?.Json,
                M2SnapshotHash = snapshotPayload?.Hash,
                PlannedQuantity = quantity,
                CookedQuantity = 0,
                Status = ProductionItemStatus.Planned,
                // Model B-lite: grupa produkcyjna i ETA
                ProductionGroup = AssignProductionGroup(key.MealId, key.DietVariantId, dietPlan),
                EstimatedReadyTime = EstimateReadyTime(
                    AssignProductionGroup(key.MealId, key.DietVariantId, dietPlan), quantity),
            });
        }

        // 7. Oblicz Food Cost
        var mealQtyDict = mealQuantities
            .GroupBy(kv => kv.Key.MealId)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

        var foodCostReport = snapshot is null
            ? await _foodCostCalculator.CalculateAsync(mealQtyDict)
            : _foodCostCalculator.CalculateFromSnapshot(mealQuantities, snapshot);

        return new PlanGenerationResult
        {
            Plan = plan,
            Items = items,
            FoodCostReport = foodCostReport,
        };
    }

    private static DietPlanEntry MapSnapshotItemToDietPlanEntry(PublishedDietPlanItemDto item)
        => new()
        {
            PlanDate = item.PlanDate,
            PlanStatus = "Published",
            MealId = item.MealId,
            MealName = item.MealName,
            CategoryId = item.CategoryId,
            CategoryName = item.CategoryName,
            DietVariantId = item.DietVariantId,
            MealSlot = item.MealSlot,
            SortOrder = item.SortOrder,
            ServingMultiplier = item.ServingMultiplier,
            ServingWeightGrams = item.CookedWeightGrams ?? item.RawWeightGrams ?? item.ServingMultiplier * 100m,
        };

    /// <summary>
    /// Agreguje: ile porcji każdego posiłku (MealId+DietVariantId) potrzeba.
    /// </summary>
    private static Dictionary<(int MealId, int DietVariantId), int> CalculateMealQuantities(
        List<ActiveOrderEntry> orders,
        List<DietPlanEntry> dietPlan)
    {
        var quantities = new Dictionary<(int, int), int>();

        foreach (var order in orders)
        {
            // Znajdź posiłki przypisane do wariantu kalorycznego zamówienia
            var mealsForVariant = dietPlan
                .Where(d => d.DietVariantId == order.DietVariantId)
                .ToList();

            foreach (var meal in mealsForVariant)
            {
                var key = (meal.MealId, meal.DietVariantId);
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
    private static int AssignProductionGroup(int mealId, int dietVariantId, List<DietPlanEntry> dietPlan)
    {
        // Prosty algorytm: podział wg SortOrder/pozycji w planie diet
        var entry = dietPlan.FirstOrDefault(d => d.MealId == mealId && d.DietVariantId == dietVariantId)
            ?? dietPlan.FirstOrDefault(d => d.MealId == mealId);
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

        var index = entry?.SortOrder - 1 ?? dietPlan.FindIndex(d => d.MealId == mealId);

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

    private static int GetPlanSortOrder(int mealId, int dietVariantId, List<DietPlanEntry> dietPlan)
        => dietPlan.FirstOrDefault(d => d.MealId == mealId && d.DietVariantId == dietVariantId)?.SortOrder
            ?? dietPlan.FirstOrDefault(d => d.MealId == mealId)?.SortOrder
            ?? int.MaxValue;

    private static SnapshotPayload CreateSnapshotPayload(PublishedDietPlanItemDto item)
    {
        var json = JsonSerializer.Serialize(item, SnapshotJsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return new SnapshotPayload(json, hash);
    }

    private sealed record SnapshotPayload(string Json, string Hash);
}
