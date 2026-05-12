using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class AllergenRepository : BaseRepository<Allergen>, IAllergenRepository
{
    public AllergenRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<IEnumerable<Allergen>> GetAllOrderedAsync()
    {
        using var db = this.Factory.CreateConnection();
        var query = db.From<Allergen>().OrderBy(a => a.Name);
        return await db.SelectAsync(query);
    }
}
