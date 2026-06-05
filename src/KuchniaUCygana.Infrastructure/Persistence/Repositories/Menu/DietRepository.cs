using Dapper;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Menu;

public sealed class DietRepository : BaseRepository<Diet>, IDietRepository
{
    public DietRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IEnumerable<Diet>> GetActiveWithVariantsAsync()
    {
        using var db = this.Factory.CreateConnection();
        const string dietSql = "SELECT * FROM [Diets] WHERE [IsActive] = 1 AND [IsDeleted] = 0;";
        var diets = await db.QueryAsync<Diet>(dietSql);
        foreach (var diet in diets)
        {
            var variantSql = "SELECT * FROM [DietVariants] WHERE [DietId] = @DietId AND [IsDeleted] = 0;";
            var variants = await db.QueryAsync<DietVariant>(variantSql, new { DietId = diet.Id });
            // variants nie są używane dalej – jeśli trzeba, przypisać do diet.Variants (ale Diet nie ma takiej właściwości)
        }
        return diets;
    }
}


