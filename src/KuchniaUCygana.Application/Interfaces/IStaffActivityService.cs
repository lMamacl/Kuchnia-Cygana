using KuchniaUCygana.Domain.Entities.Notifications;

namespace KuchniaUCygana.Application.Interfaces;

public interface IStaffActivityService
{
    Task RecordAsync(
        string action,
        string targetEntity,
        string targetId,
        object? oldValue = null,
        object? newValue = null,
        Notification? notification = null,
        IEnumerable<string>? notifyRoles = null);
}
