using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class AllergenRepository : BaseRepository<Allergen>, IAllergenRepository
{
    public AllergenRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IEnumerable<Allergen>> GetAllOrderedAsync()
    {
        using var db = this.Factory.CreateConnection();
        const string sql = "SELECT * FROM [Allergens] ORDER BY [Name];";
        return await db.QueryAsync<Allergen>(sql);
    }
}


