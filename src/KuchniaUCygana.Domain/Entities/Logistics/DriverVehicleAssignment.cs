using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Logistics;

[Table("DriverVehicleAssignments")]
public sealed class DriverVehicleAssignment : BaseEntity
{
    public int DriverId { get; set; }

    public int VehicleId { get; set; }

    public DateTimeOffset AssignedAt { get; set; }

    public DateTimeOffset? UnassignedAt { get; set; }
}
