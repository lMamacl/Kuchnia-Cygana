using AutoMapper;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class MealManagementService : IMealManagementService
{
    private readonly IMealRepository mealRepository;
    private readonly IRecipeRepository recipeRepository;
    private readonly IRecipeComponentRepository recipeComponentRepository;
    private readonly IMealVariantRepository mealVariantRepository;
    private readonly IMealImageRepository mealImageRepository;
    private readonly IRecipeEngine recipeEngine;
    private readonly ICurrentUserService currentUser;
    private readonly IMapper mapper;

    public MealManagementService(
        IMealRepository mealRepository,
        IRecipeRepository recipeRepository,
        IRecipeComponentRepository recipeComponentRepository,
        IMealVariantRepository mealVariantRepository,
        IMealImageRepository mealImageRepository,
        IRecipeEngine recipeEngine,
        ICurrentUserService currentUser,
        IMapper mapper)
    {
        this.mealRepository = mealRepository;
        this.recipeRepository = recipeRepository;
        this.recipeComponentRepository = recipeComponentRepository;
        this.mealVariantRepository = mealVariantRepository;
        this.mealImageRepository = mealImageRepository;
        this.recipeEngine = recipeEngine;
        this.currentUser = currentUser;
        this.mapper = mapper;
    }

    public async Task<MealDto?> GetMealAsync(int mealId)
    {
        var meal = await this.mealRepository.GetByIdAsync(mealId);
        return meal is null ? null : this.mapper.Map<MealDto>(meal);
    }

    public async Task<MealDetailDto?> GetMealWithDetailsAsync(int mealId)
    {
        var meal = await this.mealRepository.GetByIdAsync(mealId);
        if (meal is null) return null;
        var detail = this.mapper.Map<MealDetailDto>(meal);
        var recipes = await this.recipeRepository.GetByMealIdAsync(mealId);
        detail.Recipe = this.mapper.Map<List<RecipeItemDto>>(recipes);
        foreach (var item in detail.Recipe)
        {
            // można doczytać nazwę składnika
        }

        var componentRows = await this.recipeComponentRepository.GetMealComponentDetailsAsync(mealId);
        detail.Components = componentRows
            .GroupBy(row => row.RecipeComponentVersionId)
            .Select(group =>
            {
                var first = group.First();
                return new MealRecipeComponentDto
                {
                    RecipeComponentId = first.RecipeComponentId,
                    RecipeComponentVersionId = first.RecipeComponentVersionId,
                    ComponentName = first.ComponentName,
                    VersionNumber = first.VersionNumber,
                    VersionStatus = first.VersionStatus,
                    Role = first.Role,
                    QuantityPerServing = first.QuantityPerServing,
                    Unit = first.Unit,
                    SortOrder = first.SortOrder,
                    Instructions = first.Instructions,
                    ShelfLifeHours = first.ShelfLifeHours,
                    UseEarliestIngredientExpiry = first.UseEarliestIngredientExpiry,
                    Ingredients = group
                        .Where(row => row.IngredientId > 0)
                        .Select(row => new RecipeComponentIngredientDto
                        {
                            IngredientId = row.IngredientId,
                            IngredientName = row.IngredientName,
                            StockItemId = row.StockItemId,
                            WarehouseCategoryId = row.WarehouseCategoryId,
                            WarehouseCategoryName = row.WarehouseCategoryName,
                            WeightInGrams = row.WeightInGrams,
                            YieldFactor = row.YieldFactor <= 0 ? 1.0m : row.YieldFactor,
                            IsOptional = row.IsOptional,
                            Notes = row.Notes,
                        })
                        .ToList(),
                };
            })
            .OrderBy(component => component.SortOrder)
            .ToList();

        var variants = await this.mealVariantRepository.GetByMealIdAsync(mealId);
        foreach (var variant in variants)
        {
            var components = await this.mealVariantRepository.GetComponentsAsync(variant.Id);
            detail.Variants.Add(MapMealVariant(variant, components));
        }

        var images = await this.mealImageRepository.GetByMealIdAsync(mealId);
        detail.Images = this.mapper.Map<List<MealImageDto>>(images);
        return detail;
    }

    public async Task<IEnumerable<MealDto>> GetPublishedMealsAsync()
    {
        var meals = await this.mealRepository.GetPublishedAsync();
        return this.mapper.Map<IEnumerable<MealDto>>(meals);
    }

    public async Task<MealDto> CreateMealAsync(CreateMealRequest request)
    {
        var meal = this.mapper.Map<Meal>(request);
        meal.Status = MealStatus.Draft;
        await this.mealRepository.InsertAsync(meal);
        return this.mapper.Map<MealDto>(meal);
    }

    public async Task<int> CreateMealVariantAsync(CreateMealVariantRequest request)
    {
        var meal = await this.mealRepository.GetByIdAsync(request.MealId)
            ?? throw new InvalidOperationException($"Posilek #{request.MealId} nie istnieje.");

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Nazwa wariantu jest wymagana.");
        }

        var sourceVariantId = request.SourceMealVariantId;
        if (!sourceVariantId.HasValue)
        {
            sourceVariantId = (await this.mealVariantRepository.GetDefaultByMealIdAsync(request.MealId))?.Id;
        }

        return await this.mealVariantRepository.CreateAsync(new MealVariant
        {
            MealId = request.MealId,
            Name = request.Name.Trim(),
            VariantType = string.IsNullOrWhiteSpace(request.VariantType) ? "Standard" : request.VariantType.Trim(),
            Status = "Draft",
            Description = $"Wariant utworzony dla posilku {meal.Name}.",
            IsDefault = false,
            RawWeightGrams = meal.RawWeightGrams,
            CookedWeightGrams = meal.CookedWeightGrams,
            NutritionSource = "Aggregated",
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = this.currentUser.GetUserName(),
        }, sourceVariantId, this.currentUser.GetUserName());
    }

    public async Task UpdateMealAsync(int mealId, UpdateMealRequest request)
    {
        var meal = await this.mealRepository.GetByIdAsync(mealId);
        if (meal is null) return;
        this.mapper.Map(request, meal);
        await this.mealRepository.UpdateAsync(meal);
    }

    public async Task DeleteMealAsync(int mealId)
    {
        await this.mealRepository.DeleteAsync(mealId);
    }

    public async Task PublishMealAsync(int mealId)
    {
        var meal = await this.mealRepository.GetByIdAsync(mealId);
        if (meal is null) return;
        if (await this.recipeEngine.ValidateRecipeAsync(mealId))
        {
            meal.Status = MealStatus.Published;
            await this.mealRepository.UpdateAsync(meal);
        }
    }

    public async Task ArchiveMealAsync(int mealId)
    {
        var meal = await this.mealRepository.GetByIdAsync(mealId);
        if (meal is null) return;
        meal.Status = MealStatus.Archived;
        await this.mealRepository.UpdateAsync(meal);
    }

    private static MealVariantDto MapMealVariant(
        MealVariantRow row,
        IReadOnlyList<MealVariantComponentRow> components)
    {
        var warnings = new List<string>();
        if (components.Count == 0)
        {
            warnings.Add("brak skladowych wariantu");
        }

        if (!row.CaloriesPer100g.HasValue
            || !row.ProteinPer100g.HasValue
            || !row.CarbohydratesPer100g.HasValue
            || !row.FatPer100g.HasValue
            || !row.FiberPer100g.HasValue)
        {
            warnings.Add("brak pelnego nutrition wariantu");
        }

        if (!row.AllergensApproved)
        {
            warnings.Add("alergeny wariantu nie sa zatwierdzone");
        }

        return new MealVariantDto
        {
            Id = row.Id,
            MealId = row.MealId,
            Name = row.Name,
            VariantType = row.VariantType,
            Status = row.Status,
            Description = row.Description,
            IsDefault = row.IsDefault,
            RawWeightGrams = row.RawWeightGrams,
            CookedWeightGrams = row.CookedWeightGrams,
            CaloriesPer100g = row.CaloriesPer100g,
            ProteinPer100g = row.ProteinPer100g,
            CarbohydratesPer100g = row.CarbohydratesPer100g,
            FatPer100g = row.FatPer100g,
            FiberPer100g = row.FiberPer100g,
            NutritionSource = row.NutritionSource,
            NutritionOverrideReason = row.NutritionOverrideReason,
            AllergensApproved = row.AllergensApproved,
            AllergenOverrideReason = row.AllergenOverrideReason,
            ValidationWarnings = warnings,
            IsComplete = warnings.Count == 0,
            Components = components.Select(component => new MealVariantComponentDto
            {
                Id = component.Id,
                MealVariantId = component.MealVariantId,
                RecipeComponentId = component.RecipeComponentId,
                RecipeComponentVersionId = component.RecipeComponentVersionId,
                ComponentName = component.ComponentName,
                VersionNumber = component.VersionNumber,
                VersionStatus = component.VersionStatus,
                Role = component.Role,
                QuantityPerServing = component.QuantityPerServing,
                Unit = component.Unit,
                SortOrder = component.SortOrder,
                IsOptional = component.IsOptional,
            }).ToList(),
        };
    }
}
