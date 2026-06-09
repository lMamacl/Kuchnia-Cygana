using KuchniaUCygana.Domain.Entities.Production;

namespace KuchniaUCygana.Domain.Interfaces.Production;

public interface IBoxLabelRepository : IRepository<BoxLabel>
{
    Task<BoxLabel?> GetLatestForPackingItemAsync(int packingItemId);

    Task<BoxLabel?> GetByQrCodeAsync(string qrCode);

    Task<int> GetPrintCountAsync(int packingItemId);
}
