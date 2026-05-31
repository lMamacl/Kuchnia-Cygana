using KuchniaUCygana.Domain.Entities.Packing;

namespace KuchniaUCygana.Domain.Interfaces.Packing;

public interface IPackingBagRepository : IRepository<PackingBag>
{
    Task<IEnumerable<PackingBag>> GetBySessionIdAsync(int packingSessionId);

    Task<PackingBag?> GetByCodeAsync(string bagCode);

    Task<PackingBag?> GetDefaultForSessionAsync(int packingSessionId);
}
