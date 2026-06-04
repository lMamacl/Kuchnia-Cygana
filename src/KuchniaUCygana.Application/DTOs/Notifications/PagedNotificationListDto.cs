namespace KuchniaUCygana.Application.DTOs.Notifications;

public sealed class PagedNotificationListDto
{
    public NotificationListFilterDto Filter { get; set; } = new();

    public IReadOnlyList<NotificationDto> Items { get; set; } = Array.Empty<NotificationDto>();

    public int TotalCount { get; set; }

    public int UnreadCount { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
}
