using Dapper;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class HumanResourcesReadRepository : IHumanResourcesReadRepository
{
    private const int MaxPageSize = 100;

    private readonly IDbConnectionFactory factory;

    public HumanResourcesReadRepository(IDbConnectionFactory factory)
    {
        this.factory = factory;
    }

    public async Task<HumanResourcesSummaryRow> GetSummaryAsync(DateOnly today)
    {
        using var db = factory.CreateConnection();
        return await db.QuerySingleAsync<HumanResourcesSummaryRow>(
            """
            SELECT
                COALESCE((SELECT COUNT(1) FROM [LeaveRequests] WHERE [IsDeleted] = 0 AND [Status] = 0), 0) AS [PendingLeaveCount],
                COALESCE((SELECT COUNT(1) FROM [Employees] WHERE [IsDeleted] = 0 AND [IsActive] = 1), 0) AS [ActiveEmployeeCount],
                COALESCE((SELECT COUNT(1) FROM [Employees] WHERE [IsDeleted] = 0 AND [IsActive] = 0), 0) AS [InactiveEmployeeCount],
                COALESCE((SELECT COUNT(1) FROM [WorkSchedules] WHERE [IsDeleted] = 0 AND [ShiftDate] = @Today), 0) AS [TodayScheduleCount];
            """,
            new { Today = today });
    }

    public async Task<IReadOnlyList<DepartmentStaffSummaryRow>> GetDepartmentStaffSummariesAsync(string? search = null)
    {
        using var db = factory.CreateConnection();
        var parameters = new DynamicParameters();
        var where = BuildDepartmentWhere(search, parameters);
        var rows = await db.QueryAsync<DepartmentStaffSummaryRow>(
            $"""
            SELECT
                department.[Id] AS [DepartmentId],
                department.[Name] AS [DepartmentName],
                COALESCE(stats.[ActiveEmployeeCount], 0) AS [ActiveEmployeeCount],
                COALESCE(stats.[TotalEmployeeCount], 0) AS [TotalEmployeeCount],
                NULLIF(LTRIM(RTRIM(CONCAT(head.[FirstName], ' ', head.[LastName]))), '') AS [HeadEmployeeFullName],
                COALESCE(samples.[SampleEmployeeNames], '') AS [SampleEmployeeNames]
            FROM [Departments] department
            LEFT JOIN [Employees] head ON head.[Id] = department.[HeadEmployeeId] AND head.[IsDeleted] = 0
            OUTER APPLY
            (
                SELECT
                    SUM(CASE WHEN employee.[IsActive] = 1 THEN 1 ELSE 0 END) AS [ActiveEmployeeCount],
                    COUNT(1) AS [TotalEmployeeCount]
                FROM [Employees] employee
                WHERE employee.[DepartmentId] = department.[Id]
                  AND employee.[IsDeleted] = 0
            ) stats
            OUTER APPLY
            (
                SELECT STRING_AGG(sample.[FullName], ', ') WITHIN GROUP (ORDER BY sample.[FullName]) AS [SampleEmployeeNames]
                FROM
                (
                    SELECT TOP (4) NULLIF(LTRIM(RTRIM(CONCAT(employee.[FirstName], ' ', employee.[LastName]))), '') AS [FullName]
                    FROM [Employees] employee
                    WHERE employee.[DepartmentId] = department.[Id]
                      AND employee.[IsDeleted] = 0
                    ORDER BY employee.[LastName], employee.[FirstName], employee.[Id]
                ) sample
                WHERE sample.[FullName] IS NOT NULL
            ) samples
            {where}
            ORDER BY department.[Name], department.[Id];
            """,
            parameters);

        return rows.ToList();
    }

    public async Task<DepartmentSearchResult> SearchDepartmentsAsync(DepartmentSearchQuery query)
    {
        using var db = factory.CreateConnection();
        var parameters = new DynamicParameters();
        var where = BuildDepartmentWhere(query.Search, parameters);
        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(1)
            FROM [Departments] department
            LEFT JOIN [Employees] head ON head.[Id] = department.[HeadEmployeeId] AND head.[IsDeleted] = 0
            {where};
            """,
            parameters);
        var (page, pageSize) = NormalizePage(query.Page, query.PageSize, totalCount);
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var rows = await db.QueryAsync<DepartmentSearchRow>(
            $"""
            SELECT
                department.[Id],
                department.[Name],
                department.[Description],
                department.[HeadEmployeeId],
                NULLIF(LTRIM(RTRIM(CONCAT(head.[FirstName], ' ', head.[LastName]))), '') AS [HeadEmployeeFullName],
                COALESCE(stats.[EmployeeCount], 0) AS [EmployeeCount],
                COALESCE(samples.[SampleEmployeeNames], '') AS [SampleEmployeeNames]
            FROM [Departments] department
            LEFT JOIN [Employees] head ON head.[Id] = department.[HeadEmployeeId] AND head.[IsDeleted] = 0
            OUTER APPLY
            (
                SELECT COUNT(1) AS [EmployeeCount]
                FROM [Employees] employee
                WHERE employee.[DepartmentId] = department.[Id]
                  AND employee.[IsDeleted] = 0
            ) stats
            OUTER APPLY
            (
                SELECT STRING_AGG(sample.[FullName], ', ') WITHIN GROUP (ORDER BY sample.[FullName]) AS [SampleEmployeeNames]
                FROM
                (
                    SELECT TOP (3) NULLIF(LTRIM(RTRIM(CONCAT(employee.[FirstName], ' ', employee.[LastName]))), '') AS [FullName]
                    FROM [Employees] employee
                    WHERE employee.[DepartmentId] = department.[Id]
                      AND employee.[IsDeleted] = 0
                    ORDER BY employee.[LastName], employee.[FirstName], employee.[Id]
                ) sample
                WHERE sample.[FullName] IS NOT NULL
            ) samples
            {where}
            ORDER BY department.[Name], department.[Id]
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            parameters);

        return new DepartmentSearchResult
        {
            Items = rows.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<EmployeeSearchResult> SearchEmployeesAsync(EmployeeSearchQuery query)
    {
        using var db = factory.CreateConnection();
        var parameters = new DynamicParameters();
        var where = BuildEmployeeWhere(query, parameters);
        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(1)
            FROM [Employees] employee
            LEFT JOIN [Departments] department ON department.[Id] = employee.[DepartmentId]
            LEFT JOIN [Users] account ON account.[Id] = employee.[UserId]
            {where};
            """,
            parameters);
        var (page, pageSize) = NormalizePage(query.Page, query.PageSize, totalCount);
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var rows = await db.QueryAsync<EmployeeSearchRow>(
            $"""
            SELECT
                employee.[Id],
                employee.[UserId],
                employee.[FirstName],
                employee.[LastName],
                employee.[Email],
                employee.[PhoneNumber],
                employee.[HireDate],
                employee.[TerminationDate],
                employee.[DepartmentId],
                department.[Name] AS [DepartmentName],
                employee.[Position],
                employee.[IsActive],
                account.[Role] AS [UserRole],
                employee.[CreatedAt],
                employee.[UpdatedAt]
            FROM [Employees] employee
            LEFT JOIN [Departments] department ON department.[Id] = employee.[DepartmentId]
            LEFT JOIN [Users] account ON account.[Id] = employee.[UserId]
            {where}
            ORDER BY employee.[LastName], employee.[FirstName], employee.[Id]
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            parameters);

        return new EmployeeSearchResult
        {
            Items = rows.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<EmployeeSearchRow?> GetEmployeeByUserIdAsync(int userId)
    {
        using var db = factory.CreateConnection();
        return await db.QueryFirstOrDefaultAsync<EmployeeSearchRow>(
            """
            SELECT TOP (1)
                employee.[Id],
                employee.[UserId],
                employee.[FirstName],
                employee.[LastName],
                employee.[Email],
                employee.[PhoneNumber],
                employee.[HireDate],
                employee.[TerminationDate],
                employee.[DepartmentId],
                department.[Name] AS [DepartmentName],
                employee.[Position],
                employee.[IsActive],
                account.[Role] AS [UserRole],
                employee.[CreatedAt],
                employee.[UpdatedAt]
            FROM [Employees] employee
            LEFT JOIN [Departments] department ON department.[Id] = employee.[DepartmentId]
            LEFT JOIN [Users] account ON account.[Id] = employee.[UserId]
            WHERE employee.[IsDeleted] = 0
              AND employee.[UserId] = @UserId
            ORDER BY employee.[IsActive] DESC, employee.[Id] DESC;
            """,
            new { UserId = userId });
    }

    public async Task<IReadOnlyList<EmployeeOptionRow>> GetEmployeeOptionsAsync(bool activeOnly, int limit)
    {
        using var db = factory.CreateConnection();
        var safeLimit = Math.Clamp(limit <= 0 ? 100 : limit, 1, 500);
        var rows = await db.QueryAsync<EmployeeOptionRow>(
            $"""
            SELECT TOP (@Limit)
                employee.[Id],
                employee.[UserId],
                NULLIF(LTRIM(RTRIM(CONCAT(employee.[FirstName], ' ', employee.[LastName]))), '') AS [FullName],
                employee.[IsActive]
            FROM [Employees] employee
            WHERE employee.[IsDeleted] = 0
              AND (@ActiveOnly = 0 OR employee.[IsActive] = 1)
            ORDER BY employee.[LastName], employee.[FirstName], employee.[Id];
            """,
            new { Limit = safeLimit, ActiveOnly = activeOnly });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<EmployeeOptionRow>> GetEmployeeOptionsByIdsAsync(IEnumerable<int> employeeIds)
    {
        using var db = factory.CreateConnection();
        var ids = employeeIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<EmployeeOptionRow>();
        }

        var rows = await db.QueryAsync<EmployeeOptionRow>(
            """
            SELECT
                employee.[Id],
                employee.[UserId],
                NULLIF(LTRIM(RTRIM(CONCAT(employee.[FirstName], ' ', employee.[LastName]))), '') AS [FullName],
                employee.[IsActive]
            FROM [Employees] employee
            WHERE employee.[IsDeleted] = 0
              AND employee.[Id] IN @EmployeeIds
            ORDER BY employee.[LastName], employee.[FirstName], employee.[Id];
            """,
            new { EmployeeIds = ids });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<EmployeeOptionRow>> GetEmployeeOptionsByUserIdsAsync(IEnumerable<int> userIds)
    {
        using var db = factory.CreateConnection();
        var ids = userIds
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<EmployeeOptionRow>();
        }

        var rows = await db.QueryAsync<EmployeeOptionRow>(
            """
            SELECT
                employee.[Id],
                employee.[UserId],
                NULLIF(LTRIM(RTRIM(CONCAT(employee.[FirstName], ' ', employee.[LastName]))), '') AS [FullName],
                employee.[IsActive]
            FROM [Employees] employee
            WHERE employee.[IsDeleted] = 0
              AND employee.[UserId] IN @UserIds
            ORDER BY employee.[LastName], employee.[FirstName], employee.[Id];
            """,
            new { UserIds = ids });

        return rows.ToList();
    }

    public async Task<LeaveRequestSearchResult> SearchLeaveRequestsAsync(LeaveRequestSearchQuery query)
    {
        using var db = factory.CreateConnection();
        var parameters = new DynamicParameters();
        var where = BuildLeaveWhere(query, parameters);
        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(1)
            FROM [LeaveRequests] leaveRequest
            LEFT JOIN [Employees] employee ON employee.[Id] = leaveRequest.[EmployeeId] AND employee.[IsDeleted] = 0
            LEFT JOIN [Employees] approver ON approver.[Id] = leaveRequest.[ApprovedByEmployeeId] AND approver.[IsDeleted] = 0
            {where};
            """,
            parameters);
        var (page, pageSize) = NormalizePage(query.Page, query.PageSize, totalCount);
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var rows = await db.QueryAsync<LeaveRequestSearchRow>(
            $"""
            SELECT
                leaveRequest.[Id],
                leaveRequest.[EmployeeId],
                NULLIF(LTRIM(RTRIM(CONCAT(employee.[FirstName], ' ', employee.[LastName]))), '') AS [EmployeeFullName],
                leaveRequest.[LeaveType],
                leaveRequest.[StartDate],
                leaveRequest.[EndDate],
                leaveRequest.[Status],
                leaveRequest.[ApprovedByEmployeeId],
                NULLIF(LTRIM(RTRIM(CONCAT(approver.[FirstName], ' ', approver.[LastName]))), '') AS [ApprovedByEmployeeFullName],
                leaveRequest.[RejectionReason],
                leaveRequest.[CreatedAt],
                leaveRequest.[UpdatedAt]
            FROM [LeaveRequests] leaveRequest
            LEFT JOIN [Employees] employee ON employee.[Id] = leaveRequest.[EmployeeId] AND employee.[IsDeleted] = 0
            LEFT JOIN [Employees] approver ON approver.[Id] = leaveRequest.[ApprovedByEmployeeId] AND approver.[IsDeleted] = 0
            {where}
            ORDER BY leaveRequest.[CreatedAt] DESC, leaveRequest.[Id] DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            parameters);

        return new LeaveRequestSearchResult
        {
            Items = rows.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<IReadOnlyList<LeaveRequestSearchRow>> GetLeaveRequestsByEmployeeAsync(int employeeId)
    {
        using var db = factory.CreateConnection();
        var rows = await db.QueryAsync<LeaveRequestSearchRow>(
            """
            SELECT
                leaveRequest.[Id],
                leaveRequest.[EmployeeId],
                NULLIF(LTRIM(RTRIM(CONCAT(employee.[FirstName], ' ', employee.[LastName]))), '') AS [EmployeeFullName],
                leaveRequest.[LeaveType],
                leaveRequest.[StartDate],
                leaveRequest.[EndDate],
                leaveRequest.[Status],
                leaveRequest.[ApprovedByEmployeeId],
                NULLIF(LTRIM(RTRIM(CONCAT(approver.[FirstName], ' ', approver.[LastName]))), '') AS [ApprovedByEmployeeFullName],
                leaveRequest.[RejectionReason],
                leaveRequest.[CreatedAt],
                leaveRequest.[UpdatedAt]
            FROM [LeaveRequests] leaveRequest
            LEFT JOIN [Employees] employee ON employee.[Id] = leaveRequest.[EmployeeId] AND employee.[IsDeleted] = 0
            LEFT JOIN [Employees] approver ON approver.[Id] = leaveRequest.[ApprovedByEmployeeId] AND approver.[IsDeleted] = 0
            WHERE leaveRequest.[IsDeleted] = 0
              AND leaveRequest.[EmployeeId] = @EmployeeId
            ORDER BY leaveRequest.[CreatedAt] DESC, leaveRequest.[Id] DESC;
            """,
            new { EmployeeId = employeeId });

        return rows.ToList();
    }

    public async Task<WorkScheduleSearchResult> SearchWorkSchedulesAsync(WorkScheduleSearchQuery query)
    {
        using var db = factory.CreateConnection();
        var parameters = new DynamicParameters();
        var where = BuildScheduleWhere(query, parameters);
        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(1)
            FROM [WorkSchedules] schedule
            LEFT JOIN [Users] account ON account.[Id] = schedule.[UserId]
            LEFT JOIN [Employees] employee ON employee.[UserId] = schedule.[UserId] AND employee.[IsDeleted] = 0
            {where};
            """,
            parameters);
        var (page, pageSize) = NormalizePage(query.Page, query.PageSize, totalCount);
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var rows = await db.QueryAsync<WorkScheduleSearchRow>(
            $"""
            SELECT
                schedule.[Id],
                schedule.[UserId],
                COALESCE(
                    NULLIF(LTRIM(RTRIM(CONCAT(employee.[FirstName], ' ', employee.[LastName]))), ''),
                    NULLIF(LTRIM(RTRIM(CONCAT(account.[FirstName], ' ', account.[LastName]))), ''),
                    account.[Email]) AS [EmployeeFullName],
                account.[Role] AS [UserRole],
                schedule.[ShiftDate],
                schedule.[Shift],
                schedule.[RoleAtShift],
                schedule.[CreatedAt],
                schedule.[UpdatedAt]
            FROM [WorkSchedules] schedule
            LEFT JOIN [Users] account ON account.[Id] = schedule.[UserId]
            LEFT JOIN [Employees] employee ON employee.[UserId] = schedule.[UserId] AND employee.[IsDeleted] = 0
            {where}
            ORDER BY schedule.[ShiftDate], schedule.[Shift], schedule.[Id]
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            parameters);

        return new WorkScheduleSearchResult
        {
            Items = rows.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    private static string BuildDepartmentWhere(string? search, DynamicParameters parameters)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return string.Empty;
        }

        parameters.Add("Search", $"%{search.Trim()}%");
        return """
            WHERE department.[Name] LIKE @Search
               OR department.[Description] LIKE @Search
               OR CONCAT(head.[FirstName], ' ', head.[LastName]) LIKE @Search
               OR EXISTS
               (
                   SELECT 1
                   FROM [Employees] employee
                   WHERE employee.[DepartmentId] = department.[Id]
                     AND employee.[IsDeleted] = 0
                     AND CONCAT(employee.[FirstName], ' ', employee.[LastName]) LIKE @Search
               )
            """;
    }

    private static string BuildEmployeeWhere(EmployeeSearchQuery query, DynamicParameters parameters)
    {
        var where = new List<string> { "employee.[IsDeleted] = 0" };

        if (query.DepartmentId is > 0)
        {
            where.Add("employee.[DepartmentId] = @DepartmentId");
            parameters.Add("DepartmentId", query.DepartmentId.Value);
        }

        if (string.Equals(query.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            where.Add("employee.[IsActive] = 1");
        }
        else if (string.Equals(query.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            where.Add("employee.[IsActive] = 0");
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            where.Add("account.[Role] = @Role");
            parameters.Add("Role", query.Role.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("""
                (employee.[FirstName] LIKE @Search
                 OR employee.[LastName] LIKE @Search
                 OR CONCAT(employee.[FirstName], ' ', employee.[LastName]) LIKE @Search
                 OR employee.[Email] LIKE @Search
                 OR employee.[PhoneNumber] LIKE @Search
                 OR employee.[Position] LIKE @Search
                 OR department.[Name] LIKE @Search
                 OR account.[Role] LIKE @Search
                 OR CONVERT(varchar(20), employee.[Id]) LIKE @Search
                 OR CONVERT(varchar(20), employee.[UserId]) LIKE @Search)
                """);
            parameters.Add("Search", $"%{query.Search.Trim()}%");
        }

        return "WHERE " + string.Join(" AND ", where);
    }

    private static string BuildLeaveWhere(LeaveRequestSearchQuery query, DynamicParameters parameters)
    {
        var where = new List<string> { "leaveRequest.[IsDeleted] = 0" };
        if (query.Status.HasValue)
        {
            where.Add("leaveRequest.[Status] = @Status");
            parameters.Add("Status", query.Status.Value);
        }

        if (query.LeaveType.HasValue)
        {
            where.Add("leaveRequest.[LeaveType] = @LeaveType");
            parameters.Add("LeaveType", query.LeaveType.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("""
                (CONCAT(employee.[FirstName], ' ', employee.[LastName]) LIKE @Search
                 OR CONCAT(approver.[FirstName], ' ', approver.[LastName]) LIKE @Search
                 OR leaveRequest.[RejectionReason] LIKE @Search
                 OR CONVERT(varchar(20), leaveRequest.[Id]) LIKE @Search
                 OR CONVERT(varchar(20), leaveRequest.[EmployeeId]) LIKE @Search)
                """);
            parameters.Add("Search", $"%{query.Search.Trim()}%");
        }

        return "WHERE " + string.Join(" AND ", where);
    }

    private static string BuildScheduleWhere(WorkScheduleSearchQuery query, DynamicParameters parameters)
    {
        var where = new List<string> { "schedule.[IsDeleted] = 0" };
        if (query.From.HasValue)
        {
            where.Add("schedule.[ShiftDate] >= @From");
            parameters.Add("From", query.From.Value);
        }

        if (query.To.HasValue)
        {
            where.Add("schedule.[ShiftDate] <= @To");
            parameters.Add("To", query.To.Value);
        }

        if (query.Shift.HasValue)
        {
            where.Add("schedule.[Shift] = @Shift");
            parameters.Add("Shift", (int)query.Shift.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            where.Add("account.[Role] = @Role");
            parameters.Add("Role", query.Role.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("""
                (CONCAT(employee.[FirstName], ' ', employee.[LastName]) LIKE @Search
                 OR CONCAT(account.[FirstName], ' ', account.[LastName]) LIKE @Search
                 OR account.[Email] LIKE @Search
                 OR account.[Role] LIKE @Search
                 OR schedule.[RoleAtShift] LIKE @Search
                 OR CONVERT(varchar(20), schedule.[Id]) LIKE @Search
                 OR CONVERT(varchar(20), schedule.[UserId]) LIKE @Search)
                """);
            parameters.Add("Search", $"%{query.Search.Trim()}%");
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
