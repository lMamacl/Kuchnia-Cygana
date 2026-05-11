using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class CustomerProfileService : ICustomerProfileService
{
    private readonly ICustomerProfileRepository customerProfileRepository;

    public CustomerProfileService(ICustomerProfileRepository customerProfileRepository)
    {
        this.customerProfileRepository = customerProfileRepository;
    }

    public Task<CustomerProfileDto?> GetByUserIdAsync(int userId) =>
        throw new NotImplementedException();

    public Task EnsureProfileExistsAsync(int userId) =>
        throw new NotImplementedException();

    public Task<bool> UpdateAsync(UpdateCustomerProfileRequest request, int userId) =>
        throw new NotImplementedException();
}
