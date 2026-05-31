namespace KuchniaUCygana.Application.DTOs.Notifications;

public sealed class NotificationListFilterDto
{
    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public string Status { get; set; } = "All";

    public string? Severity { get; set; }

    public string? Type { get; set; }

    public DateOnly? DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    public string? Search { get; set; }
}
