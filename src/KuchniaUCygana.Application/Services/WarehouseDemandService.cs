using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Warehouse;

namespace KuchniaUCygana.Application.Services;

public sealed class WarehouseDemandService : IWarehouseDemandService
{
    private readonly IDietDataProvider _dietDataProvider;
    private readonly IOrderDataProvider _orderDataProvider;
    private readonly IBatchRepository _batchRepository;

    public WarehouseDemandService(
        IDietDataProvider dietDataProvider,
        IOrderDataProvider orderDataProvider,
        IBatchRepository batchRepository)
    {
        _dietDataProvider = dietDataProvider;
        _orderDataProvider = orderDataProvider;
        _batchRepository = batchRepository;
    }

    public async Task<WarehouseDemandDto> GetDemandAsync(DateOnly startDate, int days)
    {
        var normalizedDays = Math.Clamp(days <= 0 ? 7 : days, 1, 31);
        var result = new WarehouseDemandDto
        {
            StartDate = startDate,
            RangeDays = normalizedDays,
        };

        var rowsByKey = new Dictionary<string, WarehouseDemandRowDto>();

        for (var offset = 0; offset < normalizedDays; offset++)
        {
            var planDate = startDate.AddDays(offset);
            var snapshot = await _dietDataProvider.GetPublishedPlanSnapshotAsync(planDate);
            var deliveries = (await _orderDataProvider.GetDeliveriesForDateAsync(planDate.ToDateTime(TimeOnly.MinValue)))
                .ToList();
            var orderItems = deliveries.SelectMany(delivery => delivery.Items).ToList();

            var day = new WarehouseDemandDayDto
            {
                PlanDate = planDate,
                Status = snapshot?.PlanStatus ?? "Missing",
                DietMenuPlanId = snapshot?.DietMenuPlanId,
                SnapshotItemCount = snapshot?.Items.Count ?? 0,
                OrderItemCount = orderItems.Count,
                AlertCount = snapshot?.Alerts.Count ?? 0,
                HasPublishedSnapshot = snapshot is not null
                    && string.Equals(snapshot.PlanStatus, "Published", StringComparison.OrdinalIgnoreCase),
            };

            if (snapshot is null)
            {
                day.MissingSnapshotMatches = orderItems.Count;
                result.Days.Add(day);
                continue;
            }

            var matchedOrderItemIds = 0;
            foreach (var orderItem in orderItems)
            {
                var snapshotItems = FindSnapshotItems(snapshot, orderItem).ToList();
                if (snapshotItems.Count == 0)
                {
                    day.MissingSnapshotMatches++;
                    continue;
                }

                matchedOrderItemIds++;
                foreach (var snapshotItem in snapshotItems)
                {
                    AddIngredientDemand(rowsByKey, snapshotItem);
                    AddPackagingDemand(rowsByKey, snapshotItem);
                }
            }

            day.MatchedOrderItemCount = matchedOrderItemIds;
            result.Days.Add(day);
        }

        foreach (var row in rowsByKey.Values)
        {
            await EnrichAvailabilityAsync(row);
        }

        result.Rows = rowsByKey.Values
            .OrderByDescending(row => row.ShortageQuantity)
            .ThenBy(row => row.ResourceType)
            .ThenBy(row => row.ResourceName)
            .ToList();
        result.TotalOrderItems = result.Days.Sum(day => day.OrderItemCount);
        result.TotalRows = result.Rows.Count;
        result.ShortageRows = result.Rows.Count(row => row.ShortageQuantity > 0);

        return result;
    }

