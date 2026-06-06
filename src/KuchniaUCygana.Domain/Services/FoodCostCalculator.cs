using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Domain.Services;

/// <summary>
/// Pozycja kalkulacji food cost — ile danego składnika potrzeba.
/// </summary>
public sealed class FoodCostEntry
{
    public int IngredientId { get; set; }

    public int? StockItemId { get; set; }

    public int? WarehouseCategoryId { get; set; }

    public string? WarehouseCategoryName { get; set; }

    public int? RecipeComponentVersionId { get; set; }

    public string? ComponentName { get; set; }

    public string IngredientName { get; set; } = string.Empty;

    public decimal TotalWeightGrams { get; set; }

    /// <summary>Ile dostępne w magazynie (uzupełniane przez WarehouseService).</summary>
    public decimal? AvailableInStock { get; set; }

    /// <summary>Brak (TotalWeightGrams - AvailableInStock), jeśli ujemny → brak.</summary>
    public decimal? Shortage { get; set; }
}

/// <summary>
/// Wynik kalkulacji food cost.
/// </summary>
public sealed class FoodCostReport
{
    /// <summary>Łączna lista składników z zagregowanymi ilościami.</summary>
    public List<FoodCostEntry> Entries { get; set; } = new();

    /// <summary>Składniki, których brakuje (Shortage > 0).</summary>
    public List<FoodCostEntry> Shortages => Entries.Where(e => e.Shortage > 0).ToList();

    /// <summary>Czy wszystkie składniki dostępne.</summary>
    public bool AllAvailable => Shortages.Count == 0;
}

/// <summary>
/// Kalkulator Food Cost — oblicza zapotrzebowanie materiałowe na podstawie
/// receptur (M2) i ilości porcji do ugotowania.
/// Korzysta z prawdziwego IDietDataProvider (adapter M2, nie mock).
/// </summary>
public sealed class FoodCostCalculator
{
    private readonly IDietDataProvider _dietDataProvider;

    public FoodCostCalculator(IDietDataProvider dietDataProvider)
    {
        _dietDataProvider = dietDataProvider;
    }

    /// <summary>
    /// Oblicza zapotrzebowanie materiałowe dla listy posiłków z ilościami.
    /// </summary>
    /// <param name="mealQuantities">Słownik: MealId → ilość porcji do ugotowania.</param>
    /// <returns>Zagregowana lista składników z łącznymi gramaturami.</returns>
    public async Task<FoodCostReport> CalculateAsync(Dictionary<int, int> mealQuantities)
    {
        var aggregated = new Dictionary<int, FoodCostEntry>();

        foreach (var (mealId, quantity) in mealQuantities)
        {
            var recipe = await _dietDataProvider.GetRecipeForMealAsync(mealId);

            foreach (var ingredient in recipe)
            {
                var totalGrams = ingredient.WeightInGrams * quantity;

                if (!ingredient.StockItemId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"Skladnik '{ingredient.IngredientName}' (ID {ingredient.IngredientId}) nie jest polaczony z magazynem.");
                }

                var stockItemId = ingredient.StockItemId.Value;

                if (aggregated.TryGetValue(stockItemId, out var existing))
                {
                    existing.TotalWeightGrams += totalGrams;
                }
                else
                {
                    aggregated[stockItemId] = new FoodCostEntry
                    {
                        IngredientId = ingredient.IngredientId,
                        StockItemId = stockItemId,
                        IngredientName = ingredient.IngredientName,
                        TotalWeightGrams = totalGrams,
                    };
                }
            }
        }

