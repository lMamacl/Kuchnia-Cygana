namespace KuchniaUCygana.Application.DTOs.Notifications;

public sealed class NotificationDto
{
    public long UserNotificationId { get; set; }

    public long NotificationId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? LinkUrl { get; set; }

    public bool IsRead { get; set; }

    public DateTimeOffset DeliveredAt { get; set; }
}
