using KuchniaUCygana.Application.DTOs;

namespace KuchniaUCygana.Application.Interfaces;

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllAsync();
}
