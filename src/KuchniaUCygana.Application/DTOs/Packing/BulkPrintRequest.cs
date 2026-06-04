using System.Collections.Generic;

namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class BulkPrintRequest
{
    public List<int> SessionIds { get; set; } = new();

    public string OperatorName { get; set; } = string.Empty;
}
