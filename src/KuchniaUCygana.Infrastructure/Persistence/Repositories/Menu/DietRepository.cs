using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class DietRepository : BaseRepository<Diet>, IDietRepository
{
    public DietRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<IEnumerable<Diet>> GetActiveWithVariantsAsync()
    {
        using var db = this.Factory.CreateConnection();
        var diets = await db.SelectAsync<Diet>(d => d.IsActive && !d.IsDeleted);

        foreach (var diet in diets)
        {
            var variants = await db.SelectAsync<DietVariant>(dv => dv.DietId == diet.Id && !dv.IsDeleted);
        }

        return diets;
    }
}
