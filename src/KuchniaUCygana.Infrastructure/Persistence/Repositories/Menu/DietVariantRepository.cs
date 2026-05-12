using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class DietVariantRepository : BaseRepository<DietVariant>, IDietVariantRepository
{
    public DietVariantRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<IEnumerable<DietVariant>> GetByDietIdAsync(int dietId)
    {
        using var db = this.Factory.CreateConnection();
        return await db.SelectAsync<DietVariant>(dv => dv.DietId == dietId && !dv.IsDeleted);
    }

    public async Task<DietVariant?> GetDefaultForDietAsync(int dietId)
    {
        using var db = this.Factory.CreateConnection();
        return await db.SingleAsync<DietVariant>(dv =>
            dv.DietId == dietId && dv.IsDefault && !dv.IsDeleted);
    }
}
