using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class UserService : IUserService
{
    private readonly IRepository<User> repository;

    public UserService(IRepository<User> repository)
    {
        this.repository = repository;
    }

    public async Task<IEnumerable<UserDto>> GetAllAsync()
    {
        var users = await this.repository.GetAllAsync();
        return users.Select(
            user => new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = $"{user.FirstName} {user.LastName}",
                Role = user.Role,
            });
    }
}
