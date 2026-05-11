using KuchniaUCygana.Domain.Entities.Orders;

namespace KuchniaUCygana.Domain.Interfaces;

public interface IDeliveryWindowRepository : IRepository<DeliveryWindow>
{
    Task<IEnumerable<DeliveryWindow>> GetActiveWindowsAsync();
}
