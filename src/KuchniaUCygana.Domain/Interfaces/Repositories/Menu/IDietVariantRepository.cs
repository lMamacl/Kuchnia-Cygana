using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Menu;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface IDietVariantRepository : IRepository<DietVariant>
{
    Task<IEnumerable<DietVariant>> GetByDietIdAsync(int dietId);

    Task<DietVariant?> GetDefaultForDietAsync(int dietId);
}
