using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IMealAllergenRepository
{
    Task<IEnumerable<MealAllergen>> GetByMealIdAsync(int mealId);

    Task RecalculateForMealAsync(int mealId);

    Task<bool> DeleteByMealAsync(int mealId);
}
