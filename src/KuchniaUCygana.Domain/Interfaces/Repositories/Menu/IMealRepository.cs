using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IMealRepository : IRepository<Meal>
{
    Task<IEnumerable<Meal>> GetPublishedAsync();

    Task<Meal?> GetWithRecipeAsync(int mealId);
}
