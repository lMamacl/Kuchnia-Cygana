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
