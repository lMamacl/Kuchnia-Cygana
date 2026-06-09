using KuchniaUCygana.Application.DTOs.Notifications;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class NotificationService : INotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserService _currentUserService;

    public NotificationService(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        ICurrentUserService currentUserService)
    {
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _currentUserService = currentUserService;
    }

    public async Task<NotificationSummaryDto> GetCurrentUserSummaryAsync(int limit = 5)
    {
        var userId = _currentUserService.GetUserId();
        if (!userId.HasValue)
        {
            return new NotificationSummaryDto();
        }

        var (items, unreadCount) = await _notificationRepository.GetForUserAsync(userId.Value, limit);
        return new NotificationSummaryDto
        {
            UnreadCount = unreadCount,
            Items = items.Select(Map).ToList(),
        };
    }

    public async Task<PagedNotificationListDto> GetCurrentUserNotificationsAsync(NotificationListFilterDto filter)
    {
        var userId = _currentUserService.GetUserId();
        if (!userId.HasValue)
        {
            return new PagedNotificationListDto { Filter = NormalizeFilter(filter) };
        }

        var normalized = NormalizeFilter(filter);
        var page = await _notificationRepository.GetPageForUserAsync(
            userId.Value,
            new UserNotificationQuery
            {
                Page = normalized.Page,
                PageSize = normalized.PageSize,
                Status = normalized.Status,
                Severity = normalized.Severity,
                Type = normalized.Type,
                DateFrom = normalized.DateFrom,
                DateTo = normalized.DateTo,
                Search = normalized.Search,
            });

        return new PagedNotificationListDto
        {
            Filter = normalized,
            Items = page.Items.Select(Map).ToList(),
            TotalCount = page.TotalCount,
            UnreadCount = page.UnreadCount,
            Page = page.Page,
            PageSize = page.PageSize,
        };
    }

    public async Task CreateForRolesAsync(Notification notification, IEnumerable<string> roles)
    {
        var users = await _userRepository.GetByRolesAsync(roles);
        var userIds = users.Select(user => user.Id).Distinct().ToArray();
        if (userIds.Length == 0)
        {
            return;
        }

        await _notificationRepository.CreateForUsersAsync(notification, userIds);
    }

    public async Task<bool> MarkAsReadAsync(long userNotificationId)
    {
        var userId = _currentUserService.GetUserId();
        return userId.HasValue
            && await _notificationRepository.MarkAsReadAsync(userNotificationId, userId.Value);
    }

    public async Task<int> MarkManyAsReadAsync(IEnumerable<long> userNotificationIds)
    {
        var userId = _currentUserService.GetUserId();
        return userId.HasValue
            ? await _notificationRepository.MarkManyAsReadAsync(
                userId.Value,
                userNotificationIds.Where(id => id > 0).Distinct().ToArray())
            : 0;
    }

    public async Task<int> MarkAllAsReadAsync()
    {
        var userId = _currentUserService.GetUserId();
        return userId.HasValue
            ? await _notificationRepository.MarkAllAsReadAsync(userId.Value)
            : 0;
    }

    public async Task<bool> ArchiveAsync(long userNotificationId)
    {
        var userId = _currentUserService.GetUserId();
        return userId.HasValue
            && await _notificationRepository.ArchiveAsync(userNotificationId, userId.Value);
    }

    public async Task<int> ArchiveManyAsync(IEnumerable<long> userNotificationIds)
    {
        var userId = _currentUserService.GetUserId();
        return userId.HasValue
            ? await _notificationRepository.ArchiveManyAsync(
                userId.Value,
                userNotificationIds.Where(id => id > 0).Distinct().ToArray())
            : 0;
    }

    public async Task<int> ArchiveReadAsync()
    {
        var userId = _currentUserService.GetUserId();
        return userId.HasValue
            ? await _notificationRepository.ArchiveReadAsync(userId.Value)
            : 0;
    }

    private static NotificationDto Map(UserNotificationRow row)
    {
        return new NotificationDto
        {
            UserNotificationId = row.UserNotificationId,
            NotificationId = row.NotificationId,
            Type = row.Type,
            Severity = row.Severity,
            Title = row.Title,
            Message = row.Message,
            LinkUrl = row.LinkUrl,
            IsRead = row.IsRead,
            DeliveredAt = row.DeliveredAt,
            ReadAt = row.ReadAt,
        };
    }

    private static NotificationListFilterDto NormalizeFilter(NotificationListFilterDto? filter)
    {
        var normalized = filter ?? new NotificationListFilterDto();
        normalized.Page = Math.Max(normalized.Page, 1);
        normalized.PageSize = Math.Clamp(normalized.PageSize, 1, 100);
        normalized.Status = normalized.Status switch
        {
            "Unread" => "Unread",
            "Read" => "Read",
            _ => "All",
        };
        normalized.Severity = string.IsNullOrWhiteSpace(normalized.Severity) || normalized.Severity == "All"
            ? null
            : normalized.Severity.Trim();
        normalized.Type = string.IsNullOrWhiteSpace(normalized.Type) || normalized.Type == "All"
            ? null
            : normalized.Type.Trim();
        normalized.Search = string.IsNullOrWhiteSpace(normalized.Search)
            ? null
            : normalized.Search.Trim();

        return normalized;
    }
}
