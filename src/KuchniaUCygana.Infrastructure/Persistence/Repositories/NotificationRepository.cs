using Dapper;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class NotificationRepository : BaseRepository<Notification, long>, INotificationRepository
{
    public NotificationRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
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
              AND [ArchivedAt] IS NULL
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
                un.[DeliveredAt],
                un.[ReadAt]
            FROM [UserNotifications] un
            INNER JOIN [Notifications] n ON n.[Id] = un.[NotificationId]
            WHERE un.[UserId] = @userId
              AND un.[ArchivedAt] IS NULL
              AND un.[IsRead] = 0
            ORDER BY un.[DeliveredAt] DESC, un.[Id] DESC;
            """,
            new { userId, safeLimit });

        return (items.ToList(), unreadCount);
    }

    public async Task<UserNotificationPage> GetPageForUserAsync(int userId, UserNotificationQuery query)
    {
        using var db = Factory.CreateConnection();
        var safePage = Math.Max(query.Page, 1);
        var safePageSize = Math.Clamp(query.PageSize, 1, 100);
        var offset = (safePage - 1) * safePageSize;

        var where = new List<string>
        {
            "un.[UserId] = @userId",
            "un.[ArchivedAt] IS NULL",
        };
        var parameters = new DynamicParameters();
        parameters.Add("userId", userId);
        parameters.Add("offset", offset);
        parameters.Add("pageSize", safePageSize);

        if (string.Equals(query.Status, "Unread", StringComparison.OrdinalIgnoreCase))
        {
            where.Add("un.[IsRead] = 0");
        }
        else if (string.Equals(query.Status, "Read", StringComparison.OrdinalIgnoreCase))
        {
            where.Add("un.[IsRead] = 1");
        }

        if (!string.IsNullOrWhiteSpace(query.Severity) &&
            !string.Equals(query.Severity, "All", StringComparison.OrdinalIgnoreCase))
        {
            where.Add("n.[Severity] = @severity");
            parameters.Add("severity", query.Severity.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Type) &&
            !string.Equals(query.Type, "All", StringComparison.OrdinalIgnoreCase))
        {
            where.Add("n.[Type] = @type");
            parameters.Add("type", query.Type.Trim());
        }

        if (query.DateFrom.HasValue)
        {
            where.Add("un.[DeliveredAt] >= @dateFrom");
            parameters.Add(
                "dateFrom",
                new DateTimeOffset(query.DateFrom.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        }

        if (query.DateTo.HasValue)
        {
            where.Add("un.[DeliveredAt] < @dateTo");
            parameters.Add(
                "dateTo",
                new DateTimeOffset(query.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("(n.[Title] LIKE @search OR n.[Message] LIKE @search OR n.[Type] LIKE @search)");
            parameters.Add("search", $"%{query.Search.Trim()}%");
        }

        var whereSql = string.Join(" AND ", where);
        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(1)
            FROM [UserNotifications] un
            INNER JOIN [Notifications] n ON n.[Id] = un.[NotificationId]
            WHERE {whereSql};
            """,
            parameters);

        var unreadCount = await db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM [UserNotifications]
            WHERE [UserId] = @userId
              AND [ArchivedAt] IS NULL
              AND [IsRead] = 0;
            """,
            new { userId });

        var items = await db.QueryAsync<UserNotificationRow>(
            $"""
            SELECT
                un.[Id] AS [UserNotificationId],
                n.[Id] AS [NotificationId],
                n.[Type],
                n.[Severity],
                n.[Title],
                n.[Message],
                n.[LinkUrl],
                un.[IsRead],
                un.[DeliveredAt],
                un.[ReadAt]
            FROM [UserNotifications] un
            INNER JOIN [Notifications] n ON n.[Id] = un.[NotificationId]
            WHERE {whereSql}
            ORDER BY un.[IsRead] ASC, un.[DeliveredAt] DESC, un.[Id] DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """,
            parameters);

        return new UserNotificationPage
        {
            Items = items.ToList(),
            TotalCount = totalCount,
            UnreadCount = unreadCount,
            Page = safePage,
            PageSize = safePageSize,
        };
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
              AND [UserId] = @userId
              AND [ArchivedAt] IS NULL;
            """,
            new { userNotificationId, userId, now = DateTimeOffset.UtcNow });

        return affected > 0;
    }

    public async Task<int> MarkManyAsReadAsync(int userId, IReadOnlyCollection<long> userNotificationIds)
    {
        var ids = userNotificationIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return 0;
        }

        using var db = Factory.CreateConnection();
        return await db.ExecuteAsync(
            """
            UPDATE [UserNotifications]
            SET [IsRead] = 1,
                [ReadAt] = COALESCE([ReadAt], @now),
                [UpdatedAt] = @now
            WHERE [UserId] = @userId
              AND [ArchivedAt] IS NULL
              AND [Id] IN @ids;
            """,
            new { userId, ids, now = DateTimeOffset.UtcNow });
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
              AND [ArchivedAt] IS NULL
              AND [IsRead] = 0;
            """,
            new { userId, now = DateTimeOffset.UtcNow });
    }

    public async Task<bool> ArchiveAsync(long userNotificationId, int userId)
    {
        using var db = Factory.CreateConnection();
        var affected = await db.ExecuteAsync(
            """
            UPDATE [UserNotifications]
            SET [ArchivedAt] = @now,
                [ArchivedByUserId] = @userId,
                [UpdatedAt] = @now
            WHERE [Id] = @userNotificationId
              AND [UserId] = @userId
              AND [ArchivedAt] IS NULL;
            """,
            new { userNotificationId, userId, now = DateTimeOffset.UtcNow });

        return affected > 0;
    }

    public async Task<int> ArchiveManyAsync(int userId, IReadOnlyCollection<long> userNotificationIds)
    {
        var ids = userNotificationIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return 0;
        }

        using var db = Factory.CreateConnection();
        return await db.ExecuteAsync(
            """
            UPDATE [UserNotifications]
            SET [ArchivedAt] = @now,
                [ArchivedByUserId] = @userId,
                [UpdatedAt] = @now
            WHERE [UserId] = @userId
              AND [ArchivedAt] IS NULL
              AND [Id] IN @ids;
            """,
            new { userId, ids, now = DateTimeOffset.UtcNow });
    }

    public async Task<int> ArchiveReadAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        return await db.ExecuteAsync(
            """
            UPDATE [UserNotifications]
            SET [ArchivedAt] = @now,
                [ArchivedByUserId] = @userId,
                [UpdatedAt] = @now
            WHERE [UserId] = @userId
              AND [ArchivedAt] IS NULL
              AND [IsRead] = 1;
            """,
            new { userId, now = DateTimeOffset.UtcNow });
    }
}


