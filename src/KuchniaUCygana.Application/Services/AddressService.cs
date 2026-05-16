using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class AddressService : IAddressService
{
    private readonly IAddressRepository addressRepository;
    private readonly IMapper mapper;

    public AddressService(IAddressRepository addressRepository, IMapper mapper)
    {
        this.addressRepository = addressRepository;
        this.mapper = mapper;
    }

    public async Task<IEnumerable<AddressDto>> GetByUserIdAsync(int userId)
    {
        var addresses = await addressRepository.GetByUserIdAsync(userId);
        return mapper.Map<IEnumerable<AddressDto>>(addresses);
    }

    public async Task<AddressDto?> GetByIdAsync(int addressId, int userId)
    {
        var address = await addressRepository.GetByIdAsync(addressId);

        if (address is null || address.UserId != userId)
            return null;

        return mapper.Map<AddressDto>(address);
    }

    public Task<int> CreateAsync(CreateAddressRequest request, int userId) =>
        throw new NotImplementedException();

    public Task<bool> UpdateAsync(UpdateAddressRequest request, int userId) =>
        throw new NotImplementedException();

    public Task<bool> DeleteAsync(int addressId, int userId) =>
        throw new NotImplementedException();

    public Task<bool> SetDefaultAsync(int addressId, int userId) =>
        throw new NotImplementedException();
}
