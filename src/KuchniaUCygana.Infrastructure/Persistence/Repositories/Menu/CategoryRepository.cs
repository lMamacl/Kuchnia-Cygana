using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class CategoryRepository : BaseRepository<Category>, ICategoryRepository
{
    public CategoryRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<IEnumerable<Category>> GetOrderedAsync()
    {
        using var db = this.Factory.CreateConnection();
        var query = db.From<Category>().OrderBy(c => c.SortOrder);
        return await db.SelectAsync(query);
    }
}
