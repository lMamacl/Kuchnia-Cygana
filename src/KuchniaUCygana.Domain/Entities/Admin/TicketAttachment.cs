using KuchniaUCygana.Domain.Common;


namespace KuchniaUCygana.Domain.Entities.Admin;


public sealed class TicketAttachment : BaseEntity
{

    public int TicketId { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;


    public int UploadedByUserId { get; set; }

    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
}