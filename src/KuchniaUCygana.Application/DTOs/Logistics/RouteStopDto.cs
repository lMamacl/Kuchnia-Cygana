using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuchniaUCygana.Application.DTOs.Logistics;


public sealed class RouteStopDto
{
    public int Id { get; set; }

    public int SequenceNumber { get; set; }

    public string FullAddress { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? PlannedArrivalTime { get; set; }
}
