using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Table("HaccpLocations")]
public sealed class HaccpLocation : BaseEntity
{
    [Required]
    [StringLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public decimal MinTemperatureCelsius { get; set; }

    public decimal MaxTemperatureCelsius { get; set; }

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; }

    [StringLength(255)]
    public string? Notes { get; set; }
}
