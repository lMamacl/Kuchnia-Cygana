using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IMealImageRepository : IRepository<MealImage>
{
    Task<IEnumerable<MealImage>> GetByMealIdAsync(int mealId);

    Task<MealImage?> GetMainForMealAsync(int mealId);

    Task<bool> DeleteByMealAsync(int mealId);
}
