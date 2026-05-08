using BCrypt.Net;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Microsoft.Extensions.Logging;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Seeding;

public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private readonly IDbConnectionFactory connectionFactory;
    private readonly ILogger<DatabaseSeeder> logger;

    public DatabaseSeeder(IDbConnectionFactory connectionFactory, ILogger<DatabaseSeeder> logger)
    {
        this.connectionFactory = connectionFactory;
        this.logger = logger;
    }

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

    private async Task SeedMinimalRealisticAsync(CancellationToken cancellationToken)
    {
        using var db = connectionFactory.CreateConnection();
        var now = DateTimeOffset.UtcNow;

        await SeedUsersAsync(db, now, cancellationToken);
    }

    private async Task SeedUsersAsync(System.Data.IDbConnection db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await db.CountAsync<User>(token: cancellationToken) > 0)
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

        await db.InsertAllAsync(users, cancellationToken);
        this.logger.LogInformation("Seeded {Count} users.", users.Length);
    }
}
