using BCrypt.Net;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Microsoft.Extensions.Logging;
using Dapper;

namespace KuchniaUCygana.Infrastructure.Persistence.Seeding;

public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private readonly IDbConnectionFactory connectionFactory;
    private readonly ILogger<DatabaseSeeder> logger;

    /// <summary>
    /// Initializes a new instance of <see cref="DatabaseSeeder"/> with the specified database connection factory and logger.
    /// </summary>
    public DatabaseSeeder(IDbConnectionFactory connectionFactory, ILogger<DatabaseSeeder> logger)
    {
        this.connectionFactory = connectionFactory;
        this.logger = logger;
    }

    /// <summary>
    /// Seed the database according to the specified seeding profile.
    /// </summary>
    /// <param name="profile">The seeding profile that determines which data set to apply.</param>
    /// <param name="cancellationToken">Token to observe for cancellation.</param>
    /// <returns>A task that completes when seeding has finished.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the provided seeding profile is not supported.</exception>
    public async Task SeedAsync(DatabaseSeedingProfile profile, CancellationToken cancellationToken = default)
    {
        switch (profile)
        {
            case DatabaseSeedingProfile.MinimalRealistic:
                await SeedMinimalRealisticAsync(cancellationToken);
                return;
            default:
                throw new InvalidOperationException($"Unsupported seeding profile: {profile}");
        }
    }

    /// <summary>
    /// Seeds a minimal realistic set of initial data into the database (currently users).
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the seeding operation.</param>
    private async Task SeedMinimalRealisticAsync(CancellationToken cancellationToken)
    {
        using var db = connectionFactory.CreateConnection();
        var now = DateTimeOffset.UtcNow;

        await SeedUsersAsync(db, now, cancellationToken);
    }

    /// <summary>
    /// Inserts a set of default user accounts into the Users table when the table contains no rows.
    /// </summary>
    /// <param name="db">Open database connection used to query and insert users.</param>
    /// <param name="now">Timestamp to assign to the users' CreatedAt fields.</param>
    /// <param name="cancellationToken">Token to cancel database operations.</param>
    private async Task SeedUsersAsync(System.Data.IDbConnection db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await db.ExecuteScalarAsync<int>(
                new CommandDefinition("SELECT COUNT(1) FROM [Users];", cancellationToken: cancellationToken)) > 0)
        {
            return;
        }

        var users = new[]
        {
            new User
            {
                Email = "admin@kuchnia.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                FirstName = "System",
                LastName = "Administrator",
                Role = UserRoles.Admin,
                CreatedAt = now,
            },
            new User
            {
                Email = "kitchen@kuchnia.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Kitchen123!"),
                FirstName = "Kuchnia",
                LastName = "Operator",
                Role = UserRoles.Kitchen,
                CreatedAt = now,
            },
            new User
            {
                Email = "driver@kuchnia.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Driver123!"),
                FirstName = "Dostawa",
                LastName = "Kierowca",
                Role = UserRoles.Driver,
                CreatedAt = now,
            },
        };

        const string sql = """
            INSERT INTO [Users] ([Email], [PasswordHash], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt])
            VALUES (@Email, @PasswordHash, @FirstName, @LastName, @Role, @CreatedAt, @UpdatedAt);
            """;

        await db.ExecuteAsync(new CommandDefinition(sql, users, cancellationToken: cancellationToken));
        this.logger.LogInformation("Seeded {Count} users.", users.Length);
    }
}
