using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class CategoryRepository : BaseRepository<Category>, ICategoryRepository
{
    public CategoryRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IEnumerable<Category>> GetOrderedAsync()
    {
        using var db = this.Factory.CreateConnection();
        const string sql = "SELECT * FROM [Categories] ORDER BY [SortOrder];";
        return await db.QueryAsync<Category>(sql);
    }
}


