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

/// <summary>
/// Serwis aplikacyjny produkcji — obsługuje cały proces od planu po odpis surowców.
/// </summary>
public sealed class ProductionService : IProductionService
{
    private readonly ProductionPlanGenerator _planGenerator;
    private readonly IProductionPlanRepository _planRepository;
    private readonly IRepository<ProductionPlanItem> _itemRepository;
    private readonly IDietDataProvider _dietProvider;
    private readonly FefoService _fefoService;
    private readonly IMapper _mapper;
    private readonly ILogger<ProductionService> _logger;

    public ProductionService(
        ProductionPlanGenerator planGenerator,
        IProductionPlanRepository planRepository,
        IRepository<ProductionPlanItem> itemRepository,
        IDietDataProvider dietProvider,
        FefoService fefoService,
        IMapper mapper,
        ILogger<ProductionService> logger)
    {
        _planGenerator = planGenerator;
        _planRepository = planRepository;
        _itemRepository = itemRepository;
        _dietProvider = dietProvider;
        _fefoService = fefoService;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ProductionPlanDto> GenerateDailyPlanAsync(CreateProductionPlanRequest request)
    {
        // Domena (ProductionPlanGenerator) dba o to, czy plan istnieje i zgłosi wyjątek w razie potrzeby.
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

        _logger.LogInformation("Wygenerowano plan produkcji na dzień {Date}", request.ProductionDate);

        return dto;
    }

    /// <inheritdoc/>
    public async Task<ProductionPlanDto?> GetDailyPlanByDateAsync(DateOnly date)
    {
        var plan = await _planRepository.GetByDateAsync(date);
        if (plan == null) return null;

        var items = await _planRepository.GetPlanItemsAsync(plan.Id);

        var dto = _mapper.Map<ProductionPlanDto>(plan);
        dto.Items = _mapper.Map<List<ProductionPlanItemDto>>(items);

        return dto;
    }

    /// <inheritdoc/>
    public async Task<ProductionPlanDto?> GetPlanByIdAsync(int planId)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan == null) return null;

        var items = await _planRepository.GetPlanItemsAsync(plan.Id);

        var dto = _mapper.Map<ProductionPlanDto>(plan);
        dto.Items = _mapper.Map<List<ProductionPlanItemDto>>(items);

        return dto;
    }

    /// <inheritdoc/>
    public async Task<CookingCardDto> GetCookingCardAsync(int planItemId)
    {
        var item = await _itemRepository.GetByIdAsync(planItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {planItemId} nie istnieje.");

        var recipe = await _dietProvider.GetRecipeForMealAsync(item.MealId);

        var card = new CookingCardDto
        {
            PlanItemId = item.Id,
            MealName = item.MealName,
            DietVariantId = item.DietVariantId,
            PlannedQuantity = item.PlannedQuantity,
            ProductionGroup = item.ProductionGroup,
            EstimatedReadyTime = item.EstimatedReadyTime.HasValue 
                ? item.EstimatedReadyTime.Value.ToString("HH:mm") 
                : null,
        };

        foreach (var ingredient in recipe)
        {
            card.Ingredients.Add(new CookingCardIngredientDto
            {
                IngredientId = ingredient.IngredientId,
                IngredientName = ingredient.IngredientName,
                WeightPerServing = ingredient.WeightInGrams,
                TotalWeight = ingredient.WeightInGrams * item.PlannedQuantity,
                IsOptional = ingredient.IsOptional,
            });
        }

        return card;
    }

    /// <inheritdoc/>
    public async Task ApproveCookingAsync(int planItemId, decimal actualQuantity)
    {
        var item = await _itemRepository.GetByIdAsync(planItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {planItemId} nie istnieje.");

        item.CookedQuantity = (int)actualQuantity; // zakłada całkowite wartości
        item.Status = ProductionItemStatus.Cooked;
        item.ActualReadyTime = TimeOnly.FromDateTime(DateTime.Now);

        await _itemRepository.UpdateAsync(item);

        if (item.CookedQuantity < item.PlannedQuantity * 0.9m)
        {
            _logger.LogWarning(
                "ALERT PRODUKCYJNY: Ugotowano zbyt mało porcji. Danie {Meal}, Plan: {Plan}, Realizacja: {Actual}",
                item.MealName,
                item.PlannedQuantity,
                item.CookedQuantity);
        }

        _logger.LogInformation(
            "Zatwierdzono produkcję pozycji {PlanItemId}: wyprodukowano {ActualQuantity} porcji",
            planItemId,
            actualQuantity);
    }

    /// <inheritdoc/>
    public async Task ProduceSemiFinishedAsync(int planId)
    {
        var plan = await _planRepository.GetByIdAsync(planId)
            ?? throw new InvalidOperationException($"Plan produkcji {planId} nie istnieje.");

        var planItems = await _planRepository.GetPlanItemsAsync(planId);

        // Pobieramy surowce za wszystkie pozycje w planie
        foreach (var item in planItems)
        {
            var recipe = await _dietProvider.GetRecipeForMealAsync(item.MealId);

            foreach (var ingredient in recipe)
            {
                var totalQuantity = ingredient.WeightInGrams * item.PlannedQuantity;

                var deductionResult = await _fefoService.DeductByFefoAsync(
                    ingredient.IngredientId,
                    totalQuantity,
                    $"Produkcja półproduktów plan {planId}, posiłek {item.MealId}",
                    $"PLAN-{planId}");

                if (!deductionResult.IsFullyDeducted)
                {
                    _logger.LogWarning(
                        "Brak w magazynie! Potrzebne: {Needed}, Zabrano: {Deducted}, Brak: {Shortage} dla składnika {IngredientId}",
                        totalQuantity,
                        deductionResult.TotalDeducted,
                        deductionResult.Shortage,
                        ingredient.IngredientId);
                }
            }
        }

        plan.Status = ProductionPlanStatus.InProgress;
        await _planRepository.UpdateAsync(plan);

        _logger.LogInformation("Utworzono półprodukty dla planu {PlanId} i zaktualizowano magazyn", planId);
    }
}
