namespace KuchniaUCygana.Application.DTOs.Notifications;

public sealed class NotificationSummaryDto
{
    public int UnreadCount { get; set; }

    public IReadOnlyList<NotificationDto> Items { get; set; } = Array.Empty<NotificationDto>();
}
