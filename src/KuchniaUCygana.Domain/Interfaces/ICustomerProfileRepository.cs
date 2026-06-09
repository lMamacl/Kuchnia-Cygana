using KuchniaUCygana.Domain.Entities.Customers;

namespace KuchniaUCygana.Domain.Interfaces;

public interface ICustomerProfileRepository : IRepository<CustomerProfile>
{
    Task<CustomerProfile?> GetByUserIdAsync(int userId);
}
