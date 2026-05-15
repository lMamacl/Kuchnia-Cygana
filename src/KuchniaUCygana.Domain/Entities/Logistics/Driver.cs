using KuchniaUCygana.Domain.Common;
using KuchniaUCygana.Domain.Enums;
using ServiceStack.AI;

namespace KuchniaUCygana.Domain.Entities.Logistics;

/// <summary>
/// Reprezentacja osoby uprawnionej do realizacji
/// dostaw, przechowywanie numeru prawa jazdy
/// i powiązanie z kontem użytkownika.
/// </summary>


public sealed class Driver : AuditableEntity
{
    public int UserId { get; set; }

    public string LicenseNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public bool IsAvailable()
    {
        return this.IsActive;
    }
}
