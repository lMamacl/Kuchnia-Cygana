using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Table("UnitsOfMeasure")]
public class UnitOfMeasure : BaseEntity<int>
{
    [Required]
    [StringLength(10)]
    public string Symbol { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;
    
    public string? Description { get; set; }
}
