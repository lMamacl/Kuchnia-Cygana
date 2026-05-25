using System.Data;
using BCrypt.Net;
using Dapper;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Microsoft.Extensions.Logging;

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
            case DatabaseSeedingProfile.DemoData:
                await SeedMinimalRealisticAsync(cancellationToken);
                await SeedDemoDataAsync(cancellationToken);
                return;
            default:
                throw new InvalidOperationException($"Unsupported seeding profile: {profile}");
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  MinimalRealistic — użytkownicy
    // ─────────────────────────────────────────────────────────────────

    private async Task SeedMinimalRealisticAsync(CancellationToken cancellationToken)
    {
        using var db = connectionFactory.CreateConnection();
        var now = DateTimeOffset.UtcNow;

        await SeedUsersAsync(db, now, cancellationToken);
    }

    private async Task SeedUsersAsync(IDbConnection db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await CountRowsAsync(db, "Users", cancellationToken) > 0)
        {
            return;
        }

        var users = new[]
        {
            MakeUser("admin@kuchnia.local", "Admin123!", "System", "Administrator", UserRoles.Admin, now),
            MakeUser("kitchen@kuchnia.local", "Kitchen123!", "Kuchnia", "Operator", UserRoles.Kitchen, now),
            MakeUser("kitchenm@kuchnia.local", "Kitchen123!", "Kuchnia", "Szef", UserRoles.KitchenManager, now),
            MakeUser("warehouse@kuchnia.local", "Warehouse123!", "Magazyn", "Pracownik", UserRoles.Warehouse, now),
            MakeUser("warehousem@kuchnia.local", "Warehouse123!", "Magazyn", "Kierownik", UserRoles.WarehouseManager, now),
            MakeUser("packing@kuchnia.local", "Packing123!", "Pakowanie", "Pracownik", UserRoles.Packing, now),
            MakeUser("packingm@kuchnia.local", "Packing123!", "Pakowanie", "Kierownik", UserRoles.PackingManager, now),
            MakeUser("dietitian@kuchnia.local", "Diet123!", "Anna", "Dietetyk", UserRoles.Dietitian, now),
            MakeUser("driver@kuchnia.local", "Driver123!", "Dostawa", "Kierowca", UserRoles.Driver, now),
        };

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Users] ([Email], [PasswordHash], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt])
            VALUES (@Email, @PasswordHash, @FirstName, @LastName, @Role, @CreatedAt, @UpdatedAt);
            """,
            users,
            cancellationToken: cancellationToken));
        this.logger.LogInformation("Seeded {Count} users with all M3/M4/M5 roles.", users.Length);
    }

    private static User MakeUser(string email, string password, string firstName, string lastName, string role, DateTimeOffset now)
        => new()
        {
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            FirstName = firstName,
            LastName = lastName,
            Role = role,
            CreatedAt = now,
        };

    // ─────────────────────────────────────────────────────────────────
    //  DemoData — dane operacyjne M3 (Magazyn, Produkcja, Pakowanie)
    // ─────────────────────────────────────────────────────────────────

    private async Task SeedDemoDataAsync(CancellationToken cancellationToken)
    {
        using var db = connectionFactory.CreateConnection();
        var now = DateTimeOffset.UtcNow;
        var auditUser = "DemoSeeder";

        await SeedUnitsOfMeasureAsync(db, now, cancellationToken);
        await SeedStockItemsAsync(db, now, auditUser, cancellationToken);
        await SeedBatchesAsync(db, now, auditUser, cancellationToken);
        await SeedTemperatureLogsAsync(db, now, auditUser, cancellationToken);
    }

    // ── Jednostki miar ────────────────────────────────────────────

    private async Task SeedUnitsOfMeasureAsync(IDbConnection db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (await CountRowsAsync(db, "UnitsOfMeasure", cancellationToken) > 0)
        {
            return;
        }

        var units = new UnitOfMeasure[]
        {
            new() { Symbol = "kg",  Name = "Kilogram",  Description = "Jednostka masy", CreatedAt = now },
            new() { Symbol = "g",   Name = "Gram",      Description = "Jednostka masy (mała)", CreatedAt = now },
            new() { Symbol = "L",   Name = "Litr",       Description = "Jednostka objętości", CreatedAt = now },
            new() { Symbol = "ml",  Name = "Mililitr",   Description = "Jednostka objętości (mała)", CreatedAt = now },
            new() { Symbol = "szt", Name = "Sztuka",     Description = "Jednostka ilościowa", CreatedAt = now },
        };

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [UnitsOfMeasure] ([Symbol], [Name], [Description], [CreatedAt], [UpdatedAt])
            VALUES (@Symbol, @Name, @Description, @CreatedAt, @UpdatedAt);
            """,
            units,
            cancellationToken: cancellationToken));
        this.logger.LogInformation("Seeded {Count} units of measure.", units.Length);
    }

    // ── Składniki magazynowe (StockItem) ──────────────────────────

    private async Task SeedStockItemsAsync(IDbConnection db, DateTimeOffset now, string auditUser, CancellationToken cancellationToken)
    {
        if (await CountRowsAsync(db, "StockItems", cancellationToken) > 0)
        {
            return;
        }

        // Pobierz ID jednostek z bazy (insertowane wcześniej)
        var units = (await db.QueryAsync<UnitOfMeasure>(new CommandDefinition(
            "SELECT * FROM [UnitsOfMeasure];",
            cancellationToken: cancellationToken))).ToList();
        int KgId() => units.First(u => u.Symbol == "kg").Id;
        int LId() => units.First(u => u.Symbol == "L").Id;
        int SztId() => units.First(u => u.Symbol == "szt").Id;

        var items = new StockItem[]
        {
            // ── Mięso ────────────────────────────────────────
            new() { Name = "Pierś z kurczaka świeża",      DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 10.0m,  LeadTimeDays = 1, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Łosoś filet świeży",           DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 5.0m,   LeadTimeDays = 1, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Mięso mielone wołowe",         DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 8.0m,   LeadTimeDays = 2, CreatedBy = auditUser, CreatedAt = now },

            // ── Nabiał ───────────────────────────────────────
            new() { Name = "Śmietanka UHT 30%",            DefaultUnitOfMeasureId = LId(),   MinimumLevel = 20.0m,  LeadTimeDays = 3, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Masło Extra 82%",               DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 5.0m,   LeadTimeDays = 3, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Ser żółty Gouda",               DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 4.0m,   LeadTimeDays = 3, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Jogurt naturalny",              DefaultUnitOfMeasureId = LId(),   MinimumLevel = 10.0m,  LeadTimeDays = 2, CreatedBy = auditUser, CreatedAt = now },

            // ── Warzywa i owoce ──────────────────────────────
            new() { Name = "Brokuły świeże",                DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 5.0m,   LeadTimeDays = 1, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Dynia piżmowa",                 DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 8.0m,   LeadTimeDays = 2, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Bataty (słodkie ziemniaki)",    DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 6.0m,   LeadTimeDays = 3, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Jagody mrożone",                DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 3.0m,   LeadTimeDays = 5, CreatedBy = auditUser, CreatedAt = now },

            // ── Suche ────────────────────────────────────────
            new() { Name = "Mąka pszenna typ 500",          DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 50.0m,  LeadTimeDays = 7, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Ryż jaśminowy",                  DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 20.0m,  LeadTimeDays = 7, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Pomidory krojone (puszka)",     DefaultUnitOfMeasureId = SztId(), MinimumLevel = 15.0m,  LeadTimeDays = 14, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Makaron penne",                  DefaultUnitOfMeasureId = KgId(),  MinimumLevel = 10.0m,  LeadTimeDays = 14, CreatedBy = auditUser, CreatedAt = now },
        };

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [StockItems]
                ([Name], [BaseIngredientId], [DefaultUnitOfMeasureId], [MinimumLevel], [LeadTimeDays],
                 [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            VALUES
                (@Name, @BaseIngredientId, @DefaultUnitOfMeasureId, @MinimumLevel, @LeadTimeDays,
                 @CreatedBy, @UpdatedBy, @IsDeleted, @DeletedAt, @DeletedBy, @CreatedAt, @UpdatedAt);
            """,
            items,
            cancellationToken: cancellationToken));
        this.logger.LogInformation("Seeded {Count} stock items.", items.Length);
    }

    // ── Partie magazynowe (Batch) — FEFO ─────────────────────────

    private async Task SeedBatchesAsync(IDbConnection db, DateTimeOffset now, string auditUser, CancellationToken cancellationToken)
    {
        if (await CountRowsAsync(db, "Batches", cancellationToken) > 0)
        {
            return;
        }

        var stockItems = (await db.QueryAsync<StockItem>(new CommandDefinition(
            "SELECT * FROM [StockItems];",
            cancellationToken: cancellationToken))).ToList();
        var batches = new List<Batch>();
        var batchCounter = 1000;

        foreach (var item in stockItems)
        {
            // Partia 1 — starsza, mniejsza ilość (FEFO: powinna iść pierwsza)
            batches.Add(new Batch
            {
                StockItemId = item.Id,
                SupplierBatchNumber = $"BAT-{++batchCounter}",
                CurrentQuantity = item.MinimumLevel * 0.4m,
                ReceivedDate = now.AddDays(-14),
                ExpiryDate = now.AddDays(3), // blisko wygaśnięcia!
                IsDepleted = false,
                CreatedBy = auditUser,
                CreatedAt = now.AddDays(-14),
            });

            // Partia 2 — nowsza, pełniejsza
            batches.Add(new Batch
            {
                StockItemId = item.Id,
                SupplierBatchNumber = $"BAT-{++batchCounter}",
                CurrentQuantity = item.MinimumLevel * 1.2m,
                ReceivedDate = now.AddDays(-3),
                ExpiryDate = now.AddDays(21),
                IsDepleted = false,
                CreatedBy = auditUser,
                CreatedAt = now.AddDays(-3),
            });
        }

        // Dodaj jedną wyczerpaną partię (test filtra IsDepleted)
        batches.Add(new Batch
        {
            StockItemId = stockItems.First().Id,
            SupplierBatchNumber = $"BAT-{++batchCounter}",
            CurrentQuantity = 0m,
            ReceivedDate = now.AddDays(-30),
            ExpiryDate = now.AddDays(-5),
            IsDepleted = true,
            CreatedBy = auditUser,
            CreatedAt = now.AddDays(-30),
        });

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Batches]
                ([StockItemId], [SupplierBatchNumber], [CurrentQuantity], [ExpiryDate], [ReceivedDate], [IsDepleted],
                 [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            VALUES
                (@StockItemId, @SupplierBatchNumber, @CurrentQuantity, @ExpiryDate, @ReceivedDate, @IsDepleted,
                 @CreatedBy, @UpdatedBy, @IsDeleted, @DeletedAt, @DeletedBy, @CreatedAt, @UpdatedAt);
            """,
            batches,
            cancellationToken: cancellationToken));
        this.logger.LogInformation("Seeded {Count} batches (FEFO demo data).", batches.Count);
    }

    // ── Rejestry temperatur (TemperatureLog) ─────────────────────

    private async Task SeedTemperatureLogsAsync(IDbConnection db, DateTimeOffset now, string auditUser, CancellationToken cancellationToken)
    {
        if (await CountRowsAsync(db, "TemperatureLogs", cancellationToken) > 0)
        {
            return;
        }

        var devices = new[] { "Chłodnia A", "Chłodnia B", "Zamrażarka 1", "Magazyn Suchy" };
        var logs = new List<TemperatureLog>();

        // 7 dni wstecz, 3 pomiary dziennie per urządzenie
        for (var day = -6; day <= 0; day++)
        {
            foreach (var device in devices)
            {
                for (var reading = 0; reading < 3; reading++)
                {
                    var recordedAt = now.AddDays(day).AddHours(6 + reading * 6); // 06:00, 12:00, 18:00

                    decimal temp;
                    string? remarks = null;

                    if (device.Contains("Zamrażarka"))
                    {
                        temp = -20.0m + (day + reading) * 0.3m; // ok. -20°C ± szum
                    }
                    else if (device.Contains("Magazyn Suchy"))
                    {
                        temp = 18.0m + reading * 0.5m;
                    }
                    else
                    {
                        // Chłodnie — norma 2-8°C
                        temp = 4.0m + day * 0.2m + reading * 0.3m;
                    }

                    // Symuluj 2 przekroczenia normy (alerty HACCP)
                    if (device == "Chłodnia B" && day == -2 && reading == 2)
                    {
                        temp = 9.5m; // przekroczenie!
                        remarks = "ALERT: Temperatura powyżej normy 8°C. Drzwi otwarte przez 20 min.";
                    }

                    if (device == "Zamrażarka 1" && day == -4 && reading == 0)
                    {
                        temp = -15.0m; // przekroczenie (za ciepło dla zamrażarki)
                        remarks = "ALERT: Temperatura powyżej normy -18°C. Awaria sprężarki — serwis wezwany.";
                    }

                    logs.Add(new TemperatureLog
                    {
                        DeviceNameOrLocation = device,
                        RecordedTemperatureCelsius = Math.Round(temp, 1),
                        RecordedAt = recordedAt,
                        Remarks = remarks,
                        CreatedBy = "SensorSystem",
                        CreatedAt = recordedAt,
                    });
                }
            }
        }

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [TemperatureLogs]
                ([DeviceNameOrLocation], [RecordedTemperatureCelsius], [RecordedAt], [Remarks],
                 [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            VALUES
                (@DeviceNameOrLocation, @RecordedTemperatureCelsius, @RecordedAt, @Remarks,
                 @CreatedBy, @UpdatedBy, @IsDeleted, @DeletedAt, @DeletedBy, @CreatedAt, @UpdatedAt);
            """,
            logs,
            cancellationToken: cancellationToken));
        this.logger.LogInformation("Seeded {Count} temperature logs (7 days, {DeviceCount} devices).", logs.Count, devices.Length);
    }

    private static Task<int> CountRowsAsync(IDbConnection db, string tableName, CancellationToken cancellationToken)
        => db.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM [{tableName}];",
            cancellationToken: cancellationToken));
}
