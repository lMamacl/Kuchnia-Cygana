using System.Text.Json;
using KuchniaUCygana.Application.DTOs.Admin;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class StaffActivityService : IStaffActivityService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly IAuditLogService auditLogService;
    private readonly ICurrentUserService currentUserService;
    private readonly INotificationService notificationService;

    public StaffActivityService(
        IAuditLogService auditLogService,
        ICurrentUserService currentUserService,
        INotificationService notificationService)
    {
        this.auditLogService = auditLogService;
        this.currentUserService = currentUserService;
        this.notificationService = notificationService;
    }

    public async Task RecordAsync(
        string action,
        string targetEntity,
        string targetId,
        object? oldValue = null,
        object? newValue = null,
        Notification? notification = null,
        IEnumerable<string>? notifyRoles = null)
    {
        var userId = currentUserService.GetUserId();
        if (userId is > 0)
        {
            await auditLogService.CreateSystemLogAsync(new CreateSystemLogRequest
            {
                UserId = userId.Value,
                Action = Trim(action, 100),
                TargetEntity = Trim(targetEntity, 50),
                TargetId = Trim(targetId, 100),
                OldValue = Serialize(oldValue),
                NewValue = Serialize(newValue),
                IPAddress = Trim(currentUserService.GetIpAddress(), 45),
            });
        }

        if (notification is not null && notifyRoles is not null)
        {
            var roles = notifyRoles
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            if (roles.Length > 0)
            {
                await notificationService.CreateForRolesAsync(notification, roles);
            }
        }
    }

    private static string? Serialize(object? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
    }

    private static string Trim(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
