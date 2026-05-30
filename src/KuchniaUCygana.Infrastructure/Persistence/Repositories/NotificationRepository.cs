using Dapper;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository : BaseRepository<Notification, long>, INotificationRepository
{
    public NotificationRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<long> CreateForUsersAsync(Notification notification, IReadOnlyCollection<int> userIds)
    {
        var recipientIds = userIds.Where(id => id > 0).Distinct().ToArray();
        if (recipientIds.Length == 0)
        {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        using var db = Factory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction();

        long notificationId = 0;
        if (!string.IsNullOrWhiteSpace(notification.DeduplicationKey))
        {
            notificationId = await db.ExecuteScalarAsync<long?>(
                """
                SELECT TOP 1 [Id]
                FROM [Notifications]
                WHERE [DeduplicationKey] = @DeduplicationKey;
                """,
                notification,
                tx) ?? 0;
        }

        if (notificationId == 0)
        {
            notification.CreatedAt = now;
            notificationId = await db.ExecuteScalarAsync<long>(
                """
                INSERT INTO [Notifications]
                    ([Type], [Severity], [Title], [Message], [LinkUrl], [DeduplicationKey],
                     [SourceType], [SourceId], [CreatedAt], [UpdatedAt])
                OUTPUT INSERTED.[Id]
                VALUES
                    (@Type, @Severity, @Title, @Message, @LinkUrl, @DeduplicationKey,
                     @SourceType, @SourceId, @CreatedAt, @UpdatedAt);
                """,
                notification,
                tx);
        }

        await db.ExecuteAsync(
            """
            IF NOT EXISTS (
                SELECT 1
                FROM [UserNotifications]
                WHERE [NotificationId] = @NotificationId
                  AND [UserId] = @UserId
            )
            BEGIN
                INSERT INTO [UserNotifications]
                    ([NotificationId], [UserId], [IsRead], [DeliveredAt], [ReadAt], [CreatedAt], [UpdatedAt])
                VALUES
                    (@NotificationId, @UserId, 0, @DeliveredAt, NULL, @DeliveredAt, NULL);
            END
            """,
            recipientIds.Select(userId => new
            {
                NotificationId = notificationId,
                UserId = userId,
                DeliveredAt = now,
            }),
            tx);

        tx.Commit();
        return notificationId;
    }

    public async Task<(IReadOnlyList<UserNotificationRow> Items, int UnreadCount)> GetForUserAsync(int userId, int limit)
    {
        using var db = Factory.CreateConnection();
        var safeLimit = Math.Clamp(limit, 1, 20);

        var unreadCount = await db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM [UserNotifications]
            WHERE [UserId] = @userId
              AND [IsRead] = 0;
            """,
            new { userId });

        var items = await db.QueryAsync<UserNotificationRow>(
            """
            SELECT TOP (@safeLimit)
                un.[Id] AS [UserNotificationId],
                n.[Id] AS [NotificationId],
                n.[Type],
                n.[Severity],
                n.[Title],
                n.[Message],
                n.[LinkUrl],
                un.[IsRead],
                un.[DeliveredAt]
            FROM [UserNotifications] un
            INNER JOIN [Notifications] n ON n.[Id] = un.[NotificationId]
            WHERE un.[UserId] = @userId
            ORDER BY un.[IsRead] ASC, un.[DeliveredAt] DESC, un.[Id] DESC;
            """,
            new { userId, safeLimit });

        return (items.ToList(), unreadCount);
    }

    public async Task<bool> MarkAsReadAsync(long userNotificationId, int userId)
    {
        using var db = Factory.CreateConnection();
        var affected = await db.ExecuteAsync(
            """
            UPDATE [UserNotifications]
            SET [IsRead] = 1,
                [ReadAt] = @now,
                [UpdatedAt] = @now
            WHERE [Id] = @userNotificationId
              AND [UserId] = @userId;
            """,
            new { userNotificationId, userId, now = DateTimeOffset.UtcNow });

        return affected > 0;
    }

    public async Task<int> MarkAllAsReadAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        return await db.ExecuteAsync(
            """
            UPDATE [UserNotifications]
            SET [IsRead] = 1,
                [ReadAt] = @now,
                [UpdatedAt] = @now
            WHERE [UserId] = @userId
              AND [IsRead] = 0;
            """,
            new { userId, now = DateTimeOffset.UtcNow });
    }
}
