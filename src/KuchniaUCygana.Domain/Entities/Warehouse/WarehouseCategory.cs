using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Table("WarehouseCategories")]
public sealed class WarehouseCategory : BaseEntity
{
    [Required]
    [StringLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }
}
