using KuchniaUCygana.Domain.Common;
using ServiceStack.DataAnnotations;

namespace KuchniaUCygana.Domain.Entities.HR;

[Alias("Departments")]
public sealed class Department : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    [References(typeof(Employee))]
    public int? HeadEmployeeId { get; set; }
}