using KuchniaUCygana.Domain.Entities.Orders;

namespace KuchniaUCygana.Domain.Interfaces;

public interface IDeliveryWindowRepository : IRepository<DeliveryWindow>
{
    Task<IReadOnlyList<DeliveryWindow>> GetByIdsAsync(IEnumerable<int> ids);
    Task<IEnumerable<DeliveryWindow>> GetActiveWindowsAsync();
}
