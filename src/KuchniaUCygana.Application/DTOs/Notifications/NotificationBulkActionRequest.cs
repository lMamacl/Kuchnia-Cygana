namespace KuchniaUCygana.Application.DTOs.Notifications;

public sealed class NotificationBulkActionRequest
{
    public string Action { get; set; } = string.Empty;

    public List<long> UserNotificationIds { get; set; } = new();
}
