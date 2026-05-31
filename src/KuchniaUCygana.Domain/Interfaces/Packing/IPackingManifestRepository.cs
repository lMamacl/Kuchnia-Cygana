using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Packing;

public interface IPackingManifestRepository : IRepository<PackingManifest>
{
    Task<PackingManifest?> GetLatestAsync(DateOnly date, int routeId);

    Task<IReadOnlyDictionary<int, PackingManifest>> GetLatestByRoutesAsync(DateOnly date, IEnumerable<int> routeIds);
}
