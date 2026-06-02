using System.Data;
using System.Text.Json;
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
        await SeedMenuAsync(db, now, auditUser, cancellationToken);
        await SeedProductionPlansAsync(db, now, auditUser, cancellationToken);
        await SeedPackingSessionsAsync(db, now, auditUser, cancellationToken);
        await EnsureTodayDemoLifecycleAsync(db, now, auditUser, cancellationToken);
        await SeedLogisticsDemoAsync(db, now, auditUser, cancellationToken);
    }

    private async Task SeedLogisticsDemoAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayDate = today.ToDateTime(TimeOnly.MinValue);
        var preferredDietVariantId = await EnsureDemoLifecycleMenuFoundationAsync(db, now, auditUser, cancellationToken);
        var dietMenuPlanId = await EnsureTodayDemoDietMenuPlanAsync(db, todayDate, now, auditUser, cancellationToken);
        var meals = (await GetTodayDemoMealsAsync(db, dietMenuPlanId, preferredDietVariantId, cancellationToken)).ToList();

        if (meals.Count < 3)
        {
            this.logger.LogWarning(
                "Skipped logistics demo seed because only {MealCount} menu plan items were available for {DemoDate}.",
                meals.Count,
                today);
            return;
        }

        var productionPlanId = await EnsureTodayDemoProductionPlanAsync(db, todayDate, now, auditUser, cancellationToken);
        var productionPlanItemIds = await EnsureTodayDemoProductionPlanItemsAsync(
            db,
            productionPlanId,
            meals,
            now,
            auditUser,
            cancellationToken);

        var vehicles = new[]
        {
            new LogisticsDemoVehicle("BI 1001A", "Fiat Ducato Chlodnia", 650m, VehicleStatus.Active),
            new LogisticsDemoVehicle("BI 2042C", "Mercedes Sprinter Long", 900m, VehicleStatus.Active),
            new LogisticsDemoVehicle("BI 3307E", "Renault Master Izoterma", 1200m, VehicleStatus.Active),
            new LogisticsDemoVehicle("BI 4040S", "Volkswagen Crafter Serwis", 850m, VehicleStatus.Maintenance),
        };
        var vehicleIds = new Dictionary<string, int>();
        foreach (var vehicle in vehicles)
        {
            vehicleIds[vehicle.RegistrationNumber] = await EnsureLogisticsDemoVehicleAsync(
                db,
                vehicle,
                now,
                auditUser,
                cancellationToken);
        }

        var drivers = new[]
        {
            new LogisticsDemoDriver("driver@kuchnia.local", "Dostawa", "Kierowca", "M4-BI-001", "BI 1001A"),
            new LogisticsDemoDriver("driver2@kuchnia.local", "Marek", "Sokolowski", "M4-BI-002", "BI 2042C"),
            new LogisticsDemoDriver("driver3@kuchnia.local", "Ewa", "Wysocka", "M4-BI-003", "BI 3307E"),
        };
        foreach (var driver in drivers)
        {
            var userId = await EnsureLogisticsDemoUserAsync(db, driver, now, cancellationToken);
            var driverId = await EnsureLogisticsDemoDriverAsync(
                db,
                userId,
                driver.LicenseNumber,
                now,
                auditUser,
                cancellationToken);
            await EnsureLogisticsDemoAssignmentAsync(
                db,
                driverId,
                vehicleIds[driver.VehicleRegistration],
                now,
                cancellationToken);
        }

        var deliveries = GetLogisticsDemoDeliveries();
        for (var index = 0; index < deliveries.Length; index++)
        {
            var delivery = deliveries[index];
            var customerId = await EnsureLogisticsDemoCustomerAsync(db, index + 1, delivery, now, cancellationToken);
            var addressId = await EnsureLogisticsDemoAddressAsync(
                db,
                customerId,
                index + 1,
                delivery,
                now,
                auditUser,
                cancellationToken);
            var clientPublicId = await EnsureDemoLifecycleCustomerProfileAsync(
                db,
                customerId,
                addressId,
                now,
                auditUser,
                cancellationToken);

            var mealCount = 2 + (index % 3);
            var orderMeals = meals
                .OrderBy(meal => meal.SortOrder)
                .Take(mealCount)
                .ToList();
            var orderNumber = $"DEMO-M4-{today:yyyyMMdd}-{index + 1:D2}";
            var totalPrice = orderMeals.Sum(meal => meal.PricePerDay);
            var orderId = await EnsureTodayDemoOrderAsync(
                db,
                customerId,
                orderNumber,
                todayDate,
                totalPrice,
                now,
                auditUser,
                cancellationToken);

            await EnsureTodayDemoOrderItemsAsync(db, orderId, orderMeals, now, auditUser, cancellationToken);
            var deliveryCalendarId = await EnsureTodayDemoDeliveryCalendarAsync(
                db,
                orderId,
                addressId,
                today.ToDateTime(new TimeOnly(6 + ((index * 35) / 60), (index * 35) % 60)),
                now,
                auditUser,
                cancellationToken);
            await EnsureTodayDemoPackingAsync(
                db,
                today,
                todayDate,
                orderId,
                deliveryCalendarId,
                $"{delivery.FirstName} {delivery.LastName}",
                clientPublicId,
                orderMeals,
                productionPlanItemIds,
                now,
                auditUser,
                cancellationToken);
        }

        this.logger.LogInformation(
            "Seeded logistics demo data for {DemoDate}: {VehicleCount} vehicles, {DriverCount} drivers, {DeliveryCount} delivery candidates.",
            today,
            vehicles.Length,
            drivers.Length,
            deliveries.Length);
    }

    private static async Task<int> EnsureLogisticsDemoVehicleAsync(
        IDbConnection db,
        LogisticsDemoVehicle vehicle,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var vehicleId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT [Id] FROM [Vehicles] WHERE [RegistrationNumber] = @registrationNumber;",
            new { registrationNumber = vehicle.RegistrationNumber },
            cancellationToken: cancellationToken));

        if (vehicleId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Vehicles]
                SET [Model] = @model,
                    [MaxLoadKg] = @maxLoadKg,
                    [Status] = @status,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @vehicleId;
                """,
                new
                {
                    vehicleId = vehicleId.Value,
                    model = vehicle.Model,
                    maxLoadKg = vehicle.MaxLoadKg,
                    status = (int)vehicle.Status,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));
            return vehicleId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Vehicles]
                ([RegistrationNumber], [Model], [MaxLoadKg], [Status], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@registrationNumber, @model, @maxLoadKg, @status, @now, @auditUser, 0);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new
            {
                registrationNumber = vehicle.RegistrationNumber,
                model = vehicle.Model,
                maxLoadKg = vehicle.MaxLoadKg,
                status = (int)vehicle.Status,
                now,
                auditUser,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureLogisticsDemoUserAsync(
        IDbConnection db,
        LogisticsDemoDriver driver,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var userId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT [Id] FROM [Users] WHERE [Email] = @email;",
            new { email = driver.Email },
            cancellationToken: cancellationToken));

        if (userId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Users]
                SET [FirstName] = @firstName,
                    [LastName] = @lastName,
                    [Role] = @role,
                    [UpdatedAt] = @now
                WHERE [Id] = @userId;
                """,
                new
                {
                    userId = userId.Value,
                    firstName = driver.FirstName,
                    lastName = driver.LastName,
                    role = UserRoles.Driver,
                    now,
                },
                cancellationToken: cancellationToken));
            return userId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Users] ([Email], [PasswordHash], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt])
            VALUES (@email, @passwordHash, @firstName, @lastName, @role, @now, NULL);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new
            {
                email = driver.Email,
                passwordHash = BCrypt.Net.BCrypt.HashPassword("Driver123!"),
                firstName = driver.FirstName,
                lastName = driver.LastName,
                role = UserRoles.Driver,
                now,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureLogisticsDemoDriverAsync(
        IDbConnection db,
        int userId,
        string licenseNumber,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var driverId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [Drivers]
            WHERE ([UserId] = @userId OR [LicenseNumber] = @licenseNumber)
              AND [IsDeleted] = 0
            ORDER BY CASE WHEN [UserId] = @userId THEN 0 ELSE 1 END, [Id];
            """,
            new { userId, licenseNumber },
            cancellationToken: cancellationToken));

        if (driverId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Drivers]
                SET [UserId] = @userId,
                    [LicenseNumber] = @licenseNumber,
                    [IsActive] = 1,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @driverId;
                """,
                new { driverId = driverId.Value, userId, licenseNumber, now, auditUser },
                cancellationToken: cancellationToken));
            return driverId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Drivers]
                ([UserId], [LicenseNumber], [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@userId, @licenseNumber, 1, @now, @auditUser, 0);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new { userId, licenseNumber, now, auditUser },
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureLogisticsDemoAssignmentAsync(
        IDbConnection db,
        int driverId,
        int vehicleId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT [Id]
            FROM [DriverVehicleAssignments]
            WHERE [DriverId] = @driverId
              AND [VehicleId] = @vehicleId
              AND [UnassignedAt] IS NULL;
            """,
            new { driverId, vehicleId },
            cancellationToken: cancellationToken));

        if (existing.HasValue)
        {
            return;
        }

        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [DriverVehicleAssignments]
            SET [UnassignedAt] = @now,
                [UpdatedAt] = @now
            WHERE ([DriverId] = @driverId OR [VehicleId] = @vehicleId)
              AND [UnassignedAt] IS NULL;

            INSERT INTO [DriverVehicleAssignments]
                ([DriverId], [VehicleId], [AssignedAt], [UnassignedAt], [CreatedAt], [UpdatedAt])
            VALUES
                (@driverId, @vehicleId, @now, NULL, @now, NULL);
            """,
            new { driverId, vehicleId, now },
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureLogisticsDemoCustomerAsync(
        IDbConnection db,
        int sequence,
        LogisticsDemoDelivery delivery,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var email = $"demo-m4-klient-{sequence:D2}@kuchnia.local";
        var userId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT [Id] FROM [Users] WHERE [Email] = @email;",
            new { email },
            cancellationToken: cancellationToken));

        if (userId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Users]
                SET [FirstName] = @firstName,
                    [LastName] = @lastName,
                    [Role] = @role,
                    [UpdatedAt] = @now
                WHERE [Id] = @userId;
                """,
                new
                {
                    userId = userId.Value,
                    delivery.FirstName,
                    delivery.LastName,
                    role = UserRoles.Client,
                    now,
                },
                cancellationToken: cancellationToken));
            return userId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Users] ([Email], [PasswordHash], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt])
            VALUES (@email, @passwordHash, @firstName, @lastName, @role, @now, NULL);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new
            {
                email,
                passwordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
                delivery.FirstName,
                delivery.LastName,
                role = UserRoles.Client,
                now,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureLogisticsDemoAddressAsync(
        IDbConnection db,
        int customerId,
        int sequence,
        LogisticsDemoDelivery delivery,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var label = $"Demo M4 {sequence:D2}";
        var addressId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [Addresses]
            WHERE [UserId] = @customerId
              AND [Label] = @label
            ORDER BY [Id];
            """,
            new { customerId, label },
            cancellationToken: cancellationToken));

        if (addressId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Addresses]
                SET [Street] = @street,
                    [BuildingNumber] = @buildingNumber,
                    [ApartmentNumber] = @apartmentNumber,
                    [City] = N'Bialystok',
                    [PostalCode] = @postalCode,
                    [IsDefault] = 1,
                    [DeliveryNotes] = N'Demo M4: punkt do generowania tras logistycznych.',
                    [Latitude] = @latitude,
                    [Longitude] = @longitude,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @addressId;
                """,
                new
                {
                    addressId = addressId.Value,
                    delivery.Street,
                    delivery.BuildingNumber,
                    delivery.ApartmentNumber,
                    delivery.PostalCode,
                    delivery.Latitude,
                    delivery.Longitude,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));
            return addressId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Addresses]
                ([UserId], [Label], [Street], [BuildingNumber], [ApartmentNumber], [City], [PostalCode],
                 [IsDefault], [DeliveryNotes], [CreatedAt], [CreatedBy], [IsDeleted], [Latitude], [Longitude])
            VALUES
                (@customerId, @label, @street, @buildingNumber, @apartmentNumber, N'Bialystok', @postalCode,
                 1, N'Demo M4: punkt do generowania tras logistycznych.', @now, @auditUser, 0, @latitude, @longitude);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new
            {
                customerId,
                label,
                delivery.Street,
                delivery.BuildingNumber,
                delivery.ApartmentNumber,
                delivery.PostalCode,
                delivery.Latitude,
                delivery.Longitude,
                now,
                auditUser,
            },
            cancellationToken: cancellationToken));
    }

    private static LogisticsDemoDelivery[] GetLogisticsDemoDeliveries() =>
    [
        new("Katarzyna", "Nowak", "Lipowa", "12", "4", "15-427", 53.13218, 23.15841),
        new("Piotr", "Kaminski", "Warszawska", "8", null, "15-062", 53.13073, 23.17442),
        new("Magdalena", "Krol", "Mickiewicza", "31", "9", "15-213", 53.12464, 23.17085),
        new("Tomasz", "Zielinski", "Sienkiewicza", "44", null, "15-092", 53.13547, 23.16512),
        new("Alicja", "Grabowska", "Zwierzyniecka", "19", "2", "15-312", 53.11794, 23.16128),
        new("Robert", "Wisniewski", "Antoniuk Fabryczny", "7", null, "15-762", 53.14785, 23.13264),
        new("Monika", "Lewandowska", "Hetmanska", "22", "11", "15-727", 53.12721, 23.12876),
        new("Pawel", "Mazur", "Wasilkowska", "71", null, "15-137", 53.15819, 23.16933),
        new("Natalia", "Wojcik", "Piastowska", "4", "6", "15-207", 53.12271, 23.18745),
        new("Michal", "Kaczmarek", "Branickiego", "10", null, "15-085", 53.12784, 23.17359),
        new("Joanna", "Dabrowska", "Swietojanska", "17", "3", "15-082", 53.12939, 23.15426),
        new("Kamil", "Pawlak", "Kawaleryjska", "38", null, "15-325", 53.10985, 23.15154),
        new("Ewelina", "Sikora", "Zwyciestwa", "6", "18", "15-703", 53.14398, 23.11941),
        new("Adam", "Baran", "Transportowa", "2", null, "15-399", 53.11091, 23.20368),
        new("Patrycja", "Lis", "Produkcyjna", "54", "7", "15-680", 53.15324, 23.10196),
    ];

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

            // ── Opakowania ───────────────────────────────────
            new() { Name = "Pudełko cateringowe 500ml",    DefaultUnitOfMeasureId = SztId(), MinimumLevel = 100.0m, LeadTimeDays = 2, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Pudełko cateringowe 250ml",    DefaultUnitOfMeasureId = SztId(), MinimumLevel = 100.0m, LeadTimeDays = 2, CreatedBy = auditUser, CreatedAt = now },
            new() { Name = "Torba papierowa u Cygana",     DefaultUnitOfMeasureId = SztId(), MinimumLevel = 50.0m,  LeadTimeDays = 2, CreatedBy = auditUser, CreatedAt = now },
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

        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [StockItems]
            SET [WarehouseCategoryId] = CASE
                WHEN LOWER([Name]) LIKE N'%pude%' OR LOWER([Name]) LIKE N'%torba%' OR LOWER([Name]) LIKE N'%opakow%' THEN 5
                WHEN LOWER([Name]) LIKE N'%kurczak%' OR LOWER([Name]) LIKE N'%oso%' OR LOWER([Name]) LIKE N'%mięs%' OR LOWER([Name]) LIKE N'%mies%' OR LOWER([Name]) LIKE N'%ryb%' OR LOWER([Name]) LIKE N'%indyka%' THEN 1
                WHEN LOWER([Name]) LIKE N'%mietanka%' OR LOWER([Name]) LIKE N'%śmietanka%' OR LOWER([Name]) LIKE N'%mas%' OR LOWER([Name]) LIKE N'%ser %' OR LOWER([Name]) LIKE N'%gouda%' OR LOWER([Name]) LIKE N'%jogurt%' OR LOWER([Name]) LIKE N'%mleko%' THEN 2
                WHEN LOWER([Name]) LIKE N'%broku%' OR LOWER([Name]) LIKE N'%dynia%' OR LOWER([Name]) LIKE N'%batat%' OR LOWER([Name]) LIKE N'%jagod%' OR LOWER([Name]) LIKE N'%ziemniak%' OR (LOWER([Name]) LIKE N'%pomidor%' AND LOWER([Name]) NOT LIKE N'%puszka%') THEN 3
                ELSE 4
            END
            WHERE [WarehouseCategoryId] = 4;
            """,
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

        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [TemperatureLogs]
            SET [HaccpLocationId] = CASE
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%nabia%' THEN 1
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%chłodnia%' OR LOWER([DeviceNameOrLocation]) LIKE N'%chlodnia%' THEN 2
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%zamra%' OR LOWER([DeviceNameOrLocation]) LIKE N'%freezer%' THEN 3
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%warzyw%' THEN 4
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%witryna%' THEN 5
                WHEN LOWER([DeviceNameOrLocation]) LIKE N'%suchy%' THEN 6
                ELSE NULL
            END
            WHERE [HaccpLocationId] IS NULL;
            """,
            cancellationToken: cancellationToken));

        this.logger.LogInformation("Seeded {Count} temperature logs (7 days, {DeviceCount} devices).", logs.Count, devices.Length);
    }

    private async Task SeedMenuAsync(IDbConnection db, DateTimeOffset now, string auditUser, CancellationToken cancellationToken)
    {
        if (await CountRowsAsync(db, "Categories", cancellationToken) > 0)
        {
            return;
        }

        // 1. Kategorie posiłków
        await db.ExecuteAsync(
            """
            SET IDENTITY_INSERT [Categories] ON;
            INSERT INTO [Categories] ([Id], [Name], [Description], [SortOrder], [CreatedAt])
            VALUES
                (1, 'Śniadanie', 'Pierwszy posiłek dnia', 1, @now),
                (2, 'Drugie Śniadanie', 'Lekka przekąska przedpołudniowa', 2, @now),
                (3, 'Obiad', 'Główny ciepły posiłek', 3, @now),
                (4, 'Podwieczorek', 'Słodka lub słona przekąska popołudniowa', 4, @now),
                (5, 'Kolacja', 'Ostatni posiłek dnia', 5, @now);
            SET IDENTITY_INSERT [Categories] OFF;
            """, new { now });

        // 2. Alergeny
        await db.ExecuteAsync(
            """
            SET IDENTITY_INSERT [Allergens] ON;
            INSERT INTO [Allergens] ([Id], [Name], [Code], [IconUrl], [CreatedAt])
            VALUES
                (1, 'Gluten', 'GLU', null, @now),
                (2, 'Laktoza', 'LAC', null, @now),
                (3, 'Ryby', 'FIS', null, @now),
                (4, 'Orzechy', 'NUT', null, @now),
                (5, 'Jaja', 'EGG', null, @now);
            SET IDENTITY_INSERT [Allergens] OFF;
            """, new { now });

        // 3. Składniki (Ingredients) odpowiadające StockItems pod FEFO
        await db.ExecuteAsync(
            """
            SET IDENTITY_INSERT [Ingredients] ON;
            INSERT INTO [Ingredients] ([Id], [Name], [Unit], [CostPerUnit], [IsActive], [CreatedAt], [CreatedBy])
            VALUES
                (1, 'Pierś z kurczaka świeża', 'kg', 24.50, 1, @now, @auditUser),
                (2, 'Łosoś filet świeży', 'kg', 89.00, 1, @now, @auditUser),
                (3, 'Mięso mielone wołowe', 'kg', 32.00, 1, @now, @auditUser),
                (4, 'Śmietanka UHT 30%', 'L', 14.20, 1, @now, @auditUser),
                (5, 'Masło Extra 82%', 'kg', 35.00, 1, @now, @auditUser),
                (6, 'Ser żółty Gouda', 'kg', 28.00, 1, @now, @auditUser),
                (7, 'Jogurt naturalny', 'L', 6.50, 1, @now, @auditUser),
                (8, 'Brokuły świeże', 'kg', 12.00, 1, @now, @auditUser),
                (9, 'Dynia piżmowa', 'kg', 8.00, 1, @now, @auditUser),
                (10, 'Bataty (słodkie ziemniaki)', 'kg', 9.50, 1, @now, @auditUser),
                (11, 'Jagody mrożone', 'kg', 18.00, 1, @now, @auditUser),
                (12, 'Mąka pszenna typ 500', 'kg', 3.20, 1, @now, @auditUser),
                (13, 'Ryż jaśminowy', 'kg', 7.80, 1, @now, @auditUser),
                (14, 'Pomidory krojone (puszka)', 'szt', 4.50, 1, @now, @auditUser),
                (15, 'Makaron penne', 'kg', 6.00, 1, @now, @auditUser);
            SET IDENTITY_INSERT [Ingredients] OFF;
            """, new { now, auditUser });

        // 4. Posiłki (Meals)
        await db.ExecuteAsync(
            """
            SET IDENTITY_INSERT [Meals] ON;
            INSERT INTO [Meals] ([Id], [CategoryId], [Name], [Description], [Status], [PreparationTimeMinutes], [IsActive], [CreatedAt], [CreatedBy])
            VALUES
                (1, 1, 'Jajecznica z szczypiorkiem na maśle', 'Klasyczna jajecznica z 3 jaj na prawdziwym maśle ze świeżym szczypiorkiem i pieczywem.', 'Published', 10, 1, @now, @auditUser),
                (2, 2, 'Pudding chia z jagodami i śmietanką', 'Kremowy deser chia na bazie jogurtu i śmietanki ze słodkim musem z mrożonych jagód.', 'Published', 15, 1, @now, @auditUser),
                (3, 3, 'Pikantna zupa pomidorowa z makaronem', 'Rozgrzewająca, aromatyczna zupa ze słodkich pomidorów krojonych z makaronem penne i nutą śmietanki.', 'Published', 25, 1, @now, @auditUser),
                (4, 3, 'Pieczony filet z łososia z ryżem i brokułami', 'Delikatny łosoś pieczony w ziołach, podawany z sypkim ryżem jaśminowym i gotowanymi brokułami.', 'Published', 35, 1, @now, @auditUser),
                (5, 5, 'Bowl z wołowiną, dynią i batatami', 'Pożywna kolacja z pieczonym mięsem wołowym, batatami i słodką dynią piżmową z przyprawami.', 'Published', 30, 1, @now, @auditUser);
            SET IDENTITY_INSERT [Meals] OFF;
            """, new { now, auditUser });

        // 5. Diety
        await db.ExecuteAsync(
            """
            SET IDENTITY_INSERT [Diets] ON;
            INSERT INTO [Diets] ([Id], [Name], [Description], [MarketingDescription], [Status], [IsActive], [CreatedAt], [CreatedBy])
            VALUES
                (1, 'Standard', 'Zbilansowana dieta dla każdego.', 'Zdrowy catering na każdy dzień.', 'Active', 1, @now, @auditUser),
                (2, 'Sport / High Protein', 'Dieta o podwyższonej zawartości białka dla aktywnych.', 'Zbuduj formę z Cyganem.', 'Active', 1, @now, @auditUser);
            SET IDENTITY_INSERT [Diets] OFF;
            """, new { now, auditUser });

        // 6. Warianty diet dopasowane do MockOrderDataProvider (IDs: 1, 2, 3, 4, 5)
        await db.ExecuteAsync(
            """
            SET IDENTITY_INSERT [DietVariants] ON;
            INSERT INTO [DietVariants] ([Id], [DietId], [Name], [TargetCalories], [PriceMultiplier], [IsDefault], [CreatedAt], [CreatedBy])
            VALUES
                (1, 1, 'Standard 1500', 1500, 1.00, 0, @now, @auditUser),
                (2, 1, 'Standard 1800', 1800, 1.10, 1, @now, @auditUser),
                (3, 1, 'Standard 2000', 2000, 1.20, 0, @now, @auditUser),
                (4, 2, 'Sport 2200', 2200, 1.30, 0, @now, @auditUser),
                (5, 2, 'Sport 2500', 2500, 1.40, 1, @now, @auditUser);
            SET IDENTITY_INSERT [DietVariants] OFF;
            """, new { now, auditUser });

        // 7. Przypisanie posiłków do wariantów
        await db.ExecuteAsync(
            """
            INSERT INTO [DietVariantMeals] ([DietVariantId], [MealId], [ServingSizeMultiplier], [SortOrder])
            VALUES
                (1, 1, 0.85, 1), (1, 2, 0.85, 2), (1, 3, 0.85, 3), (1, 4, 0.85, 4), (1, 5, 0.85, 5),
                (2, 1, 1.00, 1), (2, 2, 1.00, 2), (2, 3, 1.00, 3), (2, 4, 1.00, 4), (2, 5, 1.00, 5),
                (3, 1, 1.15, 1), (3, 2, 1.15, 2), (3, 3, 1.15, 3), (3, 4, 1.15, 4), (3, 5, 1.15, 5),
                (4, 1, 1.25, 1), (4, 2, 1.25, 2), (4, 3, 1.25, 3), (4, 4, 1.25, 4), (4, 5, 1.25, 5),
                (5, 1, 1.40, 1), (5, 2, 1.40, 2), (5, 3, 1.40, 3), (5, 4, 1.40, 4), (5, 5, 1.40, 5);
            """);

        await db.ExecuteAsync(
            """
            UPDATE i
            SET
                i.StockItemId = si.Id,
                i.WarehouseCategoryId = si.WarehouseCategoryId,
                i.RequiresCoreTemperatureCheck = CASE WHEN si.WarehouseCategoryId = 1 THEN 1 ELSE 0 END,
                i.MinimumCoreTemperatureCelsius = CASE WHEN si.WarehouseCategoryId = 1 THEN 75 ELSE NULL END
            FROM [Ingredients] i
            INNER JOIN [StockItems] si ON si.BaseIngredientId = i.Id
            WHERE i.StockItemId IS NULL;
            """);

        await SeedDietMenuPlansAsync(db, now, auditUser, cancellationToken);

        // 8. Receptury (Recipes)
        await db.ExecuteAsync(
            """
            INSERT INTO [Recipes] ([MealId], [IngredientId], [WeightInGrams], [IsOptional], [Notes])
            VALUES
                (1, 5, 15.00, 0, 'Masło do smażenia jajecznicy'),
                (2, 7, 120.00, 0, 'Baza jogurtowa'),
                (2, 4, 50.00, 0, 'Dodatek śmietanki'),
                (2, 11, 40.00, 0, 'Jagody na wierzch'),
                (3, 14, 1.00, 0, 'Pomidory puszka'),
                (3, 15, 60.00, 0, 'Makaron'),
                (3, 4, 30.00, 0, 'Zabielenie zupy'),
                (4, 2, 150.00, 0, 'Filet z łososia'),
                (4, 8, 100.00, 0, 'Brokuł'),
                (4, 13, 75.00, 0, 'Ryż jaśminowy'),
                (5, 3, 120.00, 0, 'Mielona wołowina'),
                (5, 9, 80.00, 0, 'Kawałki dyni'),
                (5, 10, 80.00, 0, 'Słupki batatów');
            """);

        // 9. Wartości odżywcze (NutritionFacts)
        await db.ExecuteAsync(
            """
            INSERT INTO [NutritionFacts] ([MealId], [CaloriesPer100g], [ProteinPer100g], [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [CreatedAt])
            VALUES
                (1, 180.00, 12.50, 1.20, 14.00, 0.00, @now),
                (2, 210.00, 4.50, 18.00, 12.00, 3.50, @now),
                (3, 95.00, 3.20, 14.50, 2.80, 1.20, @now),
                (4, 150.00, 18.20, 16.00, 6.50, 1.80, @now),
                (5, 165.00, 14.00, 15.00, 8.20, 2.50, @now);
            """, new { now });

        // 10. Przypisanie alergenów do posiłków
        await db.ExecuteAsync(
            """
            INSERT INTO [MealAllergens] ([MealId], [AllergenId], [IsTrace])
            VALUES
                (1, 5, 0),
                (2, 2, 0),
                (3, 1, 0),
                (3, 2, 0),
                (4, 3, 0);
            """);

        this.logger.LogInformation("Seeded all Menu, Recipes, Allergens and Diets tables.");
    }

    private async Task SeedDietMenuPlansAsync(IDbConnection db, DateTimeOffset now, string auditUser, CancellationToken cancellationToken)
    {
        if (await CountRowsAsync(db, "DietMenuPlans", cancellationToken) > 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        for (var offset = 0; offset < 7; offset++)
        {
            var planDate = today.AddDays(offset).ToDateTime(TimeOnly.MinValue);
            var planId = await db.ExecuteScalarAsync<int>(
                """
                INSERT INTO [DietMenuPlans]
                    ([PlanDate], [Status], [Notes], [PublishedAt], [PublishedBy], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@planDate, 'Published', 'Demo plan M2 opublikowany z 7-dniowym wyprzedzeniem.', @now, @auditUser, @now, @auditUser, 0);
                SELECT CAST(SCOPE_IDENTITY() as int);
                """,
                new { planDate, now, auditUser });

            await db.ExecuteAsync(
                """
                INSERT INTO [DietMenuPlanItems]
                    ([DietMenuPlanId], [DietVariantId], [MealId], [MealSlot], [ServingSizeMultiplier], [SortOrder], [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
                SELECT
                    @planId,
                    dvm.[DietVariantId],
                    dvm.[MealId],
                    CASE dvm.[SortOrder]
                        WHEN 1 THEN 'Breakfast'
                        WHEN 2 THEN 'Snack1'
                        WHEN 3 THEN 'Lunch'
                        WHEN 4 THEN 'Snack2'
                        WHEN 5 THEN 'Dinner'
                        ELSE CONCAT('Meal', dvm.[SortOrder])
                    END,
                    dvm.[ServingSizeMultiplier],
                    dvm.[SortOrder],
                    1,
                    @now,
                    @auditUser,
                    0
                FROM [DietVariantMeals] dvm;
                """,
                new { planId, now, auditUser });
        }

        this.logger.LogInformation("Seeded published M2 diet menu plans for the next 7 days.");
    }

    private async Task SeedProductionPlansAsync(IDbConnection db, DateTimeOffset now, string auditUser, CancellationToken cancellationToken)
    {
        if (await CountRowsAsync(db, "ProductionPlans", cancellationToken) > 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var yesterday = today.AddDays(-1);

        // Plan 1: wczoraj
        var yesterdayPlanId = await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO [ProductionPlans] ([ProductionDate], [Status], [Notes], [IsSharedWithLogistics], [CreatedBy], [CreatedAt])
            VALUES (@yesterday, 3, 'Wczorajsza produkcja ukończona.', 1, @auditUser, @yesterdayTime);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new { yesterday = yesterday.ToDateTime(TimeOnly.MinValue), auditUser, yesterdayTime = now.AddDays(-1) });

        // Plan 2: dzisiaj (InProgress)
        var todayPlanId = await db.ExecuteScalarAsync<int>(
            """
            INSERT INTO [ProductionPlans] ([ProductionDate], [Status], [Notes], [IsSharedWithLogistics], [CreatedBy], [CreatedAt])
            VALUES (@today, 2, 'Uwagi Szefa Kuchni: Zupa pomidorowa ma mieć łagodny smak. Priorytet gotowania!', 1, @auditUser, @now);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new { today = today.ToDateTime(TimeOnly.MinValue), auditUser, now });

        // Pozycje planu
        await db.ExecuteAsync(
            """
            INSERT INTO [ProductionPlanItems] 
                ([ProductionPlanId], [MealId], [MealName], [DietVariantId], [PlannedQuantity], [CookedQuantity], [Status], [ProductionGroup], [EstimatedReadyTime], [ActualReadyTime], [CreatedBy], [CreatedAt])
            VALUES
                (@yesterdayPlanId, 1, 'Jajecznica z szczypiorkiem na maśle', 2, 20, 20, 2, 1, '06:30:00', '06:25:00', @auditUser, @yesterdayTime),
                (@yesterdayPlanId, 3, 'Pikantna zupa pomidorowa z makaronem', 2, 20, 20, 2, 2, '07:30:00', '07:35:00', @auditUser, @yesterdayTime),
                (@yesterdayPlanId, 4, 'Pieczony filet z łososia z ryżem i brokułami', 2, 20, 21, 2, 3, '08:30:00', '08:28:00', @auditUser, @yesterdayTime),
                (@yesterdayPlanId, 5, 'Bowl z wołowiną, dynią i batatami', 2, 20, 20, 2, 4, '09:00:00', '08:55:00', @auditUser, @yesterdayTime),

                (@todayPlanId, 1, 'Jajecznica z szczypiorkiem na maśle', 2, 15, 15, 2, 1, '06:30:00', '06:28:00', @auditUser, @now),
                (@todayPlanId, 2, 'Pudding chia z jagodami i śmietanką', 2, 15, 15, 2, 1, '06:30:00', '06:32:00', @auditUser, @now),
                (@todayPlanId, 3, 'Pikantna zupa pomidorowa z makaronem', 2, 15, 0, 0, 2, '07:30:00', null, @auditUser, @now),
                (@todayPlanId, 4, 'Pieczony filet z łososia z ryżem i brokułami', 2, 15, 0, 0, 3, '08:30:00', null, @auditUser, @now),
                (@todayPlanId, 5, 'Bowl z wołowiną, dynią i batatami', 2, 15, 0, 0, 4, '09:00:00', null, @auditUser, @now);
            """,
            new { yesterdayPlanId, todayPlanId, auditUser, yesterdayTime = now.AddDays(-1), now });

        // Partie produkcyjne (półprodukty) dla dzisiejszego planu
        await db.ExecuteAsync(
            """
            INSERT INTO [ProductionBatches] ([ProductionPlanId], [MealId], [Name], [PlannedQuantity], [ProducedQuantity], [CreatedBy], [CreatedAt])
            VALUES
                (@todayPlanId, 3, 'Baza zupy pomidorowej', 10.0, 10.0, @auditUser, @now),
                (@todayPlanId, 4, 'Ugotowany ryż jaśminowy', 5.0, 0.0, @auditUser, @now);
            """,
            new { todayPlanId, auditUser, now });

        this.logger.LogInformation("Seeded ProductionPlans, ProductionPlanItems and ProductionBatches.");
    }

    private async Task SeedPackingSessionsAsync(IDbConnection db, DateTimeOffset now, string auditUser, CancellationToken cancellationToken)
    {
        if (await CountRowsAsync(db, "PackingSessions", cancellationToken) > 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var seed = today.DayNumber;
        var rng = new Random(seed);
        var orderCount = rng.Next(12, 25);

        var clientNames = new[]
        {
            "Jan Kowalski", "Anna Nowak", "Piotr Wiśniewski", "Maria Wójcik",
            "Tomasz Kamiński", "Katarzyna Lewandowska", "Michał Zieliński",
            "Agnieszka Szymańska", "Krzysztof Woźniak", "Magdalena Dąbrowska",
            "Robert Kozłowski", "Joanna Jankowska", "Andrzej Mazur",
            "Ewa Krawczyk", "Marcin Piotrowski", "Monika Grabowska",
        };

        var dietVariantIds = new[] { 1, 2, 3, 4, 5 };

        // Wstawiamy 3 przykładowe sesje z pudełkami w różnych statusach
        for (var i = 0; i < Math.Min(3, orderCount); i++)
        {
            var orderId = seed * 100 + i + 1;
            var clientName = clientNames[i % clientNames.Length];
            var dietVariantId = dietVariantIds[rng.Next(dietVariantIds.Length)];
            
            // Statusy: Pending (0), Packed (1), Labeled (2)
            var sessionStatus = i switch
            {
                0 => PackingStatus.Pending,
                1 => PackingStatus.Packed,
                _ => PackingStatus.Labeled
            };

            var sessionId = await db.ExecuteScalarAsync<int>(
                """
                INSERT INTO [PackingSessions] ([PackingDate], [OrderId], [ClientName], [PackedBy], [Status], [DeliveryCalendarId], [CreatedBy], [CreatedAt])
                VALUES (@packingDate, @orderId, @clientName, @packedBy, @status, @deliveryCalendarId, @auditUser, @now);
                SELECT CAST(SCOPE_IDENTITY() as int);
                """,
                new
                {
                    packingDate = today.ToDateTime(TimeOnly.MinValue),
                    orderId,
                    clientName,
                    packedBy = sessionStatus == PackingStatus.Pending ? null : "DemoPacker",
                    status = (int)sessionStatus,
                    deliveryCalendarId = (int?)null, // Demo: brak przypisanego DeliveryCalendarId
                    auditUser,
                    now
                });

            var mealNames = new[]
            {
                "Jajecznica z szczypiorkiem na maśle",
                "Pudding chia z jagodami i śmietanką",
                "Pikantna zupa pomidorowa z makaronem",
                "Pieczony filet z łososia z ryżem i brokułami",
                "Bowl z wołowiną, dynią i batatami"
            };

            for (var m = 0; m < 5; m++)
            {
                // Statusy pudełek w zależności od statusu sesji
                var boxStatus = sessionStatus switch
                {
                    PackingStatus.Pending => m < 2 ? PackingItemStatus.FoilPrinted : PackingItemStatus.Pending,
                    _ => PackingItemStatus.Packed
                };

                var boxCode = $"BOX-{today:yyyyMMdd}-{sessionId:D3}-{m + 1}";

                var itemId = await db.ExecuteScalarAsync<int>(
                    """
                    INSERT INTO [PackingItems] ([PackingSessionId], [MealId], [MealName], [DietVariantId], [BoxCode], [Status], [FoilPrintedAt], [PackedAt], [PackedBy], [CreatedBy], [CreatedAt])
                    VALUES (@sessionId, @mealId, @mealName, @dietVariantId, @boxCode, @status, @foilPrintedAt, @packedAt, @packedBy, @auditUser, @now);
                    SELECT CAST(SCOPE_IDENTITY() as int);
                    """,
                    new
                    {
                        sessionId,
                        mealId = m + 1,
                        mealName = mealNames[m],
                        dietVariantId,
                        boxCode,
                        status = (int)boxStatus,
                        foilPrintedAt = boxStatus >= PackingItemStatus.FoilPrinted ? now.AddMinutes(-30) : (DateTimeOffset?)null,
                        packedAt = boxStatus == PackingItemStatus.Packed ? now.AddMinutes(-10) : (DateTimeOffset?)null,
                        packedBy = boxStatus == PackingItemStatus.Packed ? "DemoPacker" : null,
                        auditUser,
                        now
                    });

                // Etykieta produktowa, jeśli zafoliowano
                if (boxStatus >= PackingItemStatus.FoilPrinted)
                {
                    await db.ExecuteAsync(
                        """
                        INSERT INTO [PackingLabels] ([PackingItemId], [LabelType], [QrCode], [DishName], [Allergens], [Kcal], [ClientName], [CreatedAt])
                        VALUES (@itemId, 0, @qrCode, @dishName, @allergens, @kcal, @clientName, @now);
                        """,
                        new
                        {
                            itemId,
                            qrCode = boxCode,
                            dishName = mealNames[m],
                            allergens = m switch
                            {
                                0 => "Jaja",
                                1 => "Laktoza",
                                2 => "Gluten, Laktoza",
                                3 => "Ryby",
                                _ => "Brak"
                            },
                            kcal = m switch
                            {
                                0 => 270,
                                1 => 315,
                                2 => 140,
                                3 => 450,
                                _ => 330
                            },
                            clientName,
                            now
                        });
                }
            }

            // Etykieta wysyłkowa na torbę
            if (sessionStatus == PackingStatus.Labeled)
            {
                var shippingCode = $"BAG-{today:yyyyMMdd}-{sessionId:D3}";
                await db.ExecuteAsync(
                    """
                    INSERT INTO [PackingLabels] ([PackingSessionId], [LabelType], [QrCode], [ClientName], [RouteInfo], [DeliveryWindow], [CreatedAt])
                    VALUES (@sessionId, 1, @qrCode, @clientName, @routeInfo, @deliveryWindow, @now);
                    """,
                    new
                    {
                        sessionId,
                        qrCode = shippingCode,
                        clientName,
                        routeInfo = "Trasa 1, auto PO 12345, stop " + (i + 1),
                        deliveryWindow = "06:00-08:00",
                        now
                    });
            }
        }

        this.logger.LogInformation("Seeded packing sessions, boxes and labels for today.");
    }

    private async Task EnsureTodayDemoLifecycleAsync(IDbConnection db, DateTimeOffset now, string auditUser, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayDate = today.ToDateTime(TimeOnly.MinValue);
        var deliveryDateTime = today.ToDateTime(new TimeOnly(8, 0));

        var customerId = await EnsureDemoLifecycleCustomerAsync(db, now, cancellationToken);
        var addressId = await EnsureDemoLifecycleAddressAsync(db, customerId, now, auditUser, cancellationToken);
        var clientPublicId = await EnsureDemoLifecycleCustomerProfileAsync(db, customerId, addressId, now, auditUser, cancellationToken);
        var preferredDietVariantId = await EnsureDemoLifecycleMenuFoundationAsync(db, now, auditUser, cancellationToken);
        var dietMenuPlanId = await EnsureTodayDemoDietMenuPlanAsync(db, todayDate, now, auditUser, cancellationToken);
        var meals = (await GetTodayDemoMealsAsync(db, dietMenuPlanId, preferredDietVariantId, cancellationToken)).Take(5).ToList();

        if (meals.Count < 3)
        {
            this.logger.LogWarning(
                "Skipped today demo lifecycle seed because only {MealCount} menu plan items were available for {DemoDate}.",
                meals.Count,
                today);
            return;
        }

        var orderNumber = $"DEMO-LIFE-{today:yyyyMMdd}";
        var clientName = "Demo Klient";
        var totalPrice = meals.Sum(meal => meal.PricePerDay);
        var orderId = await EnsureTodayDemoOrderAsync(
            db,
            customerId,
            orderNumber,
            todayDate,
            totalPrice,
            now,
            auditUser,
            cancellationToken);

        await EnsureTodayDemoOrderItemsAsync(db, orderId, meals, now, auditUser, cancellationToken);

        var deliveryCalendarId = await EnsureTodayDemoDeliveryCalendarAsync(
            db,
            orderId,
            addressId,
            deliveryDateTime,
            now,
            auditUser,
            cancellationToken);

        await EnsureTodayDemoPaymentAsync(db, orderId, today, totalPrice, now, auditUser, cancellationToken);

        var productionPlanId = await EnsureTodayDemoProductionPlanAsync(db, todayDate, now, auditUser, cancellationToken);
        var productionPlanItemIds = await EnsureTodayDemoProductionPlanItemsAsync(
            db,
            productionPlanId,
            meals,
            now,
            auditUser,
            cancellationToken);

        await EnsureTodayDemoPackingAsync(
            db,
            today,
            todayDate,
            orderId,
            deliveryCalendarId,
            clientName,
            clientPublicId,
            meals,
            productionPlanItemIds,
            now,
            auditUser,
            cancellationToken);

        this.logger.LogInformation(
            "Ensured today demo lifecycle for {DemoDate}: order {OrderNumber}, {MealCount} meals, delivery calendar {DeliveryCalendarId}.",
            today,
            orderNumber,
            meals.Count,
            deliveryCalendarId);
    }

    private static async Task<int> EnsureDemoLifecycleMenuFoundationAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var categoryId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 [Id] FROM [Categories] WHERE [Name] = N'Demo lifecycle menu' ORDER BY [Id];",
            cancellationToken: cancellationToken));

        if (!categoryId.HasValue)
        {
            categoryId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO [Categories] ([Name], [Description], [SortOrder], [CreatedAt])
                VALUES (N'Demo lifecycle menu', N'Demo dishes for the current-day lifecycle seed.', 90, @now);
                SELECT CAST(SCOPE_IDENTITY() as int);
                """,
                new { now },
                cancellationToken: cancellationToken));
        }

        var dietId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [Diets]
            WHERE [Name] = N'Demo Lifecycle'
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            cancellationToken: cancellationToken));

        if (dietId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Diets]
                SET [Status] = N'Active',
                    [IsActive] = 1,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @dietId;
                """,
                new { dietId = dietId.Value, now, auditUser },
                cancellationToken: cancellationToken));
        }
        else
        {
            dietId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO [Diets]
                    ([Name], [Description], [MarketingDescription], [Status], [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (N'Demo Lifecycle', N'Dieta pokazowa do dzisiejszej sciezki zycia zamowienia.',
                     N'Zestaw demo od zamowienia po pakowanie.', N'Active', 1, @now, @auditUser, 0);
                SELECT CAST(SCOPE_IDENTITY() as int);
                """,
                new { now, auditUser },
                cancellationToken: cancellationToken));
        }

        var dietVariantId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [DietVariants]
            WHERE [DietId] = @dietId
              AND [Name] = N'Demo 1800'
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { dietId = dietId.Value },
            cancellationToken: cancellationToken));

        if (dietVariantId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [DietVariants]
                SET [TargetCalories] = 1800,
                    [PriceMultiplier] = 1.00,
                    [IsDefault] = 1,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @dietVariantId;
                """,
                new { dietVariantId = dietVariantId.Value, now, auditUser },
                cancellationToken: cancellationToken));
        }
        else
        {
            dietVariantId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO [DietVariants]
                    ([DietId], [Name], [TargetCalories], [PriceMultiplier], [IsDefault], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@dietId, N'Demo 1800', 1800, 1.00, 1, @now, @auditUser, 0);
                SELECT CAST(SCOPE_IDENTITY() as int);
                """,
                new { dietId = dietId.Value, now, auditUser },
                cancellationToken: cancellationToken));
        }

        var demoMeals = new[]
        {
            new { Name = "Demo sniadanie proteinowe", SlotOrder = 1, Calories = 420 },
            new { Name = "Demo przekaska owocowa", SlotOrder = 2, Calories = 260 },
            new { Name = "Demo obiad z lososiem", SlotOrder = 3, Calories = 560 },
            new { Name = "Demo podwieczorek fit", SlotOrder = 4, Calories = 240 },
            new { Name = "Demo kolacja bowl", SlotOrder = 5, Calories = 360 },
        };

        foreach (var demoMeal in demoMeals)
        {
            var mealId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
                """
                SELECT TOP 1 [Id]
                FROM [Meals]
                WHERE [Name] = @name
                  AND [IsDeleted] = 0
                ORDER BY [Id];
                """,
                new { name = demoMeal.Name },
                cancellationToken: cancellationToken));

            if (mealId.HasValue)
            {
                await db.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE [Meals]
                    SET [CategoryId] = @categoryId,
                        [Description] = N'Danie pokazowe do dzisiejszej sciezki zycia zamowienia.',
                        [Status] = N'Published',
                        [PreparationTimeMinutes] = 20,
                        [IsActive] = 1,
                        [UpdatedAt] = @now,
                        [UpdatedBy] = @auditUser,
                        [IsDeleted] = 0
                    WHERE [Id] = @mealId;
                    """,
                    new { mealId = mealId.Value, categoryId = categoryId.Value, now, auditUser },
                    cancellationToken: cancellationToken));
            }
            else
            {
                mealId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    INSERT INTO [Meals]
                        ([CategoryId], [Name], [Description], [Status], [PreparationTimeMinutes], [IsActive],
                         [CreatedAt], [CreatedBy], [IsDeleted])
                    VALUES
                        (@categoryId, @name, N'Danie pokazowe do dzisiejszej sciezki zycia zamowienia.',
                         N'Published', 20, 1, @now, @auditUser, 0);
                    SELECT CAST(SCOPE_IDENTITY() as int);
                    """,
                    new { categoryId = categoryId.Value, name = demoMeal.Name, now, auditUser },
                    cancellationToken: cancellationToken));
            }

            await db.ExecuteAsync(new CommandDefinition(
                """
                IF EXISTS
                (
                    SELECT 1
                    FROM [DietVariantMeals]
                    WHERE [DietVariantId] = @dietVariantId
                      AND [MealId] = @mealId
                )
                BEGIN
                    UPDATE [DietVariantMeals]
                    SET [ServingSizeMultiplier] = 1.00,
                        [SortOrder] = @sortOrder
                    WHERE [DietVariantId] = @dietVariantId
                      AND [MealId] = @mealId;
                END
                ELSE
                BEGIN
                    INSERT INTO [DietVariantMeals] ([DietVariantId], [MealId], [ServingSizeMultiplier], [SortOrder])
                    VALUES (@dietVariantId, @mealId, 1.00, @sortOrder);
                END
                """,
                new
                {
                    dietVariantId = dietVariantId.Value,
                    mealId = mealId.Value,
                    sortOrder = demoMeal.SlotOrder,
                },
                cancellationToken: cancellationToken));
        }

        return dietVariantId.Value;
    }

    private static async Task<int> EnsureDemoLifecycleCustomerAsync(IDbConnection db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        const string email = "demo-klient@kuchnia.local";

        var userId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT [Id] FROM [Users] WHERE [Email] = @email;",
            new { email },
            cancellationToken: cancellationToken));

        if (userId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Users]
                SET [FirstName] = N'Demo',
                    [LastName] = N'Klient',
                    [Role] = @role,
                    [UpdatedAt] = @now
                WHERE [Id] = @userId;
                """,
                new { userId = userId.Value, role = UserRoles.Client, now },
                cancellationToken: cancellationToken));

            return userId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Users] ([Email], [PasswordHash], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt])
            VALUES (@email, @passwordHash, N'Demo', N'Klient', @role, @now, NULL);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new
            {
                email,
                passwordHash = BCrypt.Net.BCrypt.HashPassword("Demo123!"),
                role = UserRoles.Client,
                now,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureDemoLifecycleAddressAsync(
        IDbConnection db,
        int customerId,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var addressId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [Addresses]
            WHERE [UserId] = @customerId
              AND [Label] = N'Demo lifecycle'
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { customerId },
            cancellationToken: cancellationToken));

        if (addressId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Addresses]
                SET [Street] = N'Lipowa',
                    [BuildingNumber] = N'1',
                    [ApartmentNumber] = N'2',
                    [City] = N'Bialystok',
                    [PostalCode] = N'15-424',
                    [IsDefault] = 1,
                    [DeliveryNotes] = N'Dane demo na dzisiejsza sciezke zycia zamowienia.',
                    [Latitude] = 53.1322,
                    [Longitude] = 23.1584,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @addressId;
                """,
                new { addressId = addressId.Value, now, auditUser },
                cancellationToken: cancellationToken));

            return addressId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Addresses]
                ([UserId], [Label], [Street], [BuildingNumber], [ApartmentNumber], [City], [PostalCode],
                 [IsDefault], [DeliveryNotes], [CreatedAt], [CreatedBy], [IsDeleted], [Latitude], [Longitude])
            VALUES
                (@customerId, N'Demo lifecycle', N'Lipowa', N'1', N'2', N'Bialystok', N'15-424',
                 1, N'Dane demo na dzisiejsza sciezke zycia zamowienia.', @now, @auditUser, 0, 53.1322, 23.1584);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new { customerId, now, auditUser },
            cancellationToken: cancellationToken));
    }

    private static async Task<string> EnsureDemoLifecycleCustomerProfileAsync(
        IDbConnection db,
        int customerId,
        int addressId,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var profileId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT [Id] FROM [CustomerProfiles] WHERE [UserId] = @customerId AND [IsDeleted] = 0;",
            new { customerId },
            cancellationToken: cancellationToken));

        if (profileId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [CustomerProfiles]
                SET [Phone] = N'+48 500 000 001',
                    [DietaryNotes] = N'Demo: kilka dan w jednym zamowieniu na dzisiaj.',
                    [DefaultAddressId] = @addressId,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @profileId;
                """,
                new { profileId = profileId.Value, addressId, now, auditUser },
                cancellationToken: cancellationToken));
        }
        else
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [CustomerProfiles]
                    ([UserId], [Phone], [DietaryNotes], [DefaultAddressId], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@customerId, N'+48 500 000 001', N'Demo: kilka dan w jednym zamowieniu na dzisiaj.', @addressId, @now, @auditUser, 0);
                """,
                new { customerId, addressId, now, auditUser },
                cancellationToken: cancellationToken));
        }

        var publicId = await db.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT LOWER(CONVERT(varchar(36), [PublicId]))
            FROM [CustomerProfiles]
            WHERE [UserId] = @customerId AND [IsDeleted] = 0;
            """,
            new { customerId },
            cancellationToken: cancellationToken));

        return string.IsNullOrWhiteSpace(publicId) ? $"demo-{customerId}" : publicId;
    }

    private static async Task<int> EnsureTodayDemoDietMenuPlanAsync(
        IDbConnection db,
        DateTime todayDate,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var planId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT [Id] FROM [DietMenuPlans] WHERE [PlanDate] = @todayDate;",
            new { todayDate },
            cancellationToken: cancellationToken));

        if (planId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [DietMenuPlans]
                SET [Status] = N'Published',
                    [Notes] = N'Dzisiejszy demo plan menu do sciezki zycia zamowienia.',
                    [PublishedAt] = COALESCE([PublishedAt], @now),
                    [PublishedBy] = COALESCE([PublishedBy], @auditUser),
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @planId;
                """,
                new { planId = planId.Value, now, auditUser },
                cancellationToken: cancellationToken));
        }
        else
        {
            planId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO [DietMenuPlans]
                    ([PlanDate], [Status], [Notes], [PublishedAt], [PublishedBy], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@todayDate, N'Published', N'Dzisiejszy demo plan menu do sciezki zycia zamowienia.', @now, @auditUser, @now, @auditUser, 0);
                SELECT CAST(SCOPE_IDENTITY() as int);
                """,
                new { todayDate, now, auditUser },
                cancellationToken: cancellationToken));
        }

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [DietMenuPlanItems]
                ([DietMenuPlanId], [DietVariantId], [MealId], [MealSlot], [ServingSizeMultiplier], [SortOrder],
                 [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                @planId,
                dvm.[DietVariantId],
                dvm.[MealId],
                CASE dvm.[SortOrder]
                    WHEN 1 THEN N'Breakfast'
                    WHEN 2 THEN N'Snack1'
                    WHEN 3 THEN N'Lunch'
                    WHEN 4 THEN N'Snack2'
                    WHEN 5 THEN N'Dinner'
                    ELSE CONCAT(N'Meal', dvm.[SortOrder])
                END,
                dvm.[ServingSizeMultiplier],
                dvm.[SortOrder],
                1,
                @now,
                @auditUser,
                0
            FROM [DietVariantMeals] dvm
            WHERE NOT EXISTS
            (
                SELECT 1
                FROM [DietMenuPlanItems] existing
                WHERE existing.[DietMenuPlanId] = @planId
                  AND existing.[DietVariantId] = dvm.[DietVariantId]
                  AND existing.[MealId] = dvm.[MealId]
                  AND existing.[SortOrder] = dvm.[SortOrder]
                  AND existing.[IsDeleted] = 0
            );
            """,
            new { planId = planId.Value, now, auditUser },
            cancellationToken: cancellationToken));

        return planId.Value;
    }

    private static Task<IEnumerable<DemoLifecycleMeal>> GetTodayDemoMealsAsync(
        IDbConnection db,
        int dietMenuPlanId,
        int preferredDietVariantId,
        CancellationToken cancellationToken)
        => db.QueryAsync<DemoLifecycleMeal>(new CommandDefinition(
            """
            SELECT TOP (5)
                dpi.[Id] AS DietMenuPlanItemId,
                dpi.[MealId],
                m.[Name] AS MealName,
                dpi.[DietVariantId],
                dv.[Name] AS VariantName,
                dv.[TargetCalories] AS CaloriesPerDay,
                d.[Id] AS DietId,
                d.[Name] AS DietName,
                dpi.[SortOrder],
                dpi.[MealSlot],
                CAST(24.90 + (dpi.[SortOrder] * 6.00) AS decimal(10,2)) AS PricePerDay
            FROM [DietMenuPlanItems] dpi
            INNER JOIN [Meals] m ON m.[Id] = dpi.[MealId]
            INNER JOIN [DietVariants] dv ON dv.[Id] = dpi.[DietVariantId]
            INNER JOIN [Diets] d ON d.[Id] = dv.[DietId]
            WHERE dpi.[DietMenuPlanId] = @dietMenuPlanId
              AND dpi.[IsActive] = 1
              AND dpi.[IsDeleted] = 0
              AND m.[IsDeleted] = 0
              AND dv.[IsDeleted] = 0
              AND d.[IsDeleted] = 0
            ORDER BY
                CASE WHEN dpi.[DietVariantId] = @preferredDietVariantId THEN 0 ELSE 1 END,
                dpi.[SortOrder],
                dpi.[Id];
            """,
            new { dietMenuPlanId, preferredDietVariantId },
            cancellationToken: cancellationToken));

    private static async Task<int> EnsureTodayDemoOrderAsync(
        IDbConnection db,
        int customerId,
        string orderNumber,
        DateTime todayDate,
        decimal totalPrice,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var orderId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT [Id] FROM [Orders] WHERE [OrderNumber] = @orderNumber;",
            new { orderNumber },
            cancellationToken: cancellationToken));

        if (orderId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Orders]
                SET [CustomerId] = @customerId,
                    [Status] = @status,
                    [TotalPrice] = @totalPrice,
                    [DiscountAmount] = 0,
                    [FinalPrice] = @totalPrice,
                    [Notes] = N'Demo lifecycle: dzisiejsze zamowienie z kilkoma daniami.',
                    [StartDate] = @todayDate,
                    [EndDate] = @todayDate,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @orderId;
                """,
                new
                {
                    orderId = orderId.Value,
                    customerId,
                    status = (int)OrderStatus.Paid,
                    totalPrice,
                    todayDate,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));

            return orderId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Orders]
                ([CustomerId], [OrderNumber], [Status], [TotalPrice], [DiscountAmount], [FinalPrice], [Notes],
                 [StartDate], [EndDate], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@customerId, @orderNumber, @status, @totalPrice, 0, @totalPrice, N'Demo lifecycle: dzisiejsze zamowienie z kilkoma daniami.',
                 @todayDate, @todayDate, @now, @auditUser, 0);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new
            {
                customerId,
                orderNumber,
                status = (int)OrderStatus.Paid,
                totalPrice,
                todayDate,
                now,
                auditUser,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureTodayDemoOrderItemsAsync(
        IDbConnection db,
        int orderId,
        IReadOnlyList<DemoLifecycleMeal> meals,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [OrderItems] WHERE [OrderId] = @orderId;",
            new { orderId },
            cancellationToken: cancellationToken));

        foreach (var meal in meals)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [OrderItems]
                    ([OrderId], [DietId], [DietVariantId], [DietName], [VariantName], [CaloriesPerDay],
                     [PricePerDay], [TotalDays], [TotalPrice], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@orderId, @dietId, @dietVariantId, @dietName, @variantName, @caloriesPerDay,
                     @pricePerDay, 1, @pricePerDay, @now, @auditUser, 0);
                """,
                new
                {
                    orderId,
                    dietId = meal.DietId,
                    dietVariantId = meal.DietVariantId,
                    dietName = $"{meal.DietName}: {meal.MealName}",
                    variantName = $"{meal.VariantName} / {meal.MealSlot}",
                    caloriesPerDay = meal.CaloriesPerDay,
                    pricePerDay = meal.PricePerDay,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));
        }
    }

    private static async Task<int> EnsureTodayDemoDeliveryCalendarAsync(
        IDbConnection db,
        int orderId,
        int addressId,
        DateTime deliveryDateTime,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var deliveryCalendarId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [DeliveryCalendar]
            WHERE [OrderId] = @orderId
              AND [DeliveryDate] >= @from
              AND [DeliveryDate] < @to
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { orderId, from = deliveryDateTime.Date, to = deliveryDateTime.Date.AddDays(1) },
            cancellationToken: cancellationToken));

        var deliveryWindowId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 [Id] FROM [DeliveryWindows] WHERE [IsActive] = 1 ORDER BY [SortOrder], [Id];",
            cancellationToken: cancellationToken));

        if (deliveryCalendarId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [DeliveryCalendar]
                SET [AddressId] = @addressId,
                    [DeliveryWindowId] = @deliveryWindowId,
                    [DeliveryDate] = @deliveryDateTime,
                    [Status] = @status,
                    [IsSkipped] = 0,
                    [SkipReason] = NULL,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @deliveryCalendarId;
                """,
                new
                {
                    deliveryCalendarId = deliveryCalendarId.Value,
                    addressId,
                    deliveryWindowId,
                    deliveryDateTime,
                    status = (int)DeliveryStatus.Scheduled,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));

            return deliveryCalendarId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [DeliveryCalendar]
                ([OrderId], [AddressId], [DeliveryWindowId], [DeliveryDate], [Status], [IsSkipped],
                 [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@orderId, @addressId, @deliveryWindowId, @deliveryDateTime, @status, 0, @now, @auditUser, 0);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new
            {
                orderId,
                addressId,
                deliveryWindowId,
                deliveryDateTime,
                status = (int)DeliveryStatus.Scheduled,
                now,
                auditUser,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureTodayDemoPaymentAsync(
        IDbConnection db,
        int orderId,
        DateOnly today,
        decimal amount,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var paymentIntentId = $"pi_demo_lifecycle_{today:yyyyMMdd}";
        var paymentId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT [Id] FROM [Payments] WHERE [StripePaymentIntentId] = @paymentIntentId;",
            new { paymentIntentId },
            cancellationToken: cancellationToken));

        if (paymentId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Payments]
                SET [OrderId] = @orderId,
                    [Amount] = @amount,
                    [Currency] = N'PLN',
                    [Status] = @status,
                    [AttemptCount] = 1,
                    [LastAttemptAt] = @now,
                    [PaidAt] = @now,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @paymentId;
                """,
                new
                {
                    paymentId = paymentId.Value,
                    orderId,
                    amount,
                    status = (int)PaymentStatus.Succeeded,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));

            return;
        }

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Payments]
                ([OrderId], [StripePaymentIntentId], [StripeClientSecret], [Amount], [Currency], [Status],
                 [AttemptCount], [LastAttemptAt], [PaidAt], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@orderId, @paymentIntentId, @clientSecret, @amount, N'PLN', @status,
                 1, @now, @now, @now, @auditUser, 0);
            """,
            new
            {
                orderId,
                paymentIntentId,
                clientSecret = $"{paymentIntentId}_secret_demo",
                amount,
                status = (int)PaymentStatus.Succeeded,
                now,
                auditUser,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureTodayDemoProductionPlanAsync(
        IDbConnection db,
        DateTime todayDate,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var productionPlanId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [ProductionPlans]
            WHERE [ProductionDate] = @todayDate
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { todayDate },
            cancellationToken: cancellationToken));

        if (productionPlanId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [ProductionPlans]
                SET [Status] = @status,
                    [Notes] = N'Demo lifecycle: produkcja dzisiejszego zamowienia pokazowego.',
                    [IsSharedWithLogistics] = 1,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @productionPlanId;
                """,
                new
                {
                    productionPlanId = productionPlanId.Value,
                    status = (int)ProductionPlanStatus.InProgress,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));

            return productionPlanId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [ProductionPlans]
                ([ProductionDate], [Status], [Notes], [IsSharedWithLogistics], [CreatedBy], [CreatedAt], [IsDeleted])
            VALUES
                (@todayDate, @status, N'Demo lifecycle: produkcja dzisiejszego zamowienia pokazowego.', 1, @auditUser, @now, 0);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new
            {
                todayDate,
                status = (int)ProductionPlanStatus.InProgress,
                auditUser,
                now,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task<Dictionary<int, int>> EnsureTodayDemoProductionPlanItemsAsync(
        IDbConnection db,
        int productionPlanId,
        IReadOnlyList<DemoLifecycleMeal> meals,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, int>();

        foreach (var meal in meals)
        {
            var existingItemId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
                """
                SELECT TOP 1 [Id]
                FROM [ProductionPlanItems]
                WHERE [ProductionPlanId] = @productionPlanId
                  AND [MealId] = @mealId
                  AND [DietVariantId] = @dietVariantId
                  AND [IsDeleted] = 0
                ORDER BY [Id];
                """,
                new { productionPlanId, mealId = meal.MealId, dietVariantId = meal.DietVariantId },
                cancellationToken: cancellationToken));

            var plannedQuantity = meal.SortOrder <= 2 ? 18 : 12;
            var cookedQuantity = meal.SortOrder <= 2 ? plannedQuantity : 0;
            var itemStatus = meal.SortOrder <= 2 ? ProductionItemStatus.Cooked : ProductionItemStatus.Planned;
            var productionGroup = meal.SortOrder <= 2 ? 1 : meal.SortOrder <= 4 ? 2 : 3;
            var estimatedReadyTime = TimeSpan.FromHours(6 + meal.SortOrder);
            var actualReadyTime = itemStatus == ProductionItemStatus.Cooked
                ? estimatedReadyTime.Add(TimeSpan.FromMinutes(5))
                : (TimeSpan?)null;

            if (existingItemId.HasValue)
            {
                await db.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE [ProductionPlanItems]
                    SET [MealName] = @mealName,
                        [PlannedQuantity] = @plannedQuantity,
                        [CookedQuantity] = @cookedQuantity,
                        [Status] = @status,
                        [ProductionGroup] = @productionGroup,
                        [EstimatedReadyTime] = @estimatedReadyTime,
                        [ActualReadyTime] = @actualReadyTime,
                        [DietMenuPlanItemId] = @dietMenuPlanItemId,
                        [UpdatedAt] = @now,
                        [UpdatedBy] = @auditUser,
                        [IsDeleted] = 0
                    WHERE [Id] = @itemId;
                    """,
                    new
                    {
                        itemId = existingItemId.Value,
                        mealName = meal.MealName,
                        plannedQuantity,
                        cookedQuantity,
                        status = (int)itemStatus,
                        productionGroup,
                        estimatedReadyTime,
                        actualReadyTime,
                        dietMenuPlanItemId = meal.DietMenuPlanItemId,
                        now,
                        auditUser,
                    },
                    cancellationToken: cancellationToken));

                result[meal.MealId] = existingItemId.Value;
                continue;
            }

            var insertedItemId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO [ProductionPlanItems]
                    ([ProductionPlanId], [MealId], [MealName], [DietVariantId], [PlannedQuantity], [CookedQuantity],
                     [Status], [ProductionGroup], [EstimatedReadyTime], [ActualReadyTime], [DietMenuPlanItemId],
                     [CreatedBy], [CreatedAt], [IsDeleted])
                VALUES
                    (@productionPlanId, @mealId, @mealName, @dietVariantId, @plannedQuantity, @cookedQuantity,
                     @status, @productionGroup, @estimatedReadyTime, @actualReadyTime, @dietMenuPlanItemId,
                     @auditUser, @now, 0);
                SELECT CAST(SCOPE_IDENTITY() as int);
                """,
                new
                {
                    productionPlanId,
                    mealId = meal.MealId,
                    mealName = meal.MealName,
                    dietVariantId = meal.DietVariantId,
                    plannedQuantity,
                    cookedQuantity,
                    status = (int)itemStatus,
                    productionGroup,
                    estimatedReadyTime,
                    actualReadyTime,
                    dietMenuPlanItemId = meal.DietMenuPlanItemId,
                    auditUser,
                    now,
                },
                cancellationToken: cancellationToken));

            result[meal.MealId] = insertedItemId;
        }

        return result;
    }

    private static async Task EnsureTodayDemoPackingAsync(
        IDbConnection db,
        DateOnly today,
        DateTime todayDate,
        int orderId,
        int deliveryCalendarId,
        string clientName,
        string clientPublicId,
        IReadOnlyList<DemoLifecycleMeal> meals,
        IReadOnlyDictionary<int, int> productionPlanItemIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var sessionId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [PackingSessions]
            WHERE [PackingDate] = @todayDate
              AND [OrderId] = @orderId
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { todayDate, orderId },
            cancellationToken: cancellationToken));

        if (sessionId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [PackingSessions]
                SET [ClientName] = @clientName,
                    [ClientPublicId] = @clientPublicId,
                    [PackedBy] = N'DemoPacker',
                    [Status] = @status,
                    [DeliveryCalendarId] = @deliveryCalendarId,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @sessionId;
                """,
                new
                {
                    sessionId = sessionId.Value,
                    clientName,
                    clientPublicId,
                    status = (int)PackingStatus.Labeled,
                    deliveryCalendarId,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));
        }
        else
        {
            sessionId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO [PackingSessions]
                    ([PackingDate], [OrderId], [ClientName], [ClientPublicId], [PackedBy], [Status],
                     [DeliveryCalendarId], [CreatedBy], [CreatedAt], [IsDeleted])
                VALUES
                    (@todayDate, @orderId, @clientName, @clientPublicId, N'DemoPacker', @status,
                     @deliveryCalendarId, @auditUser, @now, 0);
                SELECT CAST(SCOPE_IDENTITY() as int);
                """,
                new
                {
                    todayDate,
                    orderId,
                    clientName,
                    clientPublicId,
                    status = (int)PackingStatus.Labeled,
                    deliveryCalendarId,
                    auditUser,
                    now,
                },
                cancellationToken: cancellationToken));
        }

        var packingBagId = await EnsureTodayDemoPackingBagAsync(
            db,
            sessionId.Value,
            today,
            now,
            auditUser,
            cancellationToken);

        await db.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM [BoxLabels]
            WHERE [PackingItemId] IN
            (
                SELECT [Id]
                FROM [PackingItems]
                WHERE [PackingSessionId] = @sessionId
            );

            DELETE FROM [PackingLabels]
            WHERE [PackingSessionId] = @sessionId
               OR [PackingItemId] IN
               (
                   SELECT [Id]
                   FROM [PackingItems]
                   WHERE [PackingSessionId] = @sessionId
               );

            DELETE FROM [PackingItems]
            WHERE [PackingSessionId] = @sessionId;
            """,
            new { sessionId = sessionId.Value },
            cancellationToken: cancellationToken));

        foreach (var meal in meals)
        {
            productionPlanItemIds.TryGetValue(meal.MealId, out var productionPlanItemId);
            var boxCode = $"DEMO-BOX-{today:yyyyMMdd}-{orderId}-{meal.SortOrder:D2}";
            var itemStatus = meal.SortOrder <= 2 ? PackingItemStatus.Packed : PackingItemStatus.FoilPrinted;
            var packedAt = itemStatus == PackingItemStatus.Packed ? now.AddMinutes(-10) : (DateTimeOffset?)null;

            var packingItemId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO [PackingItems]
                    ([PackingSessionId], [PackingBagId], [ProductionPlanItemId], [MealId], [MealName], [DietVariantId],
                     [BoxCode], [Status], [ExpiryDate], [FoilPrintedAt], [PackedAt], [PackedBy],
                     [CreatedBy], [CreatedAt], [IsDeleted])
                VALUES
                    (@sessionId, @packingBagId, @productionPlanItemId, @mealId, @mealName, @dietVariantId,
                     @boxCode, @status, @expiryDate, @foilPrintedAt, @packedAt, @packedBy,
                     @auditUser, @now, 0);
                SELECT CAST(SCOPE_IDENTITY() as int);
                """,
                new
                {
                    sessionId = sessionId.Value,
                    packingBagId,
                    productionPlanItemId = productionPlanItemId == 0 ? (int?)null : productionPlanItemId,
                    mealId = meal.MealId,
                    mealName = meal.MealName,
                    dietVariantId = meal.DietVariantId,
                    boxCode,
                    status = (int)itemStatus,
                    expiryDate = now.AddDays(1),
                    foilPrintedAt = now.AddMinutes(-30),
                    packedAt,
                    packedBy = itemStatus == PackingItemStatus.Packed ? "DemoPacker" : null,
                    auditUser,
                    now,
                },
                cancellationToken: cancellationToken));

            var productLabelDataJson = CreateDemoProductLabelData(
                today,
                orderId,
                deliveryCalendarId,
                packingItemId,
                packingBagId,
                meal);

            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [PackingLabels]
                    ([PackingItemId], [PackingSessionId], [LabelType], [QrCode], [DishName], [Allergens], [Kcal],
                     [ClientName], [PrintNumber], [LabelDataJson], [PrintedAt], [PrintedBy], [CreatedAt])
                VALUES
                    (@packingItemId, NULL, @labelType, @qrCode, @dishName, @allergens, @kcal,
                     @clientName, 1, @labelDataJson, @now, @auditUser, @now);
                """,
                new
                {
                    packingItemId,
                    labelType = 0,
                    qrCode = boxCode,
                    dishName = meal.MealName,
                    allergens = GetDemoAllergens(meal.MealId),
                    kcal = Math.Max(1, meal.CaloriesPerDay / 5),
                    clientName,
                    labelDataJson = productLabelDataJson,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));

            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [BoxLabels]
                    ([PackingItemId], [QrCode], [LabelDataJson], [PrintNumber], [PrintedAt], [PrintedBy], [CreatedAt])
                VALUES
                    (@packingItemId, @qrCode, @labelDataJson, 1, @now, @auditUser, @now);
                """,
                new
                {
                    packingItemId,
                    qrCode = boxCode,
                    labelDataJson = productLabelDataJson,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));
        }

        var shippingCode = $"DEMO-BAG-{today:yyyyMMdd}-{sessionId.Value:D3}";
        var routeInfo = $"Oczekuje na trase M4 (DeliveryCalendar #{deliveryCalendarId})";
        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [PackingLabels]
                ([PackingItemId], [PackingSessionId], [PackingBagId], [LabelType], [QrCode], [ClientName], [RouteInfo],
                 [DeliveryWindow], [MealsList], [PrintNumber], [LabelDataJson], [PrintedAt], [PrintedBy],
                 [AttachedAt], [AttachedBy], [CreatedAt])
            VALUES
                (NULL, @sessionId, @packingBagId, @labelType, @qrCode, @clientName, @routeInfo,
                 N'06:00-10:00', @mealsList, 1, @labelDataJson, @now, @auditUser,
                 @now, @auditUser, @now);
            """,
            new
            {
                sessionId = sessionId.Value,
                packingBagId,
                labelType = 1,
                qrCode = shippingCode,
                clientName,
                routeInfo,
                mealsList = string.Join(", ", meals.Select(meal => meal.MealName)),
                labelDataJson = CreateDemoTransportLabelData(
                    today,
                    orderId,
                    deliveryCalendarId,
                    packingBagId,
                    shippingCode,
                    routeInfo,
                    meals),
                now,
                auditUser,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureTodayDemoPackingBagAsync(
        IDbConnection db,
        int sessionId,
        DateOnly today,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var bagCode = $"DEMO-BAG-{today:yyyyMMdd}-{sessionId:D3}";
        var bagId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [PackingBags]
            WHERE [PackingSessionId] = @sessionId
              AND [BagNumber] = 1
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { sessionId },
            cancellationToken: cancellationToken));

        if (bagId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [PackingBags]
                SET [BagCode] = @bagCode,
                    [Status] = @status,
                    [PackedAt] = @now,
                    [PackedBy] = @auditUser,
                    [LabeledAt] = @now,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @bagId;
                """,
                new
                {
                    bagId = bagId.Value,
                    bagCode,
                    status = (int)PackingBagStatus.Labeled,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));

            return bagId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [PackingBags]
                ([PackingSessionId], [BagNumber], [BagCode], [Status], [PackedAt], [PackedBy],
                 [LabeledAt], [CreatedBy], [CreatedAt], [IsDeleted])
            VALUES
                (@sessionId, 1, @bagCode, @status, @now, @auditUser,
                 @now, @auditUser, @now, 0);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new
            {
                sessionId,
                bagCode,
                status = (int)PackingBagStatus.Labeled,
                now,
                auditUser,
            },
            cancellationToken: cancellationToken));
    }

    private static string GetDemoAllergens(int mealId)
        => mealId switch
        {
            1 => "Jaja",
            2 => "Laktoza",
            3 => "Gluten, Laktoza",
            4 => "Ryby",
            _ => "Brak",
        };

    private static string CreateDemoProductLabelData(
        DateOnly today,
        int orderId,
        int deliveryCalendarId,
        int packingItemId,
        int packingBagId,
        DemoLifecycleMeal meal)
        => JsonSerializer.Serialize(new
        {
            source = "DemoLifecycle",
            type = "FoilLabel",
            date = today.ToString("yyyy-MM-dd"),
            orderId,
            deliveryCalendarId,
            packingItemId,
            packingBagId,
            meal.MealId,
            meal.MealName,
            meal.DietVariantId,
            meal.DietName,
            meal.VariantName,
            meal.MealSlot,
            allergens = GetDemoAllergens(meal.MealId),
            kcal = Math.Max(1, meal.CaloriesPerDay / 5),
        });

    private static string CreateDemoTransportLabelData(
        DateOnly today,
        int orderId,
        int deliveryCalendarId,
        int packingBagId,
        string shippingCode,
        string routeInfo,
        IReadOnlyList<DemoLifecycleMeal> meals)
        => JsonSerializer.Serialize(new
        {
            source = "DemoLifecycle",
            type = "TransportLabel",
            date = today.ToString("yyyy-MM-dd"),
            orderId,
            deliveryCalendarId,
            packingBagId,
            qrCode = shippingCode,
            routeInfo,
            meals = meals
                .OrderBy(meal => meal.SortOrder)
                .Select(meal => new
                {
                    meal.MealId,
                    meal.MealName,
                    meal.DietVariantId,
                    meal.DietName,
                    meal.VariantName,
                    meal.MealSlot,
                }),
        });

    private sealed class DemoLifecycleMeal
    {
        public int DietMenuPlanItemId { get; set; }

        public int MealId { get; set; }

        public string MealName { get; set; } = string.Empty;

        public int DietVariantId { get; set; }

        public string VariantName { get; set; } = string.Empty;

        public int CaloriesPerDay { get; set; }

        public int DietId { get; set; }

        public string DietName { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public string MealSlot { get; set; } = string.Empty;

        public decimal PricePerDay { get; set; }
    }

    private sealed record LogisticsDemoVehicle(
        string RegistrationNumber,
        string Model,
        decimal MaxLoadKg,
        VehicleStatus Status);

    private sealed record LogisticsDemoDriver(
        string Email,
        string FirstName,
        string LastName,
        string LicenseNumber,
        string VehicleRegistration);

    private sealed record LogisticsDemoDelivery(
        string FirstName,
        string LastName,
        string Street,
        string BuildingNumber,
        string? ApartmentNumber,
        string PostalCode,
        double Latitude,
        double Longitude);

    private static Task<int> CountRowsAsync(IDbConnection db, string tableName, CancellationToken cancellationToken)
        => db.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM [{tableName}];",
            cancellationToken: cancellationToken));
}