    private static IEnumerable<PublishedDietPlanItemDto> FindSnapshotItems(
        PublishedDietPlanSnapshotDto snapshot,
        OrderItemInfo orderItem)
    {
        if (orderItem.DietMenuPlanItemId.HasValue)
        {
            var item = snapshot.Items.FirstOrDefault(snapshotItem =>
                snapshotItem.DietMenuPlanItemId == orderItem.DietMenuPlanItemId.Value);
            if (item is not null)
            {
                yield return item;
            }

            yield break;
        }

        if (orderItem.MealId.HasValue)
        {
            foreach (var item in snapshot.Items.Where(snapshotItem =>
                snapshotItem.MealId == orderItem.MealId.Value
                && snapshotItem.DietVariantId == orderItem.DietVariantId
                && (!orderItem.MealVariantId.HasValue || snapshotItem.MealVariantId == orderItem.MealVariantId)))
            {
                yield return item;
            }

            yield break;
        }

        foreach (var item in snapshot.Items.Where(snapshotItem =>
            snapshotItem.DietVariantId == orderItem.DietVariantId))
        {
            yield return item;
        }
    }

    private static void AddIngredientDemand(
        Dictionary<string, WarehouseDemandRowDto> rowsByKey,
        PublishedDietPlanItemDto snapshotItem)
    {
        if (snapshotItem.AggregateIngredients.Count > 0)
        {
            foreach (var ingredient in snapshotItem.AggregateIngredients)
            {
                AddDemand(
                    rowsByKey,
                    resourceType: "Ingredient",
                    resourceName: ingredient.IngredientName,
                    ingredientId: ingredient.IngredientId,
                    stockItemId: ingredient.StockItemId,
                    warehouseCategoryId: ingredient.WarehouseCategoryId,
                    warehouseCategoryName: ingredient.WarehouseCategoryName,
                    requiredQuantity: ingredient.NetWeightInGrams * snapshotItem.ServingMultiplier,
                    unit: "g",
                    mealName: snapshotItem.MealName,
                    dietMenuPlanItemId: snapshotItem.DietMenuPlanItemId);
            }

            return;
        }

        foreach (var component in snapshotItem.Components)
        {
            foreach (var ingredient in component.Ingredients)
            {
                AddDemand(
                    rowsByKey,
                    resourceType: "Ingredient",
                    resourceName: ingredient.IngredientName,
                    ingredientId: ingredient.IngredientId,
                    stockItemId: ingredient.StockItemId,
                    warehouseCategoryId: ingredient.WarehouseCategoryId,
                    warehouseCategoryName: ingredient.WarehouseCategoryName,
                    requiredQuantity: ingredient.WeightInGrams
                        * component.QuantityPerServing
                        * snapshotItem.ServingMultiplier,
                    unit: "g",
                    mealName: snapshotItem.MealName,
                    dietMenuPlanItemId: snapshotItem.DietMenuPlanItemId);
            }
        }
    }

    private static void AddPackagingDemand(
        Dictionary<string, WarehouseDemandRowDto> rowsByKey,
        PublishedDietPlanItemDto snapshotItem)
    {
        var hasAggregatePackaging = snapshotItem.PackagingRequirements.Any(packaging =>
            packaging.RecipeComponentVersionId.HasValue);

        foreach (var packaging in snapshotItem.PackagingRequirements)
        {
            AddDemand(
                rowsByKey,
                resourceType: "Packaging",
                resourceName: packaging.ResourceName,
                ingredientId: null,
                stockItemId: packaging.StockItemId,
                warehouseCategoryId: packaging.WarehouseCategoryId,
                warehouseCategoryName: null,
                requiredQuantity: GetPackagingQuantityPerServing(snapshotItem, packaging),
                unit: string.IsNullOrWhiteSpace(packaging.Unit) ? "pcs" : packaging.Unit,
                mealName: snapshotItem.MealName,
                dietMenuPlanItemId: snapshotItem.DietMenuPlanItemId);
        }

        if (hasAggregatePackaging)
        {
            return;
        }

        foreach (var component in snapshotItem.Components)
        {
            foreach (var packaging in component.PackagingRequirements)
            {
                AddDemand(
                    rowsByKey,
                    resourceType: "Packaging",
                    resourceName: packaging.ResourceName,
                    ingredientId: null,
                    stockItemId: packaging.StockItemId,
                    warehouseCategoryId: packaging.WarehouseCategoryId,
                    warehouseCategoryName: null,
                    requiredQuantity: packaging.Quantity
                        * component.QuantityPerServing
                        * snapshotItem.ServingMultiplier,
                    unit: string.IsNullOrWhiteSpace(packaging.Unit) ? "pcs" : packaging.Unit,
                    mealName: snapshotItem.MealName,
                    dietMenuPlanItemId: snapshotItem.DietMenuPlanItemId);
            }
        }
    }

