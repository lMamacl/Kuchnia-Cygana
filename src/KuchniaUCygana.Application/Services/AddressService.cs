using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class AddressService : IAddressService
{
    private readonly IAddressRepository addressRepository;

    public AddressService(IAddressRepository addressRepository)
    {
        this.addressRepository = addressRepository;
    }

    public Task<IEnumerable<AddressDto>> GetByUserIdAsync(int userId) =>
        throw new NotImplementedException();

    public Task<AddressDto?> GetByIdAsync(int addressId, int userId) =>
        throw new NotImplementedException();

    public Task<int> CreateAsync(CreateAddressRequest request, int userId) =>
        throw new NotImplementedException();

    public Task<bool> UpdateAsync(UpdateAddressRequest request, int userId) =>
        throw new NotImplementedException();

    public Task<bool> DeleteAsync(int addressId, int userId) =>
        throw new NotImplementedException();

    public Task<bool> SetDefaultAsync(int addressId, int userId) =>
        throw new NotImplementedException();
}
