using KuchniaUCygana.Domain.Entities.Packing;

namespace KuchniaUCygana.Domain.Interfaces.Packing;

public interface IPackingBagRepository : IRepository<PackingBag>
{
    Task<IEnumerable<PackingBag>> GetBySessionIdAsync(int packingSessionId);

    Task<IReadOnlyList<PackingBag>> GetBySessionIdsAsync(IEnumerable<int> packingSessionIds);

    Task<PackingBag?> GetByCodeAsync(string bagCode);

    Task<PackingBag?> GetDefaultForSessionAsync(int packingSessionId);
}
