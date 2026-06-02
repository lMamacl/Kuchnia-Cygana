using System.ComponentModel.DataAnnotations;

namespace KuchniaUCygana.Application.DTOs.Logistics;

public sealed class UpdateDeliveryRouteRequest
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int VehicleId { get; set; }

    public List<UpdateDeliveryRouteStopRequest> Stops { get; set; } = new();
}

public sealed class UpdateDeliveryRouteStopRequest
{
    public int StopId { get; set; }

    [Range(1, int.MaxValue)]
    public int SequenceNumber { get; set; }
}
