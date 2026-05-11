using KuchniaUCygana.Domain.Common;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.Customers;

[Alias("CustomerProfiles")]
public sealed class CustomerProfile : AuditableEntity
{
    [Index(Unique = true)]
    public int UserId { get; set; }

    public string? Phone { get; set; }

    public string? DietaryNotes { get; set; }

    public int? DefaultAddressId { get; set; }
}
