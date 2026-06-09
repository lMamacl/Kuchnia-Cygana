namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class TicketAttachmentDto
{
    public int Id { get; set; }

    public int TicketId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public int UploadedByUserId { get; set; }

    public string? UploadedByFullName { get; set; }

    public DateTimeOffset UploadedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
