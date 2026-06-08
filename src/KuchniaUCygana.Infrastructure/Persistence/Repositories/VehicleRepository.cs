using System.Data;
using Dapper;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repozytorium pojazdĂłw
/// </summary>
public sealed class VehicleRepository : BaseRepository<Vehicle>, IVehicleRepository
{
    private const int MaxPageSize = 100;

    private readonly IDbConnectionFactory _connectionFactory;

    public VehicleRepository(IDbConnectionFactory connectionFactory, ICurrentUserService? currentUserService = null) : base(connectionFactory, currentUserService)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Vehicle>> GetByIdsAsync(IEnumerable<int> ids)
    {
        using var db = _connectionFactory.CreateConnection();
        var idList = ids
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (idList.Length == 0)
        {
            return Array.Empty<Vehicle>();
        }

        var vehicles = await db.QueryAsync<Vehicle>(
            "SELECT * FROM [Vehicles] WHERE [Id] IN @Ids AND [IsDeleted] = 0 ORDER BY [Id];",
            new { Ids = idList });
        return vehicles.ToList();
    }

    public async Task<Vehicle?> GetByRegistrationNumberAsync(string registrationNumber)
    {
        using var db = _connectionFactory.CreateConnection();
        var vehicle = await db.QuerySingleOrDefaultAsync<Vehicle>(
            "SELECT * FROM [Vehicles] WHERE [RegistrationNumber] = @registrationNumber AND [IsDeleted] = 0;",
            new { registrationNumber });
        return vehicle;
    }

    public async Task<VehicleSearchResult> SearchAsync(VehicleSearchQuery query)
    {
        using var db = _connectionFactory.CreateConnection();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 10 : query.PageSize, 5, MaxPageSize);
        var searchWhere = new List<string> { "[IsDeleted] = 0" };
        var pageWhere = new List<string> { "[IsDeleted] = 0" };
        var searchParameters = new DynamicParameters();
        var pageParameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            const string searchSql = """
                ([RegistrationNumber] LIKE @Search
                 OR [Model] LIKE @Search
                 OR CONVERT(varchar(20), [Id]) LIKE @Search)
                """;
            var search = $"%{query.Search.Trim()}%";
            searchWhere.Add(searchSql);
            pageWhere.Add(searchSql);
            searchParameters.Add("Search", search);
            pageParameters.Add("Search", search);
        }

        if (query.Status.HasValue)
        {
            pageWhere.Add("[Status] = @Status");
            pageParameters.Add("Status", (int)query.Status.Value);
        }

        var searchWhereSql = "WHERE " + string.Join(" AND ", searchWhere);
        var pageWhereSql = "WHERE " + string.Join(" AND ", pageWhere);
        searchParameters.Add("ActiveStatus", (int)VehicleStatus.Active);
        searchParameters.Add("MaintenanceStatus", (int)VehicleStatus.Maintenance);

        var summary = await db.QuerySingleAsync<VehicleFleetSummaryRow>(
            $"""
            SELECT
                COUNT(1) AS [TotalFleetCount],
                COALESCE(SUM(CASE WHEN [Status] = @ActiveStatus THEN 1 ELSE 0 END), 0) AS [ActiveCount],
                COALESCE(SUM(CASE WHEN [Status] = @MaintenanceStatus THEN 1 ELSE 0 END), 0) AS [MaintenanceCount],
                COALESCE(SUM(CASE WHEN [Status] = @ActiveStatus THEN [MaxLoadKg] ELSE 0 END), 0) AS [ActiveCapacityKg]
            FROM [Vehicles]
            {searchWhereSql};
            """,
            searchParameters);

        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(1)
            FROM [Vehicles]
            {pageWhereSql};
            """,
            pageParameters);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);
        pageParameters.Add("Offset", (page - 1) * pageSize);
        pageParameters.Add("PageSize", pageSize);

        var vehicles = await db.QueryAsync<Vehicle>(
            $"""
            SELECT [Id], [RegistrationNumber], [Model], [MaxLoadKg], [Status], [CreatedAt], [UpdatedAt]
            FROM [Vehicles]
            {pageWhereSql}
            ORDER BY [RegistrationNumber], [Id]
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            pageParameters);

        return new VehicleSearchResult
        {
            Items = vehicles.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalFleetCount = summary.TotalFleetCount,
            ActiveCount = summary.ActiveCount,
            MaintenanceCount = summary.MaintenanceCount,
            ActiveCapacityKg = summary.ActiveCapacityKg,
        };
    }

    public async Task<IReadOnlyList<Vehicle>> GetByIdsAsync(IReadOnlyCollection<int> vehicleIds)
    {
        var ids = vehicleIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<Vehicle>();
        }

        using var db = _connectionFactory.CreateConnection();
        var vehicles = await db.QueryAsync<Vehicle>(
            """
            SELECT *
            FROM [Vehicles]
            WHERE [Id] IN @Ids
              AND [IsDeleted] = 0
            ORDER BY [RegistrationNumber], [Id];
            """,
            new { Ids = ids });

        return vehicles.ToList();
    }

    private sealed class VehicleFleetSummaryRow
    {
        public int TotalFleetCount { get; set; }

        public int ActiveCount { get; set; }

        public int MaintenanceCount { get; set; }

        public decimal ActiveCapacityKg { get; set; }
    }
}


