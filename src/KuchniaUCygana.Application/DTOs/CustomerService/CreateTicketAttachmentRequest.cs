namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class CreateTicketAttachmentRequest
{
    public int TicketId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public int UploadedByUserId { get; set; }
}