        return new FoodCostReport
        {
            Entries = aggregated.Values
                .OrderByDescending(e => e.TotalWeightGrams)
                .ToList(),
        };
    }

    public FoodCostReport CalculateFromSnapshot(
        IReadOnlyDictionary<ProductionMealKey, int> mealQuantities,
        PublishedDietPlanSnapshotDto snapshot)
    {
        var aggregated = new Dictionary<string, FoodCostEntry>();

        foreach (var item in snapshot.Items)
        {
            var key = new ProductionMealKey(
                item.MealId,
                item.DietVariantId,
                item.DietMenuPlanItemId,
                item.MealVariantId);
            if (!mealQuantities.TryGetValue(key, out var orderedPortions))
            {
                continue;
            }

            if (item.AggregateIngredients.Count > 0)
            {
                foreach (var ingredient in item.AggregateIngredients)
                {
                    AddSnapshotIngredient(
                        aggregated,
                        ingredient.IngredientId,
                        ingredient.IngredientName,
                        ingredient.StockItemId,
                        ingredient.WarehouseCategoryId,
                        ingredient.WarehouseCategoryName,
                        ingredient.SourceRecipeComponentVersionIds.Count > 0
                            ? ingredient.SourceRecipeComponentVersionIds[0]
                            : null,
                        ingredient.SourceComponentNames.FirstOrDefault(),
                        ingredient.NetWeightInGrams * item.ServingMultiplier * orderedPortions);
                }

                continue;
            }

            foreach (var component in item.Components)
            {
                foreach (var ingredient in component.Ingredients)
                {
                    AddSnapshotIngredient(
                        aggregated,
                        ingredient.IngredientId,
                        ingredient.IngredientName,
                        ingredient.StockItemId,
                        ingredient.WarehouseCategoryId,
                        ingredient.WarehouseCategoryName,
                        component.RecipeComponentVersionId,
                        component.ComponentName,
                        ingredient.WeightInGrams
                            * component.QuantityPerServing
                            * item.ServingMultiplier
                            * orderedPortions);
                }
            }
        }

        return new FoodCostReport
        {
            Entries = aggregated.Values
                .OrderByDescending(e => e.TotalWeightGrams)
                .ToList(),
        };
    }

    private static void AddSnapshotIngredient(
        Dictionary<string, FoodCostEntry> aggregated,
        int ingredientId,
        string ingredientName,
        int? stockItemId,
        int? warehouseCategoryId,
        string? warehouseCategoryName,
        int? recipeComponentVersionId,
        string? componentName,
        decimal totalGrams)
    {
        if (!stockItemId.HasValue && !warehouseCategoryId.HasValue)
        {
            throw new InvalidOperationException(
                $"Skladnik '{ingredientName}' (ID {ingredientId}) nie ma mapowania magazynowego.");
        }

        var aggregateKey = stockItemId.HasValue
            ? $"S:{stockItemId.Value}"
            : $"C:{warehouseCategoryId!.Value}";

        if (aggregated.TryGetValue(aggregateKey, out var existing))
        {
            existing.TotalWeightGrams += totalGrams;
            return;
        }

        aggregated[aggregateKey] = new FoodCostEntry
        {
            IngredientId = ingredientId,
            StockItemId = stockItemId,
            WarehouseCategoryId = warehouseCategoryId,
            WarehouseCategoryName = warehouseCategoryName,
            RecipeComponentVersionId = recipeComponentVersionId,
            ComponentName = componentName,
            IngredientName = ingredientName,
            TotalWeightGrams = totalGrams,
        };
    }

    /// <summary>
    /// Wzbogaca raport o dane magazynowe (dostępność).
    /// Wywoływane przez WarehouseService po wyliczeniu food cost.
    /// </summary>
    public void EnrichWithStockData(FoodCostReport report, Dictionary<int, decimal> stockByStockItemId)
    {
        foreach (var entry in report.Entries)
        {
            if (entry.StockItemId.HasValue && stockByStockItemId.TryGetValue(entry.StockItemId.Value, out var available))
            {
                entry.AvailableInStock = available;
                entry.Shortage = entry.TotalWeightGrams - available > 0
                    ? entry.TotalWeightGrams - available
                    : 0;
            }
            else
            {
                entry.AvailableInStock = 0;
                entry.Shortage = entry.TotalWeightGrams;
            }
        }
    }
}
