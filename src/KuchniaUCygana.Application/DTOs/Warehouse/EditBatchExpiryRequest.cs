using System;

namespace KuchniaUCygana.Application.DTOs.Warehouse;

public sealed class EditBatchExpiryRequest
{
    public int BatchId { get; set; }

    public DateTimeOffset NewExpiryDate { get; set; }

    public string Reason { get; set; } = string.Empty;
}
