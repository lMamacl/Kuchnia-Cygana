using System.Text.Json;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Domain.Interfaces.Warehouse;

namespace KuchniaUCygana.Application.Services;

public sealed class WarehouseDemandService : IWarehouseDemandService
{
    private readonly IDietDataProvider _dietDataProvider;
    private readonly IOrderDataProvider _orderDataProvider;
    private readonly IBatchRepository _batchRepository;
    private readonly IProductionPlanRepository? _productionPlanRepository;

    public WarehouseDemandService(
        IDietDataProvider dietDataProvider,
        IOrderDataProvider orderDataProvider,
        IBatchRepository batchRepository,
        IProductionPlanRepository? productionPlanRepository = null)
    {
        _dietDataProvider = dietDataProvider;
        _orderDataProvider = orderDataProvider;
        _batchRepository = batchRepository;
        _productionPlanRepository = productionPlanRepository;
    }

    public Task<WarehouseDemandDto> GetDemandAsync(DateOnly startDate, int days)
        => GetDemandAsync(new WarehouseDemandFilterDto
        {
            StartDate = startDate,
            Days = days,
        });

    public async Task<WarehouseDemandDto> GetDemandAsync(WarehouseDemandFilterDto filter)
    {
        var normalized = NormalizeFilter(filter);
        var result = new WarehouseDemandDto
        {
            Filter = normalized,
            StartDate = normalized.StartDate,
            RangeDays = normalized.Days,
            Page = normalized.Page,
            PageSize = normalized.PageSize,
        };

        var rowsByKey = new Dictionary<string, WarehouseDemandRowDto>();

        for (var offset = 0; offset < normalized.Days; offset++)
        {
            var planDate = normalized.StartDate.AddDays(offset);
            var productionDemandSource = await GetProductionDemandSourceAsync(planDate);
            if (productionDemandSource is not null)
            {
                var productionDay = new WarehouseDemandDayDto
                {
                    PlanDate = planDate,
                    Status = "ProductionSnapshot",
                    DietMenuPlanId = productionDemandSource.SnapshotItems.FirstOrDefault()?.SnapshotItem.DietMenuPlanId,
                    SnapshotItemCount = productionDemandSource.SnapshotItems.Count,
                    OrderItemCount = productionDemandSource.SnapshotItems.Sum(item => item.PlannedQuantity),
                    MatchedOrderItemCount = productionDemandSource.SnapshotItems.Sum(item => item.PlannedQuantity),
                    MissingSnapshotMatches = productionDemandSource.MissingSnapshotCount,
                    HasPublishedSnapshot = productionDemandSource.SnapshotItems.Count > 0,
                };

                foreach (var (snapshotItem, plannedQuantity) in productionDemandSource.SnapshotItems)
                {
                    AddIngredientDemand(rowsByKey, snapshotItem, plannedQuantity);
                    AddPackagingDemand(rowsByKey, snapshotItem, plannedQuantity);
                }

                result.Days.Add(productionDay);
                continue;
            }

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

        var stockItemBatches = new Dictionary<int, IReadOnlyList<Batch>>();
        var warehouseCategoryBatches = new Dictionary<int, IReadOnlyList<Batch>>();
        var reservedQuantityByBatchId = new Dictionary<int, decimal>();
        foreach (var row in rowsByKey.Values
            .OrderBy(GetDemandAllocationPriority)
            .ThenBy(row => row.ResourceType)
            .ThenBy(row => row.ResourceName))
        {
            await EnrichAvailabilityAsync(row, stockItemBatches, warehouseCategoryBatches, reservedQuantityByBatchId);
        }

        var allRows = rowsByKey.Values
            .OrderByDescending(row => row.ShortageQuantity)
            .ThenBy(row => row.ResourceType)
            .ThenBy(row => row.ResourceName)
            .ToList();
        result.TotalOrderItems = result.Days.Sum(day => day.OrderItemCount);
        result.TotalRows = allRows.Count;
        result.ShortageRows = allRows.Count(row => row.ShortageQuantity > 0);

        var filteredRows = allRows
            .Where(row => MatchesFilter(row, normalized))
            .ToList();

        result.FilteredRows = filteredRows.Count;
        var sortedRows = SortRows(filteredRows, normalized).ToList();
        var totalPages = filteredRows.Count == 0
            ? 0
            : (int)Math.Ceiling((double)filteredRows.Count / normalized.PageSize);
        var page = totalPages == 0 ? 1 : Math.Min(normalized.Page, totalPages);

        if (normalized.ExportAll)
        {
            normalized.Page = 1;
            normalized.PageSize = Math.Max(1, sortedRows.Count);
            result.Rows = sortedRows;
            result.Filter = normalized;
            result.Page = normalized.Page;
            result.PageSize = normalized.PageSize;
            return result;
        }

        result.Rows = sortedRows
            .Skip((page - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .ToList();
        normalized.Page = page;
        result.Filter = normalized;
        result.Page = page;
        result.PageSize = normalized.PageSize;

        return result;
    }

    private async Task<ProductionDemandSource?> GetProductionDemandSourceAsync(DateOnly planDate)
    {
        if (_productionPlanRepository is null)
        {
            return null;
        }

        var productionPlan = await _productionPlanRepository.GetByDateAsync(planDate);
        if (productionPlan is null)
        {
            return null;
        }

        var planItems = (await _productionPlanRepository.GetPlanItemsAsync(productionPlan.Id)).ToList();
        var snapshotItems = new List<ProductionDemandSnapshotItem>();
        var missingSnapshotCount = 0;
        foreach (var item in planItems.Where(item => item.PlannedQuantity > 0))
        {
            var snapshotItem = TryDeserializeSnapshotItem(item);
            if (snapshotItem is null)
            {
                missingSnapshotCount += item.PlannedQuantity;
                continue;
            }

            snapshotItems.Add(new ProductionDemandSnapshotItem(snapshotItem, item.PlannedQuantity));
        }

        return new ProductionDemandSource(snapshotItems, missingSnapshotCount);
    }

    private static PublishedDietPlanItemDto? TryDeserializeSnapshotItem(ProductionPlanItem item)
    {
        if (string.IsNullOrWhiteSpace(item.M2SnapshotJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PublishedDietPlanItemDto>(item.M2SnapshotJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static WarehouseDemandFilterDto NormalizeFilter(WarehouseDemandFilterDto filter)
        => new()
        {
            StartDate = filter.StartDate == default ? DateOnly.FromDateTime(DateTime.Today) : filter.StartDate,
            Days = Math.Clamp(filter.Days <= 0 ? 7 : filter.Days, 1, 31),
            Search = NormalizeSearch(filter.Search),
            ResourceType = NormalizeResourceType(filter.ResourceType),
            Risk = NormalizeRisk(filter.Risk),
            SortBy = NormalizeSortBy(filter.SortBy),
            SortDirection = string.Equals(filter.SortDirection, "desc", StringComparison.OrdinalIgnoreCase)
                ? "desc"
                : "asc",
            Page = Math.Max(filter.Page, 1),
            PageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 10, 100),
            ExportAll = filter.ExportAll,
        };

    private static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var trimmed = search.Trim();
        return trimmed.Length > 120 ? trimmed[..120] : trimmed;
    }

    private static string? NormalizeResourceType(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "ingredient" or "ingredients" or "skladnik" or "składnik" => "Ingredient",
            "packaging" or "package" or "opakowanie" => "Packaging",
            _ => null,
        };
    }

    private static string? NormalizeRisk(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "shortage" or "brak" or "braki" => "Shortage",
            "missingmapping" or "missing-mapping" or "brakmapowania" or "brak-mapowania" => "MissingMapping",
            "expired" or "przeterminowane" => "Expired",
            "expiryrisk" or "expiry-risk" or "risk" or "ryzyko" => "ExpiryRisk",
            "ok" => "Ok",
            _ => null,
        };
    }

    private static string NormalizeSortBy(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "required" or "requiredquantity" or "potrzeba" => "required",
            "available" or "availablequantity" or "dostepne" or "dostępne" => "available",
            "shortage" or "brak" => "shortage",
            "type" or "resourcetype" => "type",
            "risk" or "ryzyko" => "risk",
            _ => "name",
        };
    }

    private static bool MatchesFilter(WarehouseDemandRowDto row, WarehouseDemandFilterDto filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.ResourceType)
            && !string.Equals(row.ResourceType, filter.ResourceType, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Risk)
            && !string.Equals(row.RiskLabel, filter.Risk, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(filter.Search)
            && !WarehouseDemandSearchText(row).Contains(filter.Search, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static string WarehouseDemandSearchText(WarehouseDemandRowDto row)
        => string.Join(" ", new[]
            {
                row.ResourceName,
                row.ResourceType,
                row.WarehouseCategoryName,
                row.WarehouseCategoryId?.ToString(),
                row.StockItemId?.ToString(),
                row.IngredientId?.ToString(),
                row.SelectionMode,
                row.RiskLabel,
            }
            .Concat(row.SourceMeals)
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    private static IEnumerable<WarehouseDemandRowDto> SortRows(
        IReadOnlyList<WarehouseDemandRowDto> rows,
        WarehouseDemandFilterDto filter)
    {
        var descending = string.Equals(filter.SortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return (filter.SortBy, descending) switch
        {
            ("required", true) => rows.OrderByDescending(row => row.RequiredQuantity).ThenBy(row => row.ResourceName),
            ("required", false) => rows.OrderBy(row => row.RequiredQuantity).ThenBy(row => row.ResourceName),
            ("available", true) => rows.OrderByDescending(row => row.AvailableQuantity).ThenBy(row => row.ResourceName),
            ("available", false) => rows.OrderBy(row => row.AvailableQuantity).ThenBy(row => row.ResourceName),
            ("shortage", false) => rows.OrderBy(row => row.ShortageQuantity).ThenBy(row => row.ResourceName),
            ("type", true) => rows.OrderByDescending(row => row.ResourceType).ThenBy(row => row.ResourceName),
            ("type", false) => rows.OrderBy(row => row.ResourceType).ThenBy(row => row.ResourceName),
            ("risk", true) => rows.OrderByDescending(row => row.RiskLabel).ThenBy(row => row.ResourceName),
            ("risk", false) => rows.OrderBy(row => row.RiskLabel).ThenBy(row => row.ResourceName),
            ("name", true) => rows.OrderByDescending(row => row.ResourceName),
            ("name", false) => rows.OrderBy(row => row.ResourceName),
            _ => rows.OrderByDescending(row => row.ShortageQuantity)
                .ThenBy(row => row.ResourceType)
                .ThenBy(row => row.ResourceName),
        };
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
        PublishedDietPlanItemDto snapshotItem,
        int quantityMultiplier = 1)
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
                    requiredQuantity: ingredient.NetWeightInGrams * snapshotItem.ServingMultiplier * quantityMultiplier,
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
                        * snapshotItem.ServingMultiplier
                        * quantityMultiplier,
                    unit: "g",
                    mealName: snapshotItem.MealName,
                    dietMenuPlanItemId: snapshotItem.DietMenuPlanItemId);
            }
        }
    }

    private static void AddPackagingDemand(
        Dictionary<string, WarehouseDemandRowDto> rowsByKey,
        PublishedDietPlanItemDto snapshotItem,
        int quantityMultiplier = 1)
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
                requiredQuantity: GetPackagingQuantityPerServing(snapshotItem, packaging) * quantityMultiplier,
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
                        * snapshotItem.ServingMultiplier
                        * quantityMultiplier,
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
        if (requiredQuantity <= 0)
        {
            return;
        }

        var selectionMode = stockItemId.HasValue
            ? "StockItem"
            : warehouseCategoryId.HasValue ? "WarehouseCategory" : "MissingMapping";
        var missingMappingKey = ingredientId.HasValue
            ? ingredientId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : resourceName.Trim().ToUpperInvariant();
        var key = selectionMode switch
        {
            "StockItem" => $"{resourceType}:S:{stockItemId!.Value}",
            "WarehouseCategory" => $"{resourceType}:C:{warehouseCategoryId!.Value}:{resourceName}",
            _ => $"{resourceType}:M:{missingMappingKey}:{unit}",
        };

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
                SelectionMode = selectionMode,
                Unit = unit,
            };
            rowsByKey[key] = row;
        }

