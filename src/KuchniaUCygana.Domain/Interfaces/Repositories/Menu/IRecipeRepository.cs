using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IRecipeRepository
{
    Task<IEnumerable<Recipe>> GetByMealIdAsync(int mealId);

    Task<int> InsertAsync(Recipe recipe);

    Task<bool> UpdateAsync(Recipe recipe);

    Task<bool> DeleteAsync(int id);

    Task<bool> DeleteByMealAsync(int mealId);
}
