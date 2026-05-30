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
            Items = items.Select(row => new NotificationDto
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
            }).ToList(),
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

    public async Task<int> MarkAllAsReadAsync()
    {
        var userId = _currentUserService.GetUserId();
        return userId.HasValue
            ? await _notificationRepository.MarkAllAsReadAsync(userId.Value)
            : 0;
    }
}
