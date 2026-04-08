using KuchniaUCygana.Domain.Entities.Auth;

namespace KuchniaUCygana.Infrastructure.Auth;

public interface IJwtService
{
    string GenerateToken(User user);
}
