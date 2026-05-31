using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuchniaUCygana.Application.DTOs.Logistics;


public sealed class RouteStopDto
{
    public int Id { get; set; }

    public int DeliveryCalendarId { get; set; }

    public int SequenceNumber { get; set; }

    public string FullAddress { get; set; } = string.Empty;

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public decimal EstimatedLoadKg { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? PlannedArrivalTime { get; set; }
}
