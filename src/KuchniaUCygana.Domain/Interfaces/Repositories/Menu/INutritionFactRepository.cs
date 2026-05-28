using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface INutritionFactRepository : IRepository<NutritionFact>
{
    Task<NutritionFact?> GetForMealAsync(int mealId);

    Task<NutritionFact?> GetForIngredientAsync(int ingredientId);
}
