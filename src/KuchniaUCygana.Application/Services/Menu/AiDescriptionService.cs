using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class AiDescriptionService : IAiDescriptionService
{
    private readonly IMealRepository mealRepository;
    private readonly IRecipeRepository recipeRepository;
    private readonly IInternalAiService internalAiService;

    public AiDescriptionService(
        IMealRepository mealRepository,
        IRecipeRepository recipeRepository,
        IInternalAiService internalAiService)
    {
        this.mealRepository = mealRepository;
        this.recipeRepository = recipeRepository;
        this.internalAiService = internalAiService;
    }

    public async Task<AiDescriptionResponse> GenerateDescriptionAsync(AiGenerateDescriptionRequest request)
    {
        var meal = await this.mealRepository.GetByIdAsync(request.MealId);
        if (meal is null)
        {
            throw new ArgumentException("Posiłek nie istnieje.");
        }

        var recipes = await this.recipeRepository.GetByMealIdAsync(request.MealId);
        var ingredientNames = recipes.Select(r => r.IngredientId.ToString()).ToList();
        var prompt = $"Posiłek: {meal.Name}. Składniki: {string.Join(", ", ingredientNames)}. Opisz marketingowo.";

        var description = await this.internalAiService.GenerateDescriptionAsync(prompt);
        meal.MarketingDescription = description;
        await this.mealRepository.UpdateAsync(meal);

        return new AiDescriptionResponse
        {
            MealId = meal.Id,
            MarketingDescription = description
        };
    }
}
