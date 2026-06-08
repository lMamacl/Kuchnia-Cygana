using Dapper;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class TicketRepository : BaseRepository<Ticket>, ITicketRepository
{
    private const int MaxPageSize = 100;

    public TicketRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService) { }

    public async Task<IEnumerable<Ticket>> GetByClientIdAsync(int clientUserId)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<Ticket>(
            "SELECT * FROM [Tickets] WHERE [ClientUserId] = @ClientUserId AND [IsDeleted] = 0", 
            new { ClientUserId = clientUserId });
    }

    public async Task<IEnumerable<Ticket>> GetByStatusAsync(TicketStatus status)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<Ticket>(
            "SELECT * FROM [Tickets] WHERE [Status] = @Status AND [IsDeleted] = 0", 
            new { Status = (int)status });
    }

    public async Task<TicketSearchResult> SearchAsync(TicketSearchQuery query)
    {
        using var db = Factory.CreateConnection();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 10 : query.PageSize, 5, MaxPageSize);
        var where = new List<string>
        {
            "ticket.[IsDeleted] = 0",
        };
        var parameters = new DynamicParameters();

        if (query.OpenOnly)
        {
            where.Add("ticket.[Status] NOT IN @ClosedStatuses");
            parameters.Add("ClosedStatuses", new[] { (int)TicketStatus.Resolved, (int)TicketStatus.Closed });
        }

        if (query.Status.HasValue)
        {
            where.Add("ticket.[Status] = @Status");
            parameters.Add("Status", (int)query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            where.Add("ticket.[Priority] = @Priority");
            parameters.Add("Priority", (int)query.Priority.Value);
        }

        if (query.UnassignedOnly)
        {
            where.Add("ticket.[AssignedToUserId] IS NULL");
        }
        else if (query.AssignedToUserId is > 0)
        {
            where.Add("ticket.[AssignedToUserId] = @AssignedToUserId");
            parameters.Add("AssignedToUserId", query.AssignedToUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add(
                """
                (ticket.[Title] LIKE @Search
                 OR ticket.[Description] LIKE @Search
                 OR CONVERT(varchar(20), ticket.[Id]) LIKE @Search
                 OR CONVERT(varchar(20), ticket.[ClientUserId]) LIKE @Search
                 OR CONVERT(varchar(20), ticket.[OrderId]) LIKE @Search
                 OR CONVERT(varchar(20), ticket.[DeliveryCalendarId]) LIKE @Search
                 OR client.[Email] LIKE @Search
                 OR assigned.[Email] LIKE @Search
                 OR CONCAT(client.[FirstName], ' ', client.[LastName]) LIKE @Search
                 OR CONCAT(assigned.[FirstName], ' ', assigned.[LastName]) LIKE @Search)
                """);
            parameters.Add("Search", $"%{query.Search.Trim()}%");
        }

        var whereSql = "WHERE " + string.Join(" AND ", where);
        var fromSql =
            """
            FROM [Tickets] ticket
            LEFT JOIN [Users] client ON client.[Id] = ticket.[ClientUserId]
            LEFT JOIN [Users] assigned ON assigned.[Id] = ticket.[AssignedToUserId]
            """;

        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(1)
            {fromSql}
            {whereSql};
            """,
            parameters);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);
        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var orderSql = query.QueueOrder
            ? """
              ORDER BY
                CASE ticket.[Priority]
                    WHEN @CriticalPriority THEN 0
                    WHEN @HighPriority THEN 1
                    WHEN @MediumPriority THEN 2
                    ELSE 3
                END,
                ticket.[CreatedAt] ASC,
                ticket.[Id] ASC
              """
            : "ORDER BY ticket.[CreatedAt] DESC, ticket.[Id] DESC";
        parameters.Add("CriticalPriority", (int)TicketPriority.Critical);
        parameters.Add("HighPriority", (int)TicketPriority.High);
        parameters.Add("MediumPriority", (int)TicketPriority.Medium);

        var rows = await db.QueryAsync<TicketSearchRow>(
            $"""
            SELECT
                ticket.[Id],
                ticket.[Title],
                ticket.[Description],
                ticket.[ClientUserId],
                COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(client.[FirstName], ' ', client.[LastName]))), ''), client.[Email]) AS [ClientFullName],
                ticket.[OrderId],
                ticket.[DeliveryCalendarId],
                ticket.[AssignedToUserId],
                COALESCE(NULLIF(LTRIM(RTRIM(CONCAT(assigned.[FirstName], ' ', assigned.[LastName]))), ''), assigned.[Email]) AS [AssignedToFullName],
                ticket.[Status],
                ticket.[Priority],
                ticket.[ClosedAt],
                ticket.[CreatedAt],
                ticket.[UpdatedAt]
            {fromSql}
            {whereSql}
            {orderSql}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            parameters);

        return new TicketSearchResult
        {
            Items = rows.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<TicketDashboardSummary> GetDashboardSummaryAsync(
        DateTimeOffset closedFromInclusive,
        DateTimeOffset closedToExclusive)
    {
        using var db = Factory.CreateConnection();
        const string sql =
            """
            SELECT
                COALESCE(SUM(CASE WHEN [Status] NOT IN @ClosedStatuses THEN 1 ELSE 0 END), 0) AS [WaitingCount],
                COALESCE(SUM(CASE WHEN [Status] NOT IN @ClosedStatuses AND [AssignedToUserId] IS NULL THEN 1 ELSE 0 END), 0) AS [UnassignedCount],
                COALESCE(SUM(CASE WHEN [Status] NOT IN @ClosedStatuses AND [Priority] IN @HighPriorities THEN 1 ELSE 0 END), 0) AS [HighPriorityCount],
                COALESCE(SUM(CASE WHEN [ClosedAt] >= @ClosedFromInclusive AND [ClosedAt] < @ClosedToExclusive THEN 1 ELSE 0 END), 0) AS [ClosedTodayCount]
            FROM [Tickets]
            WHERE [IsDeleted] = 0;
            """;

        return await db.QuerySingleAsync<TicketDashboardSummary>(
            sql,
            new
            {
                ClosedStatuses = new[] { (int)TicketStatus.Resolved, (int)TicketStatus.Closed },
                HighPriorities = new[] { (int)TicketPriority.High, (int)TicketPriority.Critical },
                ClosedFromInclusive = closedFromInclusive,
                ClosedToExclusive = closedToExclusive,
            });
    }
}

