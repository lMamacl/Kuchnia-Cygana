using AutoMapper;
using KuchniaUCygana.Application.DTOs.Production;
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

    public async Task<CookingCardDto> GetCookingCardAsync(int planItemId)
    {
        var item = await _itemRepository.GetByIdAsync(planItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {planItemId} nie istnieje.");

        var details = await _dietProvider.GetMealCookingDetailsAsync(item.MealId);
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

                item.FefoDeductedAt = DateTimeOffset.UtcNow;
                item.FefoReferenceDocument = referenceDocument;
                if (item.Status == ProductionItemStatus.Planned)
                {
                    item.Status = ProductionItemStatus.Cooking;
                }

                await _itemRepository.UpdateAsync(item);
            }
        }

        plan.Status = ProductionPlanStatus.InProgress;
        await _planRepository.UpdateAsync(plan);

        _logger.LogInformation("Utworzono polprodukty dla planu {PlanId} i zaktualizowano magazyn", planId);
    }
}
