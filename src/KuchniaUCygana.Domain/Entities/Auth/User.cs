using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Auth;

[Table("Users")]
public sealed class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Role { get; set; } = UserRoles.Client;
}
