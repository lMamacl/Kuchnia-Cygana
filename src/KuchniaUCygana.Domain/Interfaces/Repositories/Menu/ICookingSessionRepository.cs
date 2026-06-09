using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface ICookingSessionRepository : IRepository<CookingSession>
{
    Task<CookingSession?> GetLatestForComponentAsync(int productionPlanItemId, int recipeComponentVersionId);
}
