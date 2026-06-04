using AutoMapper;
using System.Text.Json;
using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Domain.Services;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Application.Services;

public sealed class ProductionService : IProductionService
{
    private readonly ProductionPlanGenerator _planGenerator;
    private readonly IProductionPlanRepository _planRepository;
    private readonly IRepository<ProductionPlanItem> _itemRepository;
    private readonly IDietDataProvider _dietProvider;
    private readonly IPackingService _packingService;
    private readonly FefoService _fefoService;
    private readonly IMapper _mapper;
    private readonly ILogger<ProductionService> _logger;

    public ProductionService(
        ProductionPlanGenerator planGenerator,
        IProductionPlanRepository planRepository,
        IRepository<ProductionPlanItem> itemRepository,
        IDietDataProvider dietProvider,
        IPackingService packingService,
        FefoService fefoService,
        IMapper mapper,
        ILogger<ProductionService> logger)
    {
        _planGenerator = planGenerator;
        _planRepository = planRepository;
        _itemRepository = itemRepository;
        _dietProvider = dietProvider;
        _packingService = packingService;
        _fefoService = fefoService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ProductionPlanDto> GenerateDailyPlanAsync(CreateProductionPlanRequest request)
    {
        var result = await _planGenerator.GeneratePlanAsync(request.ProductionDate, "System");

        foreach (var item in result.Items)
        {
            await _itemRepository.InsertAsync(item);
        }

        var dto = _mapper.Map<ProductionPlanDto>(result.Plan);
        dto.Items = _mapper.Map<List<ProductionPlanItemDto>>(result.Items);

        if (result.FoodCostReport != null)
        {
            dto.FoodCostReport = _mapper.Map<FoodCostReportDto>(result.FoodCostReport);
        }

        _logger.LogInformation("Wygenerowano plan produkcji na dzien {Date}", request.ProductionDate);

        return dto;
    }

    public async Task<ProductionPlanDto?> GetDailyPlanByDateAsync(DateOnly date)
    {
        var plan = await _planRepository.GetByDateAsync(date);
        if (plan == null) return null;

        var items = await _planRepository.GetPlanItemsAsync(plan.Id);

        var dto = _mapper.Map<ProductionPlanDto>(plan);
        dto.Items = _mapper.Map<List<ProductionPlanItemDto>>(items);

        return dto;
    }

    public async Task<ProductionPlanDto?> GetPlanByIdAsync(int planId)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan == null) return null;

        var items = await _planRepository.GetPlanItemsAsync(plan.Id);

        var dto = _mapper.Map<ProductionPlanDto>(plan);
        dto.Items = _mapper.Map<List<ProductionPlanItemDto>>(items);

