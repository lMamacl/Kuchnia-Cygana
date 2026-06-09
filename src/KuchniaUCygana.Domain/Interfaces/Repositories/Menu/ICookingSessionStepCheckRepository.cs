using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

public interface ICookingSessionStepCheckRepository : IRepository<CookingSessionStepCheck>
{
    Task<CookingSessionStepCheck?> GetBySessionAndStepAsync(int cookingSessionId, int instructionStepId);

    Task<IReadOnlyList<CookingSessionStepCheck>> GetBySessionAsync(int cookingSessionId);
}
