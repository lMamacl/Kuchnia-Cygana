using KuchniaUCygana.Application.DTOs.Orders;

namespace KuchniaUCygana.Application.Interfaces;

public interface ICustomerProfileService
{
    Task<CustomerProfileDto?> GetByUserIdAsync(int userId);
    Task EnsureProfileExistsAsync(int userId);
    Task<bool> UpdateAsync(UpdateCustomerProfileRequest request, int userId);
}
