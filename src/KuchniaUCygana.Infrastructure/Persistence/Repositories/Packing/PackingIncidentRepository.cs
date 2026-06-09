using Dapper;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;

public sealed class PackingIncidentRepository : BaseRepository<PackingIncident>, IPackingIncidentRepository
{
    private const int MaxPageSize = 100;

    public PackingIncidentRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IReadOnlyList<PackingIncident>> SearchAsync(
        DateOnly? date,
        PackingIncidentStatus? status,
        PackingIncidentType? type,
        string? clientPublicId,
        int? deliveryCalendarId,
        string? search = null)
    {
        using var db = Factory.CreateConnection();
        var parameters = BuildSearchParameters(date, status, type, clientPublicId, deliveryCalendarId, search);
        var where = BuildSearchWhere(parameters);
        var rows = await db.QueryAsync<PackingIncident>(
            $"""
            SELECT *
            FROM [PackingIncidents]
            {where}
            ORDER BY
                CASE WHEN [Status] = @Resolved THEN 1 ELSE 0 END ASC,
                [ReportedAt] DESC,
                [Id] DESC;
            """,
            parameters);

        return rows.ToList();
    }

    public async Task<PackingIncidentSearchResult> SearchPageAsync(PackingIncidentSearchQuery query)
    {
        using var db = Factory.CreateConnection();
        var parameters = BuildSearchParameters(
            query.Date,
            query.Status,
            query.Type,
            query.ClientPublicId,
            query.DeliveryCalendarId,
            query.Search);
        var where = BuildSearchWhere(parameters);
        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(1)
            FROM [PackingIncidents]
            {where};
            """,
            parameters);
        var (page, pageSize) = NormalizePage(query.Page, query.PageSize, totalCount);
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var rows = await db.QueryAsync<PackingIncident>(
            $"""
            SELECT *
            FROM [PackingIncidents]
            {where}
            ORDER BY
                CASE WHEN [Status] = @Resolved THEN 1 ELSE 0 END ASC,
                [ReportedAt] DESC,
                [Id] DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            parameters);

        return new PackingIncidentSearchResult
        {
            Items = rows.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<IReadOnlyList<PackingIncident>> SearchByDeliveryCalendarIdsAsync(IEnumerable<int> deliveryCalendarIds)
    {
        using var db = Factory.CreateConnection();
        var ids = deliveryCalendarIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<PackingIncident>();
        }

        var rows = await db.QueryAsync<PackingIncident>(
            """
            SELECT *
            FROM [PackingIncidents]
            WHERE [IsDeleted] = 0
              AND [DeliveryCalendarId] IN @DeliveryCalendarIds
            ORDER BY
                CASE WHEN [Status] = @resolved THEN 1 ELSE 0 END ASC,
                [ReportedAt] DESC,
                [Id] DESC;
            """,
            new
            {
                DeliveryCalendarIds = ids,
                resolved = (int)PackingIncidentStatus.Resolved,
            });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<PackingIncident>> GetKitchenReworkAsync(DateOnly? date)
    {
        using var db = Factory.CreateConnection();
        var rows = await db.QueryAsync<PackingIncident>(
            """
            SELECT *
            FROM [PackingIncidents]
            WHERE [IsDeleted] = 0
              AND [ReplacementPackingItemId] IS NOT NULL
              AND [Status] <> @resolved
              AND (@date IS NULL OR [PackingDate] = @date)
            ORDER BY
                CASE WHEN [KitchenPreparedAt] IS NULL THEN 0 ELSE 1 END ASC,
                [ReportedAt] ASC,
                [Id] ASC;
            """,
            new
            {
                date,
                resolved = (int)PackingIncidentStatus.Resolved,
            });

        return rows.ToList();
    }

    private static DynamicParameters BuildSearchParameters(
        DateOnly? date,
        PackingIncidentStatus? status,
        PackingIncidentType? type,
        string? clientPublicId,
        int? deliveryCalendarId,
        string? search)
    {
        var parameters = new DynamicParameters();
        parameters.Add("Date", date);
        parameters.Add("Status", status.HasValue ? (int?)status.Value : null);
        parameters.Add("Type", type.HasValue ? (int?)type.Value : null);
        parameters.Add("ClientPublicId", string.IsNullOrWhiteSpace(clientPublicId) ? null : clientPublicId.Trim());
        parameters.Add("DeliveryCalendarId", deliveryCalendarId);
        parameters.Add("Search", string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%");
        parameters.Add("Resolved", (int)PackingIncidentStatus.Resolved);
        return parameters;
    }

    private static string BuildSearchWhere(DynamicParameters parameters)
    {
        var where = new List<string> { "[IsDeleted] = 0" };
        if (parameters.Get<DateOnly?>("Date").HasValue)
        {
            where.Add("[PackingDate] = @Date");
        }

        if (parameters.Get<int?>("Status").HasValue)
        {
            where.Add("[Status] = @Status");
        }

        if (parameters.Get<int?>("Type").HasValue)
        {
            where.Add("[Type] = @Type");
        }

        if (!string.IsNullOrWhiteSpace(parameters.Get<string?>("ClientPublicId")))
        {
            where.Add("[ClientPublicId] LIKE '%' + @ClientPublicId + '%'");
        }

        if (parameters.Get<int?>("DeliveryCalendarId").HasValue)
        {
            where.Add("[DeliveryCalendarId] = @DeliveryCalendarId");
        }

        if (!string.IsNullOrWhiteSpace(parameters.Get<string?>("Search")))
        {
            where.Add("""
                ([ClientPublicId] LIKE @Search
                 OR [MealName] LIKE @Search
                 OR [BoxCode] LIKE @Search
                 OR [BagCode] LIKE @Search
                 OR [Description] LIKE @Search
                 OR [AdminNotes] LIKE @Search
                 OR [WarehouseWasteError] LIKE @Search
                 OR CONVERT(varchar(20), [Id]) LIKE @Search
                 OR CONVERT(varchar(20), [DeliveryCalendarId]) LIKE @Search
                 OR CONVERT(varchar(20), [PackingSessionId]) LIKE @Search
                 OR CONVERT(varchar(20), [ReasonFlags]) LIKE @Search)
                """);
        }

        return "WHERE " + string.Join(" AND ", where);
    }

    private static (int Page, int PageSize) NormalizePage(int page, int pageSize, int totalCount)
    {
        var safePageSize = Math.Clamp(pageSize <= 0 ? 10 : pageSize, 5, MaxPageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)safePageSize));
        var safePage = Math.Clamp(page <= 0 ? 1 : page, 1, totalPages);
        return (safePage, safePageSize);
    }
}