    private static void AddDemand(
        Dictionary<string, WarehouseDemandRowDto> rowsByKey,
        string resourceType,
        string resourceName,
        int? ingredientId,
        int? stockItemId,
        int? warehouseCategoryId,
        string? warehouseCategoryName,
        decimal requiredQuantity,
        string unit,
        string mealName,
        int dietMenuPlanItemId)
    {
        if (requiredQuantity <= 0 || (!stockItemId.HasValue && !warehouseCategoryId.HasValue))
        {
            return;
        }

        var key = stockItemId.HasValue
            ? $"{resourceType}:S:{stockItemId.Value}"
            : $"{resourceType}:C:{warehouseCategoryId!.Value}:{resourceName}";

        if (!rowsByKey.TryGetValue(key, out var row))
        {
            row = new WarehouseDemandRowDto
            {
                ResourceType = resourceType,
                ResourceName = resourceName,
                IngredientId = ingredientId,
                StockItemId = stockItemId,
                WarehouseCategoryId = warehouseCategoryId,
                WarehouseCategoryName = warehouseCategoryName,
                SelectionMode = stockItemId.HasValue ? "StockItem" : "WarehouseCategory",
                Unit = unit,
            };
            rowsByKey[key] = row;
        }

        row.RequiredQuantity += requiredQuantity;

        if (!row.SourceMeals.Contains(mealName))
        {
            row.SourceMeals.Add(mealName);
        }

        if (!row.SourceDietMenuPlanItemIds.Contains(dietMenuPlanItemId))
        {
            row.SourceDietMenuPlanItemIds.Add(dietMenuPlanItemId);
        }
    }

    private async Task EnrichAvailabilityAsync(WarehouseDemandRowDto row)
    {
        var batches = row.StockItemId.HasValue
            ? await _batchRepository.GetActiveBatchesByStockItemAsync(row.StockItemId.Value)
            : await _batchRepository.GetActiveBatchesByWarehouseCategoryAsync(row.WarehouseCategoryId!.Value);

        var orderedBatches = batches
            .Where(batch => !batch.IsDepleted && batch.CurrentQuantity > 0)
            .OrderBy(batch => batch.ExpiryDate ?? DateTimeOffset.MaxValue)
            .ThenBy(batch => batch.Id)
            .ToList();

        row.AvailableQuantity = orderedBatches.Sum(batch => batch.CurrentQuantity);
        row.ShortageQuantity = Math.Max(0, row.RequiredQuantity - row.AvailableQuantity);

        var earliestBatch = orderedBatches.FirstOrDefault();
        row.EarliestBatchId = earliestBatch?.Id;
        row.EarliestExpiryDate = earliestBatch?.ExpiryDate;
        row.RiskLabel = GetRiskLabel(row, earliestBatch);
    }

    private static string GetRiskLabel(WarehouseDemandRowDto row, Batch? earliestBatch)
    {
        if (row.ShortageQuantity > 0)
        {
            return "Shortage";
        }

        if (earliestBatch?.ExpiryDate is not DateTimeOffset expiryDate)
        {
            return "Ok";
        }

        if (expiryDate <= DateTimeOffset.UtcNow)
        {
            return "Expired";
        }

        return expiryDate <= DateTimeOffset.UtcNow.AddDays(3)
            ? "ExpiryRisk"
            : "Ok";
    }

    private static decimal GetPackagingQuantityPerServing(
        PublishedDietPlanItemDto snapshotItem,
        PackagingRequirementDto packaging)
        => packaging.RecipeComponentVersionId.HasValue
            ? packaging.Quantity * snapshotItem.ServingMultiplier
            : packaging.Quantity;
}
