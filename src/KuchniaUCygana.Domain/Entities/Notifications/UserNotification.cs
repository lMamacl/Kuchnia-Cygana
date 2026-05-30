using System.ComponentModel.DataAnnotations.Schema;
using KuchniaUCygana.Domain.Common;

namespace KuchniaUCygana.Domain.Entities.Notifications;

[Table("UserNotifications")]
public sealed class UserNotification : BaseEntity<long>
{
    public long NotificationId { get; set; }

    public int UserId { get; set; }

    public bool IsRead { get; set; }

    public DateTimeOffset DeliveredAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ReadAt { get; set; }
}
