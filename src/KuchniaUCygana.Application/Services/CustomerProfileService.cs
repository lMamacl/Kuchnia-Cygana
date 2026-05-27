using AutoMapper;
using KuchniaUCygana.Application.DTOs.Orders;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Customers;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class CustomerProfileService : ICustomerProfileService
{
    private readonly ICustomerProfileRepository customerProfileRepository;
    private readonly IMapper mapper;

    public CustomerProfileService(
        ICustomerProfileRepository customerProfileRepository,
        IMapper mapper)
    {
        this.customerProfileRepository = customerProfileRepository;
        this.mapper = mapper;
    }

    public async Task<CustomerProfileDto?> GetByUserIdAsync(int userId)
    {
        var profile = await customerProfileRepository.GetByUserIdAsync(userId);
        return profile is null ? null : mapper.Map<CustomerProfileDto>(profile);
    }

    public async Task EnsureProfileExistsAsync(int userId)
    {
        var existing = await customerProfileRepository.GetByUserIdAsync(userId);
        if (existing is not null) return;

        var profile = new CustomerProfile
        {
            UserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await customerProfileRepository.InsertAsync(profile);
    }

    public async Task<bool> UpdateAsync(UpdateCustomerProfileRequest request, int userId)
    {
        var profile = await customerProfileRepository.GetByUserIdAsync(userId);
        if (profile is null) return false;

        profile.Phone = request.Phone;
        profile.DietaryNotes = request.DietaryNotes;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        return await customerProfileRepository.UpdateAsync(profile);
    }
}
