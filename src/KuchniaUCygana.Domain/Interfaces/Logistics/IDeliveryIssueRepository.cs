using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Domain.Interfaces.Logistics;

public interface IDeliveryIssueRepository : IRepository<DeliveryIssue>
{
    Task<IReadOnlyList<DeliveryIssue>> GetByRouteStopIdsAsync(IEnumerable<int> routeStopIds);
}
