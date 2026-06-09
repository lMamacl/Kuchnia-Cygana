using KuchniaUCygana.Domain.Entities.Packing;

namespace KuchniaUCygana.Application.Interfaces;

public interface IPackingBagService
{
    Task<PackingBag> EnsureDefaultBagAsync(int packingSessionId);

    Task<IReadOnlyList<PackingBag>> GetBagsForSessionAsync(int packingSessionId);
}
