using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IAllergenRepository : IRepository<Allergen>
{
    Task<IEnumerable<Allergen>> GetAllOrderedAsync();
}
