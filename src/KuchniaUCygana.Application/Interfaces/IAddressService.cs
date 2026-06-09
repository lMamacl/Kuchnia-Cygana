using KuchniaUCygana.Application.DTOs.Orders;

namespace KuchniaUCygana.Application.Interfaces;

public interface IAddressService
{
    Task<IEnumerable<AddressDto>> GetByUserIdAsync(int userId);
    Task<AddressDto?> GetByIdAsync(int addressId, int userId);
    Task<int> CreateAsync(CreateAddressRequest request, int userId);
    Task<bool> UpdateAsync(UpdateAddressRequest request, int userId);
    Task<bool> DeleteAsync(int addressId, int userId);
    Task<bool> SetDefaultAsync(int addressId, int userId);
}
