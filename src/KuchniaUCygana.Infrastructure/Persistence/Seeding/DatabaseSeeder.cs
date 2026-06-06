using System.Data;
using BCrypt.Net;
using Dapper;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Services;
using KuchniaUCygana.Infrastructure.Adapters;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Infrastructure.Persistence.Repositories;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Production;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Infrastructure.Persistence.Seeding;

public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private const int PackagingWarehouseCategoryId = 5;

    private readonly IDbConnectionFactory connectionFactory;
    private readonly ILogger<DatabaseSeeder> logger;

    public DatabaseSeeder(IDbConnectionFactory connectionFactory, ILogger<DatabaseSeeder> logger)
    {
        this.connectionFactory = connectionFactory;
        this.logger = logger;
    }

    public async Task SeedAsync(
        DatabaseSeedingProfile profile,
        bool resetDemoData = false,
        CancellationToken cancellationToken = default)
    {
        switch (profile)
        {
            case DatabaseSeedingProfile.MinimalRealistic:
                await SeedMinimalRealisticAsync(cancellationToken);
                return;
            case DatabaseSeedingProfile.DemoData:
                await SeedMinimalRealisticAsync(cancellationToken);
                if (resetDemoData)
                {
                    await ResetDemoDataAsync(cancellationToken);
                }

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
        await SeedPersonnelAsync(db, now, cancellationToken);
    }

    private async Task SeedUsersAsync(IDbConnection db, DateTimeOffset now, CancellationToken cancellationToken)
    {
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
            MakeUser("logistics@kuchnia.local", "Logistics123!", "Logistyka", "Operator", UserRoles.Logistics, now),
            MakeUser("logisticsm@kuchnia.local", "Logistics123!", "Logistyka", "Kierownik", UserRoles.LogisticsManager, now),
            MakeUser("driver@kuchnia.local", "Driver123!", "Dostawa", "Kierowca", UserRoles.Driver, now),
            MakeUser("hr@kuchnia.local", "HR123!", "HR", "Specjalista", UserRoles.HR, now),
            MakeUser("hrm@kuchnia.local", "HR123!", "HR", "Kierownik", UserRoles.HRManager, now),
            MakeUser("bok@kuchnia.local", "BOK123!", "BOK", "Konsultant", UserRoles.BOK, now),
            MakeUser("bokm@kuchnia.local", "BOK123!", "BOK", "Kierownik", UserRoles.BOKManager, now),
        };

        var inserted = await db.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = @Email)
            BEGIN
                INSERT INTO [Users] ([Email], [PasswordHash], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt])
                VALUES (@Email, @PasswordHash, @FirstName, @LastName, @Role, @CreatedAt, @UpdatedAt);
            END
            """,
            users,
            cancellationToken: cancellationToken));
        this.logger.LogInformation("Ensured {Count} seed users with all M3/M4/M5 roles. Inserted {Inserted}.", users.Length, inserted);
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

    private async Task SeedPersonnelAsync(IDbConnection db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var departments = new[]
        {
            new { Name = "Kuchnia", Description = "Produkcja posilkow i karty gotowania." },
            new { Name = "Magazyn", Description = "Stany, przyjecia i HACCP." },
            new { Name = "Kompletacja", Description = "Pakowanie, etykiety i zaladunek." },
            new { Name = "Diety", Description = "Diety, posilki i przepisy." },
            new { Name = "Logistyka", Description = "Trasy, flota i kierowcy." },
            new { Name = "HR", Description = "Kadry, urlopy i grafik." },
            new { Name = "BOK", Description = "Obsluga klienta i zgloszenia." },
            new { Name = "Administracja", Description = "Role i konfiguracja systemu." },
        };

        await db.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM [Departments] WHERE [Name] = @Name)
            BEGIN
                INSERT INTO [Departments] ([Name], [Description], [HeadEmployeeId], [CreatedAt], [UpdatedAt])
                VALUES (@Name, @Description, NULL, @CreatedAt, NULL);
            END
            """,
            departments.Select(department => new
            {
                department.Name,
                department.Description,
                CreatedAt = now,
            }),
            cancellationToken: cancellationToken));

        var employees = new[]
        {
            new { Email = "admin@kuchnia.local", DepartmentName = "Administracja", Position = "Administrator systemu" },
            new { Email = "kitchen@kuchnia.local", DepartmentName = "Kuchnia", Position = "Kucharz" },
            new { Email = "kitchenm@kuchnia.local", DepartmentName = "Kuchnia", Position = "Szef kuchni" },
            new { Email = "warehouse@kuchnia.local", DepartmentName = "Magazyn", Position = "Magazynier" },
            new { Email = "warehousem@kuchnia.local", DepartmentName = "Magazyn", Position = "Kierownik magazynu" },
            new { Email = "packing@kuchnia.local", DepartmentName = "Kompletacja", Position = "Pakowacz" },
            new { Email = "packingm@kuchnia.local", DepartmentName = "Kompletacja", Position = "Kierownik kompletacji" },
            new { Email = "dietitian@kuchnia.local", DepartmentName = "Diety", Position = "Dietetyk" },
            new { Email = "logistics@kuchnia.local", DepartmentName = "Logistyka", Position = "Logistyk" },
            new { Email = "logisticsm@kuchnia.local", DepartmentName = "Logistyka", Position = "Kierownik logistyki" },
            new { Email = "driver@kuchnia.local", DepartmentName = "Logistyka", Position = "Kierowca" },
            new { Email = "hr@kuchnia.local", DepartmentName = "HR", Position = "Specjalista HR" },
            new { Email = "hrm@kuchnia.local", DepartmentName = "HR", Position = "Kierownik HR" },
            new { Email = "bok@kuchnia.local", DepartmentName = "BOK", Position = "Konsultant BOK" },
            new { Email = "bokm@kuchnia.local", DepartmentName = "BOK", Position = "Kierownik BOK" },
        };

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Employees]
                ([UserId], [FirstName], [LastName], [Email], [PhoneNumber], [HireDate], [TerminationDate],
                 [DepartmentId], [Position], [IsActive], [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt],
                 [DeletedBy], [CreatedAt], [UpdatedAt])
            SELECT
                u.[Id], u.[FirstName], u.[LastName], u.[Email], NULL, @HireDate, NULL,
                d.[Id], @Position, 1, @CreatedBy, NULL, 0, NULL, NULL, @CreatedAt, NULL
            FROM [Users] u
            INNER JOIN [Departments] d ON d.[Name] = @DepartmentName
            WHERE u.[Email] = @Email
              AND NOT EXISTS (SELECT 1 FROM [Employees] e WHERE e.[UserId] = u.[Id]);
            """,
            employees.Select(employee => new
            {
                employee.Email,
                employee.DepartmentName,
                employee.Position,
                HireDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-3)),
                CreatedBy = "DatabaseSeeder",
                CreatedAt = now,
            }),
            cancellationToken: cancellationToken));

        var shifts = new[]
        {
            new { Email = "kitchen@kuchnia.local", ShiftDate = DateOnly.FromDateTime(DateTime.Today), Shift = WorkShift.Morning, RoleAtShift = "Produkcja sniadan" },
            new { Email = "warehouse@kuchnia.local", ShiftDate = DateOnly.FromDateTime(DateTime.Today), Shift = WorkShift.Morning, RoleAtShift = "Przyjecia dostaw" },
            new { Email = "packing@kuchnia.local", ShiftDate = DateOnly.FromDateTime(DateTime.Today), Shift = WorkShift.Evening, RoleAtShift = "Kompletacja tras" },
            new { Email = "driver@kuchnia.local", ShiftDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)), Shift = WorkShift.Morning, RoleAtShift = "Trasa poranna" },
            new { Email = "bok@kuchnia.local", ShiftDate = DateOnly.FromDateTime(DateTime.Today), Shift = WorkShift.Morning, RoleAtShift = "Kolejka BOK" },
            new { Email = "hr@kuchnia.local", ShiftDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)), Shift = WorkShift.Morning, RoleAtShift = "Dyzurowanie HR" },
        };

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [WorkSchedules]
                ([UserId], [ShiftDate], [Shift], [RoleAtShift], [CreatedBy], [UpdatedBy], [IsDeleted],
                 [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            SELECT
                u.[Id], @ShiftDate, @Shift, @RoleAtShift, @CreatedBy, NULL, 0, NULL, NULL, @CreatedAt, NULL
            FROM [Users] u
            WHERE u.[Email] = @Email
              AND NOT EXISTS (
                    SELECT 1
                    FROM [WorkSchedules] ws
                    WHERE ws.[UserId] = u.[Id]
                      AND ws.[ShiftDate] = @ShiftDate
                      AND ws.[Shift] = @Shift
                      AND ws.[IsDeleted] = 0);
            """,
            shifts.Select(shift => new
            {
                shift.Email,
                shift.ShiftDate,
                Shift = (int)shift.Shift,
                shift.RoleAtShift,
                CreatedBy = "DatabaseSeeder",
                CreatedAt = now,
            }),
            cancellationToken: cancellationToken));

        var leaveRequests = new[]
        {
            new { Email = "kitchen@kuchnia.local", StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7)), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(8)), LeaveType = 0, Status = 0 },
            new { Email = "warehouse@kuchnia.local", StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)), EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)), LeaveType = 0, Status = 1 },
        };

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [LeaveRequests]
                ([EmployeeId], [LeaveType], [StartDate], [EndDate], [Status], [ApprovedByEmployeeId],
                 [RejectionReason], [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy],
                 [CreatedAt], [UpdatedAt])
            SELECT
                e.[Id], @LeaveType, @StartDate, @EndDate, @Status,
                CASE WHEN @Status = 1 THEN approver.[Id] ELSE NULL END,
                NULL, @CreatedBy, NULL, 0, NULL, NULL, @CreatedAt, NULL
            FROM [Employees] e
            INNER JOIN [Users] u ON u.[Id] = e.[UserId]
            OUTER APPLY (
                SELECT TOP 1 hr.[Id]
                FROM [Employees] hr
                INNER JOIN [Departments] d ON d.[Id] = hr.[DepartmentId]
                WHERE d.[Name] = 'HR'
                ORDER BY hr.[Id]
            ) approver
            WHERE u.[Email] = @Email
              AND NOT EXISTS (
                    SELECT 1
                    FROM [LeaveRequests] lr
                    WHERE lr.[EmployeeId] = e.[Id]
                      AND lr.[StartDate] = @StartDate
                      AND lr.[EndDate] = @EndDate
                      AND lr.[IsDeleted] = 0);
            """,
            leaveRequests.Select(request => new
            {
                request.Email,
                request.StartDate,
                request.EndDate,
                request.LeaveType,
                request.Status,
                CreatedBy = "DatabaseSeeder",
                CreatedAt = now,
            }),
            cancellationToken: cancellationToken));

        this.logger.LogInformation("Ensured minimal HR personnel, schedules and leave requests.");
    }

    private async Task SeedDemoDataAsync(CancellationToken cancellationToken)
    {
        using var db = connectionFactory.CreateConnection();
        var now = DateTimeOffset.UtcNow;
        var auditUser = "DemoSeeder";

        await SeedUnitsOfMeasureAsync(db, now, cancellationToken);
        await SeedStockItemsAsync(db, now, auditUser, cancellationToken);
        await SeedBatchesAsync(db, now, auditUser, cancellationToken);
        await EnsureDemoPackagingInventoryAsync(db, now, auditUser, cancellationToken);
        await SeedTemperatureLogsAsync(db, now, auditUser, cancellationToken);
        await SeedMenuAsync(db, now, auditUser, cancellationToken);
        await EnsureIngredientWarehouseMappingsAsync(db, cancellationToken);
        await EnsureDemoPackagingRequirementsAsync(db, now, auditUser, cancellationToken);
        await EnsureDemoRecipeComponentsAsync(db, now, auditUser, cancellationToken);
        await EnsureDefaultMealVariantsAsync(db, now, auditUser, cancellationToken);
        await EnsureDemoMealVariantComponentsAsync(db, now, auditUser, cancellationToken);
        await EnsureDietMenuPlanItemsHaveMealVariantsAsync(db, now, auditUser, cancellationToken);
        await SeedUnifiedDemoScenarioAsync(db, now, auditUser, cancellationToken);
    }

    private async Task ResetDemoDataAsync(CancellationToken cancellationToken)
    {
        using var db = connectionFactory.CreateConnection();
        await db.ExecuteAsync(new CommandDefinition(
            """
            DECLARE @DemoOrders TABLE ([Id] int PRIMARY KEY);
            INSERT INTO @DemoOrders ([Id])
            SELECT [Id]
            FROM [Orders]
            WHERE [OrderNumber] LIKE N'DEMO-%';

            DECLARE @DemoDeliveryCalendar TABLE ([Id] int PRIMARY KEY);
            INSERT INTO @DemoDeliveryCalendar ([Id])
            SELECT [Id]
            FROM [DeliveryCalendar]
            WHERE [OrderId] IN (SELECT [Id] FROM @DemoOrders);

            DECLARE @DemoSessions TABLE ([Id] int PRIMARY KEY);
            INSERT INTO @DemoSessions ([Id])
            SELECT [Id]
            FROM [PackingSessions]
            WHERE [OrderId] IN (SELECT [Id] FROM @DemoOrders)
               OR [DeliveryCalendarId] IN (SELECT [Id] FROM @DemoDeliveryCalendar)
               OR [CreatedBy] = N'DemoSeeder';

            DECLARE @DemoBags TABLE ([Id] int PRIMARY KEY);
            INSERT INTO @DemoBags ([Id])
            SELECT [Id]
            FROM [PackingBags]
            WHERE [PackingSessionId] IN (SELECT [Id] FROM @DemoSessions);

            DECLARE @DemoItems TABLE ([Id] int PRIMARY KEY);
            INSERT INTO @DemoItems ([Id])
            SELECT [Id]
            FROM [PackingItems]
            WHERE [PackingSessionId] IN (SELECT [Id] FROM @DemoSessions);

            DECLARE @DemoRoutes TABLE ([Id] int PRIMARY KEY);
            INSERT INTO @DemoRoutes ([Id])
            SELECT DISTINCT [RouteId]
            FROM [DeliveryRouteStops]
            WHERE [DeliveryCalendarId] IN (SELECT [Id] FROM @DemoDeliveryCalendar);

            DECLARE @DemoRouteStops TABLE ([Id] int PRIMARY KEY);
            INSERT INTO @DemoRouteStops ([Id])
            SELECT [Id]
            FROM [DeliveryRouteStops]
            WHERE [RouteId] IN (SELECT [Id] FROM @DemoRoutes)
               OR [DeliveryCalendarId] IN (SELECT [Id] FROM @DemoDeliveryCalendar);

            DECLARE @DemoProductionPlans TABLE ([Id] int PRIMARY KEY);
            INSERT INTO @DemoProductionPlans ([Id])
            SELECT [Id]
            FROM [ProductionPlans]
            WHERE [CreatedBy] = N'DemoSeeder'
               OR [Notes] LIKE N'%Demo lifecycle%'
               OR [Notes] LIKE N'%Wspolny demo seed%';

            DELETE FROM [BoxLabels]
            WHERE [PackingItemId] IN (SELECT [Id] FROM @DemoItems);

            DELETE FROM [PackingLabels]
            WHERE [PackingSessionId] IN (SELECT [Id] FROM @DemoSessions)
               OR [PackingBagId] IN (SELECT [Id] FROM @DemoBags)
               OR [PackingItemId] IN (SELECT [Id] FROM @DemoItems);

            DELETE FROM [PackingManifestIssues]
            WHERE [PackingDate] IS NOT NULL
              AND [RouteId] IN (SELECT [Id] FROM @DemoRoutes);

            DELETE FROM [PackingManifests]
            WHERE [RouteId] IN (SELECT [Id] FROM @DemoRoutes);

            DELETE FROM [DeliveryIssues]
            WHERE [RouteStopId] IN (SELECT [Id] FROM @DemoRouteStops)
               OR [CreatedBy] = N'DemoSeeder';

            DELETE FROM [PackingIncidents]
            WHERE [PackingSessionId] IN (SELECT [Id] FROM @DemoSessions)
               OR [PackingBagId] IN (SELECT [Id] FROM @DemoBags)
               OR [PackingItemId] IN (SELECT [Id] FROM @DemoItems)
               OR [DeliveryCalendarId] IN (SELECT [Id] FROM @DemoDeliveryCalendar);

            DELETE FROM [PackingStatusLogs]
            WHERE [PackingSessionId] IN (SELECT [Id] FROM @DemoSessions);

            DELETE FROM [PackingItems]
            WHERE [Id] IN (SELECT [Id] FROM @DemoItems);

            DELETE FROM [PackingBags]
            WHERE [Id] IN (SELECT [Id] FROM @DemoBags);

            DELETE FROM [PackingSessions]
            WHERE [Id] IN (SELECT [Id] FROM @DemoSessions);

            DELETE FROM [BagMovementLogs]
            WHERE [RouteStopId] IN (SELECT [Id] FROM @DemoRouteStops)
               OR [ThermalBagId] IN
               (
                   SELECT [Id]
                   FROM [ThermalBags]
                   WHERE [SerialNumber] LIKE N'THERM-DEMO-%'
               );

            DELETE FROM [DeliveryRouteStops]
            WHERE [RouteId] IN (SELECT [Id] FROM @DemoRoutes)
               OR [DeliveryCalendarId] IN (SELECT [Id] FROM @DemoDeliveryCalendar);

            DELETE FROM [DeliveryRoutes]
            WHERE [Id] IN (SELECT [Id] FROM @DemoRoutes);

            DELETE FROM [ThermalBags]
            WHERE [SerialNumber] LIKE N'THERM-DEMO-%';

            DELETE FROM [ProductionBatches]
            WHERE [ProductionPlanId] IN (SELECT [Id] FROM @DemoProductionPlans);

            DELETE FROM [ProductionPlanItems]
            WHERE [ProductionPlanId] IN (SELECT [Id] FROM @DemoProductionPlans);

            DELETE FROM [ProductionPlans]
            WHERE [Id] IN (SELECT [Id] FROM @DemoProductionPlans);

            DELETE FROM [Payments]
            WHERE [OrderId] IN (SELECT [Id] FROM @DemoOrders)
               OR [StripePaymentIntentId] LIKE N'pi_demo%';

            DELETE FROM [DeliveryCalendar]
            WHERE [Id] IN (SELECT [Id] FROM @DemoDeliveryCalendar);

            DELETE FROM [OrderItems]
            WHERE [OrderId] IN (SELECT [Id] FROM @DemoOrders);

            DELETE FROM [Orders]
            WHERE [Id] IN (SELECT [Id] FROM @DemoOrders);

            DELETE FROM [Addresses]
            WHERE [Label] LIKE N'Demo M4%'
               OR [Label] = N'Demo lifecycle';

            DELETE FROM [CustomerProfiles]
            WHERE [UserId] IN
            (
                SELECT [Id]
                FROM [Users]
                WHERE [Email] LIKE N'demo-m4-klient-%@kuchnia.local'
                   OR [Email] = N'demo-klient@kuchnia.local'
            );

            DELETE FROM [DriverVehicleAssignments]
            WHERE [DriverId] IN
            (
                SELECT [Id]
                FROM [Drivers]
                WHERE [LicenseNumber] LIKE N'M4-BI-%'
            )
               OR [VehicleId] IN
            (
                SELECT [Id]
                FROM [Vehicles]
                WHERE [RegistrationNumber] IN (N'BI 1001A', N'BI 2042C', N'BI 3307E', N'BI 4040S')
            );

            DELETE FROM [Drivers]
            WHERE [LicenseNumber] LIKE N'M4-BI-%';

            DELETE FROM [Vehicles]
            WHERE [RegistrationNumber] IN (N'BI 1001A', N'BI 2042C', N'BI 3307E', N'BI 4040S');

            DELETE FROM [Users]
            WHERE [Email] LIKE N'demo-m4-klient-%@kuchnia.local'
               OR [Email] = N'demo-klient@kuchnia.local'
               OR [Email] IN (N'driver2@kuchnia.local', N'driver3@kuchnia.local');

            DELETE FROM [DietMenuPlanItems]
            WHERE [DietMenuPlanId] IN
            (
                SELECT [Id]
                FROM [DietMenuPlans]
                WHERE [CreatedBy] = N'DemoSeeder'
                   OR [Notes] LIKE N'%demo%'
            );

            DELETE FROM [DietMenuPlans]
            WHERE [CreatedBy] = N'DemoSeeder'
               OR [Notes] LIKE N'%demo%';

            DELETE FROM [DietVariantMeals]
            WHERE [DietVariantId] IN
            (
                SELECT dv.[Id]
                FROM [DietVariants] dv
                INNER JOIN [Diets] d ON d.[Id] = dv.[DietId]
                WHERE d.[Name] = N'Demo Lifecycle'
            );

            DELETE FROM [DietVariants]
            WHERE [DietId] IN (SELECT [Id] FROM [Diets] WHERE [Name] = N'Demo Lifecycle');

            DELETE FROM [Diets]
            WHERE [Name] = N'Demo Lifecycle';

            DELETE FROM [MealVariantComponents]
            WHERE [MealVariantId] IN
            (
                SELECT mv.[Id]
                FROM [MealVariants] mv
                INNER JOIN [Meals] m ON m.[Id] = mv.[MealId]
                WHERE m.[Name] LIKE N'Demo %'
            );

            DELETE FROM [PackagingRequirements]
            WHERE [MealVariantId] IN
            (
                SELECT mv.[Id]
                FROM [MealVariants] mv
                INNER JOIN [Meals] m ON m.[Id] = mv.[MealId]
                WHERE m.[Name] LIKE N'Demo %'
            );

            DELETE FROM [MealVariants]
            WHERE [MealId] IN (SELECT [Id] FROM [Meals] WHERE [Name] LIKE N'Demo %');

            DELETE FROM [MealRecipeComponents]
            WHERE [MealId] IN (SELECT [Id] FROM [Meals] WHERE [Name] LIKE N'Demo %');

            DELETE FROM [MealAllergens]
            WHERE [MealId] IN (SELECT [Id] FROM [Meals] WHERE [Name] LIKE N'Demo %');

            DELETE FROM [NutritionFacts]
            WHERE [MealId] IN (SELECT [Id] FROM [Meals] WHERE [Name] LIKE N'Demo %');

            DELETE FROM [Recipes]
            WHERE [MealId] IN (SELECT [Id] FROM [Meals] WHERE [Name] LIKE N'Demo %');

            DELETE FROM [PackagingRequirements]
            WHERE [MealId] IN (SELECT [Id] FROM [Meals] WHERE [Name] LIKE N'Demo %');

            DELETE FROM [Meals]
            WHERE [Name] LIKE N'Demo %';
            """,
            cancellationToken: cancellationToken));

        this.logger.LogInformation("Reset demo data completed for unified M1/M2/M3/M4 seed.");
    }

    private async Task SeedUnifiedDemoScenarioAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var todayDate = today.ToDateTime(TimeOnly.MinValue);
        var preferredDietVariantId = await EnsureDemoLifecycleMenuFoundationAsync(db, now, auditUser, cancellationToken);
        await EnsureDemoPackagingRequirementsAsync(db, now, auditUser, cancellationToken);
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
            await EnsureDemoLifecycleCustomerProfileAsync(
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
            await EnsureTodayDemoPaymentAsync(
                db,
                orderId,
                orderNumber,
                totalPrice,
                now,
                auditUser,
                cancellationToken);
        }

        await EnsureUnifiedDemoProductionPlanAsync(today, auditUser, cancellationToken);

        this.logger.LogInformation(
            "Seeded unified M1/M2/M3/M4 demo data for {DemoDate}: {VehicleCount} vehicles, {DriverCount} drivers, {DeliveryCount} delivery candidates.",
            today,
            vehicles.Length,
            drivers.Length,
            deliveries.Length);
    }

    private async Task EnsureUnifiedDemoProductionPlanAsync(
        DateOnly productionDate,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var dietProvider = new DietDataAdapter(connectionFactory);
        var planRepository = new ProductionPlanRepository(connectionFactory);
        var existingPlan = await planRepository.GetByDateAsync(productionDate);
        if (existingPlan is not null)
        {
            this.logger.LogInformation(
                "Production plan for unified demo date {ProductionDate} already exists as #{PlanId}.",
                productionDate,
                existingPlan.Id);
            return;
        }

        var generator = new ProductionPlanGenerator(
            new M1OrderDataProvider(connectionFactory),
            dietProvider,
            new FoodCostCalculator(dietProvider),
            planRepository);
        var result = await generator.GeneratePlanAsync(productionDate, auditUser);
        var itemRepository = new BaseRepository<ProductionPlanItem>(connectionFactory);
        foreach (var item in result.Items)
        {
            await itemRepository.InsertAsync(item);
        }

        result.Plan.Status = ProductionPlanStatus.InProgress;
        result.Plan.Notes = "Wspolny demo seed: plan wygenerowany z zamowien M1 i planu M2.";
        result.Plan.IsSharedWithLogistics = true;
        result.Plan.SharedAt = DateTimeOffset.UtcNow;
        result.Plan.UpdatedAt = DateTimeOffset.UtcNow;
        result.Plan.UpdatedBy = auditUser;
        await planRepository.UpdateAsync(result.Plan);

        this.logger.LogInformation(
            "Generated unified demo production plan #{PlanId} for {ProductionDate} with {ItemCount} items.",
            result.Plan.Id,
            productionDate,
            result.Items.Count);

        cancellationToken.ThrowIfCancellationRequested();
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

    private async Task EnsureDemoPackagingInventoryAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var sztUnitId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT [Id] FROM [UnitsOfMeasure] WHERE [Symbol] = N'szt';",
            cancellationToken: cancellationToken));

        var packagingItems = new[]
        {
            new DemoPackagingStockItem("Pudełko cateringowe 500ml", 100m, 500m, 2),
            new DemoPackagingStockItem("Pudełko cateringowe 250ml", 100m, 500m, 2),
            new DemoPackagingStockItem("Torba papierowa u Cygana", 50m, 250m, 2),
        };

        foreach (var item in packagingItems)
        {
            var stockItemId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
                """
                SELECT TOP 1 [Id]
                FROM [StockItems]
                WHERE [Name] = @Name
                  AND [IsDeleted] = 0
                ORDER BY [Id];
                """,
                new { item.Name },
                cancellationToken: cancellationToken));

            if (!stockItemId.HasValue)
            {
                stockItemId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    INSERT INTO [StockItems]
                        ([Name], [BaseIngredientId], [DefaultUnitOfMeasureId], [WarehouseCategoryId],
                         [MinimumLevel], [LeadTimeDays], [CreatedBy], [UpdatedBy], [IsDeleted],
                         [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
                    OUTPUT INSERTED.[Id]
                    VALUES
                        (@Name, NULL, @DefaultUnitOfMeasureId, @WarehouseCategoryId,
                         @MinimumLevel, @LeadTimeDays, @CreatedBy, NULL, 0,
                         NULL, NULL, @CreatedAt, NULL);
                    """,
                    new
                    {
                        item.Name,
                        DefaultUnitOfMeasureId = sztUnitId,
                        WarehouseCategoryId = PackagingWarehouseCategoryId,
                        item.MinimumLevel,
                        item.LeadTimeDays,
                        CreatedBy = auditUser,
                        CreatedAt = now,
                    },
                    cancellationToken: cancellationToken));
            }
            else
            {
                await db.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE [StockItems]
                    SET [DefaultUnitOfMeasureId] = @DefaultUnitOfMeasureId,
                        [WarehouseCategoryId] = @WarehouseCategoryId,
                        [MinimumLevel] = CASE WHEN [MinimumLevel] < @MinimumLevel THEN @MinimumLevel ELSE [MinimumLevel] END,
                        [UpdatedBy] = @UpdatedBy,
                        [UpdatedAt] = @UpdatedAt
                    WHERE [Id] = @StockItemId
                      AND ([DefaultUnitOfMeasureId] <> @DefaultUnitOfMeasureId
                           OR [WarehouseCategoryId] <> @WarehouseCategoryId
                           OR [MinimumLevel] < @MinimumLevel);
                    """,
                    new
                    {
                        StockItemId = stockItemId.Value,
                        DefaultUnitOfMeasureId = sztUnitId,
                        WarehouseCategoryId = PackagingWarehouseCategoryId,
                        item.MinimumLevel,
                        UpdatedBy = auditUser,
                        UpdatedAt = now,
                    },
                    cancellationToken: cancellationToken));
            }

            var available = await db.ExecuteScalarAsync<decimal>(new CommandDefinition(
                """
                SELECT COALESCE(SUM([CurrentQuantity]), 0)
                FROM [Batches]
                WHERE [StockItemId] = @StockItemId
                  AND [IsDeleted] = 0
                  AND [IsDepleted] = 0
                  AND [CurrentQuantity] > 0;
                """,
                new { StockItemId = stockItemId.Value },
                cancellationToken: cancellationToken));

            var missingQuantity = item.TargetQuantity - available;
            if (missingQuantity <= 0)
            {
                continue;
            }

            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [Batches]
                    ([StockItemId], [SupplierBatchNumber], [CurrentQuantity], [ExpiryDate], [ReceivedDate], [IsDepleted],
                     [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
                VALUES
                    (@StockItemId, @SupplierBatchNumber, @CurrentQuantity, @ExpiryDate, @ReceivedDate, 0,
                     @CreatedBy, NULL, 0, NULL, NULL, @CreatedAt, NULL);
                """,
                new
                {
                    StockItemId = stockItemId.Value,
                    SupplierBatchNumber = $"DEMO-PACK-{stockItemId.Value}-{now:yyyyMMddHHmmss}",
                    CurrentQuantity = missingQuantity,
                    ExpiryDate = now.AddDays(180),
                    ReceivedDate = now,
                    CreatedBy = auditUser,
                    CreatedAt = now,
                },
                cancellationToken: cancellationToken));

            this.logger.LogInformation(
                "Restocked demo packaging item {StockItemId} ({Name}) by {Quantity}.",
                stockItemId.Value,
                item.Name,
                missingQuantity);
        }
    }

    private async Task EnsureIngredientWarehouseMappingsAsync(IDbConnection db, CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE si
            SET si.[BaseIngredientId] = i.[Id]
            FROM [StockItems] si
            INNER JOIN [Ingredients] i
                ON i.[Name] COLLATE Latin1_General_100_CI_AI = si.[Name] COLLATE Latin1_General_100_CI_AI
            WHERE si.[BaseIngredientId] IS NULL
              AND si.[IsDeleted] = 0
              AND i.[IsDeleted] = 0;

            UPDATE i
            SET
                i.[StockItemId] = si.[Id],
                i.[WarehouseCategoryId] = si.[WarehouseCategoryId],
                i.[RequiresCoreTemperatureCheck] = CASE WHEN si.[WarehouseCategoryId] = 1 THEN 1 ELSE 0 END,
                i.[MinimumCoreTemperatureCelsius] = CASE WHEN si.[WarehouseCategoryId] = 1 THEN 75 ELSE NULL END
            FROM [Ingredients] i
            INNER JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id]
                OR si.[Name] COLLATE Latin1_General_100_CI_AI = i.[Name] COLLATE Latin1_General_100_CI_AI
            WHERE i.[IsDeleted] = 0
              AND si.[IsDeleted] = 0
              AND (i.[StockItemId] IS NULL OR i.[WarehouseCategoryId] IS NULL);

            UPDATE rci
            SET
                rci.[StockItemId] = COALESCE(rci.[StockItemId], i.[StockItemId], si.[Id]),
                rci.[WarehouseCategoryId] = COALESCE(rci.[WarehouseCategoryId], i.[WarehouseCategoryId], si.[WarehouseCategoryId])
            FROM [RecipeComponentIngredients] rci
            INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
            LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id]
                OR si.[Name] COLLATE Latin1_General_100_CI_AI = i.[Name] COLLATE Latin1_General_100_CI_AI
            WHERE rci.[IsDeleted] = 0
              AND i.[IsDeleted] = 0
              AND (rci.[StockItemId] IS NULL OR rci.[WarehouseCategoryId] IS NULL)
              AND (i.[StockItemId] IS NOT NULL OR i.[WarehouseCategoryId] IS NOT NULL OR si.[Id] IS NOT NULL);
            """,
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureDemoPackagingRequirementsAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            """
            DECLARE @packagingCategoryId int =
            (
                SELECT TOP 1 [Id]
                FROM [WarehouseCategories]
                WHERE [Code] = N'PACKAGING'
                   OR [Name] COLLATE Latin1_General_100_CI_AI = N'Opakowania'
                ORDER BY [Id]
            );

            DECLARE @box500Id int =
            (
                SELECT TOP 1 [Id]
                FROM [StockItems]
                WHERE [Name] = N'Pudełko cateringowe 500ml'
                  AND [IsDeleted] = 0
                ORDER BY [Id]
            );

            DECLARE @box250Id int =
            (
                SELECT TOP 1 [Id]
                FROM [StockItems]
                WHERE [Name] = N'Pudełko cateringowe 250ml'
                  AND [IsDeleted] = 0
                ORDER BY [Id]
            );

            INSERT INTO [PackagingRequirements]
                ([OwnerType], [MealId], [RecipeComponentVersionId], [StockItemId], [WarehouseCategoryId],
                 [ResourceName], [Quantity], [Unit], [ContainerRole], [IsCustomerFacing],
                 [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                N'Meal',
                m.[Id],
                NULL,
                CASE
                    WHEN m.[Name] IN
                    (
                        N'Pudding chia z jagodami i śmietanką',
                        N'Demo przekaska owocowa',
                        N'Demo podwieczorek fit'
                    )
                    THEN @box250Id
                    ELSE @box500Id
                END,
                @packagingCategoryId,
                CASE
                    WHEN m.[Name] IN
                    (
                        N'Pudding chia z jagodami i śmietanką',
                        N'Demo przekaska owocowa',
                        N'Demo podwieczorek fit'
                    )
                    THEN N'Pudełko cateringowe 250ml'
                    ELSE N'Pudełko cateringowe 500ml'
                END,
                1.000,
                N'szt',
                N'MealBox',
                1,
                @now,
                @auditUser,
                0
            FROM [Meals] m
            WHERE m.[IsDeleted] = 0
              AND m.[IsActive] = 1
              AND m.[Status] IN (N'Published', N'Active')
              AND @packagingCategoryId IS NOT NULL
              AND
              (
                  @box500Id IS NOT NULL
                  OR @box250Id IS NOT NULL
              )
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [PackagingRequirements] existing
                  WHERE existing.[MealId] = m.[Id]
                    AND existing.[OwnerType] = N'Meal'
                    AND existing.[IsDeleted] = 0
              );
            """,
            new { now, auditUser },
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureDemoRecipeComponentsAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [IngredientAllergens] ([IngredientId], [AllergenId], [TraceAmount])
            SELECT v.[IngredientId], v.[AllergenId], 0
            FROM (VALUES
                (2, 3),
                (4, 2),
                (5, 2),
                (6, 2),
                (7, 2),
                (12, 1),
                (15, 1)
            ) AS v([IngredientId], [AllergenId])
            WHERE NOT EXISTS (
                SELECT 1
                FROM [IngredientAllergens] ia
                WHERE ia.[IngredientId] = v.[IngredientId]
                  AND ia.[AllergenId] = v.[AllergenId]);

            INSERT INTO [NutritionFacts]
                ([IngredientId], [CaloriesPer100g], [ProteinPer100g], [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [CreatedAt])
            SELECT v.[IngredientId], v.[CaloriesPer100g], v.[ProteinPer100g], v.[CarbohydratesPer100g], v.[FatPer100g], v.[FiberPer100g], @now
            FROM (VALUES
                (1, CAST(110.00 AS decimal(8,2)), CAST(23.00 AS decimal(8,2)), CAST(0.00 AS decimal(8,2)), CAST(2.00 AS decimal(8,2)), CAST(0.00 AS decimal(8,2))),
                (2, CAST(208.00 AS decimal(8,2)), CAST(20.00 AS decimal(8,2)), CAST(0.00 AS decimal(8,2)), CAST(13.00 AS decimal(8,2)), CAST(0.00 AS decimal(8,2))),
                (3, CAST(250.00 AS decimal(8,2)), CAST(20.00 AS decimal(8,2)), CAST(0.00 AS decimal(8,2)), CAST(18.00 AS decimal(8,2)), CAST(0.00 AS decimal(8,2))),
                (4, CAST(292.00 AS decimal(8,2)), CAST(2.00 AS decimal(8,2)), CAST(3.00 AS decimal(8,2)), CAST(30.00 AS decimal(8,2)), CAST(0.00 AS decimal(8,2))),
                (5, CAST(748.00 AS decimal(8,2)), CAST(0.50 AS decimal(8,2)), CAST(0.50 AS decimal(8,2)), CAST(82.00 AS decimal(8,2)), CAST(0.00 AS decimal(8,2))),
                (6, CAST(356.00 AS decimal(8,2)), CAST(25.00 AS decimal(8,2)), CAST(2.00 AS decimal(8,2)), CAST(27.00 AS decimal(8,2)), CAST(0.00 AS decimal(8,2))),
                (7, CAST(61.00 AS decimal(8,2)), CAST(3.50 AS decimal(8,2)), CAST(4.70 AS decimal(8,2)), CAST(3.30 AS decimal(8,2)), CAST(0.00 AS decimal(8,2))),
                (8, CAST(34.00 AS decimal(8,2)), CAST(2.80 AS decimal(8,2)), CAST(6.60 AS decimal(8,2)), CAST(0.40 AS decimal(8,2)), CAST(2.60 AS decimal(8,2))),
                (9, CAST(45.00 AS decimal(8,2)), CAST(1.00 AS decimal(8,2)), CAST(12.00 AS decimal(8,2)), CAST(0.10 AS decimal(8,2)), CAST(2.00 AS decimal(8,2))),
                (10, CAST(86.00 AS decimal(8,2)), CAST(1.60 AS decimal(8,2)), CAST(20.00 AS decimal(8,2)), CAST(0.10 AS decimal(8,2)), CAST(3.00 AS decimal(8,2))),
                (11, CAST(57.00 AS decimal(8,2)), CAST(0.70 AS decimal(8,2)), CAST(14.50 AS decimal(8,2)), CAST(0.30 AS decimal(8,2)), CAST(2.40 AS decimal(8,2))),
                (12, CAST(364.00 AS decimal(8,2)), CAST(10.00 AS decimal(8,2)), CAST(76.00 AS decimal(8,2)), CAST(1.00 AS decimal(8,2)), CAST(2.70 AS decimal(8,2))),
                (13, CAST(365.00 AS decimal(8,2)), CAST(7.00 AS decimal(8,2)), CAST(80.00 AS decimal(8,2)), CAST(0.70 AS decimal(8,2)), CAST(1.30 AS decimal(8,2))),
                (14, CAST(32.00 AS decimal(8,2)), CAST(1.60 AS decimal(8,2)), CAST(5.00 AS decimal(8,2)), CAST(0.20 AS decimal(8,2)), CAST(1.20 AS decimal(8,2))),
                (15, CAST(350.00 AS decimal(8,2)), CAST(12.00 AS decimal(8,2)), CAST(72.00 AS decimal(8,2)), CAST(1.50 AS decimal(8,2)), CAST(3.00 AS decimal(8,2)))
            ) AS v([IngredientId], [CaloriesPer100g], [ProteinPer100g], [CarbohydratesPer100g], [FatPer100g], [FiberPer100g])
            WHERE NOT EXISTS (
                SELECT 1
                FROM [NutritionFacts] nf
                WHERE nf.[IngredientId] = v.[IngredientId]);

            ;WITH MealWeights AS (
                SELECT
                    r.[MealId],
                    SUM(r.[WeightInGrams]) AS [RawWeightGrams]
                FROM [Recipes] r
                WHERE r.[IsDeleted] = 0
                GROUP BY r.[MealId]
            )
            UPDATE m
            SET [RawWeightGrams] = COALESCE(m.[RawWeightGrams], mw.[RawWeightGrams], CAST(350 AS decimal(10,2))),
                [CookedWeightGrams] = COALESCE(m.[CookedWeightGrams], mw.[RawWeightGrams], CAST(350 AS decimal(10,2))),
                [ShelfLifeHours] = COALESCE(m.[ShelfLifeHours], 48),
                [UseEarliestIngredientExpiry] = COALESCE(m.[UseEarliestIngredientExpiry], 0),
                [UpdatedAt] = @now,
                [UpdatedBy] = @auditUser
            FROM [Meals] m
            LEFT JOIN MealWeights mw ON mw.[MealId] = m.[Id]
            WHERE m.[IsDeleted] = 0
              AND EXISTS (SELECT 1 FROM [Recipes] r WHERE r.[MealId] = m.[Id] AND r.[IsDeleted] = 0)
              AND (
                    m.[RawWeightGrams] IS NULL
                 OR m.[CookedWeightGrams] IS NULL
                 OR m.[ShelfLifeHours] IS NULL
              );

            INSERT INTO [RecipeComponents]
                ([Name], [Description], [CategoryId], [ImageUrl], [PreparationTimeMinutes],
                 [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                CONCAT(m.[Name], N' - składowa bazowa'),
                CONCAT(N'Demo składowa technologiczna utworzona z legacy Recipes dla posiłku: ', m.[Name]),
                m.[CategoryId],
                NULL,
                m.[PreparationTimeMinutes],
                1,
                @now,
                @auditUser,
                0
            FROM [Meals] m
            WHERE m.[IsDeleted] = 0
              AND m.[IsActive] = 1
              AND EXISTS (SELECT 1 FROM [Recipes] r WHERE r.[MealId] = m.[Id] AND r.[IsDeleted] = 0)
              AND NOT EXISTS (
                    SELECT 1
                    FROM [RecipeComponents] rc
                    WHERE rc.[Name] = CONCAT(m.[Name], N' - składowa bazowa')
                      AND rc.[IsDeleted] = 0);

            INSERT INTO [RecipeComponentVersions]
                ([RecipeComponentId], [VersionNumber], [Status], [Instructions], [YieldQuantity], [YieldUnit],
                 [RawWeightGrams], [CookedWeightGrams], [CaloriesPer100g], [ProteinPer100g],
                 [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [ShelfLifeHours],
                 [UseEarliestIngredientExpiry], [NutritionSource], [NutritionOverrideReason],
                 [AllergensApproved], [AllergenOverrideReason], [AllergensApprovedAt], [AllergensApprovedBy],
                 [ChangeSummary], [IsTechnologyChange], [NonTechnologyChangeReason],
                 [PublishedAt], [PublishedBy], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                rc.[Id],
                1,
                N'Published',
                CONCAT(N'Przygotuj składową bazową dla posiłku: ', m.[Name], N'. Użyj ilości składników z tabeli zapotrzebowania.'),
                1.000,
                N'portion',
                COALESCE(m.[RawWeightGrams], mw.[RawWeightGrams], CAST(350 AS decimal(10,2))),
                COALESCE(m.[CookedWeightGrams], m.[RawWeightGrams], mw.[RawWeightGrams], CAST(350 AS decimal(10,2))),
                nf.[CaloriesPer100g],
                nf.[ProteinPer100g],
                nf.[CarbohydratesPer100g],
                nf.[FatPer100g],
                nf.[FiberPer100g],
                COALESCE(m.[ShelfLifeHours], 48),
                COALESCE(m.[UseEarliestIngredientExpiry], 0),
                N'Manual',
                N'Demo seeder: nutrition przepisane z NutritionFacts posiłku do wersji składowej.',
                1,
                N'Demo seeder: alergeny zatwierdzone na podstawie danych posiłku/składników.',
                @now,
                @auditUser,
                N'Demo seeder: wersja bazowa z legacy Recipes.',
                1,
                NULL,
                @now,
                @auditUser,
                @now,
                @auditUser,
                0
            FROM [Meals] m
            INNER JOIN [RecipeComponents] rc ON rc.[Name] = CONCAT(m.[Name], N' - składowa bazowa') AND rc.[IsDeleted] = 0
            OUTER APPLY (
                SELECT SUM(r.[WeightInGrams]) AS [RawWeightGrams]
                FROM [Recipes] r
                WHERE r.[MealId] = m.[Id]
                  AND r.[IsDeleted] = 0
            ) mw
            OUTER APPLY (
                SELECT TOP 1
                    facts.[CaloriesPer100g],
                    facts.[ProteinPer100g],
                    facts.[CarbohydratesPer100g],
                    facts.[FatPer100g],
                    facts.[FiberPer100g]
                FROM [NutritionFacts] facts
                WHERE facts.[MealId] = m.[Id]
                ORDER BY facts.[Id] DESC
            ) nf
            WHERE m.[IsDeleted] = 0
              AND EXISTS (SELECT 1 FROM [Recipes] r WHERE r.[MealId] = m.[Id] AND r.[IsDeleted] = 0)
              AND nf.[CaloriesPer100g] IS NOT NULL
              AND NOT EXISTS (
                    SELECT 1
                    FROM [RecipeComponentVersions] rcv
                    WHERE rcv.[RecipeComponentId] = rc.[Id]
                      AND rcv.[VersionNumber] = 1
                      AND rcv.[IsDeleted] = 0);

            UPDATE rcv
            SET [Status] = N'Published',
                [RawWeightGrams] = COALESCE(rcv.[RawWeightGrams], m.[RawWeightGrams], mw.[RawWeightGrams], CAST(350 AS decimal(10,2))),
                [CookedWeightGrams] = COALESCE(rcv.[CookedWeightGrams], m.[CookedWeightGrams], m.[RawWeightGrams], mw.[RawWeightGrams], CAST(350 AS decimal(10,2))),
                [CaloriesPer100g] = COALESCE(rcv.[CaloriesPer100g], nf.[CaloriesPer100g]),
                [ProteinPer100g] = COALESCE(rcv.[ProteinPer100g], nf.[ProteinPer100g]),
                [CarbohydratesPer100g] = COALESCE(rcv.[CarbohydratesPer100g], nf.[CarbohydratesPer100g]),
                [FatPer100g] = COALESCE(rcv.[FatPer100g], nf.[FatPer100g]),
                [FiberPer100g] = COALESCE(rcv.[FiberPer100g], nf.[FiberPer100g]),
                [ShelfLifeHours] = COALESCE(rcv.[ShelfLifeHours], m.[ShelfLifeHours], 48),
                [NutritionSource] = COALESCE(NULLIF(rcv.[NutritionSource], N''), N'Manual'),
                [NutritionOverrideReason] = COALESCE(rcv.[NutritionOverrideReason], N'Demo seeder: nutrition przepisane z NutritionFacts posiłku do wersji składowej.'),
                [AllergensApproved] = 1,
                [AllergenOverrideReason] = COALESCE(rcv.[AllergenOverrideReason], N'Demo seeder: alergeny zatwierdzone na podstawie danych posiłku/składników.'),
                [AllergensApprovedAt] = COALESCE(rcv.[AllergensApprovedAt], @now),
                [AllergensApprovedBy] = COALESCE(rcv.[AllergensApprovedBy], @auditUser),
                [PublishedAt] = COALESCE(rcv.[PublishedAt], @now),
                [PublishedBy] = COALESCE(rcv.[PublishedBy], @auditUser),
                [UpdatedAt] = @now,
                [UpdatedBy] = @auditUser
            FROM [RecipeComponentVersions] rcv
            INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
            INNER JOIN [Meals] m ON rc.[Name] = CONCAT(m.[Name], N' - składowa bazowa')
            OUTER APPLY (
                SELECT SUM(r.[WeightInGrams]) AS [RawWeightGrams]
                FROM [Recipes] r
                WHERE r.[MealId] = m.[Id]
                  AND r.[IsDeleted] = 0
            ) mw
            OUTER APPLY (
                SELECT TOP 1 *
                FROM [NutritionFacts] facts
                WHERE facts.[MealId] = m.[Id]
                ORDER BY facts.[Id] DESC
            ) nf
            WHERE rcv.[VersionNumber] = 1
              AND rcv.[IsDeleted] = 0
              AND rc.[IsDeleted] = 0
              AND m.[IsDeleted] = 0
              AND nf.[CaloriesPer100g] IS NOT NULL;

            INSERT INTO [RecipeComponentIngredients]
                ([RecipeComponentVersionId], [IngredientId], [StockItemId], [WarehouseCategoryId],
                 [WeightInGrams], [YieldFactor], [IsOptional], [Notes], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                rcv.[Id],
                r.[IngredientId],
                COALESCE(i.[StockItemId], si.[Id]),
                COALESCE(i.[WarehouseCategoryId], si.[WarehouseCategoryId]),
                r.[WeightInGrams],
                CASE WHEN i.[YieldFactor] <= 0 THEN 1.0000 ELSE COALESCE(i.[YieldFactor], 1.0000) END,
                r.[IsOptional],
                r.[Notes],
                @now,
                @auditUser,
                0
            FROM [Recipes] r
            INNER JOIN [Meals] m ON m.[Id] = r.[MealId]
            INNER JOIN [RecipeComponents] rc ON rc.[Name] = CONCAT(m.[Name], N' - składowa bazowa') AND rc.[IsDeleted] = 0
            INNER JOIN [RecipeComponentVersions] rcv ON rcv.[RecipeComponentId] = rc.[Id] AND rcv.[VersionNumber] = 1 AND rcv.[IsDeleted] = 0
            INNER JOIN [Ingredients] i ON i.[Id] = r.[IngredientId]
            LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id]
                OR si.[Name] COLLATE Latin1_General_100_CI_AI = i.[Name] COLLATE Latin1_General_100_CI_AI
            WHERE r.[IsDeleted] = 0
              AND i.[IsDeleted] = 0
              AND COALESCE(i.[WarehouseCategoryId], si.[WarehouseCategoryId]) IS NOT NULL
              AND NOT EXISTS (
                    SELECT 1
                    FROM [RecipeComponentIngredients] existing
                    WHERE existing.[RecipeComponentVersionId] = rcv.[Id]
                      AND existing.[IngredientId] = r.[IngredientId]
                      AND existing.[IsDeleted] = 0);

            INSERT INTO [RecipeComponentInstructionSections]
                ([RecipeComponentVersionId], [Title], [SortOrder], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                rcv.[Id],
                N'Przygotowanie',
                1,
                @now,
                @auditUser,
                0
            FROM [RecipeComponentVersions] rcv
            INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
            WHERE rc.[Name] LIKE N'% - składowa bazowa'
              AND rcv.[IsDeleted] = 0
              AND NOT EXISTS (
                    SELECT 1
                    FROM [RecipeComponentInstructionSections] existing
                    WHERE existing.[RecipeComponentVersionId] = rcv.[Id]
                      AND existing.[IsDeleted] = 0);

            INSERT INTO [RecipeComponentInstructionSteps]
                ([RecipeComponentInstructionSectionId], [StepText], [SortOrder], [RequiresControl],
                 [ControlType], [ExpectedValue], [ExpectedUnit], [IsCritical], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                sectionRows.[Id],
                CONCAT(N'Przygotuj składniki i wykonaj składową zgodnie z kartą demo dla: ', rc.[Name]),
                1,
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM [RecipeComponentIngredients] rci
                    INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
                    WHERE rci.[RecipeComponentVersionId] = rcv.[Id]
                      AND i.[RequiresCoreTemperatureCheck] = 1
                ) THEN 1 ELSE 0 END,
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM [RecipeComponentIngredients] rci
                    INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
                    WHERE rci.[RecipeComponentVersionId] = rcv.[Id]
                      AND i.[RequiresCoreTemperatureCheck] = 1
                ) THEN N'Temperature' ELSE NULL END,
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM [RecipeComponentIngredients] rci
                    INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
                    WHERE rci.[RecipeComponentVersionId] = rcv.[Id]
                      AND i.[RequiresCoreTemperatureCheck] = 1
                ) THEN CAST(75.000 AS decimal(10,3)) ELSE NULL END,
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM [RecipeComponentIngredients] rci
                    INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
                    WHERE rci.[RecipeComponentVersionId] = rcv.[Id]
                      AND i.[RequiresCoreTemperatureCheck] = 1
                ) THEN N'C' ELSE NULL END,
                CASE WHEN EXISTS (
                    SELECT 1
                    FROM [RecipeComponentIngredients] rci
                    INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
                    WHERE rci.[RecipeComponentVersionId] = rcv.[Id]
                      AND i.[RequiresCoreTemperatureCheck] = 1
                ) THEN 1 ELSE 0 END,
                @now,
                @auditUser,
                0
            FROM [RecipeComponentVersions] rcv
            INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
            INNER JOIN [RecipeComponentInstructionSections] sectionRows
                ON sectionRows.[RecipeComponentVersionId] = rcv.[Id]
               AND sectionRows.[IsDeleted] = 0
            WHERE rc.[Name] LIKE N'% - składowa bazowa'
              AND rcv.[IsDeleted] = 0
              AND NOT EXISTS (
                    SELECT 1
                    FROM [RecipeComponentInstructionSteps] existing
                    WHERE existing.[RecipeComponentInstructionSectionId] = sectionRows.[Id]
                      AND existing.[IsDeleted] = 0);

            INSERT INTO [PackagingRequirements]
                ([OwnerType], [MealId], [RecipeComponentVersionId], [StockItemId], [WarehouseCategoryId],
                 [ResourceName], [Quantity], [Unit], [ContainerRole], [IsCustomerFacing],
                 [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                N'RecipeComponentVersion',
                NULL,
                rcv.[Id],
                pr.[StockItemId],
                pr.[WarehouseCategoryId],
                pr.[ResourceName],
                pr.[Quantity],
                pr.[Unit],
                COALESCE(pr.[ContainerRole], N'MealBox'),
                pr.[IsCustomerFacing],
                @now,
                @auditUser,
                0
            FROM [Meals] m
            INNER JOIN [RecipeComponents] rc ON rc.[Name] = CONCAT(m.[Name], N' - składowa bazowa') AND rc.[IsDeleted] = 0
            INNER JOIN [RecipeComponentVersions] rcv ON rcv.[RecipeComponentId] = rc.[Id] AND rcv.[VersionNumber] = 1 AND rcv.[IsDeleted] = 0
            INNER JOIN [PackagingRequirements] pr ON pr.[MealId] = m.[Id]
                AND pr.[RecipeComponentVersionId] IS NULL
                AND pr.[MealVariantId] IS NULL
                AND pr.[IsDeleted] = 0
            WHERE NOT EXISTS (
                SELECT 1
                FROM [PackagingRequirements] existing
                WHERE existing.[RecipeComponentVersionId] = rcv.[Id]
                  AND existing.[OwnerType] = N'RecipeComponentVersion'
                  AND existing.[IsDeleted] = 0);

            INSERT INTO [MealRecipeComponents]
                ([MealId], [RecipeComponentVersionId], [Role], [QuantityPerServing], [Unit],
                 [SortOrder], [IsOptional], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                m.[Id],
                rcv.[Id],
                N'Składowa bazowa',
                1.000,
                N'portion',
                1,
                0,
                @now,
                @auditUser,
                0
            FROM [Meals] m
            INNER JOIN [RecipeComponents] rc ON rc.[Name] = CONCAT(m.[Name], N' - składowa bazowa') AND rc.[IsDeleted] = 0
            INNER JOIN [RecipeComponentVersions] rcv ON rcv.[RecipeComponentId] = rc.[Id] AND rcv.[VersionNumber] = 1 AND rcv.[IsDeleted] = 0
            WHERE NOT EXISTS (
                SELECT 1
                FROM [MealRecipeComponents] existing
                WHERE existing.[MealId] = m.[Id]
                  AND existing.[RecipeComponentVersionId] = rcv.[Id]
                  AND existing.[IsDeleted] = 0);
            """,
            new { now, auditUser },
            cancellationToken: cancellationToken));
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

    private async Task EnsureCoreMenuRowsAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            """
            SET IDENTITY_INSERT [Categories] ON;
            INSERT INTO [Categories] ([Id], [Name], [Description], [SortOrder], [CreatedAt])
            SELECT v.[Id], v.[Name], v.[Description], v.[SortOrder], @now
            FROM (VALUES
                (1, N'Śniadanie', N'Pierwszy posiłek dnia', 1),
                (2, N'Drugie Śniadanie', N'Lekka przekąska przedpołudniowa', 2),
                (3, N'Obiad', N'Główny ciepły posiłek', 3),
                (4, N'Podwieczorek', N'Słodka lub słona przekąska popołudniowa', 4),
                (5, N'Kolacja', N'Ostatni posiłek dnia', 5)
            ) AS v([Id], [Name], [Description], [SortOrder])
            WHERE NOT EXISTS (SELECT 1 FROM [Categories] c WHERE c.[Id] = v.[Id]);
            SET IDENTITY_INSERT [Categories] OFF;

            SET IDENTITY_INSERT [Allergens] ON;
            INSERT INTO [Allergens] ([Id], [Name], [Code], [IconUrl], [CreatedAt])
            SELECT v.[Id], v.[Name], v.[Code], NULL, @now
            FROM (VALUES
                (1, N'Gluten', N'GLU'),
                (2, N'Laktoza', N'LAC'),
                (3, N'Ryby', N'FIS'),
                (4, N'Orzechy', N'NUT'),
                (5, N'Jaja', N'EGG')
            ) AS v([Id], [Name], [Code])
            WHERE NOT EXISTS (SELECT 1 FROM [Allergens] a WHERE a.[Id] = v.[Id]);
            SET IDENTITY_INSERT [Allergens] OFF;

            SET IDENTITY_INSERT [Ingredients] ON;
            INSERT INTO [Ingredients] ([Id], [Name], [Unit], [CostPerUnit], [IsActive], [CreatedAt], [CreatedBy])
            SELECT v.[Id], v.[Name], v.[Unit], v.[CostPerUnit], 1, @now, @auditUser
            FROM (VALUES
                (1, N'Pierś z kurczaka świeża', N'kg', CAST(24.50 AS decimal(10,4))),
                (2, N'Łosoś filet świeży', N'kg', CAST(89.00 AS decimal(10,4))),
                (3, N'Mięso mielone wołowe', N'kg', CAST(32.00 AS decimal(10,4))),
                (4, N'Śmietanka UHT 30%', N'L', CAST(14.20 AS decimal(10,4))),
                (5, N'Masło Extra 82%', N'kg', CAST(35.00 AS decimal(10,4))),
                (6, N'Ser żółty Gouda', N'kg', CAST(28.00 AS decimal(10,4))),
                (7, N'Jogurt naturalny', N'L', CAST(6.50 AS decimal(10,4))),
                (8, N'Brokuły świeże', N'kg', CAST(12.00 AS decimal(10,4))),
                (9, N'Dynia piżmowa', N'kg', CAST(8.00 AS decimal(10,4))),
                (10, N'Bataty (słodkie ziemniaki)', N'kg', CAST(9.50 AS decimal(10,4))),
                (11, N'Jagody mrożone', N'kg', CAST(18.00 AS decimal(10,4))),
                (12, N'Mąka pszenna typ 500', N'kg', CAST(3.20 AS decimal(10,4))),
                (13, N'Ryż jaśminowy', N'kg', CAST(7.80 AS decimal(10,4))),
                (14, N'Pomidory krojone (puszka)', N'szt', CAST(4.50 AS decimal(10,4))),
                (15, N'Makaron penne', N'kg', CAST(6.00 AS decimal(10,4)))
            ) AS v([Id], [Name], [Unit], [CostPerUnit])
            WHERE NOT EXISTS (SELECT 1 FROM [Ingredients] i WHERE i.[Id] = v.[Id]);
            SET IDENTITY_INSERT [Ingredients] OFF;

            SET IDENTITY_INSERT [Meals] ON;
            INSERT INTO [Meals] ([Id], [CategoryId], [Name], [Description], [Status], [PreparationTimeMinutes], [IsActive], [CreatedAt], [CreatedBy])
            SELECT v.[Id], v.[CategoryId], v.[Name], v.[Description], N'Published', v.[PreparationTimeMinutes], 1, @now, @auditUser
            FROM (VALUES
                (1, 1, N'Jajecznica z szczypiorkiem na maśle', N'Klasyczna jajecznica z 3 jaj na prawdziwym maśle ze świeżym szczypiorkiem i pieczywem.', 10),
                (2, 2, N'Pudding chia z jagodami i śmietanką', N'Kremowy deser chia na bazie jogurtu i śmietanki ze słodkim musem z mrożonych jagód.', 15),
                (3, 3, N'Pikantna zupa pomidorowa z makaronem', N'Rozgrzewająca, aromatyczna zupa ze słodkich pomidorów krojonych z makaronem penne i nutą śmietanki.', 25),
                (4, 3, N'Pieczony filet z łososia z ryżem i brokułami', N'Delikatny łosoś pieczony w ziołach, podawany z sypkim ryżem jaśminowym i gotowanymi brokułami.', 35),
                (5, 5, N'Bowl z wołowiną, dynią i batatami', N'Pożywna kolacja z pieczonym mięsem wołowym, batatami i słodką dynią piżmową z przyprawami.', 30)
            ) AS v([Id], [CategoryId], [Name], [Description], [PreparationTimeMinutes])
            WHERE NOT EXISTS (SELECT 1 FROM [Meals] m WHERE m.[Id] = v.[Id]);
            SET IDENTITY_INSERT [Meals] OFF;

            SET IDENTITY_INSERT [Diets] ON;
            INSERT INTO [Diets] ([Id], [Name], [Description], [MarketingDescription], [Status], [IsActive], [CreatedAt], [CreatedBy])
            SELECT v.[Id], v.[Name], v.[Description], v.[MarketingDescription], N'Active', 1, @now, @auditUser
            FROM (VALUES
                (1, N'Standard', N'Zbilansowana dieta dla każdego.', N'Zdrowy catering na każdy dzień.'),
                (2, N'Sport / High Protein', N'Dieta o podwyższonej zawartości białka dla aktywnych.', N'Zbuduj formę z Cyganem.')
            ) AS v([Id], [Name], [Description], [MarketingDescription])
            WHERE NOT EXISTS (SELECT 1 FROM [Diets] d WHERE d.[Id] = v.[Id]);
            SET IDENTITY_INSERT [Diets] OFF;

            SET IDENTITY_INSERT [DietVariants] ON;
            INSERT INTO [DietVariants] ([Id], [DietId], [Name], [TargetCalories], [PriceMultiplier], [IsDefault], [CreatedAt], [CreatedBy])
            SELECT v.[Id], v.[DietId], v.[Name], v.[TargetCalories], v.[PriceMultiplier], v.[IsDefault], @now, @auditUser
            FROM (VALUES
                (1, 1, N'Standard 1500', 1500, CAST(1.00 AS decimal(5,2)), CAST(0 AS bit)),
                (2, 1, N'Standard 1800', 1800, CAST(1.10 AS decimal(5,2)), CAST(1 AS bit)),
                (3, 1, N'Standard 2000', 2000, CAST(1.20 AS decimal(5,2)), CAST(0 AS bit)),
                (4, 2, N'Sport 2200', 2200, CAST(1.30 AS decimal(5,2)), CAST(0 AS bit)),
                (5, 2, N'Sport 2500', 2500, CAST(1.40 AS decimal(5,2)), CAST(1 AS bit))
            ) AS v([Id], [DietId], [Name], [TargetCalories], [PriceMultiplier], [IsDefault])
            WHERE NOT EXISTS (SELECT 1 FROM [DietVariants] dv WHERE dv.[Id] = v.[Id]);
            SET IDENTITY_INSERT [DietVariants] OFF;

            INSERT INTO [DietVariantMeals] ([DietVariantId], [MealId], [ServingSizeMultiplier], [SortOrder])
            SELECT v.[DietVariantId], v.[MealId], v.[ServingSizeMultiplier], v.[SortOrder]
            FROM (VALUES
                (1, 1, CAST(0.85 AS decimal(5,2)), 1), (1, 2, CAST(0.85 AS decimal(5,2)), 2), (1, 3, CAST(0.85 AS decimal(5,2)), 3), (1, 4, CAST(0.85 AS decimal(5,2)), 4), (1, 5, CAST(0.85 AS decimal(5,2)), 5),
                (2, 1, CAST(1.00 AS decimal(5,2)), 1), (2, 2, CAST(1.00 AS decimal(5,2)), 2), (2, 3, CAST(1.00 AS decimal(5,2)), 3), (2, 4, CAST(1.00 AS decimal(5,2)), 4), (2, 5, CAST(1.00 AS decimal(5,2)), 5),
                (3, 1, CAST(1.15 AS decimal(5,2)), 1), (3, 2, CAST(1.15 AS decimal(5,2)), 2), (3, 3, CAST(1.15 AS decimal(5,2)), 3), (3, 4, CAST(1.15 AS decimal(5,2)), 4), (3, 5, CAST(1.15 AS decimal(5,2)), 5),
                (4, 1, CAST(1.25 AS decimal(5,2)), 1), (4, 2, CAST(1.25 AS decimal(5,2)), 2), (4, 3, CAST(1.25 AS decimal(5,2)), 3), (4, 4, CAST(1.25 AS decimal(5,2)), 4), (4, 5, CAST(1.25 AS decimal(5,2)), 5),
                (5, 1, CAST(1.40 AS decimal(5,2)), 1), (5, 2, CAST(1.40 AS decimal(5,2)), 2), (5, 3, CAST(1.40 AS decimal(5,2)), 3), (5, 4, CAST(1.40 AS decimal(5,2)), 4), (5, 5, CAST(1.40 AS decimal(5,2)), 5)
            ) AS v([DietVariantId], [MealId], [ServingSizeMultiplier], [SortOrder])
            WHERE NOT EXISTS (
                SELECT 1
                FROM [DietVariantMeals] dvm
                WHERE dvm.[DietVariantId] = v.[DietVariantId]
                  AND dvm.[MealId] = v.[MealId]);

            INSERT INTO [Recipes] ([MealId], [IngredientId], [WeightInGrams], [IsOptional], [Notes])
            SELECT v.[MealId], v.[IngredientId], v.[WeightInGrams], 0, v.[Notes]
            FROM (VALUES
                (1, 5, CAST(15.00 AS decimal(8,2)), N'Masło do smażenia jajecznicy'),
                (2, 7, CAST(120.00 AS decimal(8,2)), N'Baza jogurtowa'),
                (2, 4, CAST(50.00 AS decimal(8,2)), N'Dodatek śmietanki'),
                (2, 11, CAST(40.00 AS decimal(8,2)), N'Jagody na wierzch'),
                (3, 14, CAST(1.00 AS decimal(8,2)), N'Pomidory puszka'),
                (3, 15, CAST(60.00 AS decimal(8,2)), N'Makaron'),
                (3, 4, CAST(30.00 AS decimal(8,2)), N'Zabielenie zupy'),
                (4, 2, CAST(150.00 AS decimal(8,2)), N'Filet z łososia'),
                (4, 8, CAST(100.00 AS decimal(8,2)), N'Brokuł'),
                (4, 13, CAST(75.00 AS decimal(8,2)), N'Ryż jaśminowy'),
                (5, 3, CAST(120.00 AS decimal(8,2)), N'Mielona wołowina'),
                (5, 9, CAST(80.00 AS decimal(8,2)), N'Kawałki dyni'),
                (5, 10, CAST(80.00 AS decimal(8,2)), N'Słupki batatów')
            ) AS v([MealId], [IngredientId], [WeightInGrams], [Notes])
            WHERE NOT EXISTS (
                SELECT 1
                FROM [Recipes] r
                WHERE r.[MealId] = v.[MealId]
                  AND r.[IngredientId] = v.[IngredientId]
                  AND r.[IsDeleted] = 0);

            INSERT INTO [NutritionFacts] ([MealId], [CaloriesPer100g], [ProteinPer100g], [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [CreatedAt])
            SELECT v.[MealId], v.[CaloriesPer100g], v.[ProteinPer100g], v.[CarbohydratesPer100g], v.[FatPer100g], v.[FiberPer100g], @now
            FROM (VALUES
                (1, CAST(180.00 AS decimal(8,2)), CAST(12.50 AS decimal(8,2)), CAST(1.20 AS decimal(8,2)), CAST(14.00 AS decimal(8,2)), CAST(0.00 AS decimal(8,2))),
                (2, CAST(210.00 AS decimal(8,2)), CAST(4.50 AS decimal(8,2)), CAST(18.00 AS decimal(8,2)), CAST(12.00 AS decimal(8,2)), CAST(3.50 AS decimal(8,2))),
                (3, CAST(95.00 AS decimal(8,2)), CAST(3.20 AS decimal(8,2)), CAST(14.50 AS decimal(8,2)), CAST(2.80 AS decimal(8,2)), CAST(1.20 AS decimal(8,2))),
                (4, CAST(150.00 AS decimal(8,2)), CAST(18.20 AS decimal(8,2)), CAST(16.00 AS decimal(8,2)), CAST(6.50 AS decimal(8,2)), CAST(1.80 AS decimal(8,2))),
                (5, CAST(165.00 AS decimal(8,2)), CAST(14.00 AS decimal(8,2)), CAST(15.00 AS decimal(8,2)), CAST(8.20 AS decimal(8,2)), CAST(2.50 AS decimal(8,2)))
            ) AS v([MealId], [CaloriesPer100g], [ProteinPer100g], [CarbohydratesPer100g], [FatPer100g], [FiberPer100g])
            WHERE NOT EXISTS (
                SELECT 1
                FROM [NutritionFacts] nf
                WHERE nf.[MealId] = v.[MealId]);

            INSERT INTO [MealAllergens] ([MealId], [AllergenId], [IsTrace])
            SELECT v.[MealId], v.[AllergenId], 0
            FROM (VALUES
                (1, 5),
                (2, 2),
                (3, 1),
                (3, 2),
                (4, 3)
            ) AS v([MealId], [AllergenId])
            WHERE NOT EXISTS (
                SELECT 1
                FROM [MealAllergens] ma
                WHERE ma.[MealId] = v.[MealId]
                  AND ma.[AllergenId] = v.[AllergenId]);
            """,
            new { now, auditUser },
            cancellationToken: cancellationToken));
    }

    private async Task SeedMenuAsync(IDbConnection db, DateTimeOffset now, string auditUser, CancellationToken cancellationToken)
    {
        if (await CountRowsAsync(db, "Categories", cancellationToken) > 0)
        {
            await EnsureCoreMenuRowsAsync(db, now, auditUser, cancellationToken);
            await SeedDietMenuPlansAsync(db, now, auditUser, cancellationToken);
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

    private static async Task EnsureDefaultMealVariantsAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [MealVariants]
                ([MealId], [Name], [VariantType], [Status], [Description], [IsDefault],
                 [RawWeightGrams], [CookedWeightGrams], [CaloriesPer100g], [ProteinPer100g],
                 [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [NutritionSource],
                 [AllergensApproved], [PublishedAt], [PublishedBy], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                m.[Id],
                CONCAT(m.[Name], N' - standard'),
                N'Standard',
                N'Published',
                N'Domyślny wariant dania używany przez wspólny scenariusz demo.',
                1,
                m.[RawWeightGrams],
                m.[CookedWeightGrams],
                nf.[CaloriesPer100g],
                nf.[ProteinPer100g],
                nf.[CarbohydratesPer100g],
                nf.[FatPer100g],
                nf.[FiberPer100g],
                N'Aggregated',
                1,
                @now,
                @auditUser,
                @now,
                @auditUser,
                0
            FROM [Meals] m
            OUTER APPLY (
                SELECT TOP 1
                    facts.[CaloriesPer100g],
                    facts.[ProteinPer100g],
                    facts.[CarbohydratesPer100g],
                    facts.[FatPer100g],
                    facts.[FiberPer100g]
                FROM [NutritionFacts] facts
                WHERE facts.[MealId] = m.[Id]
                ORDER BY facts.[Id] DESC
            ) nf
            WHERE m.[IsDeleted] = 0
              AND NOT EXISTS
              (
                  SELECT 1
                  FROM [MealVariants] mv
                  WHERE mv.[MealId] = m.[Id]
                    AND mv.[IsDeleted] = 0
              );
            """,
            new { now, auditUser },
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureDemoMealVariantComponentsAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE mv
            SET [RawWeightGrams] = COALESCE(mv.[RawWeightGrams], m.[RawWeightGrams], mw.[RawWeightGrams]),
                [CookedWeightGrams] = COALESCE(mv.[CookedWeightGrams], m.[CookedWeightGrams], m.[RawWeightGrams], mw.[RawWeightGrams]),
                [CaloriesPer100g] = COALESCE(mv.[CaloriesPer100g], nf.[CaloriesPer100g]),
                [ProteinPer100g] = COALESCE(mv.[ProteinPer100g], nf.[ProteinPer100g]),
                [CarbohydratesPer100g] = COALESCE(mv.[CarbohydratesPer100g], nf.[CarbohydratesPer100g]),
                [FatPer100g] = COALESCE(mv.[FatPer100g], nf.[FatPer100g]),
                [FiberPer100g] = COALESCE(mv.[FiberPer100g], nf.[FiberPer100g]),
                [NutritionSource] = CASE
                    WHEN mv.[NutritionSource] IS NULL OR mv.[NutritionSource] = N'' OR mv.[NutritionSource] = N'Meal'
                        THEN N'Aggregated'
                    ELSE mv.[NutritionSource]
                END,
                [AllergensApproved] = 1,
                [AllergenOverrideReason] = COALESCE(mv.[AllergenOverrideReason], N'Demo seeder: alergeny wariantu zatwierdzone na podstawie składowych.'),
                [PublishedAt] = COALESCE(mv.[PublishedAt], @now),
                [PublishedBy] = COALESCE(mv.[PublishedBy], @auditUser),
                [Status] = N'Published',
                [UpdatedAt] = @now,
                [UpdatedBy] = @auditUser
            FROM [MealVariants] mv
            INNER JOIN [Meals] m ON m.[Id] = mv.[MealId]
            OUTER APPLY (
                SELECT SUM(r.[WeightInGrams]) AS [RawWeightGrams]
                FROM [Recipes] r
                WHERE r.[MealId] = m.[Id]
                  AND r.[IsDeleted] = 0
            ) mw
            OUTER APPLY (
                SELECT TOP 1 *
                FROM [NutritionFacts] facts
                WHERE facts.[MealId] = m.[Id]
                ORDER BY facts.[Id] DESC
            ) nf
            WHERE mv.[IsDeleted] = 0
              AND m.[IsDeleted] = 0
              AND EXISTS (
                    SELECT 1
                    FROM [MealRecipeComponents] mrc
                    WHERE mrc.[MealId] = m.[Id]
                      AND mrc.[IsDeleted] = 0);

            INSERT INTO [MealVariantComponents]
                ([MealVariantId], [RecipeComponentVersionId], [Role], [QuantityPerServing], [Unit],
                 [SortOrder], [IsOptional], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                mv.[Id],
                mrc.[RecipeComponentVersionId],
                mrc.[Role],
                mrc.[QuantityPerServing],
                mrc.[Unit],
                mrc.[SortOrder],
                mrc.[IsOptional],
                @now,
                @auditUser,
                0
            FROM [MealVariants] mv
            INNER JOIN [MealRecipeComponents] mrc ON mrc.[MealId] = mv.[MealId] AND mrc.[IsDeleted] = 0
            INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mrc.[RecipeComponentVersionId] AND rcv.[IsDeleted] = 0
            WHERE mv.[IsDeleted] = 0
              AND NOT EXISTS (
                    SELECT 1
                    FROM [MealVariantComponents] existing
                    WHERE existing.[MealVariantId] = mv.[Id]
                      AND existing.[RecipeComponentVersionId] = mrc.[RecipeComponentVersionId]
                      AND existing.[IsDeleted] = 0);
            """,
            new { now, auditUser },
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureDietMenuPlanItemsHaveMealVariantsAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dpi
            SET [MealVariantId] = mv.[Id],
                [UpdatedAt] = @now,
                [UpdatedBy] = @auditUser
            FROM [DietMenuPlanItems] dpi
            OUTER APPLY
            (
                SELECT TOP 1 [Id]
                FROM [MealVariants]
                WHERE [MealId] = dpi.[MealId]
                  AND [Status] IN (N'Published', N'Active')
                  AND [IsDeleted] = 0
                ORDER BY [IsDefault] DESC, [Id]
            ) mv
            WHERE dpi.[IsDeleted] = 0
              AND dpi.[MealVariantId] IS NULL
              AND mv.[Id] IS NOT NULL;
            """,
            new { now, auditUser },
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureDemoLifecycleMenuFoundationAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await EnsureDefaultMealVariantsAsync(db, now, auditUser, cancellationToken);

        var preferredDietVariantId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 dv.[Id]
            FROM [DietVariants] dv
            INNER JOIN [Diets] d ON d.[Id] = dv.[DietId]
            WHERE dv.[Name] = N'Standard 1800'
              AND dv.[IsDeleted] = 0
              AND d.[IsDeleted] = 0
            ORDER BY dv.[IsDefault] DESC, dv.[Id];
            """,
            cancellationToken: cancellationToken));

        if (preferredDietVariantId.HasValue)
        {
            return preferredDietVariantId.Value;
        }

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
                ([DietMenuPlanId], [DietVariantId], [MealId], [MealVariantId], [MealSlot], [ServingSizeMultiplier], [SortOrder],
                 [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                @planId,
                dvm.[DietVariantId],
                dvm.[MealId],
                mv.[Id],
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
            OUTER APPLY
            (
                SELECT TOP 1 [Id]
                FROM [MealVariants]
                WHERE [MealId] = dvm.[MealId]
                  AND [IsDeleted] = 0
                ORDER BY [IsDefault] DESC, [Id]
            ) mv
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

        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE dpi
            SET [MealVariantId] = mv.[Id],
                [UpdatedAt] = @now,
                [UpdatedBy] = @auditUser
            FROM [DietMenuPlanItems] dpi
            OUTER APPLY
            (
                SELECT TOP 1 [Id]
                FROM [MealVariants]
                WHERE [MealId] = dpi.[MealId]
                  AND [IsDeleted] = 0
                ORDER BY [IsDefault] DESC, [Id]
            ) mv
            WHERE dpi.[DietMenuPlanId] = @planId
              AND dpi.[MealVariantId] IS NULL
              AND mv.[Id] IS NOT NULL;
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
                dpi.[MealVariantId],
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
                    ([OrderId], [DietId], [DietVariantId], [MealId], [MealVariantId], [DietMenuPlanItemId],
                     [DietName], [VariantName], [MealSlot], [CaloriesPerDay],
                     [PricePerDay], [TotalDays], [TotalPrice], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@orderId, @dietId, @dietVariantId, @mealId, @mealVariantId, @dietMenuPlanItemId,
                     @dietName, @variantName, @mealSlot, @caloriesPerDay,
                     @pricePerDay, 1, @pricePerDay, @now, @auditUser, 0);
                """,
                new
                {
                    orderId,
                    dietId = meal.DietId,
                    dietVariantId = meal.DietVariantId,
                    mealId = meal.MealId,
                    mealVariantId = meal.MealVariantId,
                    dietMenuPlanItemId = meal.DietMenuPlanItemId,
                    dietName = $"{meal.DietName}: {meal.MealName}",
                    variantName = $"{meal.VariantName} / {meal.MealSlot}",
                    mealSlot = meal.MealSlot,
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
        string orderNumber,
        decimal amount,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var paymentIntentId = $"pi_{orderNumber.ToLowerInvariant().Replace("-", "_")}";
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

    private sealed class DemoLifecycleMeal
    {
        public int DietMenuPlanItemId { get; set; }

        public int MealId { get; set; }

        public int? MealVariantId { get; set; }

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

    private sealed record DemoPackagingStockItem(
        string Name,
        decimal MinimumLevel,
        decimal TargetQuantity,
        int LeadTimeDays);

    private static Task<int> CountRowsAsync(IDbConnection db, string tableName, CancellationToken cancellationToken)
        => db.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM [{tableName}];",
            cancellationToken: cancellationToken));
}
