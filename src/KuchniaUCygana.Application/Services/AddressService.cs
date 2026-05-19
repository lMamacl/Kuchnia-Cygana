using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Orders;
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
        if (address is null || address.UserId != userId) return null;
        return mapper.Map<AddressDto>(address);
    }

    public async Task<int> CreateAsync(CreateAddressRequest request, int userId)
    {
        var existing = await addressRepository.GetByUserIdAsync(userId);
        var isFirstAddress = !existing.Any();

        var address = mapper.Map<Address>(request);
        address.UserId = userId;
        address.CreatedAt = DateTimeOffset.UtcNow;

        var shouldBeDefault = isFirstAddress || request.IsDefault;

        if (shouldBeDefault && existing.Any())
        {
            foreach (var addr in existing.Where(a => a.IsDefault))
            {
                addr.IsDefault = false;
                addr.UpdatedAt = DateTimeOffset.UtcNow;
                await addressRepository.UpdateAsync(addr);
            }
        }

        address.IsDefault = shouldBeDefault;
        return await addressRepository.InsertAsync(address);
    }

    public async Task<bool> UpdateAsync(UpdateAddressRequest request, int userId)
    {
        var address = await addressRepository.GetByIdAsync(request.Id);
        if (address is null || address.UserId != userId) return false;

        mapper.Map(request, address);
        address.UpdatedAt = DateTimeOffset.UtcNow;

        return await addressRepository.UpdateAsync(address);
    }

    public async Task<bool> DeleteAsync(int addressId, int userId)
    {
        var address = await addressRepository.GetByIdAsync(addressId);
        if (address is null || address.UserId != userId) return false;

        if (address.IsDefault)
        {
            var others = (await addressRepository.GetByUserIdAsync(userId))
                .Where(a => a.Id != addressId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();

            if (others is not null)
            {
                others.IsDefault = true;
                others.UpdatedAt = DateTimeOffset.UtcNow;
                await addressRepository.UpdateAsync(others);
            }
        }

        return await addressRepository.DeleteAsync(addressId);
    }

    public async Task<bool> SetDefaultAsync(int addressId, int userId)
    {
        var address = await addressRepository.GetByIdAsync(addressId);
        if (address is null || address.UserId != userId) return false;

        await addressRepository.SetDefaultAsync(userId, addressId);
        return true;
    }
}
