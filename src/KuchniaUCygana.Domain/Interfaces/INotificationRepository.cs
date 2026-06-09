using KuchniaUCygana.Domain.Entities.Notifications;

namespace KuchniaUCygana.Domain.Interfaces;

public interface INotificationRepository : IRepository<Notification, long>
{
    Task<long> CreateForUsersAsync(Notification notification, IReadOnlyCollection<int> userIds);

    Task<(IReadOnlyList<UserNotificationRow> Items, int UnreadCount)> GetForUserAsync(int userId, int limit);

    Task<UserNotificationPage> GetPageForUserAsync(int userId, UserNotificationQuery query);

    Task<bool> MarkAsReadAsync(long userNotificationId, int userId);

    Task<int> MarkManyAsReadAsync(int userId, IReadOnlyCollection<long> userNotificationIds);

    Task<int> MarkAllAsReadAsync(int userId);

    Task<bool> ArchiveAsync(long userNotificationId, int userId);

    Task<int> ArchiveManyAsync(int userId, IReadOnlyCollection<long> userNotificationIds);

    Task<int> ArchiveReadAsync(int userId);
}

public sealed class UserNotificationRow
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

    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class UserNotificationQuery
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

public sealed class UserNotificationPage
{
    public IReadOnlyList<UserNotificationRow> Items { get; set; } = Array.Empty<UserNotificationRow>();

    public int TotalCount { get; set; }

    public int UnreadCount { get; set; }

    public int Page { get; set; }

    public int PageSize { get; set; }
}
