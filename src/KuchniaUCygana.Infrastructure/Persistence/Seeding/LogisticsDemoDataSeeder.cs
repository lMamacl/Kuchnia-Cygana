using System.Data;
using System.Globalization;
using System.Text.Json;
using Bogus;
using Dapper;
using KuchniaUCygana.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Infrastructure.Persistence.Seeding;

public sealed class LogisticsDemoDataSeeder
{
    private const string DemoRoutePrefix = "DEMO-M4";
    private static readonly Action<ILogger, string, Exception?> DemoRouteAlreadyExists =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, nameof(DemoRouteAlreadyExists)),
            "Logistics demo route {RouteName} already exists.");

    private static readonly Action<ILogger, string, int, Exception?> DemoRouteSeeded =
        LoggerMessage.Define<string, int>(
            LogLevel.Information,
            new EventId(2, nameof(DemoRouteSeeded)),
            "Seeded logistics demo route {RouteName} with {StopCount} ready stops.");

    private readonly ILogger<LogisticsDemoDataSeeder> logger;

    public LogisticsDemoDataSeeder(ILogger<LogisticsDemoDataSeeder> logger)
    {
        this.logger = logger;
    }

    public async Task SeedAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var routeName = $"{DemoRoutePrefix}-{today:yyyyMMdd}";

        if (await RouteExistsAsync(db, routeName, cancellationToken))
        {
            DemoRouteAlreadyExists(this.logger, routeName, null);
            return;
        }

        if (db.State != ConnectionState.Open)
        {
            db.Open();
        }

        using var transaction = db.BeginTransaction();
        try
        {
            var driverUserId = await EnsureDriverUserAsync(db, transaction, now, cancellationToken);
            var driverId = await EnsureDriverAsync(db, transaction, driverUserId, now, auditUser, cancellationToken);
            var vehicle = await EnsureAssignedVehicleAsync(db, transaction, driverId, now, auditUser, cancellationToken);
            var routeId = await InsertRouteAsync(db, transaction, routeName, vehicle.Id, now, auditUser, cancellationToken);
            var deliveries = GenerateDeliveries(today);
            var packages = new List<DemoManifestPackage>();

            foreach (var delivery in deliveries)
            {
                var customerId = await EnsureCustomerAsync(db, transaction, delivery, now, cancellationToken);
                var addressId = await EnsureAddressAsync(db, transaction, customerId, delivery, now, auditUser, cancellationToken);
                var orderId = await EnsureOrderAsync(db, transaction, customerId, delivery, today, now, auditUser, cancellationToken);
                await EnsureOrderItemAsync(db, transaction, orderId, delivery, now, auditUser, cancellationToken);
                var deliveryCalendarId = await EnsureDeliveryCalendarAsync(
                    db,
                    transaction,
                    orderId,
                    addressId,
                    delivery,
                    today,
                    now,
                    auditUser,
                    cancellationToken);
                await InsertStopAsync(
                    db,
                    transaction,
                    routeId,
                    deliveryCalendarId,
                    delivery,
                    today,
                    now,
                    auditUser,
                    cancellationToken);
                var sessionId = await EnsurePackingSessionAsync(
                    db,
                    transaction,
                    orderId,
                    deliveryCalendarId,
                    delivery,
                    today,
                    now,
                    auditUser,
                    cancellationToken);
                var bag = await EnsurePackingBagAsync(
                    db,
                    transaction,
                    sessionId,
                    delivery,
                    today,
                    now,
                    auditUser,
                    cancellationToken);

                await EnsurePackingItemsAsync(
                    db,
                    transaction,
                    sessionId,
                    bag.Id,
                    delivery,
                    today,
                    now,
                    auditUser,
                    cancellationToken);
                var labelId = await EnsureShippingLabelAsync(
                    db,
                    transaction,
                    sessionId,
                    bag.Id,
                    delivery,
                    routeName,
                    today,
                    now,
                    cancellationToken);

                packages.Add(new DemoManifestPackage(
                    sessionId,
                    bag.Id,
                    bag.Code,
                    deliveryCalendarId,
                    labelId,
                    $"DEMO-LABEL-{today:yyyyMMdd}-{delivery.SequenceNumber:D2}"));
            }

            await InsertReadyManifestAsync(
                db,
                transaction,
                routeId,
                routeName,
                vehicle,
                driverUserId,
                packages,
                today,
                now,
                auditUser,
                cancellationToken);
            await EnsureThermalBagsAsync(db, transaction, now, auditUser, cancellationToken);

            transaction.Commit();
            DemoRouteSeeded(this.logger, routeName, deliveries.Count, null);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static async Task<bool> RouteExistsAsync(
        IDbConnection db,
        string routeName,
        CancellationToken cancellationToken)
    {
        var count = await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM [DeliveryRoutes]
            WHERE [Name] = @routeName
              AND [IsDeleted] = 0;
            """,
            new { routeName },
            cancellationToken: cancellationToken));
        return count > 0;
    }

    private static async Task<int> EnsureDriverUserAsync(
        IDbConnection db,
        IDbTransaction transaction,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingId = await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [Users]
            WHERE [Role] = @role
            ORDER BY [Id];
            """,
            new { role = UserRoles.Driver },
            transaction,
            cancellationToken: cancellationToken));

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Users]
                ([Email], [PasswordHash], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt])
            VALUES
                (@email, @passwordHash, @firstName, @lastName, @role, @now, NULL);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                email = "driver@kuchnia.local",
                passwordHash = BCrypt.Net.BCrypt.HashPassword("Driver123!"),
                firstName = "Dostawa",
                lastName = "Kierowca",
                role = UserRoles.Driver,
                now,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureDriverAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int userId,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var existingId = await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [Drivers]
            WHERE [UserId] = @userId
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { userId },
            transaction,
            cancellationToken: cancellationToken));

        if (existingId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                "UPDATE [Drivers] SET [IsActive] = 1 WHERE [Id] = @id;",
                new { id = existingId.Value },
                transaction,
                cancellationToken: cancellationToken));
            return existingId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Drivers]
                ([UserId], [LicenseNumber], [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@userId, @licenseNumber, 1, @now, @auditUser, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                userId,
                licenseNumber = $"DEMO-LIC-{userId:D4}",
                now,
                auditUser,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<DemoVehicle> EnsureAssignedVehicleAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int driverId,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var assignedVehicle = await db.QuerySingleOrDefaultAsync<DemoVehicle>(new CommandDefinition(
            """
            SELECT TOP 1 v.[Id], v.[RegistrationNumber]
            FROM [DriverVehicleAssignments] a
            INNER JOIN [Vehicles] v ON v.[Id] = a.[VehicleId]
            WHERE a.[DriverId] = @driverId
              AND a.[UnassignedAt] IS NULL
              AND v.[IsDeleted] = 0
              AND v.[Status] = @activeStatus
            ORDER BY a.[Id] DESC;
            """,
            new
            {
                driverId,
                activeStatus = (int)VehicleStatus.Active,
            },
            transaction,
            cancellationToken: cancellationToken));

        if (assignedVehicle is not null)
        {
            return assignedVehicle;
        }

        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [DriverVehicleAssignments]
            SET [UnassignedAt] = @now, [UpdatedAt] = @now
            WHERE [DriverId] = @driverId
              AND [UnassignedAt] IS NULL;
            """,
            new { driverId, now },
            transaction,
            cancellationToken: cancellationToken));

        var registrationNumber = $"BI DEMO{driverId:D2}";
        var vehicleId = await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [Vehicles]
            WHERE [RegistrationNumber] = @registrationNumber
              AND [IsDeleted] = 0;
            """,
            new { registrationNumber },
            transaction,
            cancellationToken: cancellationToken));

        if (!vehicleId.HasValue)
        {
            vehicleId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO [Vehicles]
                    ([RegistrationNumber], [Model], [MaxLoadKg], [Status], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@registrationNumber, @model, @maxLoadKg, @status, @now, @auditUser, 0);
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """,
                new
                {
                    registrationNumber,
                    model = "Fiat Ducato Demo",
                    maxLoadKg = 1200m,
                    status = (int)VehicleStatus.Active,
                    now,
                    auditUser,
                },
                transaction,
                cancellationToken: cancellationToken));
        }

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [DriverVehicleAssignments]
                ([DriverId], [VehicleId], [AssignedAt], [UnassignedAt], [CreatedAt], [UpdatedAt])
            VALUES
                (@driverId, @vehicleId, @now, NULL, @now, NULL);
            """,
            new { driverId, vehicleId, now },
            transaction,
            cancellationToken: cancellationToken));

        return new DemoVehicle(vehicleId.Value, registrationNumber);
    }

    private static Task<int> InsertRouteAsync(
        IDbConnection db,
        IDbTransaction transaction,
        string routeName,
        int vehicleId,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        return db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [DeliveryRoutes]
                ([RouteDate], [Name], [TotalDistanceKm], [Status], [VehicleId], [DriverId],
                 [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@routeDate, @routeName, @totalDistanceKm, @status, @vehicleId, NULL,
                 @now, @auditUser, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                routeDate = new DateTimeOffset(DateTime.Today),
                routeName,
                totalDistanceKm = 18.4,
                status = (int)RouteStatus.Assigned,
                vehicleId,
                now,
                auditUser,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static List<DemoDelivery> GenerateDeliveries(DateOnly today)
    {
        Randomizer.Seed = new Random(today.DayNumber);
        var faker = new Faker("pl");
        var locations = new[]
        {
            new DemoLocation("Lipowa", "12", "15-424", 53.132488, 23.154221),
            new DemoLocation("Swietojanska", "28", "15-277", 53.126744, 23.167932),
            new DemoLocation("Mickiewicza", "41", "15-213", 53.124144, 23.178155),
            new DemoLocation("Warszawska", "67", "15-062", 53.131816, 23.180868),
        };

        return locations
            .Select((location, index) =>
            {
                var sequence = index + 1;
                return new DemoDelivery(
                    sequence,
                    $"demo-logistics-{today:yyyyMMdd}-{sequence:D2}@kuchnia.local",
                    faker.Name.FirstName(),
                    faker.Name.LastName(),
                    location,
                    $"DEMO-LOG-{today:yyyyMMdd}-{sequence:D2}",
                    sequence % 2 == 0 ? "Sport 2200" : "Standard 1800",
                    sequence % 2 == 0 ? 2200 : 1800);
            })
            .ToList();
    }

    private static async Task<int> EnsureCustomerAsync(
        IDbConnection db,
        IDbTransaction transaction,
        DemoDelivery delivery,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existingId = await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 [Id] FROM [Users] WHERE [Email] = @email;",
            new { delivery.Email },
            transaction,
            cancellationToken: cancellationToken));

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Users]
                ([Email], [PasswordHash], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt])
            VALUES
                (@email, @passwordHash, @firstName, @lastName, @role, @now, NULL);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                delivery.Email,
                passwordHash = BCrypt.Net.BCrypt.HashPassword("Client123!"),
                delivery.FirstName,
                delivery.LastName,
                role = UserRoles.Client,
                now,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureAddressAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int customerId,
        DemoDelivery delivery,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var existingId = await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [Addresses]
            WHERE [UserId] = @customerId
              AND [Label] = @label
              AND [IsDeleted] = 0;
            """,
            new
            {
                customerId,
                label = DemoRoutePrefix,
            },
            transaction,
            cancellationToken: cancellationToken));

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Addresses]
                ([UserId], [Label], [Street], [BuildingNumber], [City], [PostalCode],
                 [IsDefault], [DeliveryNotes], [Latitude], [Longitude], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@customerId, @label, @street, @buildingNumber, @city, @postalCode,
                 1, @deliveryNotes, @latitude, @longitude, @now, @auditUser, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                customerId,
                label = DemoRoutePrefix,
                street = delivery.Location.Street,
                buildingNumber = delivery.Location.BuildingNumber,
                city = "Bialystok",
                postalCode = delivery.Location.PostalCode,
                deliveryNotes = $"Demo M4, przystanek {delivery.SequenceNumber}.",
                latitude = delivery.Location.Latitude,
                longitude = delivery.Location.Longitude,
                now,
                auditUser,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureOrderAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int customerId,
        DemoDelivery delivery,
        DateOnly today,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var existingId = await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 [Id] FROM [Orders] WHERE [OrderNumber] = @orderNumber AND [IsDeleted] = 0;",
            new { orderNumber = delivery.OrderNumber },
            transaction,
            cancellationToken: cancellationToken));

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Orders]
                ([CustomerId], [OrderNumber], [Status], [TotalPrice], [DiscountAmount], [FinalPrice],
                 [Notes], [StartDate], [EndDate], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@customerId, @orderNumber, @status, @totalPrice, 0, @totalPrice,
                 @notes, @deliveryDate, @deliveryDate, @now, @auditUser, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                customerId,
                orderNumber = delivery.OrderNumber,
                status = (int)OrderStatus.InProduction,
                totalPrice = 74.90m,
                notes = "Scenariusz demonstracyjny logistyki.",
                deliveryDate = today.ToDateTime(TimeOnly.MinValue),
                now,
                auditUser,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureOrderItemAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int orderId,
        DemoDelivery delivery,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var count = await db.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [OrderItems] WHERE [OrderId] = @orderId AND [IsDeleted] = 0;",
            new { orderId },
            transaction,
            cancellationToken: cancellationToken));

        if (count > 0)
        {
            return;
        }

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
                dietId = delivery.SequenceNumber % 2 == 0 ? 2 : 1,
                dietVariantId = delivery.SequenceNumber % 2 == 0 ? 4 : 2,
                dietName = delivery.SequenceNumber % 2 == 0 ? "Sport" : "Standard",
                variantName = delivery.DietVariant,
                caloriesPerDay = delivery.Calories,
                pricePerDay = 74.90m,
                now,
                auditUser,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureDeliveryCalendarAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int orderId,
        int addressId,
        DemoDelivery delivery,
        DateOnly today,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var existingId = await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [DeliveryCalendar]
            WHERE [OrderId] = @orderId
              AND [DeliveryDate] >= @from
              AND [DeliveryDate] < @to
              AND [IsDeleted] = 0;
            """,
            new
            {
                orderId,
                from = today.ToDateTime(TimeOnly.MinValue),
                to = today.AddDays(1).ToDateTime(TimeOnly.MinValue),
            },
            transaction,
            cancellationToken: cancellationToken));

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [DeliveryCalendar]
                ([OrderId], [AddressId], [DeliveryWindowId], [DeliveryDate], [Status],
                 [IsSkipped], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@orderId, @addressId, 1, @deliveryDate, @status,
                 0, @now, @auditUser, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                orderId,
                addressId,
                deliveryDate = today.ToDateTime(new TimeOnly(7 + delivery.SequenceNumber, 0)),
                status = (int)DeliveryStatus.Scheduled,
                now,
                auditUser,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task<int> InsertStopAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int routeId,
        int deliveryCalendarId,
        DemoDelivery delivery,
        DateOnly today,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        return db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [DeliveryRouteStops]
                ([RouteId], [DeliveryCalendarId], [SequenceNumber], [PlannedArrivalTime],
                 [Status], [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@routeId, @deliveryCalendarId, @sequenceNumber, @plannedArrivalTime,
                 @status, @now, @auditUser, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                routeId,
                deliveryCalendarId,
                sequenceNumber = delivery.SequenceNumber,
                plannedArrivalTime = new DateTimeOffset(today.ToDateTime(new TimeOnly(8 + delivery.SequenceNumber, 0))),
                status = (int)StopStatus.Assigned,
                now,
                auditUser,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsurePackingSessionAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int orderId,
        int deliveryCalendarId,
        DemoDelivery delivery,
        DateOnly today,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var existingId = await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [PackingSessions]
            WHERE [DeliveryCalendarId] = @deliveryCalendarId
              AND [PackingDate] = @packingDate
              AND [IsDeleted] = 0;
            """,
            new
            {
                deliveryCalendarId,
                packingDate = today.ToDateTime(TimeOnly.MinValue),
            },
            transaction,
            cancellationToken: cancellationToken));

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [PackingSessions]
                ([PackingDate], [OrderId], [ClientName], [PackedBy], [Status], [DeliveryCalendarId],
                 [CreatedBy], [CreatedAt], [IsDeleted])
            VALUES
                (@packingDate, @orderId, @clientName, @packedBy, @status, @deliveryCalendarId,
                 @auditUser, @now, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                packingDate = today.ToDateTime(TimeOnly.MinValue),
                orderId,
                clientName = $"{delivery.FirstName} {delivery.LastName}",
                packedBy = "DemoPacker",
                status = (int)PackingStatus.Dispatched,
                deliveryCalendarId,
                auditUser,
                now,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task<DemoPackingBag> EnsurePackingBagAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int sessionId,
        DemoDelivery delivery,
        DateOnly today,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var existing = await db.QuerySingleOrDefaultAsync<DemoPackingBag>(new CommandDefinition(
            """
            SELECT TOP 1 [Id], [BagCode] AS [Code]
            FROM [PackingBags]
            WHERE [PackingSessionId] = @sessionId
              AND [BagNumber] = 1
              AND [IsDeleted] = 0;
            """,
            new { sessionId },
            transaction,
            cancellationToken: cancellationToken));

        if (existing is not null)
        {
            return existing;
        }

        var code = $"DEMO-BAG-{today:yyyyMMdd}-{delivery.SequenceNumber:D2}";
        var id = await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [PackingBags]
                ([PackingSessionId], [BagNumber], [BagCode], [Status], [PackedAt], [PackedBy],
                 [LabeledAt], [ManifestedAt], [LoadedAt], [LoadedBy], [DispatchedAt],
                 [CreatedBy], [CreatedAt], [IsDeleted])
            VALUES
                (@sessionId, 1, @code, @status, @packedAt, @packedBy,
                 @labeledAt, @manifestedAt, @loadedAt, @loadedBy, @dispatchedAt,
                 @auditUser, @now, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                sessionId,
                code,
                status = (int)PackingBagStatus.Dispatched,
                packedAt = now.AddMinutes(-50),
                packedBy = "DemoPacker",
                labeledAt = now.AddMinutes(-45),
                manifestedAt = now.AddMinutes(-30),
                loadedAt = now.AddMinutes(-20),
                loadedBy = "DemoLoader",
                dispatchedAt = now.AddMinutes(-10),
                auditUser,
                now,
            },
            transaction,
            cancellationToken: cancellationToken));

        return new DemoPackingBag(id, code);
    }

    private static async Task EnsurePackingItemsAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int sessionId,
        int bagId,
        DemoDelivery delivery,
        DateOnly today,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var count = await db.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM [PackingItems] WHERE [PackingSessionId] = @sessionId AND [IsDeleted] = 0;",
            new { sessionId },
            transaction,
            cancellationToken: cancellationToken));

        if (count > 0)
        {
            return;
        }

        var items = new[]
        {
            new { MealId = 1, MealName = "Sniadanie demo" },
            new { MealId = 3, MealName = "Obiad demo" },
        };

        for (var index = 0; index < items.Length; index++)
        {
            var item = items[index];
            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [PackingItems]
                    ([PackingSessionId], [MealId], [MealName], [DietVariantId], [PackingBagId],
                     [BoxCode], [Status], [FoilPrintedAt], [PackedAt], [PackedBy],
                     [CreatedBy], [CreatedAt], [IsDeleted])
                VALUES
                    (@sessionId, @mealId, @mealName, @dietVariantId, @bagId,
                     @boxCode, @status, @foilPrintedAt, @packedAt, @packedBy,
                     @auditUser, @now, 0);
                """,
                new
                {
                    sessionId,
                    mealId = item.MealId,
                    mealName = item.MealName,
                    dietVariantId = delivery.SequenceNumber % 2 == 0 ? 4 : 2,
                    bagId,
                    boxCode = $"DEMO-BOX-{today:yyyyMMdd}-{delivery.SequenceNumber:D2}-{index + 1:D2}",
                    status = (int)PackingItemStatus.Packed,
                    foilPrintedAt = now.AddMinutes(-70),
                    packedAt = now.AddMinutes(-55),
                    packedBy = "DemoPacker",
                    auditUser,
                    now,
                },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private static async Task<int> EnsureShippingLabelAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int sessionId,
        int bagId,
        DemoDelivery delivery,
        string routeName,
        DateOnly today,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var code = $"DEMO-LABEL-{today:yyyyMMdd}-{delivery.SequenceNumber:D2}";
        var existingId = await db.QuerySingleOrDefaultAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 [Id] FROM [PackingLabels] WHERE [QrCode] = @code;",
            new { code },
            transaction,
            cancellationToken: cancellationToken));

        if (existingId.HasValue)
        {
            return existingId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [PackingLabels]
                ([PackingSessionId], [PackingBagId], [LabelType], [QrCode], [ClientName],
                 [RouteInfo], [DeliveryWindow], [PrintNumber], [PrintedAt], [PrintedBy],
                 [AttachedAt], [AttachedBy], [CreatedAt])
            VALUES
                (@sessionId, @bagId, @labelType, @code, @clientName,
                 @routeInfo, @deliveryWindow, 1, @printedAt, @printedBy,
                 @attachedAt, @attachedBy, @now);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            new
            {
                sessionId,
                bagId,
                labelType = 1,
                code,
                clientName = $"{delivery.FirstName} {delivery.LastName}",
                routeInfo = $"{routeName}, stop {delivery.SequenceNumber}",
                deliveryWindow = "06:00-10:00",
                printedAt = now.AddMinutes(-45),
                printedBy = "DemoPacker",
                attachedAt = now.AddMinutes(-40),
                attachedBy = "DemoPacker",
                now,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static Task<int> InsertReadyManifestAsync(
        IDbConnection db,
        IDbTransaction transaction,
        int routeId,
        string routeName,
        DemoVehicle vehicle,
        int driverUserId,
        IReadOnlyList<DemoManifestPackage> packages,
        DateOnly today,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var manifestNumber = $"DEMO-MAN-{today:yyyyMMdd}-{routeId:D4}";
        var payload = JsonSerializer.Serialize(new
        {
            manifestNumber,
            packingDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            generatedAt = now,
            generatedBy = auditUser,
            route = new
            {
                RouteId = routeId,
                RouteName = routeName,
                VehicleId = vehicle.Id,
                VehicleRegistration = vehicle.RegistrationNumber,
                packages = packages.Select(package => new
                {
                    packageId = package.SessionId,
                    packingBagId = package.BagId,
                    bagCode = package.BagCode,
                    package.DeliveryCalendarId,
                    transportLabelId = package.LabelId,
                    transportCode = package.LabelCode,
                    transportPrintNumber = 1,
                }),
            },
        });

        return db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [PackingManifests]
                ([PackingDate], [ManifestNumber], [RouteId], [RouteName], [VehicleId], [VehicleRegistration],
                 [RouteCount], [BagCount], [GeneratedAt], [GeneratedBy], [IsVerified], [VerifiedAt], [VerifiedBy],
                 [VerifiedByUserId], [WorkerApprovedAt], [WorkerApprovedBy], [WorkerApprovedByUserId],
                 [SentToLogisticsAt], [SentToLogisticsByUserId], [RequiresRegeneration], [DriverUserId],
                 [PayloadJson], [CreatedAt], [UpdatedAt])
            VALUES
                (@packingDate, @manifestNumber, @routeId, @routeName, @vehicleId, @vehicleRegistration,
                 1, @bagCount, @now, @auditUser, 1, @now, @auditUser,
                 @driverUserId, @now, @auditUser, @driverUserId,
                 @now, @driverUserId, 0, @driverUserId,
                 @payload, @now, NULL);
            """,
            new
            {
                packingDate = today.ToDateTime(TimeOnly.MinValue),
                manifestNumber,
                routeId,
                routeName,
                vehicleId = vehicle.Id,
                vehicleRegistration = vehicle.RegistrationNumber,
                bagCount = packages.Count,
                now,
                auditUser,
                driverUserId,
                payload,
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureThermalBagsAsync(
        IDbConnection db,
        IDbTransaction transaction,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        for (var index = 1; index <= 12; index++)
        {
            var serialNumber = $"THERM-DEMO-{index:D3}";
            var exists = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT COUNT(1) FROM [ThermalBags] WHERE [SerialNumber] = @serialNumber AND [IsDeleted] = 0;",
                new { serialNumber },
                transaction,
                cancellationToken: cancellationToken));

            if (exists > 0)
            {
                continue;
            }

            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [ThermalBags]
                    ([SerialNumber], [Status], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@serialNumber, @status, @now, @auditUser, 0);
                """,
                new
                {
                    serialNumber,
                    status = (int)BagStatus.Available,
                    now,
                    auditUser,
                },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private sealed record DemoLocation(
        string Street,
        string BuildingNumber,
        string PostalCode,
        double Latitude,
        double Longitude);

    private sealed record DemoDelivery(
        int SequenceNumber,
        string Email,
        string FirstName,
        string LastName,
        DemoLocation Location,
        string OrderNumber,
        string DietVariant,
        int Calories);

    private sealed record DemoVehicle(int Id, string RegistrationNumber);

    private sealed record DemoPackingBag(int Id, string Code);

    private sealed record DemoManifestPackage(
        int SessionId,
        int BagId,
        string BagCode,
        int DeliveryCalendarId,
        int LabelId,
        string LabelCode);
}
