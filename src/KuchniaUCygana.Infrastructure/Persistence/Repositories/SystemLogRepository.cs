using Dapper;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class SystemLogRepository : BaseRepository<SystemLog>, ISystemLogRepository
{
    private const int MaxPageSize = 100;

    private const string ActiveLogSourceSql =
        """
        SELECT
            sl.[Id],
            sl.[UserId],
            COALESCE(
                NULLIF(LTRIM(RTRIM(CONCAT(COALESCE(u.[FirstName], N''), N' ', COALESCE(u.[LastName], N'')))), N''),
                u.[Email]) AS [UserFullName],
            sl.[Action],
            sl.[TargetEntity],
            sl.[TargetId],
            sl.[OldValue],
            sl.[NewValue],
            sl.[Timestamp],
            sl.[IPAddress],
            CAST(0 AS bit) AS [IsArchived]
        FROM [dbo].[SystemLogs] sl
        LEFT JOIN [dbo].[Users] u ON u.[Id] = sl.[UserId]
        """;

    private const string ArchivedLogSourceSql =
        """
        SELECT
            sl.[Id],
            sl.[UserId],
            COALESCE(
                NULLIF(LTRIM(RTRIM(CONCAT(COALESCE(u.[FirstName], N''), N' ', COALESCE(u.[LastName], N'')))), N''),
                u.[Email]) AS [UserFullName],
            sl.[Action],
            sl.[TargetEntity],
            sl.[TargetId],
            sl.[OldValue],
            sl.[NewValue],
            sl.[Timestamp],
            sl.[IPAddress],
            CAST(1 AS bit) AS [IsArchived]
        FROM [dbo].[SystemLogsArchive] sl
        LEFT JOIN [dbo].[Users] u ON u.[Id] = sl.[UserId]
        """;

    public SystemLogRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null)
        : base(factory, currentUserService)
    {
    }

    public async Task<SystemLogSearchResult> SearchAsync(SystemLogSearchQuery query)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 10, MaxPageSize);
        var sourceSql = BuildLogSourceSql(query.IncludeArchived);

        using var db = Factory.CreateConnection();

        var dataParameters = new DynamicParameters();
        var dataWhereSql = BuildWhereSql(query, dataParameters, includeActionEntityFilters: true, includeTextSearch: true);
        dataParameters.Add("pageSize", pageSize);

        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            WITH LogSource AS (
                {sourceSql}
            )
            SELECT COUNT(1)
            FROM LogSource
            WHERE {dataWhereSql}
            OPTION (RECOMPILE);
            """,
            dataParameters);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);
        var offset = (page - 1) * pageSize;
        dataParameters.Add("offset", offset);

        var items = (await db.QueryAsync<SystemLogRow>(
            $"""
            WITH LogSource AS (
                {sourceSql}
            )
            SELECT
                [Id],
                [UserId],
                [UserFullName],
                [Action],
                [TargetEntity],
                [TargetId],
                [OldValue],
                [NewValue],
                [Timestamp],
                [IPAddress],
                [IsArchived]
            FROM LogSource
            WHERE {dataWhereSql}
            ORDER BY [Timestamp] DESC, [Id] DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
            OPTION (RECOMPILE);
            """,
            dataParameters)).ToArray();

        var facetParameters = new DynamicParameters();
        var facetWhereSql = BuildWhereSql(query, facetParameters, includeActionEntityFilters: false, includeTextSearch: false);

        var actions = (await db.QueryAsync<string>(
            $"""
            WITH LogSource AS (
                {sourceSql}
            )
            SELECT DISTINCT [Action]
            FROM LogSource
            WHERE {facetWhereSql}
              AND NULLIF([Action], N'') IS NOT NULL
            ORDER BY [Action]
            OPTION (RECOMPILE);
            """,
            facetParameters)).ToArray();

        var targetEntities = (await db.QueryAsync<string>(
            $"""
            WITH LogSource AS (
                {sourceSql}
            )
            SELECT DISTINCT [TargetEntity]
            FROM LogSource
            WHERE {facetWhereSql}
              AND NULLIF([TargetEntity], N'') IS NOT NULL
            ORDER BY [TargetEntity]
            OPTION (RECOMPILE);
            """,
            facetParameters)).ToArray();

        return new SystemLogSearchResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Actions = actions,
            TargetEntities = targetEntities,
        };
    }

    public async Task<IReadOnlyList<SystemLogRow>> GetLatestAsync(int limit)
    {
        var pageSize = Math.Clamp(limit <= 0 ? 50 : limit, 1, MaxPageSize);

        using var db = Factory.CreateConnection();
        var rows = await db.QueryAsync<SystemLogRow>(
            $"""
            WITH LogSource AS (
                {ActiveLogSourceSql}
            )
            SELECT TOP (@pageSize)
                [Id],
                [UserId],
                [UserFullName],
                [Action],
                [TargetEntity],
                [TargetId],
                [OldValue],
                [NewValue],
                [Timestamp],
                [IPAddress],
                [IsArchived]
            FROM LogSource
            WHERE [Timestamp] >= @from
            ORDER BY [Timestamp] DESC, [Id] DESC
            OPTION (RECOMPILE);
            """,
            new
            {
                pageSize,
                from = DateTimeOffset.UtcNow.AddDays(-30),
            });

        return rows.ToArray();
    }

    public async Task<IReadOnlyList<SystemLogRow>> GetByUserAsync(int userId, int limit)
    {
        var pageSize = Math.Clamp(limit <= 0 ? 100 : limit, 1, MaxPageSize);
        var sourceSql = BuildLogSourceSql(includeArchived: true);

        using var db = Factory.CreateConnection();
        var rows = await db.QueryAsync<SystemLogRow>(
            $"""
            WITH LogSource AS (
                {sourceSql}
            )
            SELECT TOP (@pageSize)
                [Id],
                [UserId],
                [UserFullName],
                [Action],
                [TargetEntity],
                [TargetId],
                [OldValue],
                [NewValue],
                [Timestamp],
                [IPAddress],
                [IsArchived]
            FROM LogSource
            WHERE [UserId] = @userId
            ORDER BY [Timestamp] DESC, [Id] DESC
            OPTION (RECOMPILE);
            """,
            new
            {
                userId,
                pageSize,
            });

        return rows.ToArray();
    }

    public async Task<IReadOnlyList<SystemLogRow>> GetByTargetAsync(string targetEntity, string targetId, int limit)
    {
        var pageSize = Math.Clamp(limit <= 0 ? 100 : limit, 10, MaxPageSize);
        var sourceSql = BuildLogSourceSql(includeArchived: true);

        using var db = Factory.CreateConnection();
        var rows = await db.QueryAsync<SystemLogRow>(
            $"""
            WITH LogSource AS (
                {sourceSql}
            )
            SELECT TOP (@pageSize)
                [Id],
                [UserId],
                [UserFullName],
                [Action],
                [TargetEntity],
                [TargetId],
                [OldValue],
                [NewValue],
                [Timestamp],
                [IPAddress],
                [IsArchived]
            FROM LogSource
            WHERE [TargetEntity] = @targetEntity
              AND [TargetId] = @targetId
            ORDER BY [Timestamp] DESC, [Id] DESC
            OPTION (RECOMPILE);
            """,
            new
            {
                targetEntity = targetEntity.Trim(),
                targetId = targetId.Trim(),
                pageSize,
            });

        return rows.ToArray();
    }

    public async Task<SystemLogRow?> GetRowByIdAsync(int id)
    {
        var sourceSql = BuildLogSourceSql(includeArchived: true);

        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<SystemLogRow>(
            $"""
            WITH LogSource AS (
                {sourceSql}
            )
            SELECT TOP 1
                [Id],
                [UserId],
                [UserFullName],
                [Action],
                [TargetEntity],
                [TargetId],
                [OldValue],
                [NewValue],
                [Timestamp],
                [IPAddress],
                [IsArchived]
            FROM LogSource
            WHERE [Id] = @id
            ORDER BY [IsArchived] ASC;
            """,
            new { id });
    }

    private static string BuildLogSourceSql(bool includeArchived)
        => includeArchived
            ? $"{ActiveLogSourceSql}\nUNION ALL\n{ArchivedLogSourceSql}"
            : ActiveLogSourceSql;

    private static string BuildWhereSql(
        SystemLogSearchQuery query,
        DynamicParameters parameters,
        bool includeActionEntityFilters,
        bool includeTextSearch)
    {
        var where = new List<string>();

        if (query.From.HasValue)
        {
            where.Add("[Timestamp] >= @from");
            parameters.Add("from", query.From.Value);
        }

        if (query.ToExclusive.HasValue)
        {
            where.Add("[Timestamp] < @toExclusive");
            parameters.Add("toExclusive", query.ToExclusive.Value);
        }

        if (query.UserId is > 0)
        {
            where.Add("[UserId] = @userId");
            parameters.Add("userId", query.UserId.Value);
        }

        if (includeActionEntityFilters && !string.IsNullOrWhiteSpace(query.Action))
        {
            where.Add("[Action] = @action");
            parameters.Add("action", query.Action.Trim());
        }

        if (includeActionEntityFilters && !string.IsNullOrWhiteSpace(query.TargetEntity))
        {
            where.Add("[TargetEntity] = @targetEntity");
            parameters.Add("targetEntity", query.TargetEntity.Trim());
        }

        if (includeTextSearch && !string.IsNullOrWhiteSpace(query.Search))
        {
            var search = $"%{query.Search.Trim()}%";
            where.Add(
                """
                ([Action] LIKE @search
                 OR [TargetEntity] LIKE @search
                 OR [TargetId] LIKE @search
                 OR [UserFullName] LIKE @search
                 OR [IPAddress] LIKE @search
                 OR [OldValue] LIKE @search
                 OR [NewValue] LIKE @search)
                """);
            parameters.Add("search", search);
        }

        return where.Count == 0 ? "1 = 1" : string.Join(" AND ", where);
    }
}
