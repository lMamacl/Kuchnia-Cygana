using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class MealAllergenRepository : IMealAllergenRepository
{
    private readonly IDbConnectionFactory factory;

    public MealAllergenRepository(IDbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<IEnumerable<MealAllergen>> GetByMealIdAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();
        return await db.SelectAsync<MealAllergen>(ma => ma.MealId == mealId);
    }

    public async Task RecalculateForMealAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();

        await db.DeleteAsync<MealAllergen>(ma => ma.MealId == mealId);

        var recipeIngredients = await db.SelectAsync<Recipe>(r => r.MealId == mealId);
        var ingredientIds = recipeIngredients.Select(r => r.IngredientId).ToList();

        if (ingredientIds.Count == 0)
        {
            return;
        }

        var ingredientAllergens = await db.SelectAsync<IngredientAllergen>(
            ia => ingredientIds.Contains(ia.IngredientId));

        if (ingredientAllergens.Count == 0)
        {
            return;
        }

        var allergenGroups = ingredientAllergens
            .GroupBy(ia => ia.AllergenId)
            .Select(g => new MealAllergen
            {
                MealId = mealId,
                AllergenId = g.Key,
                IsTrace = g.All(ia => ia.TraceAmount),
            });

        foreach (var ma in allergenGroups)
        {
            await db.InsertAsync(ma);
        }
    }

    public async Task<bool> DeleteByMealAsync(int mealId)
    {
        using var db = this.factory.CreateConnection();
        return await db.DeleteAsync<MealAllergen>(ma => ma.MealId == mealId) > 0;
    }
}