        row.RequiredQuantity += requiredQuantity;
        if (row.SelectionMode == "MissingMapping")
        {
            row.AvailableQuantity = 0m;
            row.ShortageQuantity = row.RequiredQuantity;
            row.RiskLabel = "MissingMapping";
        }

        if (!row.SourceMeals.Contains(mealName))
        {
            row.SourceMeals.Add(mealName);
        }

        if (!row.SourceDietMenuPlanItemIds.Contains(dietMenuPlanItemId))
        {
            row.SourceDietMenuPlanItemIds.Add(dietMenuPlanItemId);
        }
    }

    private async Task EnrichAvailabilityAsync(
        WarehouseDemandRowDto row,
        Dictionary<int, IReadOnlyList<Batch>> stockItemBatches,
        Dictionary<int, IReadOnlyList<Batch>> warehouseCategoryBatches,
        Dictionary<int, decimal> reservedQuantityByBatchId)
    {
        if (row.SelectionMode == "MissingMapping")
        {
            row.AvailableQuantity = 0m;
            row.ShortageQuantity = row.RequiredQuantity;
            row.RiskLabel = "MissingMapping";
            return;
        }

        var batches = await GetActiveBatchesForDemandRowAsync(row, stockItemBatches, warehouseCategoryBatches);

        var orderedBatches = batches
            .Where(batch => !batch.IsDepleted && batch.CurrentQuantity > 0)
            .OrderBy(batch => batch.ExpiryDate ?? DateTimeOffset.MaxValue)
            .ThenBy(batch => batch.Id)
            .ToList();

        var availableQuantity = 0m;
        foreach (var batch in orderedBatches)
        {
            availableQuantity += GetRemainingBatchQuantity(batch, reservedQuantityByBatchId);
        }

        row.AvailableQuantity = availableQuantity;
        row.ShortageQuantity = Math.Max(0, row.RequiredQuantity - row.AvailableQuantity);

        var earliestBatch = orderedBatches.FirstOrDefault(batch =>
            GetRemainingBatchQuantity(batch, reservedQuantityByBatchId) > 0);
        row.EarliestBatchId = earliestBatch?.Id;
        row.EarliestExpiryDate = earliestBatch?.ExpiryDate;
        row.RiskLabel = GetRiskLabel(row, earliestBatch);

        ReservePreviewQuantity(row, orderedBatches, reservedQuantityByBatchId);
    }

    private static int GetDemandAllocationPriority(WarehouseDemandRowDto row)
        => row.SelectionMode switch
        {
            "StockItem" => 0,
            "WarehouseCategory" => 1,
            _ => 2,
        };

    private static decimal GetRemainingBatchQuantity(
        Batch batch,
        IReadOnlyDictionary<int, decimal> reservedQuantityByBatchId)
    {
        reservedQuantityByBatchId.TryGetValue(batch.Id, out var reservedQuantity);
        return Math.Max(0m, batch.CurrentQuantity - reservedQuantity);
    }

    private static void ReservePreviewQuantity(
        WarehouseDemandRowDto row,
        IReadOnlyList<Batch> orderedBatches,
        Dictionary<int, decimal> reservedQuantityByBatchId)
    {
        var quantityToReserve = row.RequiredQuantity;
        if (quantityToReserve <= 0m)
        {
            return;
        }

        foreach (var batch in orderedBatches)
        {
            var remainingQuantity = GetRemainingBatchQuantity(batch, reservedQuantityByBatchId);
            if (remainingQuantity <= 0m)
            {
                continue;
            }

            var reservedQuantity = Math.Min(remainingQuantity, quantityToReserve);
            if (!reservedQuantityByBatchId.TryAdd(batch.Id, reservedQuantity))
            {
                reservedQuantityByBatchId[batch.Id] += reservedQuantity;
            }

            quantityToReserve -= reservedQuantity;
            if (quantityToReserve <= 0m)
            {
                return;
            }
        }
    }

    private async Task<IReadOnlyList<Batch>> GetActiveBatchesForDemandRowAsync(
        WarehouseDemandRowDto row,
        Dictionary<int, IReadOnlyList<Batch>> stockItemBatches,
        Dictionary<int, IReadOnlyList<Batch>> warehouseCategoryBatches)
    {
        if (row.StockItemId.HasValue)
        {
            var stockItemId = row.StockItemId.Value;
            if (!stockItemBatches.TryGetValue(stockItemId, out var cachedBatches))
            {
                cachedBatches = (await _batchRepository.GetActiveBatchesByStockItemAsync(stockItemId)).ToList();
                stockItemBatches[stockItemId] = cachedBatches;
            }

            return cachedBatches;
        }

        var warehouseCategoryId = row.WarehouseCategoryId!.Value;
        if (!warehouseCategoryBatches.TryGetValue(warehouseCategoryId, out var cachedCategoryBatches))
        {
            cachedCategoryBatches = (await _batchRepository.GetActiveBatchesByWarehouseCategoryAsync(warehouseCategoryId)).ToList();
            warehouseCategoryBatches[warehouseCategoryId] = cachedCategoryBatches;
        }

        return cachedCategoryBatches;
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

    private sealed record ProductionDemandSource(
        IReadOnlyList<ProductionDemandSnapshotItem> SnapshotItems,
        int MissingSnapshotCount);

    private sealed record ProductionDemandSnapshotItem(
        PublishedDietPlanItemDto SnapshotItem,
        int PlannedQuantity);
}
