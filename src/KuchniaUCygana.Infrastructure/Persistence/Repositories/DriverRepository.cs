using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repozytorium kierowcĂłw
/// </summary>
public sealed class DriverRepository : BaseRepository<Driver>, IDriverRepository
{
    private const int MaxPageSize = 100;

    public DriverRepository(IDbConnectionFactory connectionFactory, ICurrentUserService? currentUserService = null) : base(connectionFactory, currentUserService)
    {
    }

    public async Task<IReadOnlyList<Driver>> GetByIdsAsync(IEnumerable<int> ids)
    {
        using var db = Factory.CreateConnection();
        var idList = ids
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (idList.Length == 0)
        {
            return Array.Empty<Driver>();
        }

        var drivers = await db.QueryAsync<Driver>(
            "SELECT * FROM [Drivers] WHERE [Id] IN @Ids AND [IsDeleted] = 0 ORDER BY [Id];",
            new { Ids = idList });
        return drivers.ToList();
    }

    public async Task<DriverSearchResult> SearchAsync(DriverSearchQuery query)
    {
        using var db = Factory.CreateConnection();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 10 : query.PageSize, 5, MaxPageSize);
        var searchWhere = new List<string> { "d.[IsDeleted] = 0" };
        var pageWhere = new List<string> { "d.[IsDeleted] = 0" };
        var searchParameters = new DynamicParameters();
        var pageParameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            const string searchSql = """
                (u.[Email] LIKE @Search
                 OR u.[FirstName] LIKE @Search
                 OR u.[LastName] LIKE @Search
                 OR CONCAT(u.[FirstName], ' ', u.[LastName]) LIKE @Search
                 OR d.[LicenseNumber] LIKE @Search
                 OR v.[RegistrationNumber] LIKE @Search
                 OR v.[Model] LIKE @Search
                 OR CONVERT(varchar(20), d.[Id]) LIKE @Search)
                """;
            var search = $"%{query.Search.Trim()}%";
            searchWhere.Add(searchSql);
            pageWhere.Add(searchSql);
            searchParameters.Add("Search", search);
            pageParameters.Add("Search", search);
        }

        if (query.IsActive.HasValue)
        {
            pageWhere.Add("d.[IsActive] = @IsActive");
            pageParameters.Add("IsActive", query.IsActive.Value);
        }

        if (query.HasVehicleAssignment.HasValue)
        {
            pageWhere.Add(query.HasVehicleAssignment.Value
                ? "assignment.[VehicleId] IS NOT NULL"
                : "assignment.[VehicleId] IS NULL");
        }

        var searchWhereSql = "WHERE " + string.Join(" AND ", searchWhere);
        var pageWhereSql = "WHERE " + string.Join(" AND ", pageWhere);
        var fromSql = """
            FROM [Drivers] d
            INNER JOIN [Users] u ON u.[Id] = d.[UserId]
            OUTER APPLY
            (
                SELECT TOP 1 a.[VehicleId]
                FROM [DriverVehicleAssignments] a
                WHERE a.[DriverId] = d.[Id]
                  AND a.[UnassignedAt] IS NULL
                ORDER BY a.[AssignedAt] DESC, a.[Id] DESC
            ) assignment
            LEFT JOIN [Vehicles] v ON v.[Id] = assignment.[VehicleId]
                AND v.[IsDeleted] = 0
            """;

        var summary = await db.QuerySingleAsync<DriverFleetSummaryRow>(
            $"""
            SELECT
                COUNT(1) AS [TotalDriversCount],
                COALESCE(SUM(CASE WHEN d.[IsActive] = 1 THEN 1 ELSE 0 END), 0) AS [ActiveCount],
                COALESCE(SUM(CASE WHEN d.[IsActive] = 0 THEN 1 ELSE 0 END), 0) AS [InactiveCount],
                COALESCE(SUM(CASE WHEN assignment.[VehicleId] IS NOT NULL THEN 1 ELSE 0 END), 0) AS [WithVehicleCount]
            {fromSql}
            {searchWhereSql};
            """,
            searchParameters);

        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(1)
            {fromSql}
            {pageWhereSql};
            """,
            pageParameters);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);
        pageParameters.Add("Offset", (page - 1) * pageSize);
        pageParameters.Add("PageSize", pageSize);

        var rows = await db.QueryAsync<DriverListRow>(
            $"""
            SELECT
                d.[Id],
                d.[UserId],
                d.[LicenseNumber],
                d.[IsActive],
                u.[FirstName],
                u.[LastName],
                u.[Email],
                assignment.[VehicleId] AS [CurrentVehicleId],
                v.[RegistrationNumber] AS [CurrentVehicleRegistration],
                v.[Model] AS [CurrentVehicleModel]
            {fromSql}
            {pageWhereSql}
            ORDER BY u.[LastName], u.[FirstName], u.[Email], d.[Id]
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            pageParameters);

        return new DriverSearchResult
        {
            Items = rows.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalDriversCount = summary.TotalDriversCount,
            ActiveCount = summary.ActiveCount,
            WithVehicleCount = summary.WithVehicleCount,
            InactiveCount = summary.InactiveCount,
        };
    }

    // Dodatkowe metody specyficzne dla kierowcĂłw, np.:
    public async Task<Driver?> GetByUserIdAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<Driver>(
            "SELECT * FROM [Drivers] WHERE [UserId] = @userId AND [IsDeleted] = 0;",
            new { userId });
    }

    public async Task<Driver?> GetByLicenseNumberAsync(string licenseNumber)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<Driver>(
            "SELECT * FROM [Drivers] WHERE [LicenseNumber] = @licenseNumber AND [IsDeleted] = 0;",
            new { licenseNumber });
    }

    public async Task<IReadOnlyList<Driver>> GetByIdsAsync(IReadOnlyCollection<int> driverIds)
    {
        var ids = driverIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<Driver>();
        }

        using var db = Factory.CreateConnection();
        var drivers = await db.QueryAsync<Driver>(
            """
            SELECT *
            FROM [Drivers]
            WHERE [Id] IN @Ids
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { Ids = ids });

        return drivers.ToList();
    }

    private sealed class DriverFleetSummaryRow
    {
        public int TotalDriversCount { get; set; }

        public int ActiveCount { get; set; }

        public int WithVehicleCount { get; set; }

        public int InactiveCount { get; set; }
    }
}


