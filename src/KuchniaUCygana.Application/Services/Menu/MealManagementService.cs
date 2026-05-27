using AutoMapper;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class MealManagementService : IMealManagementService
{
    private readonly IMealRepository mealRepository;
    private readonly IRecipeRepository recipeRepository;
    private readonly IMealImageRepository mealImageRepository;
    private readonly IRecipeEngine recipeEngine;
    private readonly IMapper mapper;

    public MealManagementService(
        IMealRepository mealRepository,
        IRecipeRepository recipeRepository,
        IMealImageRepository mealImageRepository,
        IRecipeEngine recipeEngine,
        IMapper mapper)
    {
        this.mealRepository = mealRepository;
        this.recipeRepository = recipeRepository;
        this.mealImageRepository = mealImageRepository;
        this.recipeEngine = recipeEngine;
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
}