        return dto;
    }

    public async Task<KitchenDashboardDto> GetKitchenDashboardAsync(KitchenDashboardFilterDto filter)
    {
        var normalized = NormalizeKitchenDashboardFilter(filter);
        var dashboard = new KitchenDashboardDto
        {
            Filter = normalized,
            Items = new PagedResultDto<KitchenDashboardItemDto>
            {
                Page = normalized.Page,
                PageSize = normalized.PageSize,
                TotalCount = 0,
            },
        };

        var plan = await _planRepository.GetByDateAsync(normalized.Date);
        if (plan is null)
        {
            return dashboard;
        }

        dashboard.Plan = _mapper.Map<ProductionPlanDto>(plan);
        dashboard.Plan.Items = new List<ProductionPlanItemDto>();

        var itemQuery = new ProductionPlanItemQuery
        {
            PlanId = plan.Id,
            Search = normalized.Search,
            Status = ParseProductionStatus(normalized.Status),
            ProductionGroup = normalized.ProductionGroup,
            FefoDeducted = ParseStateFilter(normalized.Fefo),
            PackagingDeducted = ParseStateFilter(normalized.Packaging),
            HasSnapshot = ParseSnapshotFilter(normalized.Snapshot),
            Page = normalized.Page,
            PageSize = normalized.PageSize,
            SortBy = normalized.SortBy,
            SortDescending = string.Equals(normalized.SortDirection, "desc", StringComparison.OrdinalIgnoreCase),
        };

        var (items, totalCount) = await _planRepository.SearchPlanItemsAsync(itemQuery);
        var summary = await _planRepository.GetPlanItemSummaryAsync(plan.Id);

        dashboard.Items = new PagedResultDto<KitchenDashboardItemDto>
        {
            Items = items.Select(MapKitchenDashboardItem).ToList(),
            Page = normalized.Page,
            PageSize = normalized.PageSize,
            TotalCount = totalCount,
        };
        dashboard.Summary = MapKitchenSummary(summary);

        return dashboard;
    }

    public async Task<CookingCardDto> GetCookingCardAsync(int planItemId)
    {
        var item = await _itemRepository.GetByIdAsync(planItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {planItemId} nie istnieje.");

        var details = await _dietProvider.GetMealCookingDetailsAsync(item.MealId);
        var storedSnapshot = TryDeserializeSnapshotItem(item);
        if (storedSnapshot is not null)
        {
            return BuildCookingCardFromSnapshot(item, storedSnapshot, details);
        }

        var recipe = (await _dietProvider.GetRecipeForMealAsync(item.MealId)).ToList();

        var card = new CookingCardDto
        {
            PlanItemId = item.Id,
            MealId = item.MealId,
            MealName = details?.MealName ?? item.MealName,
            CategoryName = details?.CategoryName,
            Description = details?.Description,
            PreparationInstructions = details?.PreparationInstructions,
            MainImageUrl = details?.MainImageUrl,
            PreparationTimeMinutes = details?.PreparationTimeMinutes ?? 0,
            DietVariantId = item.DietVariantId,
            PlannedQuantity = item.PlannedQuantity,
            ProductionGroup = item.ProductionGroup,
            EstimatedReadyTime = item.EstimatedReadyTime.HasValue
                ? item.EstimatedReadyTime.Value.ToString("HH:mm")
                : null,
            RawWeightGrams = details?.RawWeightGrams,
            CookedWeightGrams = details?.CookedWeightGrams,
            RequiresCoreTemperatureCheck = details?.RequiresCoreTemperatureCheck == true
                || recipe.Any(r => r.RequiresCoreTemperatureCheck),
            MinimumCoreTemperatureCelsius = details?.MinimumCoreTemperatureCelsius
                ?? recipe.Where(r => r.MinimumCoreTemperatureCelsius.HasValue)
                    .Select(r => r.MinimumCoreTemperatureCelsius)
                    .DefaultIfEmpty()
                    .Max(),
            NutritionFacts = details?.NutritionFacts is null
                ? null
                : new CookingCardNutritionDto
                {
                    CaloriesPer100g = details.NutritionFacts.CaloriesPer100g,
                    ProteinPer100g = details.NutritionFacts.ProteinPer100g,
                    CarbohydratesPer100g = details.NutritionFacts.CarbohydratesPer100g,
                    FatPer100g = details.NutritionFacts.FatPer100g,
                    FiberPer100g = details.NutritionFacts.FiberPer100g,
                },
            Allergens = details?.Allergens ?? Array.Empty<string>(),
            MissingWarehouseMappings = recipe
                .Where(r => !r.StockItemId.HasValue)
                .Select(r => $"{r.IngredientName} (ID {r.IngredientId})")
                .ToList(),
        };

        foreach (var ingredient in recipe)
        {
            card.Ingredients.Add(new CookingCardIngredientDto
            {
                IngredientId = ingredient.IngredientId,
                StockItemId = ingredient.StockItemId,
                IngredientName = ingredient.IngredientName,
                WarehouseCategoryName = ingredient.WarehouseCategoryName,
                WeightPerServing = ingredient.WeightInGrams,
                TotalWeight = ingredient.WeightInGrams * item.PlannedQuantity,
                YieldFactor = ingredient.YieldFactor,
                RequiresCoreTemperatureCheck = ingredient.RequiresCoreTemperatureCheck,
                MinimumCoreTemperatureCelsius = ingredient.MinimumCoreTemperatureCelsius,
                IsOptional = ingredient.IsOptional,
            });
        }

        return card;
    }

    public async Task ApproveCookingAsync(int planItemId, decimal actualQuantity)
    {
        var item = await _itemRepository.GetByIdAsync(planItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {planItemId} nie istnieje.");

        await DeductPackagingIfNeededAsync(item, actualQuantity);

        item.CookedQuantity = (int)actualQuantity;
        item.Status = ProductionItemStatus.Cooked;
        item.ActualReadyTime = TimeOnly.FromDateTime(DateTime.Now);

        await _itemRepository.UpdateAsync(item);

        var plan = await _planRepository.GetByIdAsync(item.ProductionPlanId);
        if (plan is not null)
        {
            var sessions = (await _packingService.GetSessionsByDateAsync(plan.ProductionDate)).ToList();
            foreach (var session in sessions.Where(s => s.Items.Count == 0 && s.OrderId.HasValue))
            {
                await _packingService.PrepareOrderBoxesAsync(session.Id);
            }
        }

        if (item.CookedQuantity < item.PlannedQuantity * 0.9m)
        {
            _logger.LogWarning(
                "ALERT PRODUKCYJNY: Ugotowano zbyt malo porcji. Danie {Meal}, Plan: {Plan}, Realizacja: {Actual}",
                item.MealName,
                item.PlannedQuantity,
                item.CookedQuantity);
        }

        _logger.LogInformation(
            "Zatwierdzono produkcje pozycji {PlanItemId}: wyprodukowano {ActualQuantity} porcji",
            planItemId,
            actualQuantity);
    }

    public async Task ProduceSemiFinishedAsync(int planId)
    {
        var plan = await _planRepository.GetByIdAsync(planId)
            ?? throw new InvalidOperationException($"Plan produkcji {planId} nie istnieje.");

        var planItems = (await _planRepository.GetPlanItemsAsync(planId)).ToList();
        var pendingItems = planItems.Where(item => !item.FefoDeductedAt.HasValue).ToList();

        if (pendingItems.Count == 0)
        {
            _logger.LogInformation("FEFO dla planu {PlanId} bylo juz wykonane - pominieto ponowne zdejmowanie.", planId);
        }
        else
        {
            var snapshot = BuildStoredSnapshot(plan.ProductionDate, pendingItems);
            if (snapshot is null)
            {
                snapshot = await _dietProvider.GetPublishedPlanSnapshotAsync(plan.ProductionDate);
            }

            if (snapshot is not null)
            {
                await ProduceFromSnapshotAsync(planId, pendingItems, snapshot);
            }
            else
            {
                await ProduceFromLegacyRecipesAsync(planId, pendingItems);
            }
        }

        plan.Status = ProductionPlanStatus.InProgress;
        await _planRepository.UpdateAsync(plan);

        _logger.LogInformation("Utworzono polprodukty dla planu {PlanId} i zaktualizowano magazyn", planId);
    }

    private async Task ProduceFromLegacyRecipesAsync(int planId, List<ProductionPlanItem> pendingItems)
    {
        var recipesByItem = new Dictionary<int, List<RecipeIngredientEntry>>();
        var requiredByStockItem = new Dictionary<int, decimal>();
        var missingMappings = new List<string>();

        foreach (var item in pendingItems)
        {
            var recipe = (await _dietProvider.GetRecipeForMealAsync(item.MealId)).ToList();
            if (recipe.Count == 0)
            {
                throw new InvalidOperationException($"Brak receptury M2 dla posilku {item.MealName} (ID {item.MealId}).");
            }

            recipesByItem[item.Id] = recipe;

            foreach (var ingredient in recipe)
            {
                if (!ingredient.StockItemId.HasValue)
                {
                    missingMappings.Add($"{ingredient.IngredientName} (IngredientId {ingredient.IngredientId})");
                    continue;
                }

                var totalQuantity = ingredient.WeightInGrams * item.PlannedQuantity;
                requiredByStockItem[ingredient.StockItemId.Value] =
                    requiredByStockItem.GetValueOrDefault(ingredient.StockItemId.Value) + totalQuantity;
            }
        }

        if (missingMappings.Count > 0)
        {
            throw new InvalidOperationException(
                "Nie mozna wykonac FEFO. Brak mapowania skladnikow M2 do magazynu: " +
                string.Join(", ", missingMappings.Distinct()));
        }

        var shortages = new List<string>();
        foreach (var (stockItemId, requiredQuantity) in requiredByStockItem)
        {
            var available = await _fefoService.GetAvailableQuantityAsync(stockItemId);
            if (available < requiredQuantity)
            {
                shortages.Add($"StockItemId {stockItemId}: potrzeba {requiredQuantity:0.##}, dostepne {available:0.##}");
            }
        }

        if (shortages.Count > 0)
        {
            throw new InvalidOperationException(
                "Nie mozna wykonac FEFO. Braki magazynowe: " + string.Join("; ", shortages));
        }

        foreach (var item in pendingItems)
        {
            var referenceDocument = $"PLAN-{planId}-ITEM-{item.Id}";

            foreach (var ingredient in recipesByItem[item.Id])
            {
                var totalQuantity = ingredient.WeightInGrams * item.PlannedQuantity;

                await _fefoService.DeductByFefoAsync(
                    ingredient.StockItemId!.Value,
                    totalQuantity,
                    $"Produkcja polproduktow plan {planId}, posilek {item.MealId}",
                    referenceDocument);
            }

            await MarkItemFefoDeductedAsync(item, referenceDocument);
        }
    }

    private async Task ProduceFromSnapshotAsync(
        int planId,
        List<ProductionPlanItem> pendingItems,
        PublishedDietPlanSnapshotDto snapshot)
    {
        var snapshotItemsByPlanItem = snapshot.Items.ToDictionary(
            item => (item.MealId, item.DietVariantId),
            item => item);
        var requirementsByItem = new Dictionary<int, List<SnapshotIngredientRequirement>>();
        var missingMappings = new List<string>();

        foreach (var item in pendingItems)
        {
            if (!snapshotItemsByPlanItem.TryGetValue((item.MealId, item.DietVariantId), out var snapshotItem))
            {
                throw new InvalidOperationException(
                    $"Brak pozycji snapshotu M2 dla posilku {item.MealName} (MealId {item.MealId}, DietVariantId {item.DietVariantId}).");
            }

            var requirements = new List<SnapshotIngredientRequirement>();
            foreach (var component in snapshotItem.Components)
            {
                foreach (var ingredient in component.Ingredients)
                {
                    if (!ingredient.StockItemId.HasValue && !ingredient.WarehouseCategoryId.HasValue)
                    {
                        missingMappings.Add($"{ingredient.IngredientName} (IngredientId {ingredient.IngredientId})");
                        continue;
                    }

                    requirements.Add(new SnapshotIngredientRequirement(
                        component.RecipeComponentVersionId,
                        component.ComponentName,
                        ingredient.IngredientId,
                        ingredient.IngredientName,
                        ingredient.StockItemId,
                        ingredient.WarehouseCategoryId,
                        ingredient.WeightInGrams
                            * component.QuantityPerServing
                            * snapshotItem.ServingMultiplier
                            * item.PlannedQuantity));
                }
            }

            requirementsByItem[item.Id] = requirements;
        }

        if (missingMappings.Count > 0)
        {
            throw new InvalidOperationException(
                "Nie mozna wykonac FEFO. Brak mapowania skladnikow M2 do magazynu: " +
                string.Join(", ", missingMappings.Distinct()));
        }

        var shortages = new List<string>();
        foreach (var requirement in requirementsByItem.Values.SelectMany(r => r).GroupBy(CreateRequirementKey))
        {
            var first = requirement.First();
            var requiredQuantity = requirement.Sum(r => r.RequiredQuantity);
            var available = first.StockItemId.HasValue
                ? await _fefoService.GetAvailableQuantityAsync(first.StockItemId.Value)
                : await _fefoService.GetAvailableQuantityByCategoryAsync(first.WarehouseCategoryId!.Value);

            if (available < requiredQuantity)
            {
                var key = first.StockItemId.HasValue
                    ? $"StockItemId {first.StockItemId.Value}"
                    : $"WarehouseCategoryId {first.WarehouseCategoryId!.Value}";
                shortages.Add($"{key}: potrzeba {requiredQuantity:0.##}, dostepne {available:0.##}");
            }
        }

        if (shortages.Count > 0)
        {
            throw new InvalidOperationException(
                "Nie mozna wykonac FEFO. Braki magazynowe: " + string.Join("; ", shortages));
        }

        foreach (var item in pendingItems)
        {
            var referenceDocument = $"PLAN-{planId}-ITEM-{item.Id}";
            foreach (var requirement in requirementsByItem[item.Id])
            {
                var reason = $"Produkcja skladowej {requirement.ComponentName} plan {planId}, posilek {item.MealId}";
                if (requirement.StockItemId.HasValue)
                {
                    await _fefoService.DeductByFefoAsync(
                        requirement.StockItemId.Value,
                        requirement.RequiredQuantity,
                        reason,
                        referenceDocument);
                }
                else
                {
                    await _fefoService.DeductByFefoCategoryAsync(
                        requirement.WarehouseCategoryId!.Value,
                        requirement.RequiredQuantity,
                        reason,
                        referenceDocument);
                }
            }

            await MarkItemFefoDeductedAsync(item, referenceDocument);
        }
    }

    private async Task DeductPackagingIfNeededAsync(ProductionPlanItem item, decimal actualQuantity)
    {
        if (item.PackagingDeductedAt.HasValue)
        {
            _logger.LogInformation(
                "Opakowania dla pozycji {PlanItemId} byly juz zdjete - pominieto ponowne zdejmowanie.",
                item.Id);
            return;
        }

        var snapshotItem = TryDeserializeSnapshotItem(item);
        if (snapshotItem is null)
        {
            return;
        }

        var requirements = BuildPackagingRequirements(snapshotItem, actualQuantity).ToList();
        if (requirements.Count == 0)
        {
            throw new InvalidOperationException(
                $"Nie mozna zatwierdzic gotowania. Snapshot M2 dla posilku {item.MealName} nie zawiera wymaganych opakowan.");
        }

        var shortages = new List<string>();
        foreach (var group in requirements.GroupBy(CreatePackagingRequirementKey))
        {
            var first = group.First();
            var requiredQuantity = group.Sum(r => r.RequiredQuantity);
            if (requiredQuantity <= 0)
            {
                continue;
            }

            var available = first.StockItemId.HasValue
                ? await _fefoService.GetAvailableQuantityAsync(first.StockItemId.Value)
                : await _fefoService.GetAvailableQuantityByCategoryAsync(first.WarehouseCategoryId!.Value);

            if (available < requiredQuantity)
            {
                var key = first.StockItemId.HasValue
                    ? $"StockItemId {first.StockItemId.Value}"
                    : $"WarehouseCategoryId {first.WarehouseCategoryId!.Value}";
                shortages.Add($"{first.ResourceName} ({key}): potrzeba {requiredQuantity:0.##}, dostepne {available:0.##}");
            }
        }

        if (shortages.Count > 0)
        {
            throw new InvalidOperationException(
                "Nie mozna zatwierdzic gotowania. Braki opakowan: " + string.Join("; ", shortages));
        }

        var referenceDocument = $"PLAN-{item.ProductionPlanId}-ITEM-{item.Id}-PACKAGING";
        foreach (var requirement in requirements.Where(r => r.RequiredQuantity > 0))
        {
            var reason = $"Opakowania po gotowaniu plan {item.ProductionPlanId}, posilek {item.MealId}";
            if (requirement.StockItemId.HasValue)
            {
                await _fefoService.DeductByFefoAsync(
                    requirement.StockItemId.Value,
                    requirement.RequiredQuantity,
                    reason,
                    referenceDocument);
            }
            else
            {
                await _fefoService.DeductByFefoCategoryAsync(
                    requirement.WarehouseCategoryId!.Value,
                    requirement.RequiredQuantity,
                    reason,
                    referenceDocument);
            }
        }

        item.PackagingDeductedAt = DateTimeOffset.UtcNow;
        item.PackagingReferenceDocument = referenceDocument;
    }

    private static CookingCardDto BuildCookingCardFromSnapshot(
        ProductionPlanItem item,
        PublishedDietPlanItemDto snapshotItem,
        MealCookingDetailsEntry? details)
    {
        var card = new CookingCardDto
        {
            PlanItemId = item.Id,
            MealId = item.MealId,
            MealName = snapshotItem.MealName,
            CategoryName = snapshotItem.CategoryName ?? details?.CategoryName,
            Description = details?.Description,
            PreparationInstructions = details?.PreparationInstructions,
            MainImageUrl = details?.MainImageUrl,
            PreparationTimeMinutes = details?.PreparationTimeMinutes ?? 0,
            DietVariantId = item.DietVariantId,
            PlannedQuantity = item.PlannedQuantity,
            ProductionGroup = item.ProductionGroup,
            EstimatedReadyTime = item.EstimatedReadyTime.HasValue
                ? item.EstimatedReadyTime.Value.ToString("HH:mm")
                : null,
            RawWeightGrams = snapshotItem.RawWeightGrams,
            CookedWeightGrams = snapshotItem.CookedWeightGrams,
            NutritionFacts = snapshotItem.Nutrition is null
                ? null
                : new CookingCardNutritionDto
                {
                    CaloriesPer100g = snapshotItem.Nutrition.CaloriesPer100g,
                    ProteinPer100g = snapshotItem.Nutrition.ProteinPer100g,
                    CarbohydratesPer100g = snapshotItem.Nutrition.CarbohydratesPer100g,
                    FatPer100g = snapshotItem.Nutrition.FatPer100g,
                    FiberPer100g = snapshotItem.Nutrition.FiberPer100g,
                },
            Allergens = snapshotItem.Allergens,
        };

        foreach (var component in snapshotItem.Components.OrderBy(c => c.SortOrder))
        {
            var componentDto = new CookingCardComponentDto
            {
                RecipeComponentVersionId = component.RecipeComponentVersionId,
                ComponentName = component.ComponentName,
                Role = component.Role,
                QuantityPerServing = component.QuantityPerServing * snapshotItem.ServingMultiplier,
                Unit = component.Unit,
                TotalQuantity = component.QuantityPerServing * snapshotItem.ServingMultiplier * item.PlannedQuantity,
                Instructions = component.Instructions,
                RequiresCoreTemperatureCheck = component.Ingredients.Any(i => i.RequiresCoreTemperatureCheck),
                MinimumCoreTemperatureCelsius = MaxTemperature(component.Ingredients),
            };

            foreach (var ingredient in component.Ingredients)
            {
                var weightPerServing = ingredient.WeightInGrams * component.QuantityPerServing * snapshotItem.ServingMultiplier;
                var ingredientDto = new CookingCardIngredientDto
                {
                    IngredientId = ingredient.IngredientId,
                    StockItemId = ingredient.StockItemId,
                    IngredientName = ingredient.IngredientName,
                    WarehouseCategoryName = ingredient.WarehouseCategoryName,
                    WeightPerServing = weightPerServing,
                    TotalWeight = weightPerServing * item.PlannedQuantity,
                    YieldFactor = ingredient.YieldFactor,
                    RequiresCoreTemperatureCheck = ingredient.RequiresCoreTemperatureCheck,
                    MinimumCoreTemperatureCelsius = ingredient.MinimumCoreTemperatureCelsius,
                    IsOptional = ingredient.IsOptional,
                };

                componentDto.Ingredients.Add(ingredientDto);
                AddAggregatedIngredient(card.Ingredients, ingredientDto);
            }

            foreach (var packaging in component.PackagingRequirements)
            {
                var quantityPerServing = packaging.Quantity * component.QuantityPerServing * snapshotItem.ServingMultiplier;
                componentDto.PackagingRequirements.Add(MapPackaging(packaging, quantityPerServing, item.PlannedQuantity));
            }

            card.Components.Add(componentDto);
        }

        foreach (var packaging in snapshotItem.PackagingRequirements)
        {
            card.PackagingRequirements.Add(MapPackaging(packaging, packaging.Quantity, item.PlannedQuantity));
        }

        foreach (var component in card.Components)
        {
            card.PackagingRequirements.AddRange(component.PackagingRequirements);
        }

        card.RequiresCoreTemperatureCheck = card.Components.Any(c => c.RequiresCoreTemperatureCheck);
        card.MinimumCoreTemperatureCelsius = MaxTemperature(card.Ingredients);
        card.MissingWarehouseMappings = snapshotItem.Components
            .SelectMany(c => c.Ingredients)
            .Where(i => !i.StockItemId.HasValue && !i.WarehouseCategoryId.HasValue)
            .Select(i => $"{i.IngredientName} (ID {i.IngredientId})")
            .Distinct()
            .ToList();

        return card;
    }

    private static IEnumerable<PackagingRequirement> BuildPackagingRequirements(
        PublishedDietPlanItemDto snapshotItem,
        decimal actualQuantity)
    {
        foreach (var packaging in snapshotItem.PackagingRequirements)
        {
            yield return CreatePackagingRequirement(packaging, packaging.Quantity * actualQuantity);
        }

        foreach (var component in snapshotItem.Components)
        {
            foreach (var packaging in component.PackagingRequirements)
            {
                yield return CreatePackagingRequirement(
                    packaging,
                    packaging.Quantity * component.QuantityPerServing * snapshotItem.ServingMultiplier * actualQuantity);
            }
        }
    }

    private static PackagingRequirement CreatePackagingRequirement(PackagingRequirementDto packaging, decimal requiredQuantity)
    {
        if (!packaging.StockItemId.HasValue && !packaging.WarehouseCategoryId.HasValue)
        {
            throw new InvalidOperationException(
                $"Opakowanie '{packaging.ResourceName}' nie ma mapowania StockItemId ani WarehouseCategoryId.");
        }

        return new PackagingRequirement(
            packaging.ResourceName,
            packaging.StockItemId,
            packaging.WarehouseCategoryId,
            requiredQuantity);
    }

    private static PublishedDietPlanSnapshotDto? BuildStoredSnapshot(DateOnly planDate, List<ProductionPlanItem> pendingItems)
    {
        var snapshotItems = pendingItems
            .Select(TryDeserializeSnapshotItem)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        if (snapshotItems.Count == 0)
        {
            return null;
        }

        if (snapshotItems.Count != pendingItems.Count)
        {
            throw new InvalidOperationException(
                "Plan produkcji ma czesciowy snapshot M2. Nie mozna mieszac live danych M2 z zamrozonym snapshotem.");
        }

        return new PublishedDietPlanSnapshotDto
        {
            PlanDate = planDate,
            PlanStatus = "Snapshot",
            Items = snapshotItems,
        };
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
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Snapshot M2 dla pozycji planu {item.Id} jest uszkodzony i nie moze zostac uzyty operacyjnie.",
                ex);
        }
    }

    private static void AddAggregatedIngredient(List<CookingCardIngredientDto> target, CookingCardIngredientDto ingredient)
    {
        var existing = target.FirstOrDefault(i =>
            i.IngredientId == ingredient.IngredientId
            && i.StockItemId == ingredient.StockItemId
            && i.WarehouseCategoryName == ingredient.WarehouseCategoryName);

        if (existing is null)
        {
            target.Add(ingredient);
            return;
        }

        existing.WeightPerServing += ingredient.WeightPerServing;
        existing.TotalWeight += ingredient.TotalWeight;
        existing.RequiresCoreTemperatureCheck |= ingredient.RequiresCoreTemperatureCheck;
        existing.MinimumCoreTemperatureCelsius = MaxTemperature(existing.MinimumCoreTemperatureCelsius, ingredient.MinimumCoreTemperatureCelsius);
    }

    private static CookingCardPackagingDto MapPackaging(PackagingRequirementDto packaging, decimal quantityPerServing, int plannedQuantity)
        => new()
        {
            ResourceName = packaging.ResourceName,
            StockItemId = packaging.StockItemId,
            WarehouseCategoryId = packaging.WarehouseCategoryId,
            QuantityPerServing = quantityPerServing,
            TotalQuantity = quantityPerServing * plannedQuantity,
            Unit = packaging.Unit,
            ContainerRole = packaging.ContainerRole,
        };

    private static decimal? MaxTemperature(IEnumerable<ComponentIngredientDto> ingredients)
    {
        var values = ingredients
            .Where(i => i.RequiresCoreTemperatureCheck && i.MinimumCoreTemperatureCelsius.HasValue)
            .Select(i => i.MinimumCoreTemperatureCelsius!.Value)
            .ToList();

        return values.Count == 0 ? null : values.Max();
    }

    private static decimal? MaxTemperature(IEnumerable<CookingCardIngredientDto> ingredients)
    {
        var values = ingredients
            .Where(i => i.RequiresCoreTemperatureCheck && i.MinimumCoreTemperatureCelsius.HasValue)
            .Select(i => i.MinimumCoreTemperatureCelsius!.Value)
            .ToList();

        return values.Count == 0 ? null : values.Max();
    }

    private static decimal? MaxTemperature(decimal? first, decimal? second)
    {
        if (!first.HasValue)
        {
            return second;
        }

        if (!second.HasValue)
        {
            return first;
        }

        return Math.Max(first.Value, second.Value);
    }

    private static string CreatePackagingRequirementKey(PackagingRequirement requirement)
        => requirement.StockItemId.HasValue
            ? $"S:{requirement.StockItemId.Value}"
            : $"C:{requirement.WarehouseCategoryId!.Value}";

    private async Task MarkItemFefoDeductedAsync(ProductionPlanItem item, string referenceDocument)
    {
        item.FefoDeductedAt = DateTimeOffset.UtcNow;
        item.FefoReferenceDocument = referenceDocument;
        if (item.Status == ProductionItemStatus.Planned)
        {
            item.Status = ProductionItemStatus.Cooking;
        }

        await _itemRepository.UpdateAsync(item);
    }

    private static string CreateRequirementKey(SnapshotIngredientRequirement requirement)
        => requirement.StockItemId.HasValue
            ? $"S:{requirement.StockItemId.Value}"
            : $"C:{requirement.WarehouseCategoryId!.Value}";

    private static KitchenDashboardFilterDto NormalizeKitchenDashboardFilter(KitchenDashboardFilterDto filter)
    {
        var normalized = new KitchenDashboardFilterDto
        {
            Date = filter.Date == default ? DateOnly.FromDateTime(DateTime.Today) : filter.Date,
            Search = NormalizeSearch(filter.Search),
            Status = ParseProductionStatus(filter.Status)?.ToString(),
            ProductionGroup = filter.ProductionGroup,
            Fefo = NormalizeStateFilter(filter.Fefo),
            Packaging = NormalizeStateFilter(filter.Packaging),
            Snapshot = NormalizeSnapshotFilter(filter.Snapshot),
            SortBy = NormalizeSortBy(filter.SortBy),
            SortDirection = string.Equals(filter.SortDirection, "desc", StringComparison.OrdinalIgnoreCase)
                ? "desc"
                : "asc",
            Page = Math.Max(filter.Page, 1),
            PageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 10, 100),
        };

        return normalized;
    }

    private static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var trimmed = search.Trim();
        return trimmed.Length > 120 ? trimmed[..120] : trimmed;
    }

    private static string? NormalizeSortBy(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "meal" or "mealname" => "meal",
            "planned" or "plannedquantity" => "planned",
            "cooked" or "cookedquantity" => "cooked",
            "status" => "status",
            "group" or "productiongroup" => "group",
            "ready" or "estimatedreadytime" => "ready",
            _ => "id",
        };
    }

    private static ProductionItemStatus? ParseProductionStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<ProductionItemStatus>(status.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static string? NormalizeStateFilter(string? value)
    {
        return ParseStateFilter(value) switch
        {
            true => "done",
            false => "pending",
            _ => null,
        };
    }

    private static bool? ParseStateFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "done" or "deducted" or "yes" or "true")
        {
            return true;
        }

        if (normalized is "pending" or "missing" or "no" or "false")
        {
            return false;
        }

        return null;
    }

    private static string? NormalizeSnapshotFilter(string? value)
    {
        return ParseSnapshotFilter(value) switch
        {
            true => "present",
            false => "missing",
            _ => null,
        };
    }

    private static bool? ParseSnapshotFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "present" or "has" or "yes" or "true")
        {
            return true;
        }

        if (normalized is "missing" or "none" or "no" or "false")
        {
            return false;
        }

        return null;
    }

    private static KitchenDashboardSummaryDto MapKitchenSummary(ProductionPlanItemSummary summary)
        => new()
        {
            TotalItems = summary.TotalItems,
            TotalPlannedQuantity = summary.TotalPlannedQuantity,
            TotalCookedQuantity = summary.TotalCookedQuantity,
            PlannedItems = summary.PlannedItems,
            CookingItems = summary.CookingItems,
            CookedItems = summary.CookedItems,
            FailedItems = summary.FailedItems,
            PendingFefoItems = summary.PendingFefoItems,
            FefoDeductedItems = summary.FefoDeductedItems,
            PendingPackagingItems = summary.PendingPackagingItems,
            PackagingDeductedItems = summary.PackagingDeductedItems,
            SnapshotItems = summary.SnapshotItems,
        };

    private static KitchenDashboardItemDto MapKitchenDashboardItem(ProductionPlanItem item)
    {
        var snapshot = TryReadSnapshotForDashboard(item, out var snapshotWarning);
        var packagingCount = snapshot is null
            ? 0
            : snapshot.PackagingRequirements.Count
                + snapshot.Components.Sum(component => component.PackagingRequirements.Count);
        var validationWarningCount = snapshot is null
            ? 0
            : snapshot.ValidationWarnings.Count
                + snapshot.Components.Sum(component => component.ValidationWarnings.Count);

        return new KitchenDashboardItemDto
        {
            Id = item.Id,
            ProductionPlanId = item.ProductionPlanId,
            MealId = item.MealId,
            MealName = snapshot?.MealName ?? item.MealName,
            DietVariantId = item.DietVariantId,
            DietMenuPlanItemId = item.DietMenuPlanItemId,
            MealVariantId = snapshot?.MealVariantId,
            MealVariantName = snapshot?.MealVariantName,
            CategoryName = snapshot?.CategoryName,
            MealSlot = snapshot?.MealSlot,
            PlannedQuantity = item.PlannedQuantity,
            CookedQuantity = item.CookedQuantity,
            Status = item.Status.ToString(),
            ProductionGroup = item.ProductionGroup,
            EstimatedReadyTime = item.EstimatedReadyTime?.ToString("HH:mm"),
            ActualReadyTime = item.ActualReadyTime?.ToString("HH:mm"),
            FefoDeductedAt = item.FefoDeductedAt,
            PackagingDeductedAt = item.PackagingDeductedAt,
            M2SnapshotHash = item.M2SnapshotHash,
            HasM2Snapshot = !string.IsNullOrWhiteSpace(item.M2SnapshotJson),
            ComponentCount = snapshot?.Components.Count ?? CountComponentIds(item.RecipeComponentVersionIds),
            PackagingRequirementCount = packagingCount,
            ValidationWarningCount = validationWarningCount,
            HasMissingWarehouseMappings = snapshot?.Components
                .SelectMany(component => component.Ingredients)
                .Any(ingredient => !ingredient.StockItemId.HasValue && !ingredient.WarehouseCategoryId.HasValue) == true,
            SnapshotWarning = snapshotWarning,
        };
    }

    private static PublishedDietPlanItemDto? TryReadSnapshotForDashboard(
        ProductionPlanItem item,
        out string? warning)
    {
        warning = null;
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
            warning = "Snapshot M2 jest uszkodzony.";
            return null;
        }
    }

    private static int CountComponentIds(string? recipeComponentVersionIds)
        => string.IsNullOrWhiteSpace(recipeComponentVersionIds)
            ? 0
            : recipeComponentVersionIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Length;

    private sealed record SnapshotIngredientRequirement(
        int RecipeComponentVersionId,
        string ComponentName,
        int IngredientId,
        string IngredientName,
        int? StockItemId,
        int? WarehouseCategoryId,
        decimal RequiredQuantity);

    private sealed record PackagingRequirement(
        string ResourceName,
        int? StockItemId,
        int? WarehouseCategoryId,
        decimal RequiredQuantity);
}
