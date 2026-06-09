using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Packing;

public interface IPackingManifestIssueRepository : IRepository<PackingManifestIssue>
{
    Task<IReadOnlyList<PackingManifestIssue>> GetByRouteAsync(DateOnly date, int routeId);
}
