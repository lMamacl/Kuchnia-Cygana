using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Interfaces.Packing;

namespace KuchniaUCygana.Application.Services;

public sealed class PackingBagService : IPackingBagService
{
    private readonly IPackingBagRepository _bagRepository;

    public PackingBagService(IPackingBagRepository bagRepository)
    {
        _bagRepository = bagRepository;
    }

    public async Task<PackingBag> EnsureDefaultBagAsync(int packingSessionId)
    {
        var existing = await _bagRepository.GetDefaultForSessionAsync(packingSessionId);
        if (existing is not null)
        {
            return existing;
        }

        var bag = new PackingBag
        {
            PackingSessionId = packingSessionId,
            BagNumber = 1,
            BagCode = $"BAG-{packingSessionId:D6}-01",
        };

        var id = await _bagRepository.InsertAsync(bag);
        bag.Id = id;
        return bag;
    }

    public async Task<IReadOnlyList<PackingBag>> GetBagsForSessionAsync(int packingSessionId)
    {
        return (await _bagRepository.GetBySessionIdAsync(packingSessionId)).ToList();
    }
}
