using System.Collections.Generic;
using ServiceStack.DataAnnotations;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Warehouse;

[Alias("UnitsOfMeasure")]
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
