using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IIngredientRepository : IRepository<Ingredient>
{
    Task<bool> CanDeleteAsync(int ingredientId);
}
