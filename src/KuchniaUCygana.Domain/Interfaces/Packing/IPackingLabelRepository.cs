using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Packing;

public interface IPackingLabelRepository : IRepository<PackingLabel>
{
    Task<PackingLabel?> GetLatestShippingForBagAsync(int packingBagId);

    Task<IReadOnlyDictionary<int, PackingLabel>> GetLatestShippingForBagsAsync(IEnumerable<int> packingBagIds);

    Task<IReadOnlyList<PackingLabel>> GetShippingForBagsAsync(IEnumerable<int> packingBagIds);

    Task<IReadOnlyList<PackingLabel>> GetShippingForSessionAsync(int packingSessionId);

    Task<PackingLabel?> GetShippingByQrCodeAsync(string qrCode);

    Task<int> GetShippingPrintCountAsync(int packingSessionId, int packingBagId);
}
