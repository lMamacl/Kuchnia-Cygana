using KuchniaUCygana.Domain.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KuchniaUCygana.Domain.Entities.Customers;

[Table("CustomerProfiles")]
public sealed class CustomerProfile : AuditableEntity
{
    public int UserId { get; set; }

    public string? Phone { get; set; }

    public string? DietaryNotes { get; set; }

    public int? DefaultAddressId { get; set; }
}
