using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class DietVariantRepository : BaseRepository<DietVariant>, IDietVariantRepository
{
    public DietVariantRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IEnumerable<DietVariant>> GetByDietIdAsync(int dietId)
    {
        using var db = this.Factory.CreateConnection();
        const string sql = "SELECT * FROM [DietVariants] WHERE [DietId] = @DietId AND [IsDeleted] = 0;";
        return await db.QueryAsync<DietVariant>(sql, new { DietId = dietId });
    }

    public async Task<DietVariant?> GetDefaultForDietAsync(int dietId)
    {
        using var db = this.Factory.CreateConnection();
        const string sql = "SELECT TOP 1 * FROM [DietVariants] WHERE [DietId] = @DietId AND [IsDefault] = 1 AND [IsDeleted] = 0;";
        return await db.QuerySingleOrDefaultAsync<DietVariant>(sql, new { DietId = dietId });
    }
}


