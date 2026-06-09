using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class UserRepository : BaseRepository<User>, IUserRepository
{
    public UserRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService) { }

    private const int MaxPageSize = 100;

    public async Task<User?> FindByEmailAsync(string email)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM Users WHERE Email = @Email";
        return await db.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
    }

    public async Task<bool> ExistsWithEmailAsync(string email)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT COUNT(1) FROM Users WHERE Email = @Email";
        var count = await db.ExecuteScalarAsync<int>(sql, new { Email = email });
        return count > 0;
    }

    public async Task<IReadOnlyList<User>> GetByIdsAsync(IEnumerable<int> ids)
    {
        var idList = ids
            .Where(id => id > 0)
            .Distinct()
            .ToArray();

        if (idList.Length == 0)
        {
            return Array.Empty<User>();
        }

        using var db = Factory.CreateConnection();
        const string sql = """
            SELECT [Id], [Email], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt]
            FROM [Users]
            WHERE [Id] IN @Ids
            ORDER BY [LastName], [FirstName], [Email], [Id];
            """;

        var users = await db.QueryAsync<User>(sql, new { Ids = idList });
        return users.ToList();
    }

    public async Task<IReadOnlyList<User>> GetByRolesAsync(IEnumerable<string> roles)
    {
        using var db = Factory.CreateConnection();
        var roleList = roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roleList.Length == 0)
        {
            return Array.Empty<User>();
        }

        const string sql = """
            SELECT [Id], [Email], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt]
            FROM [Users]
            WHERE [Role] IN @Roles
            ORDER BY [Role], [LastName], [FirstName], [Id];
            """;

        var users = await db.QueryAsync<User>(sql, new { Roles = roleList });
        return users.ToList();
    }

    public async Task<User?> GetFirstByRoleAsync(string role)
    {
        using var db = Factory.CreateConnection();
        const string sql = """
            SELECT TOP 1 *
            FROM [Users]
            WHERE [Role] = @Role
            ORDER BY [Id];
            """;

        return await db.QuerySingleOrDefaultAsync<User>(sql, new { Role = role });
    }

    public async Task<UserSearchResult> SearchAsync(UserSearchQuery query)
    {
        using var db = Factory.CreateConnection();
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 10 : query.PageSize, 5, MaxPageSize);
        var where = new List<string>();
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            where.Add("[Role] = @Role");
            parameters.Add("Role", query.Role.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            where.Add("""
                ([Email] LIKE @Search
                 OR [FirstName] LIKE @Search
                 OR [LastName] LIKE @Search
                 OR CONCAT([FirstName], ' ', [LastName]) LIKE @Search
                 OR [Role] LIKE @Search
                 OR CONVERT(varchar(20), [Id]) LIKE @Search)
                """);
            parameters.Add("Search", $"%{query.Search.Trim()}%");
        }

        var whereSql = where.Count == 0
            ? string.Empty
            : "WHERE " + string.Join(" AND ", where);
        var totalCount = await db.ExecuteScalarAsync<int>(
            $"""
            SELECT COUNT(1)
            FROM [Users]
            {whereSql};
            """,
            parameters);

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);
        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var users = await db.QueryAsync<User>(
            $"""
            SELECT [Id], [Email], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt]
            FROM [Users]
            {whereSql}
            ORDER BY [Role], [LastName], [FirstName], [Email], [Id]
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            parameters);

        return new UserSearchResult
        {
            Items = users.ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<IReadOnlyList<UserRoleSummaryRow>> GetRoleSummariesAsync()
    {
        using var db = Factory.CreateConnection();
        var rows = await db.QueryAsync<UserRoleSummaryRow>(
            """
            WITH roleStats AS
            (
                SELECT
                    COALESCE(NULLIF([Role], ''), 'Brak roli') AS [Role],
                    COUNT(1) AS [TotalCount]
                FROM [Users]
                GROUP BY COALESCE(NULLIF([Role], ''), 'Brak roli')
            )
            SELECT
                roleStats.[Role],
                roleStats.[TotalCount],
                COALESCE(samples.[SampleUsers], '') AS [SampleUsers]
            FROM roleStats
            OUTER APPLY
            (
                SELECT STRING_AGG(sample.[DisplayName], ', ') WITHIN GROUP (ORDER BY sample.[DisplayName]) AS [SampleUsers]
                FROM
                (
                    SELECT TOP (3)
                        COALESCE(
                            NULLIF(LTRIM(RTRIM(CONCAT(account.[FirstName], ' ', account.[LastName]))), ''),
                            account.[Email]) AS [DisplayName]
                    FROM [Users] account
                    WHERE COALESCE(NULLIF(account.[Role], ''), 'Brak roli') = roleStats.[Role]
                    ORDER BY account.[LastName], account.[FirstName], account.[Email], account.[Id]
                ) sample
            ) samples
            ORDER BY roleStats.[TotalCount] DESC, roleStats.[Role];
            """);

        return rows.ToList();
    }
}


