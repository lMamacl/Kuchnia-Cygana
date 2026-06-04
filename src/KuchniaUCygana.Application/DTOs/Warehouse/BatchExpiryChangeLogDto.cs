using System;

namespace KuchniaUCygana.Application.DTOs.Warehouse;

/// <summary>
/// DTO dla logu zmian daty ważności partii.
/// </summary>
public sealed class BatchExpiryChangeLogDto
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateTime OldExpiryDate { get; set; }
    public DateTime NewExpiryDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int? ChangedByUserId { get; set; }
    public string ChangedByUserName { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
}
