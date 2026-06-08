using System.Data;
using BCrypt.Net;
using Dapper;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Services;
using KuchniaUCygana.Application.Services.Menu;
using KuchniaUCygana.Infrastructure.Adapters;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Infrastructure.Persistence.Repositories;
using KuchniaUCygana.Infrastructure.Persistence.Repositories.Production;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Infrastructure.Persistence.Seeding;

public sealed class DatabaseSeeder : IDatabaseSeeder
{
    private const int PackagingWarehouseCategoryId = 5;
    private const string UnifiedDemoCustomerEmailPrefix = "demo-klient-";
    private const string UnifiedDemoCustomerEmailSuffix = "@kuchnia.local";
    private const string UnifiedDemoCustomerPassword = "Demo123!";
    private const string UnifiedDemoOrderPrefix = "DEMO-M1";
    private static readonly DateOnly PresentationDemoDate = new(2026, 6, 9);
    private const string M2VolumePrefix = "VOL-M2-";

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
                await BackfillPublishedDietMenuSnapshotsAsync(cancellationToken);
                return;
            case DatabaseSeedingProfile.VolumeDemo:
                await SeedMinimalRealisticAsync(cancellationToken);
                await SeedM2VolumeDemoAsync(cancellationToken);
                await BackfillPublishedDietMenuSnapshotsAsync(cancellationToken);
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
        await SeedModule5OperationsAsync(db, now, cancellationToken);
    }

    private async Task SeedUsersAsync(IDbConnection db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var users = new[]
        {
            MakeUser("admin@kuchnia.local", "Admin123!", "System", "Administrator", UserRoles.Admin, now),
            MakeUser("kitchen@kuchnia.local", "Kitchen123!", "Marek", "Kowalski", UserRoles.Kitchen, now),
            MakeUser("kitchenm@kuchnia.local", "Kitchen123!", "Joanna", "Witkowska", UserRoles.KitchenManager, now),
            MakeUser("warehouse@kuchnia.local", "Warehouse123!", "Pawel", "Mazur", UserRoles.Warehouse, now),
            MakeUser("warehousem@kuchnia.local", "Warehouse123!", "Tomasz", "Baran", UserRoles.WarehouseManager, now),
            MakeUser("packing@kuchnia.local", "Packing123!", "Karolina", "Lis", UserRoles.Packing, now),
            MakeUser("packingm@kuchnia.local", "Packing123!", "Ewa", "Kaczmarek", UserRoles.PackingManager, now),
            MakeUser("dietitian@kuchnia.local", "Diet123!", "Anna", "Zielinska", UserRoles.Dietitian, now),
            MakeUser("logistics@kuchnia.local", "Logistics123!", "Lukasz", "Dabrowski", UserRoles.Logistics, now),
            MakeUser("logisticsm@kuchnia.local", "Logistics123!", "Monika", "Sokol", UserRoles.LogisticsManager, now),
            MakeUser("driver@kuchnia.local", "Driver123!", "Kamil", "Nowicki", UserRoles.Driver, now),
            MakeUser("driverm@kuchnia.local", "Driver123!", "Dostawa", "Koordynator", UserRoles.DriverManager, now),
            MakeUser("hr@kuchnia.local", "HR123!", "Alicja", "Nowak", UserRoles.HR, now),
            MakeUser("hrm@kuchnia.local", "HR123!", "Beata", "Lewandowska", UserRoles.HRManager, now),
            MakeUser("bok@kuchnia.local", "BOK123!", "Natalia", "Wrona", UserRoles.BOK, now),
            MakeUser("bokm@kuchnia.local", "BOK123!", "Piotr", "Malec", UserRoles.BOKManager, now),
            MakeUser("client.anna@kuchnia.local", "Client123!", "Anna", "Maj", UserRoles.Client, now),
            MakeUser("client.michal@kuchnia.local", "Client123!", "Michal", "Rutkowski", UserRoles.Client, now),
            MakeUser("client.katarzyna@kuchnia.local", "Client123!", "Katarzyna", "Wojcik", UserRoles.Client, now),
            MakeUser("client.robert@kuchnia.local", "Client123!", "Robert", "Krawczyk", UserRoles.Client, now),
        };

        var inserted = await db.ExecuteAsync(new CommandDefinition(
            """
            IF NOT EXISTS (SELECT 1 FROM [Users] WHERE [Email] = @Email)
            BEGIN
                INSERT INTO [Users] ([Email], [PasswordHash], [FirstName], [LastName], [Role], [CreatedAt], [UpdatedAt])
                VALUES (@Email, @PasswordHash, @FirstName, @LastName, @Role, @CreatedAt, @UpdatedAt);
            END
            ELSE
            BEGIN
                UPDATE [Users]
                SET [FirstName] = @FirstName,
                    [LastName] = @LastName,
                    [Role] = @Role,
                    [UpdatedAt] = @CreatedAt
                WHERE [Email] = @Email
                  AND
                  (
                      [FirstName] <> @FirstName
                      OR [LastName] <> @LastName
                      OR [Role] <> @Role
                  );
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
            new { Email = "admin@kuchnia.local", DepartmentName = "Administracja", Position = "Administrator systemu", PhoneNumber = "+48 500 100 001" },
            new { Email = "kitchen@kuchnia.local", DepartmentName = "Kuchnia", Position = "Kucharz", PhoneNumber = "+48 500 100 011" },
            new { Email = "kitchenm@kuchnia.local", DepartmentName = "Kuchnia", Position = "Szef kuchni", PhoneNumber = "+48 500 100 012" },
            new { Email = "warehouse@kuchnia.local", DepartmentName = "Magazyn", Position = "Magazynier", PhoneNumber = "+48 500 100 021" },
            new { Email = "warehousem@kuchnia.local", DepartmentName = "Magazyn", Position = "Kierownik magazynu", PhoneNumber = "+48 500 100 022" },
            new { Email = "packing@kuchnia.local", DepartmentName = "Kompletacja", Position = "Pakowacz", PhoneNumber = "+48 500 100 031" },
            new { Email = "packingm@kuchnia.local", DepartmentName = "Kompletacja", Position = "Kierownik kompletacji", PhoneNumber = "+48 500 100 032" },
            new { Email = "dietitian@kuchnia.local", DepartmentName = "Diety", Position = "Dietetyk", PhoneNumber = "+48 500 100 041" },
            new { Email = "logistics@kuchnia.local", DepartmentName = "Logistyka", Position = "Logistyk", PhoneNumber = "+48 500 100 051" },
            new { Email = "logisticsm@kuchnia.local", DepartmentName = "Logistyka", Position = "Kierownik logistyki", PhoneNumber = "+48 500 100 052" },
            new { Email = "driver@kuchnia.local", DepartmentName = "Logistyka", Position = "Kierowca", PhoneNumber = "+48 500 100 053" },
            new { Email = "hr@kuchnia.local", DepartmentName = "HR", Position = "Specjalista HR", PhoneNumber = "+48 500 100 061" },
            new { Email = "hrm@kuchnia.local", DepartmentName = "HR", Position = "Kierownik HR", PhoneNumber = "+48 500 100 062" },
            new { Email = "bok@kuchnia.local", DepartmentName = "BOK", Position = "Konsultant BOK", PhoneNumber = "+48 500 100 071" },
            new { Email = "bokm@kuchnia.local", DepartmentName = "BOK", Position = "Kierownik BOK", PhoneNumber = "+48 500 100 072" },
        };

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Employees]
                ([UserId], [FirstName], [LastName], [Email], [PhoneNumber], [HireDate], [TerminationDate],
                 [DepartmentId], [Position], [IsActive], [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt],
                 [DeletedBy], [CreatedAt], [UpdatedAt])
            SELECT
                u.[Id], u.[FirstName], u.[LastName], u.[Email], @PhoneNumber, @HireDate, NULL,
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
                employee.PhoneNumber,
                HireDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(-3)),
                CreatedBy = "DatabaseSeeder",
                CreatedAt = now,
            }),
            cancellationToken: cancellationToken));

        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE e
            SET [FirstName] = u.[FirstName],
                [LastName] = u.[LastName],
                [Email] = u.[Email],
                [PhoneNumber] = CASE
                    WHEN e.[PhoneNumber] IS NULL OR e.[PhoneNumber] = N'' THEN @PhoneNumber
                    ELSE e.[PhoneNumber]
                END,
                [DepartmentId] = d.[Id],
                [Position] = @Position,
                [UpdatedBy] = @UpdatedBy,
                [UpdatedAt] = @UpdatedAt
            FROM [Employees] e
            INNER JOIN [Users] u ON u.[Id] = e.[UserId]
            INNER JOIN [Departments] d ON d.[Name] = @DepartmentName
            WHERE u.[Email] = @Email
              AND e.[IsDeleted] = 0;
            """,
            employees.Select(employee => new
            {
                employee.Email,
                employee.DepartmentName,
                employee.Position,
                employee.PhoneNumber,
                UpdatedBy = "DatabaseSeeder",
                UpdatedAt = now,
            }),
            cancellationToken: cancellationToken));

        var departmentHeads = new[]
        {
            new { DepartmentName = "Kuchnia", Email = "kitchenm@kuchnia.local" },
            new { DepartmentName = "Magazyn", Email = "warehousem@kuchnia.local" },
            new { DepartmentName = "Kompletacja", Email = "packingm@kuchnia.local" },
            new { DepartmentName = "Diety", Email = "dietitian@kuchnia.local" },
            new { DepartmentName = "Logistyka", Email = "logisticsm@kuchnia.local" },
            new { DepartmentName = "HR", Email = "hrm@kuchnia.local" },
            new { DepartmentName = "BOK", Email = "bokm@kuchnia.local" },
            new { DepartmentName = "Administracja", Email = "admin@kuchnia.local" },
        };

        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE d
            SET [HeadEmployeeId] = e.[Id],
                [UpdatedAt] = @UpdatedAt
            FROM [Departments] d
            INNER JOIN [Employees] e ON e.[DepartmentId] = d.[Id] AND e.[IsDeleted] = 0
            INNER JOIN [Users] u ON u.[Id] = e.[UserId]
            WHERE d.[Name] = @DepartmentName
              AND u.[Email] = @Email
              AND (d.[HeadEmployeeId] IS NULL OR d.[HeadEmployeeId] <> e.[Id]);
            """,
            departmentHeads.Select(head => new
            {
                head.DepartmentName,
                head.Email,
                UpdatedAt = now,
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

    private async Task SeedModule5OperationsAsync(IDbConnection db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tickets = new[]
        {
            new
            {
                ClientEmail = "client.anna@kuchnia.local",
                AssignedEmail = "bok@kuchnia.local",
                Title = "Zmiana adresu dostawy na jutro",
                Description = "Klient prosi o zmiane adresu przed jutrzejsza dostawa i potwierdzenie SMS.",
                Status = (int)TicketStatus.Open,
                Priority = (int)TicketPriority.High,
                ClosedAt = (DateTimeOffset?)null,
                CreatedAt = now.AddHours(-22),
            },
            new
            {
                ClientEmail = "client.michal@kuchnia.local",
                AssignedEmail = (string?)null,
                Title = "Pytanie o kalorycznosc diety sport",
                Description = "Klient pyta, czy wariant sport 2500 kcal moze byc obnizony do 2300 kcal.",
                Status = (int)TicketStatus.New,
                Priority = (int)TicketPriority.Medium,
                ClosedAt = (DateTimeOffset?)null,
                CreatedAt = now.AddHours(-20),
            },
            new
            {
                ClientEmail = "client.katarzyna@kuchnia.local",
                AssignedEmail = "bokm@kuchnia.local",
                Title = "Brak jednego pudelka w dostawie",
                Description = "Klient zglasza brak kolacji w dzisiejszej dostawie i prosi o rekompensate.",
                Status = (int)TicketStatus.Pending,
                Priority = (int)TicketPriority.Critical,
                ClosedAt = (DateTimeOffset?)null,
                CreatedAt = now.AddHours(-18),
            },
            new
            {
                ClientEmail = "client.robert@kuchnia.local",
                AssignedEmail = "bok@kuchnia.local",
                Title = "Prosba o fakture za maj",
                Description = "Klient potrzebuje faktury zbiorczej za zamowienia z maja.",
                Status = (int)TicketStatus.Resolved,
                Priority = (int)TicketPriority.Low,
                ClosedAt = (DateTimeOffset?)now.AddHours(-2),
                CreatedAt = now.AddHours(-16),
            },
            new
            {
                ClientEmail = "client.anna@kuchnia.local",
                AssignedEmail = (string?)null,
                Title = "Dostawa poza oknem czasowym",
                Description = "Klient prosi o kontakt w sprawie opoznionej dostawy na trasie porannej.",
                Status = (int)TicketStatus.New,
                Priority = (int)TicketPriority.High,
                ClosedAt = (DateTimeOffset?)null,
                CreatedAt = now.AddHours(-14),
            },
            new
            {
                ClientEmail = "client.michal@kuchnia.local",
                AssignedEmail = "bok@kuchnia.local",
                Title = "Pauza abonamentu na urlop",
                Description = "Klient chce zawiesic dostawe na trzy dni urlopu i przesunac pakiet.",
                Status = (int)TicketStatus.Open,
                Priority = (int)TicketPriority.Medium,
                ClosedAt = (DateTimeOffset?)null,
                CreatedAt = now.AddHours(-12),
            },
            new
            {
                ClientEmail = "client.katarzyna@kuchnia.local",
                AssignedEmail = "bokm@kuchnia.local",
                Title = "Alergen niezgodny z profilem",
                Description = "Klient widzi orzechy w skladzie posilku mimo ustawionej preferencji bez orzechow.",
                Status = (int)TicketStatus.Pending,
                Priority = (int)TicketPriority.Critical,
                ClosedAt = (DateTimeOffset?)null,
                CreatedAt = now.AddHours(-10),
            },
            new
            {
                ClientEmail = "client.robert@kuchnia.local",
                AssignedEmail = (string?)null,
                Title = "Aktualizacja numeru telefonu",
                Description = "Klient prosi o podmiane numeru kontaktowego dla kuriera.",
                Status = (int)TicketStatus.New,
                Priority = (int)TicketPriority.Low,
                ClosedAt = (DateTimeOffset?)null,
                CreatedAt = now.AddHours(-8),
            },
            new
            {
                ClientEmail = "client.anna@kuchnia.local",
                AssignedEmail = "bok@kuchnia.local",
                Title = "Zmiana wariantu z vege na standard",
                Description = "Klient chce zmienic wariant od kolejnego tygodnia rozliczeniowego.",
                Status = (int)TicketStatus.Open,
                Priority = (int)TicketPriority.Medium,
                ClosedAt = (DateTimeOffset?)null,
                CreatedAt = now.AddHours(-6),
            },
            new
            {
                ClientEmail = "client.michal@kuchnia.local",
                AssignedEmail = "bokm@kuchnia.local",
                Title = "Reklamacja temperatury posilku",
                Description = "Klient zglasza zbyt wysoka temperature torby przy odbiorze i prosi o weryfikacje HACCP.",
                Status = (int)TicketStatus.Pending,
                Priority = (int)TicketPriority.High,
                ClosedAt = (DateTimeOffset?)null,
                CreatedAt = now.AddHours(-5),
            },
            new
            {
                ClientEmail = "client.katarzyna@kuchnia.local",
                AssignedEmail = "bok@kuchnia.local",
                Title = "Potwierdzenie platnosci",
                Description = "Klient nie widzi platnosci w panelu mimo potwierdzenia bankowego.",
                Status = (int)TicketStatus.Resolved,
                Priority = (int)TicketPriority.Medium,
                ClosedAt = (DateTimeOffset?)now.AddHours(-1),
                CreatedAt = now.AddHours(-4),
            },
            new
            {
                ClientEmail = "client.robert@kuchnia.local",
                AssignedEmail = (string?)null,
                Title = "Dodatkowa informacja dla kuriera",
                Description = "Klient chce dodac kod do domofonu i prosbe o telefon przed dostawa.",
                Status = (int)TicketStatus.New,
                Priority = (int)TicketPriority.Low,
                ClosedAt = (DateTimeOffset?)null,
                CreatedAt = now.AddHours(-3),
            },
        };

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [Tickets]
                ([Title], [Description], [ClientUserId], [OrderId], [DeliveryCalendarId], [AssignedToUserId], [Status], [Priority],
                 [ClosedAt], [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy],
                 [CreatedAt], [UpdatedAt])
            SELECT
                @Title, @Description, client.[Id], NULL, NULL, assigned.[Id], @Status, @Priority,
                @ClosedAt, @CreatedBy, NULL, 0, NULL, NULL, @CreatedAt, NULL
            FROM [Users] client
            OUTER APPLY (
                SELECT TOP 1 assignee.[Id]
                FROM [Users] assignee
                WHERE assignee.[Email] = @AssignedEmail
            ) assigned
            WHERE client.[Email] = @ClientEmail
              AND NOT EXISTS (
                    SELECT 1
                    FROM [Tickets] ticket
                    WHERE ticket.[ClientUserId] = client.[Id]
                      AND ticket.[Title] = @Title
                      AND ticket.[IsDeleted] = 0);
            """,
            tickets.Select(ticket => new
            {
                ticket.ClientEmail,
                ticket.AssignedEmail,
                ticket.Title,
                ticket.Description,
                ticket.Status,
                ticket.Priority,
                ticket.ClosedAt,
                CreatedBy = "DatabaseSeeder",
                ticket.CreatedAt,
            }),
            cancellationToken: cancellationToken));

        var auditLogs = new[]
        {
            new
            {
                UserEmail = "admin@kuchnia.local",
                Action = "Seed",
                TargetEntity = "Users",
                TargetId = "seed:module5-users",
                OldValue = (string?)null,
                NewValue = "{\"roles\":[\"Admin\",\"HR\",\"BOK\",\"Client\"],\"source\":\"DatabaseSeeder\"}",
                Timestamp = now.AddHours(-24),
                IPAddress = "127.0.0.1",
            },
            new
            {
                UserEmail = "hrm@kuchnia.local",
                Action = "Seed",
                TargetEntity = "Employees",
                TargetId = "seed:module5-employees",
                OldValue = (string?)null,
                NewValue = "{\"departments\":8,\"employees\":15,\"source\":\"DatabaseSeeder\"}",
                Timestamp = now.AddHours(-23),
                IPAddress = "127.0.0.1",
            },
            new
            {
                UserEmail = "bokm@kuchnia.local",
                Action = "Seed",
                TargetEntity = "Tickets",
                TargetId = "seed:bok-queue",
                OldValue = (string?)null,
                NewValue = "{\"tickets\":12,\"openQueue\":true,\"source\":\"DatabaseSeeder\"}",
                Timestamp = now.AddHours(-22),
                IPAddress = "127.0.0.1",
            },
            new
            {
                UserEmail = "admin@kuchnia.local",
                Action = "Update",
                TargetEntity = "Department",
                TargetId = "seed:department-heads",
                OldValue = "{\"headEmployeeId\":null}",
                NewValue = "{\"headEmployeeId\":\"assigned-from-seed\"}",
                Timestamp = now.AddHours(-21),
                IPAddress = "127.0.0.1",
            },
            new
            {
                UserEmail = "bok@kuchnia.local",
                Action = "Assign",
                TargetEntity = "Ticket",
                TargetId = "seed:ticket-address-change",
                OldValue = "{\"assignedToUserId\":null}",
                NewValue = "{\"assignedToUserEmail\":\"bok@kuchnia.local\"}",
                Timestamp = now.AddHours(-20),
                IPAddress = "127.0.0.1",
            },
            new
            {
                UserEmail = "hr@kuchnia.local",
                Action = "Create",
                TargetEntity = "WorkSchedule",
                TargetId = "seed:today-shifts",
                OldValue = (string?)null,
                NewValue = "{\"todayShifts\":4,\"tomorrowShifts\":2}",
                Timestamp = now.AddHours(-19),
                IPAddress = "127.0.0.1",
            },
        };

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [SystemLogs]
                ([UserId], [Action], [TargetEntity], [TargetId], [OldValue], [NewValue],
                 [Timestamp], [IPAddress], [CreatedAt], [UpdatedAt])
            SELECT
                u.[Id], @Action, @TargetEntity, @TargetId, @OldValue, @NewValue,
                @Timestamp, @IPAddress, @CreatedAt, NULL
            FROM [Users] u
            WHERE u.[Email] = @UserEmail
              AND NOT EXISTS (
                    SELECT 1
                    FROM [SystemLogs] log
                    WHERE log.[Action] = @Action
                      AND log.[TargetEntity] = @TargetEntity
                      AND log.[TargetId] = @TargetId);
            """,
            auditLogs.Select(log => new
            {
                log.UserEmail,
                log.Action,
                log.TargetEntity,
                log.TargetId,
                log.OldValue,
                log.NewValue,
                log.Timestamp,
                log.IPAddress,
                CreatedAt = now,
            }),
            cancellationToken: cancellationToken));

        var notifications = new[]
        {
            new
            {
                Type = "HR",
                Severity = NotificationSeverity.Info,
                Title = "HR gotowy do pracy",
                Message = "Seeder utworzyl dzialy, pracownikow, grafiki i wnioski urlopowe.",
                LinkUrl = "/hr",
                DeduplicationKey = "seed:m5:hr-ready",
                SourceType = "Seed",
                SourceId = (long?)null,
                Roles = new[] { UserRoles.HR, UserRoles.HRManager, UserRoles.Admin },
            },
            new
            {
                Type = "BOK",
                Severity = NotificationSeverity.Warning,
                Title = "Kolejka BOK ma zgloszenia",
                Message = "Seeder dodal startowa kolejke zgloszen klientow z roznymi priorytetami.",
                LinkUrl = "/bok/tickets",
                DeduplicationKey = "seed:m5:bok-queue",
                SourceType = "Seed",
                SourceId = (long?)null,
                Roles = new[] { UserRoles.BOK, UserRoles.BOKManager, UserRoles.Admin },
            },
            new
            {
                Type = "Admin",
                Severity = NotificationSeverity.Info,
                Title = "Audyt M5 aktywny",
                Message = "Operacje HR, BOK i Admin beda dopisywane do logow systemowych i powiadomien.",
                LinkUrl = "/admin/logs",
                DeduplicationKey = "seed:m5:audit-ready",
                SourceType = "Seed",
                SourceId = (long?)null,
                Roles = new[] { UserRoles.Admin },
            },
        };

        foreach (var notification in notifications)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                DECLARE @NotificationId bigint;

                SELECT @NotificationId = [Id]
                FROM [Notifications]
                WHERE [DeduplicationKey] = @DeduplicationKey;

                IF @NotificationId IS NULL
                BEGIN
                    INSERT INTO [Notifications]
                        ([Type], [Severity], [Title], [Message], [LinkUrl], [DeduplicationKey],
                         [SourceType], [SourceId], [CreatedAt], [UpdatedAt])
                    VALUES
                        (@Type, @Severity, @Title, @Message, @LinkUrl, @DeduplicationKey,
                         @SourceType, @SourceId, @CreatedAt, NULL);

                    SET @NotificationId = CAST(SCOPE_IDENTITY() AS bigint);
                END

                INSERT INTO [UserNotifications]
                    ([NotificationId], [UserId], [IsRead], [DeliveredAt], [ReadAt], [CreatedAt], [UpdatedAt])
                SELECT
                    @NotificationId, u.[Id], 0, @DeliveredAt, NULL, @DeliveredAt, NULL
                FROM [Users] u
                WHERE u.[Role] IN @Roles
                  AND NOT EXISTS (
                        SELECT 1
                        FROM [UserNotifications] un
                        WHERE un.[NotificationId] = @NotificationId
                          AND un.[UserId] = u.[Id]);
                """,
                new
                {
                    notification.Type,
                    notification.Severity,
                    notification.Title,
                    notification.Message,
                    notification.LinkUrl,
                    notification.DeduplicationKey,
                    notification.SourceType,
                    notification.SourceId,
                    notification.Roles,
                    CreatedAt = now,
                    DeliveredAt = now,
                },
                cancellationToken: cancellationToken));
        }

        this.logger.LogInformation("Ensured minimal Module 5 BOK tickets, audit logs and notifications.");
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
        await SeedCanonicalM2DemoCatalogAsync(db, now, auditUser, cancellationToken);
        await SeedUnifiedDemoScenarioAsync(db, now, auditUser, cancellationToken);
    }

    private async Task SeedM2VolumeDemoAsync(CancellationToken cancellationToken)
    {
        using var db = connectionFactory.CreateConnection();
        var now = DateTimeOffset.UtcNow;
        var auditUser = "VolumeDemoSeeder";

        await SeedUnitsOfMeasureAsync(db, now, cancellationToken);

        await db.ExecuteAsync(new CommandDefinition(
            """
            DECLARE @prefix nvarchar(20) = @M2VolumePrefix;
            DECLARE @now datetimeoffset = @Now;
            DECLARE @auditUser nvarchar(100) = @AuditUser;
            DECLARE @today date = CONVERT(date, SYSUTCDATETIME());

            IF NOT EXISTS (SELECT 1 FROM [Allergens] WHERE [Code] = N'VOL-GLU')
                INSERT INTO [Allergens] ([Name], [Code], [IconUrl], [CreatedAt], [UpdatedAt])
                VALUES (N'VOL-M2 Gluten', N'VOL-GLU', NULL, @now, NULL);
            IF NOT EXISTS (SELECT 1 FROM [Allergens] WHERE [Code] = N'VOL-LAC')
                INSERT INTO [Allergens] ([Name], [Code], [IconUrl], [CreatedAt], [UpdatedAt])
                VALUES (N'VOL-M2 Laktoza', N'VOL-LAC', NULL, @now, NULL);
            IF NOT EXISTS (SELECT 1 FROM [Allergens] WHERE [Code] = N'VOL-SEL')
                INSERT INTO [Allergens] ([Name], [Code], [IconUrl], [CreatedAt], [UpdatedAt])
                VALUES (N'VOL-M2 Seler', N'VOL-SEL', NULL, @now, NULL);
            IF NOT EXISTS (SELECT 1 FROM [Allergens] WHERE [Code] = N'VOL-SOY')
                INSERT INTO [Allergens] ([Name], [Code], [IconUrl], [CreatedAt], [UpdatedAt])
                VALUES (N'VOL-M2 Soja', N'VOL-SOY', NULL, @now, NULL);

            IF NOT EXISTS (SELECT 1 FROM [Categories] WHERE [Name] = @prefix + N'Ingredients')
                INSERT INTO [Categories] ([Name], [Description], [SortOrder], [CreatedAt], [UpdatedAt])
                VALUES (@prefix + N'Ingredients', N'VolumeDemo M2: skladniki z nutrition i partiami.', 700, @now, NULL);
            IF NOT EXISTS (SELECT 1 FROM [Categories] WHERE [Name] = @prefix + N'RecipeComponents')
                INSERT INTO [Categories] ([Name], [Description], [SortOrder], [CreatedAt], [UpdatedAt])
                VALUES (@prefix + N'RecipeComponents', N'VolumeDemo M2: receptury-skladowe.', 701, @now, NULL);
            IF NOT EXISTS (SELECT 1 FROM [Categories] WHERE [Name] = @prefix + N'Meals')
                INSERT INTO [Categories] ([Name], [Description], [SortOrder], [CreatedAt], [UpdatedAt])
                VALUES (@prefix + N'Meals', N'VolumeDemo M2: posilki.', 702, @now, NULL);

            IF NOT EXISTS (SELECT 1 FROM [WarehouseCategories] WHERE [Code] = N'VOL-M2-FOOD')
                INSERT INTO [WarehouseCategories] ([Code], [Name], [IsActive], [DisplayOrder], [CreatedAt], [UpdatedAt])
                VALUES (N'VOL-M2-FOOD', N'VOL-M2 Produkty spozywcze', 1, 700, @now, NULL);
            IF NOT EXISTS (SELECT 1 FROM [WarehouseCategories] WHERE [Code] = N'VOL-M2-PACK')
                INSERT INTO [WarehouseCategories] ([Code], [Name], [IsActive], [DisplayOrder], [CreatedAt], [UpdatedAt])
                VALUES (N'VOL-M2-PACK', N'VOL-M2 Opakowania', 1, 701, @now, NULL);

            DECLARE @ingredientCategoryId int = (SELECT TOP 1 [Id] FROM [Categories] WHERE [Name] = @prefix + N'Ingredients');
            DECLARE @recipeCategoryId int = (SELECT TOP 1 [Id] FROM [Categories] WHERE [Name] = @prefix + N'RecipeComponents');
            DECLARE @mealCategoryId int = (SELECT TOP 1 [Id] FROM [Categories] WHERE [Name] = @prefix + N'Meals');
            DECLARE @foodWarehouseCategoryId int = (SELECT TOP 1 [Id] FROM [WarehouseCategories] WHERE [Code] = N'VOL-M2-FOOD');
            DECLARE @packWarehouseCategoryId int = (SELECT TOP 1 [Id] FROM [WarehouseCategories] WHERE [Code] = N'VOL-M2-PACK');
            DECLARE @gUnitId int = (SELECT TOP 1 [Id] FROM [UnitsOfMeasure] WHERE [Symbol] = N'g' ORDER BY [Id]);
            DECLARE @pcsUnitId int = (SELECT TOP 1 [Id] FROM [UnitsOfMeasure] WHERE [Symbol] = N'szt' ORDER BY [Id]);

            ;WITH Numbers AS (
                SELECT TOP (1000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [N]
                FROM sys.all_objects a CROSS JOIN sys.all_objects b
            )
            INSERT INTO [Ingredients]
                ([Name], [Unit], [CostPerUnit], [Notes], [IsActive], [CreatedAt], [UpdatedAt],
                 [CreatedBy], [UpdatedBy], [IsDeleted], [ResourceType], [FoodCategoryId],
                 [Description], [ProductComposition], [WarehouseCategoryId], [YieldFactor],
                 [RequiresCoreTemperatureCheck], [MinimumCoreTemperatureCelsius], [WarehouseCategoryFefoApproved])
            SELECT
                @prefix + N'ING-' + RIGHT(N'0000' + CONVERT(nvarchar(4), [N]), 4),
                N'g',
                CAST(0.35 + ([N] % 37) * 0.07 AS decimal(10,4)),
                N'VolumeDemo M2 ingredient.',
                1,
                @now,
                NULL,
                @auditUser,
                NULL,
                0,
                CASE WHEN [N] % 10 = 0 THEN N'Spice' ELSE N'Food' END,
                @ingredientCategoryId,
                N'VolumeDemo M2 food resource.',
                CASE WHEN [N] <= 250 THEN N'Produkt przetworzony: skladniki VOL-M2, stabilizator, przyprawy, alergen trace.' ELSE NULL END,
                @foodWarehouseCategoryId,
                CAST(0.82 + ([N] % 12) * 0.01 AS decimal(7,4)),
                CASE WHEN [N] % 17 = 0 THEN 1 ELSE 0 END,
                CASE WHEN [N] % 17 = 0 THEN CAST(72.00 AS decimal(5,2)) ELSE NULL END,
                1
            FROM Numbers n
            WHERE NOT EXISTS (
                SELECT 1 FROM [Ingredients] existing
                WHERE existing.[Name] = @prefix + N'ING-' + RIGHT(N'0000' + CONVERT(nvarchar(4), n.[N]), 4)
                  AND existing.[IsDeleted] = 0);

            INSERT INTO [NutritionFacts]
                ([MealId], [IngredientId], [CaloriesPer100g], [ProteinPer100g], [CarbohydratesPer100g],
                 [FatPer100g], [FiberPer100g], [CreatedAt], [UpdatedAt])
            SELECT
                NULL,
                i.[Id],
                CAST(45 + (ROW_NUMBER() OVER (ORDER BY i.[Id]) % 260) AS decimal(8,2)),
                CAST(2 + (ROW_NUMBER() OVER (ORDER BY i.[Id]) % 28) AS decimal(8,2)),
                CAST(5 + (ROW_NUMBER() OVER (ORDER BY i.[Id]) % 55) AS decimal(8,2)),
                CAST(1 + (ROW_NUMBER() OVER (ORDER BY i.[Id]) % 24) AS decimal(8,2)),
                CAST(1 + (ROW_NUMBER() OVER (ORDER BY i.[Id]) % 12) AS decimal(8,2)),
                @now,
                NULL
            FROM [Ingredients] i
            WHERE i.[Name] LIKE @prefix + N'ING-%'
              AND NOT EXISTS (SELECT 1 FROM [NutritionFacts] nf WHERE nf.[IngredientId] = i.[Id]);

            INSERT INTO [IngredientAllergens] ([IngredientId], [AllergenId], [TraceAmount])
            SELECT i.[Id], a.[Id], CASE WHEN ROW_NUMBER() OVER (ORDER BY i.[Id]) % 3 = 0 THEN 1 ELSE 0 END
            FROM [Ingredients] i
            INNER JOIN [Allergens] a ON a.[Code] = CASE
                WHEN i.[Id] % 4 = 0 THEN N'VOL-GLU'
                WHEN i.[Id] % 4 = 1 THEN N'VOL-LAC'
                WHEN i.[Id] % 4 = 2 THEN N'VOL-SEL'
                ELSE N'VOL-SOY'
            END
            WHERE i.[Name] LIKE @prefix + N'ING-%'
              AND i.[Id] % 5 = 0
              AND NOT EXISTS (
                  SELECT 1 FROM [IngredientAllergens] existing
                  WHERE existing.[IngredientId] = i.[Id] AND existing.[AllergenId] = a.[Id]);

            INSERT INTO [StockItems]
                ([Name], [BaseIngredientId], [DefaultUnitOfMeasureId], [WarehouseCategoryId], [MinimumLevel],
                 [LeadTimeDays], [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            SELECT
                i.[Name],
                i.[Id],
                @gUnitId,
                @foodWarehouseCategoryId,
                1500,
                3 + (i.[Id] % 5),
                @auditUser,
                NULL,
                0,
                NULL,
                NULL,
                @now,
                NULL
            FROM [Ingredients] i
            WHERE i.[Name] LIKE @prefix + N'ING-%'
              AND NOT EXISTS (
                  SELECT 1 FROM [StockItems] si
                  WHERE si.[BaseIngredientId] = i.[Id] AND si.[IsDeleted] = 0);

            UPDATE i
            SET [StockItemId] = si.[Id],
                [WarehouseCategoryId] = @foodWarehouseCategoryId,
                [UpdatedAt] = @now,
                [UpdatedBy] = @auditUser
            FROM [Ingredients] i
            INNER JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id] AND si.[IsDeleted] = 0
            WHERE i.[Name] LIKE @prefix + N'ING-%';

            INSERT INTO [Batches]
                ([StockItemId], [SupplierBatchNumber], [CurrentQuantity], [ExpiryDate], [ReceivedDate], [IsDepleted],
                 [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            SELECT
                si.[Id],
                @prefix + N'BATCH-' + CONVERT(nvarchar(20), si.[Id]),
                5000 + (si.[Id] % 50) * 100,
                DATEADD(day, 14 + (si.[Id] % 40), CONVERT(datetime, @today)),
                DATEADD(day, -1 * (si.[Id] % 7), CONVERT(datetime, @today)),
                0,
                @auditUser,
                NULL,
                0,
                NULL,
                NULL,
                @now,
                NULL
            FROM [StockItems] si
            INNER JOIN [Ingredients] i ON i.[Id] = si.[BaseIngredientId]
            WHERE i.[Name] LIKE @prefix + N'ING-%'
              AND NOT EXISTS (
                  SELECT 1 FROM [Batches] b
                  WHERE b.[StockItemId] = si.[Id]
                    AND b.[SupplierBatchNumber] = @prefix + N'BATCH-' + CONVERT(nvarchar(20), si.[Id]));

            ;WITH Numbers AS (
                SELECT TOP (30) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [N]
                FROM sys.all_objects
            )
            INSERT INTO [StockItems]
                ([Name], [BaseIngredientId], [DefaultUnitOfMeasureId], [WarehouseCategoryId], [MinimumLevel],
                 [LeadTimeDays], [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            SELECT
                @prefix + N'PACK-' + RIGHT(N'000' + CONVERT(nvarchar(3), [N]), 3),
                NULL,
                @pcsUnitId,
                @packWarehouseCategoryId,
                500,
                7,
                @auditUser,
                NULL,
                0,
                NULL,
                NULL,
                @now,
                NULL
            FROM Numbers n
            WHERE NOT EXISTS (
                SELECT 1 FROM [StockItems] si
                WHERE si.[Name] = @prefix + N'PACK-' + RIGHT(N'000' + CONVERT(nvarchar(3), n.[N]), 3)
                  AND si.[IsDeleted] = 0);

            INSERT INTO [Batches]
                ([StockItemId], [SupplierBatchNumber], [CurrentQuantity], [ExpiryDate], [ReceivedDate], [IsDepleted],
                 [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            SELECT
                si.[Id],
                @prefix + N'PACK-BATCH-' + CONVERT(nvarchar(20), si.[Id]),
                2500,
                DATEADD(day, 180, CONVERT(datetime, @today)),
                CONVERT(datetime, @today),
                0,
                @auditUser,
                NULL,
                0,
                NULL,
                NULL,
                @now,
                NULL
            FROM [StockItems] si
            WHERE si.[Name] LIKE @prefix + N'PACK-%'
              AND NOT EXISTS (
                  SELECT 1 FROM [Batches] b
                  WHERE b.[StockItemId] = si.[Id]
                    AND b.[SupplierBatchNumber] = @prefix + N'PACK-BATCH-' + CONVERT(nvarchar(20), si.[Id]));

            ;WITH Numbers AS (
                SELECT TOP (150) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [N]
                FROM sys.all_objects a CROSS JOIN sys.all_objects b
            )
            INSERT INTO [RecipeComponents]
                ([CategoryId], [Name], [Description], [ImageUrl], [PreparationTimeMinutes], [IsActive],
                 [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
            SELECT
                @recipeCategoryId,
                @prefix + N'RC-' + RIGHT(N'000' + CONVERT(nvarchar(3), [N]), 3),
                N'VolumeDemo M2 recipe component.',
                NULL,
                8 + ([N] % 25),
                1,
                @now,
                NULL,
                @auditUser,
                NULL,
                0
            FROM Numbers n
            WHERE NOT EXISTS (
                SELECT 1 FROM [RecipeComponents] rc
                WHERE rc.[Name] = @prefix + N'RC-' + RIGHT(N'000' + CONVERT(nvarchar(3), n.[N]), 3)
                  AND rc.[IsDeleted] = 0);

            INSERT INTO [RecipeComponentVersions]
                ([RecipeComponentId], [VersionNumber], [Status], [Instructions], [YieldQuantity], [YieldUnit],
                 [RawWeightGrams], [CookedWeightGrams], [ShelfLifeHours], [UseEarliestIngredientExpiry],
                 [ChangeSummary], [IsTechnologyChange], [NonTechnologyChangeReason], [PublishedAt], [PublishedBy],
                 [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted],
                 [CaloriesPer100g], [ProteinPer100g], [CarbohydratesPer100g], [FatPer100g], [FiberPer100g],
                 [NutritionSource], [NutritionOverrideReason], [AllergensApproved], [AllergenOverrideReason],
                 [AllergensApprovedAt], [AllergensApprovedBy])
            SELECT
                rc.[Id],
                1,
                N'Published',
                N'VolumeDemo: przygotowac zgodnie z karta technologiczna.',
                1.000,
                N'portion',
                180 + (rc.[Id] % 90),
                160 + (rc.[Id] % 80),
                48,
                1,
                N'VolumeDemo initial version',
                1,
                NULL,
                @now,
                @auditUser,
                @now,
                NULL,
                @auditUser,
                NULL,
                0,
                90 + (rc.[Id] % 180),
                8 + (rc.[Id] % 25),
                10 + (rc.[Id] % 35),
                3 + (rc.[Id] % 18),
                2 + (rc.[Id] % 9),
                N'Manual',
                N'VolumeDemo baseline',
                1,
                NULL,
                @now,
                @auditUser
            FROM [RecipeComponents] rc
            WHERE rc.[Name] LIKE @prefix + N'RC-%'
              AND rc.[IsDeleted] = 0
              AND NOT EXISTS (
                  SELECT 1 FROM [RecipeComponentVersions] rcv
                  WHERE rcv.[RecipeComponentId] = rc.[Id]
                    AND rcv.[VersionNumber] = 1
                    AND rcv.[IsDeleted] = 0);

            ;WITH ComponentNumbers AS (
                SELECT rcv.[Id] AS [VersionId], ROW_NUMBER() OVER (ORDER BY rcv.[Id]) AS [N]
                FROM [RecipeComponentVersions] rcv
                INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
                WHERE rc.[Name] LIKE @prefix + N'RC-%'
                  AND rcv.[IsDeleted] = 0
            ),
            IngredientNumbers AS (
                SELECT i.[Id], i.[StockItemId], i.[WarehouseCategoryId], ROW_NUMBER() OVER (ORDER BY i.[Id]) AS [N]
                FROM [Ingredients] i
                WHERE i.[Name] LIKE @prefix + N'ING-%'
                  AND i.[IsDeleted] = 0
            )
            INSERT INTO [RecipeComponentIngredients]
                ([RecipeComponentVersionId], [IngredientId], [StockItemId], [WarehouseCategoryId],
                 [WeightInGrams], [YieldFactor], [IsOptional], [Notes], [CreatedAt], [UpdatedAt],
                 [CreatedBy], [UpdatedBy], [IsDeleted])
            SELECT
                cn.[VersionId],
                i.[Id],
                i.[StockItemId],
                i.[WarehouseCategoryId],
                45 + ((cn.[N] + src.[Offset]) % 90),
                0.9200,
                0,
                N'VolumeDemo active ingredient.',
                @now,
                NULL,
                @auditUser,
                NULL,
                0
            FROM ComponentNumbers cn
            CROSS APPLY (VALUES (0), (97)) src([Offset])
            INNER JOIN IngredientNumbers i ON i.[N] = ((cn.[N] + src.[Offset] - 1) % 200) + 1
            WHERE NOT EXISTS (
                SELECT 1 FROM [RecipeComponentIngredients] rci
                WHERE rci.[RecipeComponentVersionId] = cn.[VersionId]
                  AND rci.[IngredientId] = i.[Id]
                  AND rci.[IsDeleted] = 0);

            INSERT INTO [PackagingRequirements]
                ([OwnerType], [MealId], [MealVariantId], [RecipeComponentVersionId], [StockItemId], [WarehouseCategoryId],
                 [ResourceName], [Quantity], [Unit], [ContainerRole], [IsCustomerFacing], [CreatedAt], [UpdatedAt],
                 [CreatedBy], [UpdatedBy], [IsDeleted])
            SELECT
                N'RecipeComponentVersion',
                NULL,
                NULL,
                rcv.[Id],
                pack.[Id],
                @packWarehouseCategoryId,
                pack.[Name],
                1,
                N'pcs',
                N'component',
                0,
                @now,
                NULL,
                @auditUser,
                NULL,
                0
            FROM [RecipeComponentVersions] rcv
            INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
            CROSS APPLY (
                SELECT TOP 1 si.[Id], si.[Name]
                FROM [StockItems] si
                WHERE si.[Name] LIKE @prefix + N'PACK-%'
                  AND si.[IsDeleted] = 0
                ORDER BY ABS(CHECKSUM(si.[Id], rcv.[Id]))
            ) pack
            WHERE rc.[Name] LIKE @prefix + N'RC-%'
              AND rcv.[IsDeleted] = 0
              AND NOT EXISTS (
                  SELECT 1 FROM [PackagingRequirements] pr
                  WHERE pr.[RecipeComponentVersionId] = rcv.[Id]
                    AND pr.[OwnerType] = N'RecipeComponentVersion'
                    AND pr.[IsDeleted] = 0);

            ;WITH Numbers AS (
                SELECT TOP (60) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [N]
                FROM sys.all_objects
            )
            INSERT INTO [Meals]
                ([CategoryId], [Name], [Description], [MarketingDescription], [Status], [PreparationTimeMinutes],
                 [IsActive], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted],
                 [PreparationInstructions], [RawWeightGrams], [CookedWeightGrams], [RequiresCoreTemperatureCheck],
                 [MinimumCoreTemperatureCelsius], [ShelfLifeHours], [UseEarliestIngredientExpiry])
            SELECT
                @mealCategoryId,
                @prefix + N'MEAL-' + RIGHT(N'000' + CONVERT(nvarchar(3), [N]), 3),
                N'VolumeDemo M2 meal.',
                N'VolumeDemo M2 meal for load testing.',
                N'Published',
                18 + ([N] % 22),
                1,
                @now,
                NULL,
                @auditUser,
                NULL,
                0,
                N'VolumeDemo: assembly and portioning.',
                430 + ([N] % 80),
                380 + ([N] % 70),
                CASE WHEN [N] % 9 = 0 THEN 1 ELSE 0 END,
                CASE WHEN [N] % 9 = 0 THEN CAST(72.00 AS decimal(5,2)) ELSE NULL END,
                48,
                1
            FROM Numbers n
            WHERE NOT EXISTS (
                SELECT 1 FROM [Meals] m
                WHERE m.[Name] = @prefix + N'MEAL-' + RIGHT(N'000' + CONVERT(nvarchar(3), n.[N]), 3)
                  AND m.[IsDeleted] = 0);

            INSERT INTO [NutritionFacts]
                ([MealId], [IngredientId], [CaloriesPer100g], [ProteinPer100g], [CarbohydratesPer100g],
                 [FatPer100g], [FiberPer100g], [CreatedAt], [UpdatedAt])
            SELECT
                m.[Id],
                NULL,
                120 + (m.[Id] % 90),
                8 + (m.[Id] % 24),
                12 + (m.[Id] % 42),
                4 + (m.[Id] % 18),
                2 + (m.[Id] % 8),
                @now,
                NULL
            FROM [Meals] m
            WHERE m.[Name] LIKE @prefix + N'MEAL-%'
              AND NOT EXISTS (SELECT 1 FROM [NutritionFacts] nf WHERE nf.[MealId] = m.[Id]);

            ;WITH MealNumbers AS (
                SELECT m.[Id] AS [MealId], ROW_NUMBER() OVER (ORDER BY m.[Id]) AS [N]
                FROM [Meals] m
                WHERE m.[Name] LIKE @prefix + N'MEAL-%'
                  AND m.[IsDeleted] = 0
            ),
            VersionNumbers AS (
                SELECT rcv.[Id] AS [VersionId], ROW_NUMBER() OVER (ORDER BY rcv.[Id]) AS [N]
                FROM [RecipeComponentVersions] rcv
                INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
                WHERE rc.[Name] LIKE @prefix + N'RC-%'
                  AND rcv.[IsDeleted] = 0
            )
            INSERT INTO [MealRecipeComponents]
                ([MealId], [RecipeComponentVersionId], [Role], [QuantityPerServing], [Unit], [SortOrder], [IsOptional],
                 [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
            SELECT
                mn.[MealId],
                vn.[VersionId],
                CASE src.[SortOrder] WHEN 1 THEN N'base' ELSE N'side' END,
                1.000,
                N'portion',
                src.[SortOrder],
                0,
                @now,
                NULL,
                @auditUser,
                NULL,
                0
            FROM MealNumbers mn
            CROSS APPLY (VALUES (1, 0), (2, 41)) src([SortOrder], [Offset])
            INNER JOIN VersionNumbers vn ON vn.[N] = ((mn.[N] + src.[Offset] - 1) % 150) + 1
            WHERE NOT EXISTS (
                SELECT 1 FROM [MealRecipeComponents] mrc
                WHERE mrc.[MealId] = mn.[MealId]
                  AND mrc.[RecipeComponentVersionId] = vn.[VersionId]
                  AND mrc.[IsDeleted] = 0);

            ;WITH MealNumbers AS (
                SELECT m.[Id] AS [MealId], ROW_NUMBER() OVER (ORDER BY m.[Id]) AS [N]
                FROM [Meals] m
                WHERE m.[Name] LIKE @prefix + N'MEAL-%'
                  AND m.[IsDeleted] = 0
            ),
            VariantSource AS (
                SELECT [MealId], [N], v.[VariantNo]
                FROM MealNumbers
                CROSS APPLY (VALUES (1), (2), (3)) v([VariantNo])
                WHERE [N] <= 30 OR v.[VariantNo] <= 2
            )
            INSERT INTO [MealVariants]
                ([MealId], [Name], [VariantType], [Status], [Description], [IsDefault],
                 [RawWeightGrams], [CookedWeightGrams], [CaloriesPer100g], [ProteinPer100g],
                 [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [NutritionSource],
                 [NutritionOverrideReason], [AllergensApproved], [AllergenOverrideReason], [PublishedAt],
                 [PublishedBy], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
            SELECT
                [MealId],
                @prefix + N'VAR-' + RIGHT(N'000' + CONVERT(nvarchar(3), [N]), 3) + N'-' + CONVERT(nvarchar(2), [VariantNo]),
                CASE [VariantNo] WHEN 1 THEN N'Standard' WHEN 2 THEN N'HighProtein' ELSE N'LowCarb' END,
                N'Published',
                N'VolumeDemo meal variant.',
                CASE WHEN [VariantNo] = 1 THEN 1 ELSE 0 END,
                430 + ([N] % 80) + ([VariantNo] * 10),
                380 + ([N] % 70) + ([VariantNo] * 8),
                120 + ([N] % 90),
                8 + ([N] % 24),
                12 + ([N] % 42),
                4 + ([N] % 18),
                2 + ([N] % 8),
                N'Aggregated',
                NULL,
                1,
                NULL,
                @now,
                @auditUser,
                @now,
                NULL,
                @auditUser,
                NULL,
                0
            FROM VariantSource src
            WHERE NOT EXISTS (
                SELECT 1 FROM [MealVariants] mv
                WHERE mv.[MealId] = src.[MealId]
                  AND mv.[Name] = @prefix + N'VAR-' + RIGHT(N'000' + CONVERT(nvarchar(3), src.[N]), 3) + N'-' + CONVERT(nvarchar(2), src.[VariantNo])
                  AND mv.[IsDeleted] = 0);

            ;WITH VariantNumbers AS (
                SELECT mv.[Id] AS [MealVariantId], ROW_NUMBER() OVER (ORDER BY mv.[Id]) AS [N]
                FROM [MealVariants] mv
                WHERE mv.[Name] LIKE @prefix + N'VAR-%'
                  AND mv.[IsDeleted] = 0
            ),
            VersionNumbers AS (
                SELECT rcv.[Id] AS [VersionId], ROW_NUMBER() OVER (ORDER BY rcv.[Id]) AS [N]
                FROM [RecipeComponentVersions] rcv
                INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
                WHERE rc.[Name] LIKE @prefix + N'RC-%'
                  AND rcv.[IsDeleted] = 0
            )
            INSERT INTO [MealVariantComponents]
                ([MealVariantId], [RecipeComponentVersionId], [Role], [QuantityPerServing], [Unit], [SortOrder],
                 [IsOptional], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
            SELECT
                vn.[MealVariantId],
                rv.[VersionId],
                N'variant',
                1.000,
                N'portion',
                1,
                0,
                @now,
                NULL,
                @auditUser,
                NULL,
                0
            FROM VariantNumbers vn
            INNER JOIN VersionNumbers rv ON rv.[N] = ((vn.[N] + 73 - 1) % 150) + 1
            WHERE NOT EXISTS (
                SELECT 1 FROM [MealVariantComponents] mvc
                WHERE mvc.[MealVariantId] = vn.[MealVariantId]
                  AND mvc.[RecipeComponentVersionId] = rv.[VersionId]
                  AND mvc.[IsDeleted] = 0);

            ;WITH Numbers AS (
                SELECT TOP (5) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS [N]
                FROM sys.all_objects
            )
            INSERT INTO [Diets]
                ([Name], [Description], [MarketingDescription], [Status], [IsActive], [ThumbnailUrl],
                 [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
            SELECT
                @prefix + N'DIET-' + RIGHT(N'00' + CONVERT(nvarchar(2), [N]), 2),
                N'VolumeDemo M2 diet.',
                N'VolumeDemo M2 diet for load and audit demonstration.',
                N'Active',
                1,
                NULL,
                @now,
                NULL,
                @auditUser,
                NULL,
                0
            FROM Numbers n
            WHERE NOT EXISTS (
                SELECT 1 FROM [Diets] d
                WHERE d.[Name] = @prefix + N'DIET-' + RIGHT(N'00' + CONVERT(nvarchar(2), n.[N]), 2)
                  AND d.[IsDeleted] = 0);

            ;WITH DietNumbers AS (
                SELECT d.[Id] AS [DietId], ROW_NUMBER() OVER (ORDER BY d.[Id]) AS [N]
                FROM [Diets] d
                WHERE d.[Name] LIKE @prefix + N'DIET-%'
                  AND d.[IsDeleted] = 0
            ),
            Calories AS (
                SELECT * FROM (VALUES (1500, 0.94, 0), (1800, 1.00, 1), (2000, 1.08, 0), (2200, 1.16, 0), (2500, 1.28, 0))
                    v([TargetCalories], [PriceMultiplier], [IsDefault])
            )
            INSERT INTO [DietVariants]
                ([DietId], [Name], [TargetCalories], [PriceMultiplier], [IsDefault],
                 [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy], [IsDeleted])
            SELECT
                dn.[DietId],
                @prefix + CONVERT(nvarchar(4), c.[TargetCalories]),
                c.[TargetCalories],
                c.[PriceMultiplier],
                c.[IsDefault],
                @now,
                NULL,
                @auditUser,
                NULL,
                0
            FROM DietNumbers dn
            CROSS JOIN Calories c
            WHERE NOT EXISTS (
                SELECT 1 FROM [DietVariants] dv
                WHERE dv.[DietId] = dn.[DietId]
                  AND dv.[Name] = @prefix + CONVERT(nvarchar(4), c.[TargetCalories])
                  AND dv.[IsDeleted] = 0);

            ;WITH Numbers AS (
                SELECT TOP (14) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS [Offset]
                FROM sys.all_objects
            )
            INSERT INTO [DietMenuPlans]
                ([PlanDate], [Status], [Notes], [PublishedAt], [PublishedBy], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                DATEADD(day, [Offset], @today),
                N'Published',
                N'VOL-M2 VolumeDemo plan 14 days, około 350 pozycji.',
                @now,
                @auditUser,
                @now,
                @auditUser,
                0
            FROM Numbers n
            WHERE NOT EXISTS (
                SELECT 1 FROM [DietMenuPlans] p
                WHERE p.[PlanDate] = DATEADD(day, n.[Offset], @today)
                  AND p.[IsDeleted] = 0);

            UPDATE p
            SET [Status] = N'Published',
                [Notes] = N'VOL-M2 VolumeDemo plan 14 days, około 350 pozycji.',
                [PublishedAt] = COALESCE([PublishedAt], @now),
                [PublishedBy] = COALESCE([PublishedBy], @auditUser),
                [UpdatedAt] = @now,
                [UpdatedBy] = @auditUser,
                [IsDeleted] = 0
            FROM [DietMenuPlans] p
            WHERE p.[PlanDate] >= @today
              AND p.[PlanDate] < DATEADD(day, 14, @today);

            DECLARE @volumeDietVariantIds TABLE ([Id] int PRIMARY KEY);
            INSERT INTO @volumeDietVariantIds ([Id])
            SELECT dv.[Id]
            FROM [DietVariants] dv
            INNER JOIN [Diets] d ON d.[Id] = dv.[DietId]
            WHERE d.[Name] LIKE @prefix + N'DIET-%'
              AND d.[IsDeleted] = 0
              AND dv.[IsDeleted] = 0;

            UPDATE dpi
            SET [IsActive] = 0,
                [IsDeleted] = 1,
                [DeletedAt] = @now,
                [DeletedBy] = @auditUser,
                [UpdatedAt] = @now,
                [UpdatedBy] = @auditUser
            FROM [DietMenuPlanItems] dpi
            INNER JOIN [DietMenuPlans] p ON p.[Id] = dpi.[DietMenuPlanId]
            WHERE p.[PlanDate] >= @today
              AND p.[PlanDate] < DATEADD(day, 14, @today)
              AND dpi.[DietVariantId] IN (SELECT [Id] FROM @volumeDietVariantIds)
              AND dpi.[IsDeleted] = 0;

            ;WITH PlanNumbers AS (
                SELECT p.[Id] AS [PlanId], p.[PlanDate], ROW_NUMBER() OVER (ORDER BY p.[PlanDate]) AS [DayNo]
                FROM [DietMenuPlans] p
                WHERE p.[PlanDate] >= @today
                  AND p.[PlanDate] < DATEADD(day, 14, @today)
                  AND p.[IsDeleted] = 0
            ),
            VariantNumbers AS (
                SELECT dv.[Id] AS [DietVariantId], ROW_NUMBER() OVER (ORDER BY d.[Id], dv.[TargetCalories]) AS [VariantNo]
                FROM [DietVariants] dv
                INNER JOIN [Diets] d ON d.[Id] = dv.[DietId]
                WHERE d.[Name] LIKE @prefix + N'DIET-%'
                  AND d.[IsDeleted] = 0
                  AND dv.[IsDeleted] = 0
            ),
            MealVariantNumbers AS (
                SELECT mv.[Id] AS [MealVariantId], mv.[MealId], ROW_NUMBER() OVER (ORDER BY mv.[Id]) AS [MealVariantNo]
                FROM [MealVariants] mv
                WHERE mv.[Name] LIKE @prefix + N'VAR-%'
                  AND mv.[IsDeleted] = 0
            )
            INSERT INTO [DietMenuPlanItems]
                ([DietMenuPlanId], [DietVariantId], [MealId], [MealVariantId], [MealSlot],
                 [ServingSizeMultiplier], [SortOrder], [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
            SELECT
                pn.[PlanId],
                vn.[DietVariantId],
                mvn.[MealId],
                mvn.[MealVariantId],
                CASE ((pn.[DayNo] + vn.[VariantNo]) % 5)
                    WHEN 0 THEN N'Breakfast'
                    WHEN 1 THEN N'Snack1'
                    WHEN 2 THEN N'Lunch'
                    WHEN 3 THEN N'Snack2'
                    ELSE N'Dinner'
                END,
                1.000,
                vn.[VariantNo],
                1,
                @now,
                @auditUser,
                0
            FROM PlanNumbers pn
            CROSS JOIN VariantNumbers vn
            INNER JOIN MealVariantNumbers mvn ON mvn.[MealVariantNo] = ((pn.[DayNo] * 25 + vn.[VariantNo] - 1) % 150) + 1;
            """,
            new
            {
                M2VolumePrefix,
                Now = now,
                AuditUser = auditUser,
            },
            commandTimeout: 180,
            cancellationToken: cancellationToken));

        this.logger.LogInformation("Ensured M2 VolumeDemo dataset with prefix {Prefix}.", M2VolumePrefix);
    }

    private async Task BackfillPublishedDietMenuSnapshotsAsync(CancellationToken cancellationToken)
    {
        using var db = connectionFactory.CreateConnection();
        var plans = (await db.QueryAsync<PublishedMenuPlanBackfillRow>(new CommandDefinition(
            """
            SELECT
                p.[Id],
                p.[PlanDate]
            FROM [DietMenuPlans] p
            WHERE p.[Status] = N'Published'
              AND p.[IsDeleted] = 0
              AND EXISTS
              (
                  SELECT 1
                  FROM [DietMenuPlanItems] i
                  WHERE i.[DietMenuPlanId] = p.[Id]
                    AND i.[IsDeleted] = 0
                    AND i.[IsActive] = 1
                    AND (i.[PublishedSnapshotJson] IS NULL OR i.[PublishedSnapshotHash] IS NULL)
              )
            ORDER BY p.[PlanDate], p.[Id];
            """,
            cancellationToken: cancellationToken))).ToList();

        if (plans.Count == 0)
        {
            return;
        }

        var adapter = new DietDataAdapter(connectionFactory, new MealVariantResultCalculator());
        var now = DateTimeOffset.UtcNow;
        var auditUser = "DatabaseSeeder";
        var backfilledItems = 0;

        foreach (var plan in plans)
        {
            var snapshot = await adapter.GetPublishedPlanSnapshotAsync(DateOnly.FromDateTime(plan.PlanDate));
            if (snapshot is null || snapshot.Items.Count == 0)
            {
                continue;
            }

            var rows = snapshot.Items
                .Select(item =>
                {
                    var payload = ProductionSnapshotPayloadFactory.Create(item);
                    return new PublishedMenuPlanItemSnapshotBackfillRow
                    {
                        PlanId = plan.Id,
                        DietMenuPlanItemId = item.DietMenuPlanItemId,
                        SnapshotJson = payload.Json,
                        SnapshotHash = payload.Hash,
                    };
                })
                .ToList();

            if (rows.Count == 0)
            {
                continue;
            }

            var planSnapshotHash = ProductionSnapshotPayloadFactory.CreatePlanHash(rows.Select(row => row.SnapshotHash));

            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [DietMenuPlans]
                SET [PublishedSnapshotHash] = @planSnapshotHash,
                    [PublishedSnapshotItemCount] = @snapshotCount,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser
                WHERE [Id] = @planId;
                """,
                new
                {
                    planId = plan.Id,
                    planSnapshotHash,
                    snapshotCount = rows.Count,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));

            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [DietMenuPlanItems]
                SET [PublishedSnapshotJson] = @SnapshotJson,
                    [PublishedSnapshotHash] = @SnapshotHash,
                    [PublishedSnapshotCreatedAt] = @now,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser
                WHERE [Id] = @DietMenuPlanItemId
                  AND [DietMenuPlanId] = @PlanId;
                """,
                rows.Select(row => new
                {
                    row.PlanId,
                    row.DietMenuPlanItemId,
                    row.SnapshotJson,
                    row.SnapshotHash,
                    now,
                    auditUser,
                }),
                cancellationToken: cancellationToken));

            backfilledItems += rows.Count;
        }

        logger.LogInformation(
            "Backfilled M2 published diet menu snapshots: {ItemCount} items across {PlanCount} plans.",
            backfilledItems,
            plans.Count);
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

            UPDATE [Tickets]
            SET [OrderId] = NULL,
                [DeliveryCalendarId] = NULL,
                [UpdatedAt] = SYSUTCDATETIME()
            WHERE [OrderId] IN (SELECT [Id] FROM @DemoOrders)
               OR [DeliveryCalendarId] IN (SELECT [Id] FROM @DemoDeliveryCalendar);

            DECLARE @DemoProductionPlanItems TABLE ([Id] int PRIMARY KEY);
            INSERT INTO @DemoProductionPlanItems ([Id])
            SELECT [Id]
            FROM [ProductionPlanItems]
            WHERE [ProductionPlanId] IN (SELECT [Id] FROM @DemoProductionPlans);

            DECLARE @DemoCookingSessions TABLE ([Id] int PRIMARY KEY);
            INSERT INTO @DemoCookingSessions ([Id])
            SELECT DISTINCT [Id]
            FROM [CookingSessions]
            WHERE [ProductionPlanItemId] IN (SELECT [Id] FROM @DemoProductionPlanItems)
               OR [CreatedBy] = N'DemoSeeder';

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

            DELETE FROM [CookingSessionStepChecks]
            WHERE [CookingSessionId] IN (SELECT [Id] FROM @DemoCookingSessions);

            DELETE FROM [CookingSessions]
            WHERE [Id] IN (SELECT [Id] FROM @DemoCookingSessions);

            DELETE FROM [ProductionBatches]
            WHERE [ProductionPlanId] IN (SELECT [Id] FROM @DemoProductionPlans);

            DELETE FROM [ProductionPlanItems]
            WHERE [Id] IN (SELECT [Id] FROM @DemoProductionPlanItems);

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
            WHERE [Label] LIKE N'Demo klient %'
               OR [Label] LIKE N'Demo M4%'
               OR [Label] = N'Demo lifecycle';

            DELETE FROM [CustomerProfiles]
            WHERE [UserId] IN
            (
                SELECT [Id]
                FROM [Users]
                WHERE [Email] LIKE N'demo-klient-%@kuchnia.local'
                   OR [Email] LIKE N'demo-m4-klient-%@kuchnia.local'
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
            WHERE [Email] LIKE N'demo-klient-%@kuchnia.local'
               OR [Email] LIKE N'demo-m4-klient-%@kuchnia.local'
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

            DELETE FROM [DietMenuPlanItems]
            WHERE [MealId] IN (SELECT [Id] FROM [Meals] WHERE [Name] LIKE N'Demo %');

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

            DELETE FROM [DietVariantMeals]
            WHERE [MealId] IN (SELECT [Id] FROM [Meals] WHERE [Name] LIKE N'Demo %');

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
        var demoDates = new[] { today, PresentationDemoDate }
            .Distinct()
            .OrderBy(date => date)
            .ToArray();
        var preferredDietVariantId = await EnsureDemoLifecycleMenuFoundationAsync(db, now, auditUser, cancellationToken);
        await EnsureDemoPackagingRequirementsAsync(db, now, auditUser, cancellationToken);

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

        // Customers live in M1 tables. M2 provides menu references, while M4/M3 consume OrderId and DeliveryCalendarId.
        var customerSeeds = GetUnifiedDemoCustomerSeeds();
        foreach (var demoDate in demoDates)
        {
            var seededDeliveryCount = await SeedUnifiedDemoDeliveriesForDateAsync(
                db,
                demoDate,
                preferredDietVariantId,
                customerSeeds,
                now,
                auditUser,
                cancellationToken);

            if (seededDeliveryCount == 0)
            {
                continue;
            }

            await EnsureUnifiedDemoProductionPlanAsync(demoDate, auditUser, cancellationToken);
            await SeedModule5TicketOrderLinksAsync(db, demoDate, now, cancellationToken);
        }

        this.logger.LogInformation(
            "Seeded unified M1/M2/M3/M4 demo data for {DemoDates}: {VehicleCount} vehicles, {DriverCount} drivers, {DeliveryCount} delivery candidates per date.",
            string.Join(", ", demoDates.Select(date => date.ToString("yyyy-MM-dd"))),
            vehicles.Length,
            drivers.Length,
            customerSeeds.Length);
    }

    private async Task<int> SeedUnifiedDemoDeliveriesForDateAsync(
        IDbConnection db,
        DateOnly deliveryDate,
        int preferredDietVariantId,
        IReadOnlyList<UnifiedDemoCustomerSeed> customerSeeds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var deliveryDateStart = deliveryDate.ToDateTime(TimeOnly.MinValue);
        var dietMenuPlanId = await EnsureTodayDemoDietMenuPlanAsync(db, deliveryDateStart, now, auditUser, cancellationToken);
        var meals = (await GetTodayDemoMealsAsync(db, dietMenuPlanId, preferredDietVariantId, cancellationToken)).ToList();

        if (meals.Count < 3)
        {
            this.logger.LogWarning(
                "Skipped logistics demo seed because only {MealCount} menu plan items were available for {DemoDate}.",
                meals.Count,
                deliveryDate);
            return 0;
        }

        for (var index = 0; index < customerSeeds.Count; index++)
        {
            var customerSeed = customerSeeds[index];
            var customerId = await EnsureUnifiedDemoCustomerAsync(db, index + 1, customerSeed, now, cancellationToken);
            var addressId = await EnsureUnifiedDemoCustomerAddressAsync(
                db,
                customerId,
                index + 1,
                customerSeed,
                now,
                auditUser,
                cancellationToken);
            await EnsureUnifiedDemoCustomerProfileAsync(
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
            var orderNumber = $"{UnifiedDemoOrderPrefix}-{deliveryDate:yyyyMMdd}-{index + 1:D2}";
            var totalPrice = orderMeals.Sum(meal => meal.PricePerDay);
            var orderId = await EnsureTodayDemoOrderAsync(
                db,
                customerId,
                orderNumber,
                deliveryDateStart,
                totalPrice,
                now,
                auditUser,
                cancellationToken);

            await EnsureTodayDemoOrderItemsAsync(db, orderId, orderMeals, deliveryDate, now, auditUser, cancellationToken);
            await EnsureTodayDemoDeliveryCalendarAsync(
                db,
                orderId,
                addressId,
                deliveryDate.ToDateTime(new TimeOnly(6 + ((index * 35) / 60), (index * 35) % 60)),
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

        return customerSeeds.Count;
    }

    private async Task SeedModule5TicketOrderLinksAsync(
        IDbConnection db,
        DateOnly today,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            """
            DECLARE @Links TABLE
            (
                [Title] nvarchar(200) NOT NULL,
                [OrderNumber] nvarchar(100) NOT NULL
            );

            INSERT INTO @Links ([Title], [OrderNumber])
            VALUES
                (N'Brak jednego pudelka w dostawie', @OrderNumber03),
                (N'Dostawa poza oknem czasowym', @OrderNumber05),
                (N'Alergen niezgodny z profilem', @OrderNumber07),
                (N'Reklamacja temperatury posilku', @OrderNumber10),
                (N'Dodatkowa informacja dla kuriera', @OrderNumber12),
                (N'Zmiana adresu dostawy na jutro', @OrderNumber01);

            UPDATE ticket
            SET ticket.[ClientUserId] = orders.[CustomerId],
                ticket.[OrderId] = orders.[Id],
                ticket.[DeliveryCalendarId] = deliveries.[Id],
                ticket.[UpdatedAt] = @UpdatedAt
            FROM [Tickets] ticket
            INNER JOIN @Links links ON links.[Title] = ticket.[Title]
            INNER JOIN [Orders] orders
                ON orders.[OrderNumber] = links.[OrderNumber]
               AND orders.[IsDeleted] = 0
            INNER JOIN [DeliveryCalendar] deliveries
                ON deliveries.[OrderId] = orders.[Id]
               AND deliveries.[IsDeleted] = 0
            WHERE ticket.[CreatedBy] = N'DatabaseSeeder'
              AND ticket.[IsDeleted] = 0;
            """,
            new
            {
                OrderNumber01 = $"DEMO-M4-{today:yyyyMMdd}-01",
                OrderNumber03 = $"DEMO-M4-{today:yyyyMMdd}-03",
                OrderNumber05 = $"DEMO-M4-{today:yyyyMMdd}-05",
                OrderNumber07 = $"DEMO-M4-{today:yyyyMMdd}-07",
                OrderNumber10 = $"DEMO-M4-{today:yyyyMMdd}-10",
                OrderNumber12 = $"DEMO-M4-{today:yyyyMMdd}-12",
                UpdatedAt = now,
            },
            cancellationToken: cancellationToken));
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

        result.Plan.Status = ProductionPlanStatus.Active;
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

    private static async Task<int> EnsureUnifiedDemoCustomerAsync(
        IDbConnection db,
        int sequence,
        UnifiedDemoCustomerSeed customer,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var email = $"{UnifiedDemoCustomerEmailPrefix}{sequence:D2}{UnifiedDemoCustomerEmailSuffix}";
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
                    customer.FirstName,
                    customer.LastName,
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
                passwordHash = BCrypt.Net.BCrypt.HashPassword(UnifiedDemoCustomerPassword),
                customer.FirstName,
                customer.LastName,
                role = UserRoles.Client,
                now,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureUnifiedDemoCustomerAddressAsync(
        IDbConnection db,
        int customerId,
        int sequence,
        UnifiedDemoCustomerSeed customer,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var label = $"Demo klient {sequence:D2}";
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
                    [DeliveryNotes] = N'Unified demo: adres klienta M1 uzywany przez M4 do tras.',
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
                    customer.Street,
                    customer.BuildingNumber,
                    customer.ApartmentNumber,
                    customer.PostalCode,
                    customer.Latitude,
                    customer.Longitude,
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
                 1, N'Unified demo: adres klienta M1 uzywany przez M4 do tras.', @now, @auditUser, 0, @latitude, @longitude);
            SELECT CAST(SCOPE_IDENTITY() as int);
            """,
            new
            {
                customerId,
                label,
                customer.Street,
                customer.BuildingNumber,
                customer.ApartmentNumber,
                customer.PostalCode,
                customer.Latitude,
                customer.Longitude,
                now,
                auditUser,
            },
            cancellationToken: cancellationToken));
    }

    private static UnifiedDemoCustomerSeed[] GetUnifiedDemoCustomerSeeds() =>
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

        // 6. Warianty diet uzywane przez unified M1/M2 seed (IDs: 1, 2, 3, 4, 5)
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

    private static async Task SeedCanonicalM2DemoCatalogAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var categoryIds = await EnsureCanonicalM2CategoriesAsync(db, now, cancellationToken);
        var allergenIds = await EnsureCanonicalM2AllergensAsync(db, now, cancellationToken);
        var stockItemIds = await EnsureCanonicalPackagingStockAsync(db, now, auditUser, cancellationToken);
        var ingredientIds = await EnsureCanonicalM2IngredientsAsync(db, categoryIds, allergenIds, now, auditUser, cancellationToken);
        var componentVersionIds = await EnsureCanonicalM2RecipeComponentsAsync(
            db,
            categoryIds,
            ingredientIds,
            stockItemIds,
            now,
            auditUser,
            cancellationToken);
        var mealVariantIds = await EnsureCanonicalM2MealsAndVariantsAsync(
            db,
            categoryIds,
            componentVersionIds,
            stockItemIds,
            now,
            auditUser,
            cancellationToken);

        await EnsureCanonicalM2DietsAndPlansAsync(db, mealVariantIds, now, auditUser, cancellationToken);
    }

    private static async Task<Dictionary<string, int>> EnsureCanonicalM2CategoriesAsync(
        IDbConnection db,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var categories = new[]
        {
            new { Key = "breakfast", Name = "Demo M2 - sniadania", Description = "Demo M2: dania sniadaniowe.", SortOrder = 201 },
            new { Key = "lunch", Name = "Demo M2 - obiady", Description = "Demo M2: dania obiadowe.", SortOrder = 202 },
            new { Key = "dinner", Name = "Demo M2 - kolacje", Description = "Demo M2: dania kolacyjne.", SortOrder = 203 },
            new { Key = "snack", Name = "Demo M2 - przekaski", Description = "Demo M2: przekaski i desery.", SortOrder = 204 },
            new { Key = "meat", Name = "Demo M2 - mieso i ryby", Description = "Demo M2: mieso, ryby i alternatywy bialkowe.", SortOrder = 211 },
            new { Key = "dairy", Name = "Demo M2 - nabial", Description = "Demo M2: nabial i zamienniki.", SortOrder = 212 },
            new { Key = "veg", Name = "Demo M2 - warzywa", Description = "Demo M2: warzywa.", SortOrder = 213 },
            new { Key = "fruit", Name = "Demo M2 - owoce", Description = "Demo M2: owoce.", SortOrder = 214 },
            new { Key = "dry", Name = "Demo M2 - suche", Description = "Demo M2: kasze, ryze, platki i suche produkty.", SortOrder = 215 },
            new { Key = "processed", Name = "Demo M2 - przetworzone", Description = "Demo M2: produkty przetworzone z opisem skladu.", SortOrder = 216 },
            new { Key = "spice", Name = "Demo M2 - przyprawy", Description = "Demo M2: przyprawy jako zasob magazynowy.", SortOrder = 217 },
        };

        foreach (var category in categories)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                IF EXISTS (SELECT 1 FROM [Categories] WHERE [Name] = @Name)
                BEGIN
                    UPDATE [Categories]
                    SET [Description] = @Description,
                        [SortOrder] = @SortOrder,
                        [UpdatedAt] = @now
                    WHERE [Name] = @Name;
                END
                ELSE
                BEGIN
                    INSERT INTO [Categories] ([Name], [Description], [SortOrder], [CreatedAt], [UpdatedAt])
                    VALUES (@Name, @Description, @SortOrder, @now, NULL);
                END
                """,
                new { category.Name, category.Description, category.SortOrder, now },
                cancellationToken: cancellationToken));
        }

        var rows = await db.QueryAsync<(string Name, int Id)>(new CommandDefinition(
            "SELECT [Name], [Id] FROM [Categories] WHERE [Name] IN @names;",
            new { names = categories.Select(category => category.Name).ToArray() },
            cancellationToken: cancellationToken));

        return categories.ToDictionary(
            category => category.Key,
            category => rows.First(row => row.Name == category.Name).Id,
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, int>> EnsureCanonicalM2AllergensAsync(
        IDbConnection db,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var allergens = new[]
        {
            new { Code = "GLU", Name = "Gluten" },
            new { Code = "LAK", Name = "Laktoza" },
            new { Code = "RYB", Name = "Ryby" },
            new { Code = "ORZ", Name = "Orzechy" },
            new { Code = "JAJ", Name = "Jaja" },
            new { Code = "SOJ", Name = "Soja" },
            new { Code = "SEZ", Name = "Sezam" },
            new { Code = "SEL", Name = "Seler" },
        };

        foreach (var allergen in allergens)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                IF EXISTS (SELECT 1 FROM [Allergens] WHERE [Code] = @Code)
                BEGIN
                    UPDATE [Allergens]
                    SET [Name] = @Name,
                        [UpdatedAt] = @now
                    WHERE [Code] = @Code;
                END
                ELSE
                BEGIN
                    INSERT INTO [Allergens] ([Name], [Code], [IconUrl], [CreatedAt], [UpdatedAt])
                    VALUES (@Name, @Code, NULL, @now, NULL);
                END
                """,
                new { allergen.Code, allergen.Name, now },
                cancellationToken: cancellationToken));
        }

        var rows = await db.QueryAsync<(string Code, int Id)>(new CommandDefinition(
            "SELECT [Code], [Id] FROM [Allergens] WHERE [Code] IN @codes;",
            new { codes = allergens.Select(allergen => allergen.Code).ToArray() },
            cancellationToken: cancellationToken));

        return rows.ToDictionary(row => row.Code, row => row.Id, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<Dictionary<string, int>> EnsureCanonicalPackagingStockAsync(
        IDbConnection db,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var sztUnitId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT TOP 1 [Id] FROM [UnitsOfMeasure] WHERE [Symbol] = N'szt' ORDER BY [Id];",
            cancellationToken: cancellationToken));

        var items = new[]
        {
            new { Key = "box250", Name = "Pudełko cateringowe 250ml", MinimumLevel = 100m, TargetQuantity = 600m },
            new { Key = "box500", Name = "Pudełko cateringowe 500ml", MinimumLevel = 100m, TargetQuantity = 600m },
            new { Key = "box750", Name = "Pudełko cateringowe 750ml", MinimumLevel = 80m, TargetQuantity = 400m },
            new { Key = "sauce80", Name = "Pojemnik sosowy 80ml", MinimumLevel = 80m, TargetQuantity = 500m },
        };

        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            result[item.Key] = await EnsureCanonicalStockItemAsync(
                db,
                item.Name,
                null,
                sztUnitId,
                PackagingWarehouseCategoryId,
                item.MinimumLevel,
                item.TargetQuantity,
                2,
                now,
                auditUser,
                cancellationToken);
        }

        return result;
    }

    private static async Task<Dictionary<string, int>> EnsureCanonicalM2IngredientsAsync(
        IDbConnection db,
        IReadOnlyDictionary<string, int> categoryIds,
        IReadOnlyDictionary<string, int> allergenIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var gUnitId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT TOP 1 [Id] FROM [UnitsOfMeasure] WHERE [Symbol] = N'g' ORDER BY [Id];",
            cancellationToken: cancellationToken));

        var ingredients = GetCanonicalIngredientSeeds();
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var ingredient in ingredients)
        {
            var ingredientId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
                """
                SELECT TOP 1 [Id]
                FROM [Ingredients]
                WHERE [Name] = @Name
                  AND [IsDeleted] = 0
                ORDER BY [Id];
                """,
                new { ingredient.Name },
                cancellationToken: cancellationToken));

            if (ingredientId.HasValue)
            {
                await db.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE [Ingredients]
                    SET [Unit] = @Unit,
                        [CostPerUnit] = @CostPerUnit,
                        [Notes] = @Notes,
                        [IsActive] = 1,
                        [ResourceType] = N'Food',
                        [FoodCategoryId] = @FoodCategoryId,
                        [Description] = @Description,
                        [ProductComposition] = @ProductComposition,
                        [WarehouseCategoryId] = @WarehouseCategoryId,
                        [YieldFactor] = @YieldFactor,
                        [RequiresCoreTemperatureCheck] = @RequiresCoreTemperatureCheck,
                        [MinimumCoreTemperatureCelsius] = @MinimumCoreTemperatureCelsius,
                        [WarehouseCategoryFefoApproved] = 1,
                        [UpdatedAt] = @now,
                        [UpdatedBy] = @auditUser,
                        [IsDeleted] = 0
                    WHERE [Id] = @IngredientId;
                    """,
                    new
                    {
                        IngredientId = ingredientId.Value,
                        ingredient.Unit,
                        ingredient.CostPerUnit,
                        ingredient.Notes,
                        FoodCategoryId = categoryIds[ingredient.CategoryKey],
                        ingredient.Description,
                        ingredient.ProductComposition,
                        ingredient.WarehouseCategoryId,
                        ingredient.YieldFactor,
                        ingredient.RequiresCoreTemperatureCheck,
                        ingredient.MinimumCoreTemperatureCelsius,
                        now,
                        auditUser,
                    },
                    cancellationToken: cancellationToken));
            }
            else
            {
                ingredientId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    INSERT INTO [Ingredients]
                        ([Name], [Unit], [CostPerUnit], [Notes], [IsActive], [CreatedAt], [UpdatedAt],
                         [CreatedBy], [UpdatedBy], [IsDeleted], [ResourceType], [FoodCategoryId],
                         [Description], [ProductComposition], [WarehouseCategoryId], [YieldFactor],
                         [RequiresCoreTemperatureCheck], [MinimumCoreTemperatureCelsius], [WarehouseCategoryFefoApproved])
                    OUTPUT INSERTED.[Id]
                    VALUES
                        (@Name, @Unit, @CostPerUnit, @Notes, 1, @now, NULL,
                         @auditUser, NULL, 0, N'Food', @FoodCategoryId,
                         @Description, @ProductComposition, @WarehouseCategoryId, @YieldFactor,
                         @RequiresCoreTemperatureCheck, @MinimumCoreTemperatureCelsius, 1);
                    """,
                    new
                    {
                        ingredient.Name,
                        ingredient.Unit,
                        ingredient.CostPerUnit,
                        ingredient.Notes,
                        FoodCategoryId = categoryIds[ingredient.CategoryKey],
                        ingredient.Description,
                        ingredient.ProductComposition,
                        ingredient.WarehouseCategoryId,
                        ingredient.YieldFactor,
                        ingredient.RequiresCoreTemperatureCheck,
                        ingredient.MinimumCoreTemperatureCelsius,
                        now,
                        auditUser,
                    },
                    cancellationToken: cancellationToken));
            }

            var stockItemId = await EnsureCanonicalStockItemAsync(
                db,
                ingredient.Name,
                ingredientId.Value,
                gUnitId,
                ingredient.WarehouseCategoryId,
                ingredient.MinimumStockLevel,
                ingredient.TargetBatchQuantity,
                ingredient.LeadTimeDays,
                now,
                auditUser,
                cancellationToken);

            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Ingredients]
                SET [StockItemId] = @stockItemId,
                    [WarehouseCategoryId] = @warehouseCategoryId,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser
                WHERE [Id] = @ingredientId;
                """,
                new
                {
                    stockItemId,
                    warehouseCategoryId = ingredient.WarehouseCategoryId,
                    ingredientId = ingredientId.Value,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));

            await UpsertCanonicalNutritionFactsAsync(db, ingredientId.Value, null, ingredient.Nutrition, now, cancellationToken);
            await ReplaceCanonicalIngredientAllergensAsync(db, ingredientId.Value, ingredient.AllergenCodes, allergenIds, cancellationToken);
            result[ingredient.Name] = ingredientId.Value;
        }

        return result;
    }

    private static async Task<int> EnsureCanonicalStockItemAsync(
        IDbConnection db,
        string name,
        int? baseIngredientId,
        int unitOfMeasureId,
        int warehouseCategoryId,
        decimal minimumLevel,
        decimal targetBatchQuantity,
        int leadTimeDays,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var stockItemId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [StockItems]
            WHERE [Name] = @name
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { name },
            cancellationToken: cancellationToken));

        if (stockItemId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [StockItems]
                SET [BaseIngredientId] = COALESCE(@baseIngredientId, [BaseIngredientId]),
                    [DefaultUnitOfMeasureId] = @unitOfMeasureId,
                    [WarehouseCategoryId] = @warehouseCategoryId,
                    [MinimumLevel] = @minimumLevel,
                    [LeadTimeDays] = @leadTimeDays,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @stockItemId;
                """,
                new
                {
                    stockItemId = stockItemId.Value,
                    baseIngredientId,
                    unitOfMeasureId,
                    warehouseCategoryId,
                    minimumLevel,
                    leadTimeDays,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));
        }
        else
        {
            stockItemId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO [StockItems]
                    ([Name], [BaseIngredientId], [DefaultUnitOfMeasureId], [WarehouseCategoryId],
                     [MinimumLevel], [LeadTimeDays], [CreatedBy], [UpdatedBy], [IsDeleted],
                     [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
                OUTPUT INSERTED.[Id]
                VALUES
                    (@name, @baseIngredientId, @unitOfMeasureId, @warehouseCategoryId,
                     @minimumLevel, @leadTimeDays, @auditUser, NULL, 0,
                     NULL, NULL, @now, NULL);
                """,
                new
                {
                    name,
                    baseIngredientId,
                    unitOfMeasureId,
                    warehouseCategoryId,
                    minimumLevel,
                    leadTimeDays,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));
        }

        var availableQuantity = await db.ExecuteScalarAsync<decimal>(new CommandDefinition(
            """
            SELECT COALESCE(SUM([CurrentQuantity]), 0)
            FROM [Batches]
            WHERE [StockItemId] = @stockItemId
              AND [IsDeleted] = 0
              AND [IsDepleted] = 0;
            """,
            new { stockItemId = stockItemId.Value },
            cancellationToken: cancellationToken));

        if (availableQuantity < targetBatchQuantity)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [Batches]
                    ([StockItemId], [SupplierBatchNumber], [CurrentQuantity], [ExpiryDate], [ReceivedDate], [IsDepleted],
                     [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
                VALUES
                    (@stockItemId, @supplierBatchNumber, @currentQuantity, @expiryDate, @receivedDate, 0,
                     @auditUser, NULL, 0, NULL, NULL, @createdAt, NULL);
                """,
                new[]
                {
                    new
                    {
                        stockItemId = stockItemId.Value,
                        supplierBatchNumber = $"M2-FEFO-{stockItemId.Value}-A",
                        currentQuantity = targetBatchQuantity * 0.45m,
                        expiryDate = now.UtcDateTime.AddDays(5),
                        receivedDate = now.UtcDateTime.AddDays(-5),
                        auditUser,
                        createdAt = now.UtcDateTime.AddDays(-5),
                    },
                    new
                    {
                        stockItemId = stockItemId.Value,
                        supplierBatchNumber = $"M2-FEFO-{stockItemId.Value}-B",
                        currentQuantity = targetBatchQuantity * 0.75m,
                        expiryDate = now.UtcDateTime.AddDays(25),
                        receivedDate = now.UtcDateTime.AddDays(-1),
                        auditUser,
                        createdAt = now.UtcDateTime.AddDays(-1),
                    },
                },
                cancellationToken: cancellationToken));
        }

        return stockItemId.Value;
    }

    private static async Task UpsertCanonicalNutritionFactsAsync(
        IDbConnection db,
        int? ingredientId,
        int? mealId,
        CanonicalNutritionSeed nutrition,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            """
            IF EXISTS (
                SELECT 1
                FROM [NutritionFacts]
                WHERE ((@ingredientId IS NOT NULL AND [IngredientId] = @ingredientId)
                    OR (@mealId IS NOT NULL AND [MealId] = @mealId))
            )
            BEGIN
                UPDATE [NutritionFacts]
                SET [CaloriesPer100g] = @Calories,
                    [ProteinPer100g] = @Protein,
                    [CarbohydratesPer100g] = @Carbohydrates,
                    [FatPer100g] = @Fat,
                    [FiberPer100g] = @Fiber,
                    [UpdatedAt] = @now
                WHERE ((@ingredientId IS NOT NULL AND [IngredientId] = @ingredientId)
                    OR (@mealId IS NOT NULL AND [MealId] = @mealId));
            END
            ELSE
            BEGIN
                INSERT INTO [NutritionFacts]
                    ([MealId], [IngredientId], [CaloriesPer100g], [ProteinPer100g],
                     [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [CreatedAt], [UpdatedAt])
                VALUES
                    (@mealId, @ingredientId, @Calories, @Protein, @Carbohydrates, @Fat, @Fiber, @now, NULL);
            END
            """,
            new
            {
                ingredientId,
                mealId,
                nutrition.Calories,
                nutrition.Protein,
                nutrition.Carbohydrates,
                nutrition.Fat,
                nutrition.Fiber,
                now,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task ReplaceCanonicalIngredientAllergensAsync(
        IDbConnection db,
        int ingredientId,
        IReadOnlyCollection<string> allergenCodes,
        IReadOnlyDictionary<string, int> allergenIds,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [IngredientAllergens] WHERE [IngredientId] = @ingredientId;",
            new { ingredientId },
            cancellationToken: cancellationToken));

        foreach (var code in allergenCodes)
        {
            if (!allergenIds.TryGetValue(code, out var allergenId))
            {
                continue;
            }

            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [IngredientAllergens] ([IngredientId], [AllergenId], [TraceAmount])
                VALUES (@ingredientId, @allergenId, 0);
                """,
                new { ingredientId, allergenId },
                cancellationToken: cancellationToken));
        }
    }

    private static async Task<Dictionary<string, int>> EnsureCanonicalM2RecipeComponentsAsync(
        IDbConnection db,
        IReadOnlyDictionary<string, int> categoryIds,
        IReadOnlyDictionary<string, int> ingredientIds,
        IReadOnlyDictionary<string, int> packagingStockItemIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var components = GetCanonicalComponentSeeds();
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var component in components)
        {
            var componentId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
                """
                SELECT TOP 1 [Id]
                FROM [RecipeComponents]
                WHERE [Name] = @Name
                  AND [IsDeleted] = 0
                ORDER BY [Id];
                """,
                new { component.Name },
                cancellationToken: cancellationToken));

            if (componentId.HasValue)
            {
                await db.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE [RecipeComponents]
                    SET [Description] = @Description,
                        [CategoryId] = @CategoryId,
                        [PreparationTimeMinutes] = @PreparationTimeMinutes,
                        [IsActive] = 1,
                        [UpdatedAt] = @now,
                        [UpdatedBy] = @auditUser,
                        [IsDeleted] = 0
                    WHERE [Id] = @componentId;
                    """,
                    new
                    {
                        componentId = componentId.Value,
                        component.Description,
                        CategoryId = categoryIds[component.CategoryKey],
                        component.PreparationTimeMinutes,
                        now,
                        auditUser,
                    },
                    cancellationToken: cancellationToken));
            }
            else
            {
                componentId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    INSERT INTO [RecipeComponents]
                        ([Name], [Description], [CategoryId], [ImageUrl], [PreparationTimeMinutes],
                         [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
                    OUTPUT INSERTED.[Id]
                    VALUES
                        (@Name, @Description, @CategoryId, NULL, @PreparationTimeMinutes,
                         1, @now, @auditUser, 0);
                    """,
                    new
                    {
                        component.Name,
                        component.Description,
                        CategoryId = categoryIds[component.CategoryKey],
                        component.PreparationTimeMinutes,
                        now,
                        auditUser,
                    },
                    cancellationToken: cancellationToken));
            }

            foreach (var version in component.Versions)
            {
                var versionId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
                    """
                    SELECT TOP 1 [Id]
                    FROM [RecipeComponentVersions]
                    WHERE [RecipeComponentId] = @componentId
                      AND [VersionNumber] = @VersionNumber
                      AND [IsDeleted] = 0
                    ORDER BY [Id];
                    """,
                    new { componentId = componentId.Value, version.VersionNumber },
                    cancellationToken: cancellationToken));

                if (versionId.HasValue)
                {
                    await db.ExecuteAsync(new CommandDefinition(
                        """
                        UPDATE [RecipeComponentVersions]
                        SET [Status] = N'Published',
                            [Instructions] = @Instructions,
                            [YieldQuantity] = @YieldQuantity,
                            [YieldUnit] = @YieldUnit,
                            [RawWeightGrams] = @RawWeightGrams,
                            [CookedWeightGrams] = @CookedWeightGrams,
                            [CaloriesPer100g] = @Calories,
                            [ProteinPer100g] = @Protein,
                            [CarbohydratesPer100g] = @Carbohydrates,
                            [FatPer100g] = @Fat,
                            [FiberPer100g] = @Fiber,
                            [ShelfLifeHours] = @ShelfLifeHours,
                            [UseEarliestIngredientExpiry] = @UseEarliestIngredientExpiry,
                            [NutritionSource] = N'Manual',
                            [NutritionOverrideReason] = @NutritionOverrideReason,
                            [AllergensApproved] = 1,
                            [AllergenOverrideReason] = @AllergenOverrideReason,
                            [AllergensApprovedAt] = COALESCE([AllergensApprovedAt], @now),
                            [AllergensApprovedBy] = COALESCE([AllergensApprovedBy], @auditUser),
                            [ChangeSummary] = @ChangeSummary,
                            [IsTechnologyChange] = @IsTechnologyChange,
                            [PublishedAt] = COALESCE([PublishedAt], @now),
                            [PublishedBy] = COALESCE([PublishedBy], @auditUser),
                            [UpdatedAt] = @now,
                            [UpdatedBy] = @auditUser
                        WHERE [Id] = @versionId;
                        """,
                        new
                        {
                            versionId = versionId.Value,
                            version.Instructions,
                            version.YieldQuantity,
                            version.YieldUnit,
                            version.RawWeightGrams,
                            version.CookedWeightGrams,
                            version.Nutrition.Calories,
                            version.Nutrition.Protein,
                            version.Nutrition.Carbohydrates,
                            version.Nutrition.Fat,
                            version.Nutrition.Fiber,
                            version.ShelfLifeHours,
                            version.UseEarliestIngredientExpiry,
                            NutritionOverrideReason = "Demo M2: wartosci odzywcze wersji skladowej do testow snapshotu.",
                            AllergenOverrideReason = "Demo M2: alergeny zatwierdzone na podstawie skladnikow.",
                            version.ChangeSummary,
                            version.IsTechnologyChange,
                            now,
                            auditUser,
                        },
                        cancellationToken: cancellationToken));
                }
                else
                {
                    versionId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                        """
                        INSERT INTO [RecipeComponentVersions]
                            ([RecipeComponentId], [VersionNumber], [Status], [Instructions], [YieldQuantity], [YieldUnit],
                             [RawWeightGrams], [CookedWeightGrams], [CaloriesPer100g], [ProteinPer100g],
                             [CarbohydratesPer100g], [FatPer100g], [FiberPer100g], [ShelfLifeHours],
                             [UseEarliestIngredientExpiry], [NutritionSource], [NutritionOverrideReason],
                             [AllergensApproved], [AllergenOverrideReason], [AllergensApprovedAt], [AllergensApprovedBy],
                             [ChangeSummary], [IsTechnologyChange], [PublishedAt], [PublishedBy], [CreatedAt], [CreatedBy], [IsDeleted])
                        OUTPUT INSERTED.[Id]
                        VALUES
                            (@componentId, @VersionNumber, N'Published', @Instructions, @YieldQuantity, @YieldUnit,
                             @RawWeightGrams, @CookedWeightGrams, @Calories, @Protein,
                             @Carbohydrates, @Fat, @Fiber, @ShelfLifeHours,
                             @UseEarliestIngredientExpiry, N'Manual', @NutritionOverrideReason,
                             1, @AllergenOverrideReason, @now, @auditUser,
                             @ChangeSummary, @IsTechnologyChange, @now, @auditUser, @now, @auditUser, 0);
                        """,
                        new
                        {
                            componentId = componentId.Value,
                            version.VersionNumber,
                            version.Instructions,
                            version.YieldQuantity,
                            version.YieldUnit,
                            version.RawWeightGrams,
                            version.CookedWeightGrams,
                            version.Nutrition.Calories,
                            version.Nutrition.Protein,
                            version.Nutrition.Carbohydrates,
                            version.Nutrition.Fat,
                            version.Nutrition.Fiber,
                            version.ShelfLifeHours,
                            version.UseEarliestIngredientExpiry,
                            NutritionOverrideReason = "Demo M2: wartosci odzywcze wersji skladowej do testow snapshotu.",
                            AllergenOverrideReason = "Demo M2: alergeny zatwierdzone na podstawie skladnikow.",
                            version.ChangeSummary,
                            version.IsTechnologyChange,
                            now,
                            auditUser,
                        },
                        cancellationToken: cancellationToken));
                }

                await ReplaceCanonicalComponentIngredientsAsync(db, versionId.Value, version.Ingredients, ingredientIds, now, auditUser, cancellationToken);
                await ReplaceCanonicalInstructionStepsAsync(db, versionId.Value, version.Sections, now, auditUser, cancellationToken);
                await ReplaceCanonicalComponentPackagingAsync(db, versionId.Value, version.PackagingKey, packagingStockItemIds, now, auditUser, cancellationToken);
                result[$"{component.Key}:v{version.VersionNumber}"] = versionId.Value;
            }
        }

        return result;
    }

    private static async Task ReplaceCanonicalComponentIngredientsAsync(
        IDbConnection db,
        int versionId,
        IReadOnlyCollection<CanonicalComponentIngredientSeed> ingredients,
        IReadOnlyDictionary<string, int> ingredientIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "UPDATE [RecipeComponentIngredients] SET [IsDeleted] = 1, [DeletedAt] = @now, [DeletedBy] = @auditUser WHERE [RecipeComponentVersionId] = @versionId AND [IsDeleted] = 0;",
            new { versionId, now, auditUser },
            cancellationToken: cancellationToken));

        foreach (var ingredient in ingredients)
        {
            var ingredientId = ingredientIds[ingredient.IngredientName];
            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [RecipeComponentIngredients]
                    ([RecipeComponentVersionId], [IngredientId], [StockItemId], [WarehouseCategoryId],
                     [WeightInGrams], [YieldFactor], [IsOptional], [Notes], [CreatedAt], [CreatedBy], [IsDeleted])
                SELECT
                    @versionId,
                    i.[Id],
                    COALESCE(i.[StockItemId], si.[Id]),
                    COALESCE(i.[WarehouseCategoryId], si.[WarehouseCategoryId]),
                    @WeightInGrams,
                    COALESCE(NULLIF(@YieldFactor, 0), i.[YieldFactor], 1.0),
                    @IsOptional,
                    @Notes,
                    @now,
                    @auditUser,
                    0
                FROM [Ingredients] i
                LEFT JOIN [StockItems] si ON si.[BaseIngredientId] = i.[Id] AND si.[IsDeleted] = 0
                WHERE i.[Id] = @ingredientId;
                """,
                new
                {
                    versionId,
                    ingredientId,
                    ingredient.WeightInGrams,
                    ingredient.YieldFactor,
                    ingredient.IsOptional,
                    ingredient.Notes,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));
        }
    }

    private static async Task ReplaceCanonicalInstructionStepsAsync(
        IDbConnection db,
        int versionId,
        IReadOnlyCollection<CanonicalInstructionSectionSeed> sections,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE steps
            SET [IsDeleted] = 1,
                [DeletedAt] = @now,
                [DeletedBy] = @auditUser
            FROM [RecipeComponentInstructionSteps] steps
            INNER JOIN [RecipeComponentInstructionSections] sections
                ON sections.[Id] = steps.[RecipeComponentInstructionSectionId]
            WHERE sections.[RecipeComponentVersionId] = @versionId
              AND steps.[IsDeleted] = 0;

            UPDATE [RecipeComponentInstructionSections]
            SET [IsDeleted] = 1,
                [DeletedAt] = @now,
                [DeletedBy] = @auditUser
            WHERE [RecipeComponentVersionId] = @versionId
              AND [IsDeleted] = 0;
            """,
            new { versionId, now, auditUser },
            cancellationToken: cancellationToken));

        foreach (var section in sections)
        {
            var sectionId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                """
                INSERT INTO [RecipeComponentInstructionSections]
                    ([RecipeComponentVersionId], [Title], [SortOrder], [CreatedAt], [CreatedBy], [IsDeleted])
                OUTPUT INSERTED.[Id]
                VALUES (@versionId, @Title, @SortOrder, @now, @auditUser, 0);
                """,
                new { versionId, section.Title, section.SortOrder, now, auditUser },
                cancellationToken: cancellationToken));

            foreach (var step in section.Steps)
            {
                await db.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO [RecipeComponentInstructionSteps]
                        ([RecipeComponentInstructionSectionId], [StepText], [SortOrder], [RequiresControl],
                         [ControlType], [ExpectedValue], [ExpectedUnit], [IsCritical], [CreatedAt], [CreatedBy], [IsDeleted])
                    VALUES
                        (@sectionId, @StepText, @SortOrder, @RequiresControl,
                         @ControlType, @ExpectedValue, @ExpectedUnit, @IsCritical, @now, @auditUser, 0);
                    """,
                    new
                    {
                        sectionId,
                        step.StepText,
                        step.SortOrder,
                        step.RequiresControl,
                        step.ControlType,
                        step.ExpectedValue,
                        step.ExpectedUnit,
                        step.IsCritical,
                        now,
                        auditUser,
                    },
                    cancellationToken: cancellationToken));
            }
        }
    }

    private static async Task ReplaceCanonicalComponentPackagingAsync(
        IDbConnection db,
        int versionId,
        string packagingKey,
        IReadOnlyDictionary<string, int> packagingStockItemIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "UPDATE [PackagingRequirements] SET [IsDeleted] = 1, [DeletedAt] = @now, [DeletedBy] = @auditUser WHERE [RecipeComponentVersionId] = @versionId AND [IsDeleted] = 0;",
            new { versionId, now, auditUser },
            cancellationToken: cancellationToken));

        var stockItemId = packagingStockItemIds[packagingKey];
        var resourceName = await db.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT [Name] FROM [StockItems] WHERE [Id] = @stockItemId;",
            new { stockItemId },
            cancellationToken: cancellationToken));

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [PackagingRequirements]
                ([OwnerType], [MealId], [MealVariantId], [RecipeComponentVersionId], [StockItemId], [WarehouseCategoryId],
                 [ResourceName], [Quantity], [Unit], [ContainerRole], [IsCustomerFacing],
                 [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (N'RecipeComponentVersion', NULL, NULL, @versionId, @stockItemId, @warehouseCategoryId,
                 @resourceName, @quantity, N'szt', @containerRole, @isCustomerFacing,
                 @now, @auditUser, 0);
            """,
            new
            {
                versionId,
                stockItemId,
                warehouseCategoryId = PackagingWarehouseCategoryId,
                resourceName,
                quantity = packagingKey == "sauce80" ? 1.0m : 0.05m,
                containerRole = packagingKey == "sauce80" ? "ComponentCup" : "ProductionContainer",
                isCustomerFacing = packagingKey == "sauce80",
                now,
                auditUser,
            },
                cancellationToken: cancellationToken));
    }

    private static async Task<Dictionary<string, int>> EnsureCanonicalM2MealsAndVariantsAsync(
        IDbConnection db,
        IReadOnlyDictionary<string, int> categoryIds,
        IReadOnlyDictionary<string, int> componentVersionIds,
        IReadOnlyDictionary<string, int> packagingStockItemIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var meals = GetCanonicalMealSeeds();
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var meal in meals)
        {
            var mealId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
                """
                SELECT TOP 1 [Id]
                FROM [Meals]
                WHERE [Name] = @Name
                  AND [IsDeleted] = 0
                ORDER BY [Id];
                """,
                new { meal.Name },
                cancellationToken: cancellationToken));

            if (mealId.HasValue)
            {
                await db.ExecuteAsync(new CommandDefinition(
                    """
                    UPDATE [Meals]
                    SET [CategoryId] = @CategoryId,
                        [Description] = @Description,
                        [MarketingDescription] = @MarketingDescription,
                        [Status] = N'Published',
                        [PreparationTimeMinutes] = @PreparationTimeMinutes,
                        [PreparationInstructions] = @PreparationInstructions,
                        [ShelfLifeHours] = @ShelfLifeHours,
                        [UseEarliestIngredientExpiry] = @UseEarliestIngredientExpiry,
                        [IsActive] = 1,
                        [UpdatedAt] = @now,
                        [UpdatedBy] = @auditUser,
                        [IsDeleted] = 0
                    WHERE [Id] = @mealId;
                    """,
                    new
                    {
                        mealId = mealId.Value,
                        CategoryId = categoryIds[meal.CategoryKey],
                        meal.Description,
                        meal.MarketingDescription,
                        meal.PreparationTimeMinutes,
                        meal.PreparationInstructions,
                        meal.ShelfLifeHours,
                        meal.UseEarliestIngredientExpiry,
                        now,
                        auditUser,
                    },
                    cancellationToken: cancellationToken));
            }
            else
            {
                mealId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                    """
                    INSERT INTO [Meals]
                        ([CategoryId], [Name], [Description], [MarketingDescription], [Status],
                         [PreparationTimeMinutes], [PreparationInstructions], [ShelfLifeHours],
                         [UseEarliestIngredientExpiry], [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
                    OUTPUT INSERTED.[Id]
                    VALUES
                        (@CategoryId, @Name, @Description, @MarketingDescription, N'Published',
                         @PreparationTimeMinutes, @PreparationInstructions, @ShelfLifeHours,
                         @UseEarliestIngredientExpiry, 1, @now, @auditUser, 0);
                    """,
                    new
                    {
                        CategoryId = categoryIds[meal.CategoryKey],
                        meal.Name,
                        meal.Description,
                        meal.MarketingDescription,
                        meal.PreparationTimeMinutes,
                        meal.PreparationInstructions,
                        meal.ShelfLifeHours,
                        meal.UseEarliestIngredientExpiry,
                        now,
                        auditUser,
                    },
                    cancellationToken: cancellationToken));
            }

            await ReplaceCanonicalMealRecipeComponentsAsync(db, mealId.Value, meal.BaseComponents, componentVersionIds, now, auditUser, cancellationToken);
            await ReplaceCanonicalMealPackagingAsync(db, mealId.Value, meal.PackagingKey, packagingStockItemIds, now, auditUser, cancellationToken);

            foreach (var variant in meal.Variants)
            {
                var variantId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
                    """
                    SELECT TOP 1 [Id]
                    FROM [MealVariants]
                    WHERE [MealId] = @mealId
                      AND [Name] = @Name
                      AND [IsDeleted] = 0
                    ORDER BY [Id];
                    """,
                    new { mealId = mealId.Value, variant.Name },
                    cancellationToken: cancellationToken));

                if (variantId.HasValue)
                {
                    await db.ExecuteAsync(new CommandDefinition(
                        """
                        UPDATE [MealVariants]
                        SET [VariantType] = @VariantType,
                            [Status] = N'Published',
                            [Description] = @Description,
                            [IsDefault] = @IsDefault,
                            [NutritionSource] = @NutritionSource,
                            [NutritionOverrideReason] = @NutritionOverrideReason,
                            [AllergensApproved] = 1,
                            [AllergenOverrideReason] = N'Demo M2: alergeny wariantu zatwierdzone z komponentow.',
                            [PublishedAt] = COALESCE([PublishedAt], @now),
                            [PublishedBy] = COALESCE([PublishedBy], @auditUser),
                            [UpdatedAt] = @now,
                            [UpdatedBy] = @auditUser,
                            [IsDeleted] = 0
                        WHERE [Id] = @variantId;
                        """,
                        new
                        {
                            variantId = variantId.Value,
                            variant.VariantType,
                            variant.Description,
                            variant.IsDefault,
                            variant.NutritionSource,
                            variant.NutritionOverrideReason,
                            now,
                            auditUser,
                        },
                        cancellationToken: cancellationToken));
                }
                else
                {
                    variantId = await db.ExecuteScalarAsync<int>(new CommandDefinition(
                        """
                        INSERT INTO [MealVariants]
                            ([MealId], [Name], [VariantType], [Status], [Description], [IsDefault],
                             [NutritionSource], [NutritionOverrideReason], [AllergensApproved],
                             [AllergenOverrideReason], [PublishedAt], [PublishedBy], [CreatedAt], [CreatedBy], [IsDeleted])
                        OUTPUT INSERTED.[Id]
                        VALUES
                            (@mealId, @Name, @VariantType, N'Published', @Description, @IsDefault,
                             @NutritionSource, @NutritionOverrideReason, 1,
                             N'Demo M2: alergeny wariantu zatwierdzone z komponentow.', @now, @auditUser, @now, @auditUser, 0);
                        """,
                        new
                        {
                            mealId = mealId.Value,
                            variant.Name,
                            variant.VariantType,
                            variant.Description,
                            variant.IsDefault,
                            variant.NutritionSource,
                            variant.NutritionOverrideReason,
                            now,
                            auditUser,
                        },
                        cancellationToken: cancellationToken));
                }

                await ReplaceCanonicalMealVariantComponentsAsync(db, variantId.Value, variant.Components, componentVersionIds, now, auditUser, cancellationToken);
                await ReplaceCanonicalMealVariantPackagingAsync(db, variantId.Value, variant.PackagingKey ?? meal.PackagingKey, packagingStockItemIds, now, auditUser, cancellationToken);
                result[$"{meal.Key}:{variant.Key}"] = variantId.Value;
            }

            await UpdateCanonicalMealVariantResultFieldsAsync(db, mealId.Value, now, auditUser, cancellationToken);
            await UpdateCanonicalMealFactsFromDefaultVariantAsync(db, mealId.Value, now, cancellationToken);
        }

        return result;
    }

    private static async Task ReplaceCanonicalMealRecipeComponentsAsync(
        IDbConnection db,
        int mealId,
        IReadOnlyCollection<CanonicalMealComponentSeed> components,
        IReadOnlyDictionary<string, int> componentVersionIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "UPDATE [MealRecipeComponents] SET [IsDeleted] = 1, [DeletedAt] = @now, [DeletedBy] = @auditUser WHERE [MealId] = @mealId AND [IsDeleted] = 0;",
            new { mealId, now, auditUser },
            cancellationToken: cancellationToken));

        foreach (var component in components)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [MealRecipeComponents]
                    ([MealId], [RecipeComponentVersionId], [Role], [QuantityPerServing], [Unit],
                     [SortOrder], [IsOptional], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@mealId, @versionId, @Role, @QuantityPerServing, @Unit,
                     @SortOrder, @IsOptional, @now, @auditUser, 0);
                """,
                new
                {
                    mealId,
                    versionId = componentVersionIds[component.ComponentVersionKey],
                    component.Role,
                    component.QuantityPerServing,
                    component.Unit,
                    component.SortOrder,
                    component.IsOptional,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));
        }
    }

    private static async Task ReplaceCanonicalMealVariantComponentsAsync(
        IDbConnection db,
        int mealVariantId,
        IReadOnlyCollection<CanonicalMealComponentSeed> components,
        IReadOnlyDictionary<string, int> componentVersionIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "UPDATE [MealVariantComponents] SET [IsDeleted] = 1, [DeletedAt] = @now, [DeletedBy] = @auditUser WHERE [MealVariantId] = @mealVariantId AND [IsDeleted] = 0;",
            new { mealVariantId, now, auditUser },
            cancellationToken: cancellationToken));

        foreach (var component in components)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO [MealVariantComponents]
                    ([MealVariantId], [RecipeComponentVersionId], [Role], [QuantityPerServing], [Unit],
                     [SortOrder], [IsOptional], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@mealVariantId, @versionId, @Role, @QuantityPerServing, @Unit,
                     @SortOrder, @IsOptional, @now, @auditUser, 0);
                """,
                new
                {
                    mealVariantId,
                    versionId = componentVersionIds[component.ComponentVersionKey],
                    component.Role,
                    component.QuantityPerServing,
                    component.Unit,
                    component.SortOrder,
                    component.IsOptional,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));
        }
    }

    private static async Task ReplaceCanonicalMealPackagingAsync(
        IDbConnection db,
        int mealId,
        string packagingKey,
        IReadOnlyDictionary<string, int> packagingStockItemIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "UPDATE [PackagingRequirements] SET [IsDeleted] = 1, [DeletedAt] = @now, [DeletedBy] = @auditUser WHERE [MealId] = @mealId AND [MealVariantId] IS NULL AND [IsDeleted] = 0;",
            new { mealId, now, auditUser },
            cancellationToken: cancellationToken));

        await InsertCanonicalPackagingRequirementAsync(db, mealId, null, null, packagingKey, packagingStockItemIds, 1.0m, "MealBox", true, now, auditUser, cancellationToken);
    }

    private static async Task ReplaceCanonicalMealVariantPackagingAsync(
        IDbConnection db,
        int mealVariantId,
        string packagingKey,
        IReadOnlyDictionary<string, int> packagingStockItemIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            "UPDATE [PackagingRequirements] SET [IsDeleted] = 1, [DeletedAt] = @now, [DeletedBy] = @auditUser WHERE [MealVariantId] = @mealVariantId AND [IsDeleted] = 0;",
            new { mealVariantId, now, auditUser },
            cancellationToken: cancellationToken));

        await InsertCanonicalPackagingRequirementAsync(db, null, mealVariantId, null, packagingKey, packagingStockItemIds, 1.0m, "VariantMealBox", true, now, auditUser, cancellationToken);
    }

    private static async Task InsertCanonicalPackagingRequirementAsync(
        IDbConnection db,
        int? mealId,
        int? mealVariantId,
        int? recipeComponentVersionId,
        string packagingKey,
        IReadOnlyDictionary<string, int> packagingStockItemIds,
        decimal quantity,
        string containerRole,
        bool isCustomerFacing,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var stockItemId = packagingStockItemIds[packagingKey];
        var resourceName = await db.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT [Name] FROM [StockItems] WHERE [Id] = @stockItemId;",
            new { stockItemId },
            cancellationToken: cancellationToken));

        await db.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [PackagingRequirements]
                ([OwnerType], [MealId], [MealVariantId], [RecipeComponentVersionId], [StockItemId], [WarehouseCategoryId],
                 [ResourceName], [Quantity], [Unit], [ContainerRole], [IsCustomerFacing],
                 [CreatedAt], [CreatedBy], [IsDeleted])
            VALUES
                (@OwnerType, @mealId, @mealVariantId, @recipeComponentVersionId, @stockItemId, @warehouseCategoryId,
                 @resourceName, @quantity, N'szt', @containerRole, @isCustomerFacing,
                 @now, @auditUser, 0);
            """,
            new
            {
                OwnerType = mealVariantId.HasValue
                    ? "MealVariant"
                    : recipeComponentVersionId.HasValue
                        ? "RecipeComponentVersion"
                        : "Meal",
                mealId,
                mealVariantId,
                recipeComponentVersionId,
                stockItemId,
                warehouseCategoryId = PackagingWarehouseCategoryId,
                resourceName,
                quantity,
                containerRole,
                isCustomerFacing,
                now,
                auditUser,
            },
            cancellationToken: cancellationToken));
    }

    private static async Task UpdateCanonicalMealVariantResultFieldsAsync(
        IDbConnection db,
        int mealId,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(new CommandDefinition(
            """
            ;WITH ComponentScale AS
            (
                SELECT
                    mvc.[MealVariantId],
                    rcv.[RawWeightGrams],
                    rcv.[CookedWeightGrams],
                    rcv.[CaloriesPer100g],
                    rcv.[ProteinPer100g],
                    rcv.[CarbohydratesPer100g],
                    rcv.[FatPer100g],
                    rcv.[FiberPer100g],
                    CASE
                        WHEN LOWER(mvc.[Unit]) IN (N'portion', N'portions')
                            THEN mvc.[QuantityPerServing] / NULLIF(rcv.[YieldQuantity], 0)
                        WHEN LOWER(mvc.[Unit]) IN (N'g', N'gram', N'grams')
                            THEN mvc.[QuantityPerServing] / NULLIF(COALESCE(rcv.[CookedWeightGrams], rcv.[RawWeightGrams]), 0)
                        ELSE 0
                    END AS [ScaleFactor]
                FROM [MealVariantComponents] mvc
                INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mvc.[RecipeComponentVersionId]
                INNER JOIN [MealVariants] mv ON mv.[Id] = mvc.[MealVariantId]
                WHERE mv.[MealId] = @mealId
                  AND mv.[IsDeleted] = 0
                  AND mvc.[IsDeleted] = 0
                  AND rcv.[IsDeleted] = 0
            ),
            Totals AS
            (
                SELECT
                    [MealVariantId],
                    SUM([RawWeightGrams] * [ScaleFactor]) AS [RawWeightGrams],
                    SUM([CookedWeightGrams] * [ScaleFactor]) AS [CookedWeightGrams],
                    SUM(([CaloriesPer100g] / 100.0) * COALESCE([CookedWeightGrams], [RawWeightGrams]) * [ScaleFactor]) AS [CaloriesTotal],
                    SUM(([ProteinPer100g] / 100.0) * COALESCE([CookedWeightGrams], [RawWeightGrams]) * [ScaleFactor]) AS [ProteinTotal],
                    SUM(([CarbohydratesPer100g] / 100.0) * COALESCE([CookedWeightGrams], [RawWeightGrams]) * [ScaleFactor]) AS [CarbohydratesTotal],
                    SUM(([FatPer100g] / 100.0) * COALESCE([CookedWeightGrams], [RawWeightGrams]) * [ScaleFactor]) AS [FatTotal],
                    SUM(([FiberPer100g] / 100.0) * COALESCE([CookedWeightGrams], [RawWeightGrams]) * [ScaleFactor]) AS [FiberTotal]
                FROM ComponentScale
                GROUP BY [MealVariantId]
            )
            UPDATE mv
            SET [RawWeightGrams] = totals.[RawWeightGrams],
                [CookedWeightGrams] = totals.[CookedWeightGrams],
                [CaloriesPer100g] = CASE WHEN totals.[CookedWeightGrams] > 0 THEN totals.[CaloriesTotal] / totals.[CookedWeightGrams] * 100 ELSE mv.[CaloriesPer100g] END,
                [ProteinPer100g] = CASE WHEN totals.[CookedWeightGrams] > 0 THEN totals.[ProteinTotal] / totals.[CookedWeightGrams] * 100 ELSE mv.[ProteinPer100g] END,
                [CarbohydratesPer100g] = CASE WHEN totals.[CookedWeightGrams] > 0 THEN totals.[CarbohydratesTotal] / totals.[CookedWeightGrams] * 100 ELSE mv.[CarbohydratesPer100g] END,
                [FatPer100g] = CASE WHEN totals.[CookedWeightGrams] > 0 THEN totals.[FatTotal] / totals.[CookedWeightGrams] * 100 ELSE mv.[FatPer100g] END,
                [FiberPer100g] = CASE WHEN totals.[CookedWeightGrams] > 0 THEN totals.[FiberTotal] / totals.[CookedWeightGrams] * 100 ELSE mv.[FiberPer100g] END,
                [NutritionSource] = COALESCE(NULLIF(mv.[NutritionSource], N''), N'Aggregated'),
                [AllergensApproved] = 1,
                [Status] = N'Published',
                [UpdatedAt] = @now,
                [UpdatedBy] = @auditUser
            FROM [MealVariants] mv
            INNER JOIN Totals totals ON totals.[MealVariantId] = mv.[Id]
            WHERE mv.[MealId] = @mealId
              AND mv.[IsDeleted] = 0;
            """,
            new { mealId, now, auditUser },
            cancellationToken: cancellationToken));
    }

    private static async Task UpdateCanonicalMealFactsFromDefaultVariantAsync(
        IDbConnection db,
        int mealId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var variant = await db.QuerySingleOrDefaultAsync<CanonicalVariantNutritionRow>(new CommandDefinition(
            """
            SELECT TOP 1
                [RawWeightGrams],
                [CookedWeightGrams],
                [CaloriesPer100g] AS [Calories],
                [ProteinPer100g] AS [Protein],
                [CarbohydratesPer100g] AS [Carbohydrates],
                [FatPer100g] AS [Fat],
                [FiberPer100g] AS [Fiber]
            FROM [MealVariants]
            WHERE [MealId] = @mealId
              AND [IsDeleted] = 0
            ORDER BY [IsDefault] DESC, [Id];
            """,
            new { mealId },
            cancellationToken: cancellationToken));

        if (variant is null)
        {
            return;
        }

        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [Meals]
            SET [RawWeightGrams] = @RawWeightGrams,
                [CookedWeightGrams] = @CookedWeightGrams,
                [UpdatedAt] = @now
            WHERE [Id] = @mealId;
            """,
            new { mealId, variant.RawWeightGrams, variant.CookedWeightGrams, now },
            cancellationToken: cancellationToken));

        await UpsertCanonicalNutritionFactsAsync(
            db,
            null,
            mealId,
            new CanonicalNutritionSeed(
                variant.Calories ?? 0m,
                variant.Protein ?? 0m,
                variant.Carbohydrates ?? 0m,
                variant.Fat ?? 0m,
                variant.Fiber ?? 0m),
            now,
            cancellationToken);
    }

    private static async Task EnsureCanonicalM2DietsAndPlansAsync(
        IDbConnection db,
        IReadOnlyDictionary<string, int> mealVariantIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var dietVariantIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var diet in GetCanonicalDietSeeds())
        {
            var dietId = await EnsureCanonicalDietAsync(db, diet.Name, diet.Description, diet.MarketingDescription, now, auditUser, cancellationToken);
            foreach (var variant in diet.Variants)
            {
                dietVariantIds[variant.Key] = await EnsureCanonicalDietVariantAsync(
                    db,
                    dietId,
                    variant.Name,
                    variant.TargetCalories,
                    variant.PriceMultiplier,
                    variant.IsDefault,
                    now,
                    auditUser,
                    cancellationToken);
            }
        }

        foreach (var plan in GetCanonicalDietVariantMealPlans())
        {
            var dietVariantId = dietVariantIds[plan.DietVariantKey];
            await db.ExecuteAsync(new CommandDefinition(
                "DELETE FROM [DietVariantMeals] WHERE [DietVariantId] = @dietVariantId;",
                new { dietVariantId },
                cancellationToken: cancellationToken));

            foreach (var slot in plan.Slots)
            {
                var mealVariantId = mealVariantIds[slot.MealVariantKey];
                await db.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO [DietVariantMeals] ([DietVariantId], [MealId], [ServingSizeMultiplier], [SortOrder])
                    SELECT @dietVariantId, mv.[MealId], @ServingSizeMultiplier, @SortOrder
                    FROM [MealVariants] mv
                    WHERE mv.[Id] = @mealVariantId;
                    """,
                    new
                    {
                        dietVariantId,
                        mealVariantId,
                        slot.ServingSizeMultiplier,
                        slot.SortOrder,
                    },
                    cancellationToken: cancellationToken));
            }
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        for (var offset = 0; offset < 8; offset++)
        {
            await EnsureCanonicalDietMenuPlanForDateAsync(
                db,
                today.AddDays(offset).ToDateTime(TimeOnly.MinValue),
                dietVariantIds,
                mealVariantIds,
                now,
                auditUser,
                cancellationToken);
        }
    }

    private static async Task<int> EnsureCanonicalDietAsync(
        IDbConnection db,
        string name,
        string description,
        string marketingDescription,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var dietId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 [Id] FROM [Diets] WHERE [Name] = @name AND [IsDeleted] = 0 ORDER BY [Id];",
            new { name },
            cancellationToken: cancellationToken));

        if (dietId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [Diets]
                SET [Description] = @description,
                    [MarketingDescription] = @marketingDescription,
                    [Status] = N'Active',
                    [IsActive] = 1,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @dietId;
                """,
                new { dietId = dietId.Value, description, marketingDescription, now, auditUser },
                cancellationToken: cancellationToken));

            return dietId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [Diets]
                ([Name], [Description], [MarketingDescription], [Status], [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
            OUTPUT INSERTED.[Id]
            VALUES
                (@name, @description, @marketingDescription, N'Active', 1, @now, @auditUser, 0);
            """,
            new { name, description, marketingDescription, now, auditUser },
            cancellationToken: cancellationToken));
    }

    private static async Task<int> EnsureCanonicalDietVariantAsync(
        IDbConnection db,
        int dietId,
        string name,
        int targetCalories,
        decimal priceMultiplier,
        bool isDefault,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var variantId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT TOP 1 [Id]
            FROM [DietVariants]
            WHERE [DietId] = @dietId
              AND [Name] = @name
              AND [IsDeleted] = 0
            ORDER BY [Id];
            """,
            new { dietId, name },
            cancellationToken: cancellationToken));

        if (variantId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [DietVariants]
                SET [TargetCalories] = @targetCalories,
                    [PriceMultiplier] = @priceMultiplier,
                    [IsDefault] = @isDefault,
                    [UpdatedAt] = @now,
                    [UpdatedBy] = @auditUser,
                    [IsDeleted] = 0
                WHERE [Id] = @variantId;
                """,
                new { variantId = variantId.Value, targetCalories, priceMultiplier, isDefault, now, auditUser },
                cancellationToken: cancellationToken));

            return variantId.Value;
        }

        return await db.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            INSERT INTO [DietVariants]
                ([DietId], [Name], [TargetCalories], [PriceMultiplier], [IsDefault], [CreatedAt], [CreatedBy], [IsDeleted])
            OUTPUT INSERTED.[Id]
            VALUES
                (@dietId, @name, @targetCalories, @priceMultiplier, @isDefault, @now, @auditUser, 0);
            """,
            new { dietId, name, targetCalories, priceMultiplier, isDefault, now, auditUser },
            cancellationToken: cancellationToken));
    }

    private static async Task EnsureCanonicalDietMenuPlanForDateAsync(
        IDbConnection db,
        DateTime planDate,
        IReadOnlyDictionary<string, int> dietVariantIds,
        IReadOnlyDictionary<string, int> mealVariantIds,
        DateTimeOffset now,
        string auditUser,
        CancellationToken cancellationToken)
    {
        var planId = await db.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT TOP 1 [Id] FROM [DietMenuPlans] WHERE [PlanDate] = @planDate ORDER BY [Id];",
            new { planDate },
            cancellationToken: cancellationToken));

        if (planId.HasValue)
        {
            await db.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [DietMenuPlans]
                SET [Status] = N'Published',
                    [Notes] = N'Demo M2: kanoniczny plan 8 dni ze snapshotem wariantow.',
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
                OUTPUT INSERTED.[Id]
                VALUES
                    (@planDate, N'Published', N'Demo M2: kanoniczny plan 8 dni ze snapshotem wariantow.',
                     @now, @auditUser, @now, @auditUser, 0);
                """,
                new { planDate, now, auditUser },
                cancellationToken: cancellationToken));
        }

        var canonicalDietVariantIds = dietVariantIds.Values.ToArray();
        await db.ExecuteAsync(new CommandDefinition(
            """
            UPDATE [DietMenuPlanItems]
            SET [IsDeleted] = 1,
                [DeletedAt] = @now,
                [DeletedBy] = @auditUser
            WHERE [DietMenuPlanId] = @planId
              AND [DietVariantId] IN @canonicalDietVariantIds
              AND [IsDeleted] = 0;
            """,
            new { planId = planId.Value, canonicalDietVariantIds, now, auditUser },
            cancellationToken: cancellationToken));

        foreach (var plan in GetCanonicalDietVariantMealPlans())
        {
            var dietVariantId = dietVariantIds[plan.DietVariantKey];
            foreach (var slot in plan.Slots)
            {
                var mealVariantId = mealVariantIds[slot.MealVariantKey];
                await db.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO [DietMenuPlanItems]
                        ([DietMenuPlanId], [DietVariantId], [MealId], [MealVariantId], [MealSlot],
                         [ServingSizeMultiplier], [SortOrder], [IsActive], [CreatedAt], [CreatedBy], [IsDeleted])
                    SELECT
                        @planId,
                        @dietVariantId,
                        mv.[MealId],
                        mv.[Id],
                        @MealSlot,
                        @ServingSizeMultiplier,
                        @SortOrder,
                        1,
                        @now,
                        @auditUser,
                        0
                    FROM [MealVariants] mv
                    WHERE mv.[Id] = @mealVariantId;
                    """,
                    new
                    {
                        planId = planId.Value,
                        dietVariantId,
                        mealVariantId,
                        slot.MealSlot,
                        slot.ServingSizeMultiplier,
                        slot.SortOrder,
                        now,
                        auditUser,
                    },
                    cancellationToken: cancellationToken));
            }
        }
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

    private static async Task<string> EnsureUnifiedDemoCustomerProfileAsync(
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
                    [DietaryNotes] = N'Unified demo: klient M1 uzywany przez M2/M3/M4 workflow.',
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
                    (@customerId, N'+48 500 000 001', N'Unified demo: klient M1 uzywany przez M2/M3/M4 workflow.', @addressId, @now, @auditUser, 0);
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
                    [Notes] = N'Unified demo: zamowienie M1 uzywane przez M2/M3/M4 workflow.',
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
                (@customerId, @orderNumber, @status, @totalPrice, 0, @totalPrice, N'Unified demo: zamowienie M1 uzywane przez M2/M3/M4 workflow.',
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
        DateOnly deliveryDate,
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
                     [DietName], [VariantName], [MealSlot], [DeliveryDate], [CaloriesPerDay],
                     [PricePerDay], [TotalDays], [TotalPrice], [CreatedAt], [CreatedBy], [IsDeleted])
                VALUES
                    (@orderId, @dietId, @dietVariantId, @mealId, @mealVariantId, @dietMenuPlanItemId,
                     @dietName, @variantName, @mealSlot, @deliveryDate, @caloriesPerDay,
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
                    deliveryDate = deliveryDate.ToDateTime(TimeOnly.MinValue),
                    caloriesPerDay = meal.CaloriesPerDay,
                    pricePerDay = meal.PricePerDay,
                    now,
                    auditUser,
                },
                cancellationToken: cancellationToken));
        }
    }

    private static IReadOnlyList<CanonicalIngredientSeed> GetCanonicalIngredientSeeds()
        =>
        [
            new("Demo M2 Piers z kurczaka", "g", 0.032m, "meat", 1, new(110m, 23m, 0m, 2m, 0m), "Chude mieso drobiowe do dan obiadowych.", null, [], 0.92m, true, 75m, 12000m, 65000m, 1),
            new("Demo M2 Udziec indyka", "g", 0.030m, "meat", 1, new(125m, 21m, 0m, 4m, 0m), "Mieso z indyka do wariantow high protein.", null, [], 0.90m, true, 75m, 10000m, 52000m, 1),
            new("Demo M2 Losos filet", "g", 0.075m, "meat", 1, new(208m, 20m, 0m, 13m, 0m), "Filet z lososia do dan rybnych.", null, ["RYB"], 0.88m, true, 63m, 7000m, 36000m, 1),
            new("Demo M2 Dorsz filet", "g", 0.052m, "meat", 1, new(82m, 18m, 0m, 0.7m, 0m), "Filet z dorsza do lekkich dan.", null, ["RYB"], 0.90m, true, 63m, 6000m, 30000m, 1),
            new("Demo M2 Mieso wolowe mielone", "g", 0.040m, "meat", 1, new(217m, 19m, 0m, 15m, 0m), "Wolowina do chili i sosow.", null, [], 0.88m, true, 70m, 9000m, 45000m, 2),
            new("Demo M2 Tofu naturalne", "g", 0.026m, "meat", 4, new(144m, 15m, 2m, 8m, 1m), "Alternatywa bialkowa dla diet vege.", "Soja, woda, koagulant wapniowy.", ["SOJ"], 0.96m, false, null, 8000m, 42000m, 3),
            new("Demo M2 Jaja", "g", 0.018m, "dairy", 2, new(143m, 13m, 1m, 10m, 0m), "Jaja kurze do sniadan.", null, ["JAJ"], 1.00m, true, 70m, 6000m, 30000m, 2),
            new("Demo M2 Jogurt grecki 2%", "g", 0.016m, "dairy", 2, new(73m, 9m, 3.5m, 2m, 0m), "Jogurt do sosow i deserow.", "Mleko, zywe kultury bakterii.", ["LAK"], 1.00m, false, null, 10000m, 50000m, 2),
            new("Demo M2 Jogurt bez laktozy", "g", 0.019m, "dairy", 2, new(72m, 8.5m, 4m, 2m, 0m), "Jogurt do wariantow bez laktozy.", "Mleko bez laktozy, kultury bakterii.", [], 1.00m, false, null, 7000m, 35000m, 2),
            new("Demo M2 Mleko kokosowe", "g", 0.021m, "processed", 4, new(190m, 2m, 3m, 19m, 0m), "Produkt przetworzony do sosow i deserow.", "Ekstrakt kokosowy, woda.", [], 1.00m, false, null, 7000m, 35000m, 7),
            new("Demo M2 Twarog poltlusty", "g", 0.020m, "dairy", 2, new(133m, 18m, 3.5m, 5m, 0m), "Nabial wysokobialkowy.", "Mleko, kultury bakterii.", ["LAK"], 1.00m, false, null, 5000m, 25000m, 2),
            new("Demo M2 Ser feta", "g", 0.034m, "dairy", 2, new(265m, 14m, 4m, 21m, 0m), "Ser do salatek.", "Mleko, sol, kultury bakterii.", ["LAK"], 1.00m, false, null, 4000m, 22000m, 3),
            new("Demo M2 Ryz basmati", "g", 0.010m, "dry", 4, new(360m, 7m, 79m, 0.6m, 1m), "Suchy ryz do gotowania.", null, [], 1.00m, false, null, 18000m, 90000m, 7),
            new("Demo M2 Komosa ryzowa", "g", 0.023m, "dry", 4, new(368m, 14m, 64m, 6m, 7m), "Quinoa do bowl i dan rybnych.", null, [], 1.00m, false, null, 9000m, 45000m, 7),
            new("Demo M2 Makaron pelnoziarnisty", "g", 0.011m, "dry", 4, new(350m, 13m, 68m, 2m, 8m), "Makaron z glutenem.", "Maka pszenna pelnoziarnista.", ["GLU"], 1.00m, false, null, 10000m, 50000m, 7),
            new("Demo M2 Tortilla pszenna", "g", 0.019m, "processed", 4, new(310m, 8m, 52m, 8m, 3m), "Produkt przetworzony do wrapow.", "Maka pszenna, woda, olej rzepakowy, sol.", ["GLU"], 1.00m, false, null, 6000m, 35000m, 7),
            new("Demo M2 Kasza bulgur", "g", 0.012m, "dry", 4, new(342m, 12m, 76m, 1m, 12m), "Kasza pszenna.", "Pszenica durum.", ["GLU"], 1.00m, false, null, 9000m, 45000m, 7),
            new("Demo M2 Platki owsiane", "g", 0.009m, "dry", 4, new(370m, 13m, 60m, 7m, 10m), "Platki do sniadan.", "Owies.", ["GLU"], 1.00m, false, null, 10000m, 50000m, 7),
            new("Demo M2 Granola orzechowa", "g", 0.026m, "processed", 4, new(450m, 10m, 58m, 18m, 8m), "Produkt przetworzony z wlasnym skladem.", "Platki owsiane, orzechy, miod, olej.", ["GLU", "ORZ"], 1.00m, false, null, 6000m, 30000m, 7),
            new("Demo M2 Nasiona chia", "g", 0.030m, "dry", 4, new(486m, 17m, 42m, 31m, 34m), "Nasiona do puddingow.", null, [], 1.00m, false, null, 3000m, 18000m, 10),
            new("Demo M2 Czekolada gorzka 70%", "g", 0.045m, "processed", 4, new(560m, 8m, 34m, 43m, 11m), "Produkt przetworzony do deserow.", "Miazga kakaowa, cukier, tluszcz kakaowy, lecytyna sojowa.", ["SOJ"], 1.00m, false, null, 4000m, 22000m, 10),
            new("Demo M2 Hummus", "g", 0.024m, "processed", 4, new(166m, 8m, 14m, 10m, 6m), "Produkt przetworzony do wrapow.", "Ciecierzyca, pasta sezamowa, oliwa, czosnek, sok z cytryny.", ["SEZ"], 1.00m, false, null, 6000m, 30000m, 5),
            new("Demo M2 Passata pomidorowa", "g", 0.008m, "processed", 4, new(32m, 1.4m, 5m, 0.2m, 1.5m), "Produkt przetworzony do sosow.", "Pomidory przetarte.", [], 1.00m, false, null, 10000m, 55000m, 10),
            new("Demo M2 Soczewica czerwona", "g", 0.012m, "dry", 4, new(352m, 24m, 52m, 1.5m, 11m), "Straczek do sosow vege.", null, [], 1.00m, false, null, 10000m, 52000m, 7),
            new("Demo M2 Ciecierzyca gotowana", "g", 0.014m, "processed", 4, new(164m, 9m, 27m, 2.6m, 8m), "Produkt przetworzony do hummusu i bowl.", "Ciecierzyca, woda, sol.", [], 1.00m, false, null, 8000m, 40000m, 7),
            new("Demo M2 Fasola czerwona", "g", 0.013m, "processed", 4, new(127m, 9m, 23m, 0.5m, 6m), "Produkt przetworzony do chili.", "Fasola czerwona, woda, sol.", [], 1.00m, false, null, 8000m, 40000m, 7),
            new("Demo M2 Brokul", "g", 0.012m, "veg", 3, new(34m, 2.8m, 6.6m, 0.4m, 2.6m), "Warzywo do gotowania na parze.", null, [], 0.92m, false, null, 8000m, 45000m, 1),
            new("Demo M2 Szpinak", "g", 0.014m, "veg", 3, new(23m, 2.9m, 3.6m, 0.4m, 2.2m), "Warzywo lisciaste.", null, [], 0.95m, false, null, 5000m, 25000m, 1),
            new("Demo M2 Papryka czerwona", "g", 0.013m, "veg", 3, new(31m, 1m, 6m, 0.3m, 2.1m), "Warzywo do wrapow i sosow.", null, [], 0.90m, false, null, 7000m, 35000m, 1),
            new("Demo M2 Cukinia", "g", 0.010m, "veg", 3, new(17m, 1.2m, 3.1m, 0.3m, 1m), "Warzywo do duszenia.", null, [], 0.92m, false, null, 7000m, 35000m, 1),
            new("Demo M2 Marchew", "g", 0.008m, "veg", 3, new(41m, 0.9m, 10m, 0.2m, 2.8m), "Warzywo korzeniowe.", null, [], 0.88m, false, null, 9000m, 45000m, 2),
            new("Demo M2 Pomidor koktajlowy", "g", 0.014m, "veg", 3, new(18m, 0.9m, 3.9m, 0.2m, 1.2m), "Warzywo do salatek.", null, [], 0.95m, false, null, 7000m, 35000m, 1),
            new("Demo M2 Batat", "g", 0.010m, "veg", 3, new(86m, 1.6m, 20m, 0.1m, 3m), "Warzywo skrobiowe.", null, [], 0.88m, false, null, 10000m, 55000m, 2),
            new("Demo M2 Ziemniaki", "g", 0.006m, "veg", 3, new(77m, 2m, 17m, 0.1m, 2.2m), "Warzywo skrobiowe.", null, [], 0.88m, false, null, 12000m, 65000m, 2),
            new("Demo M2 Cebula", "g", 0.006m, "veg", 3, new(40m, 1.1m, 9m, 0.1m, 1.7m), "Baza sosow.", null, [], 0.85m, false, null, 8000m, 40000m, 2),
            new("Demo M2 Czosnek", "g", 0.018m, "veg", 3, new(149m, 6.4m, 33m, 0.5m, 2.1m), "Przyprawa/warzywo aromatyczne.", null, [], 0.90m, false, null, 2000m, 12000m, 3),
            new("Demo M2 Szczypiorek", "g", 0.010m, "veg", 3, new(30m, 3.3m, 4.4m, 0.7m, 2.5m), "Dodatek do sniadan.", null, [], 0.85m, false, null, 1500m, 9000m, 2),
            new("Demo M2 Ogorek", "g", 0.007m, "veg", 3, new(15m, 0.7m, 3.6m, 0.1m, 0.5m), "Warzywo do salatek.", null, [], 0.95m, false, null, 7000m, 35000m, 1),
            new("Demo M2 Salata rzymska", "g", 0.011m, "veg", 3, new(17m, 1.2m, 3.3m, 0.3m, 2.1m), "Warzywo lisciaste.", null, [], 0.90m, false, null, 5000m, 25000m, 1),
            new("Demo M2 Borowki", "g", 0.020m, "fruit", 3, new(57m, 0.7m, 14.5m, 0.3m, 2.4m), "Owoc do deserow.", null, [], 0.98m, false, null, 5000m, 25000m, 4),
            new("Demo M2 Banan", "g", 0.007m, "fruit", 3, new(89m, 1.1m, 23m, 0.3m, 2.6m), "Owoc do sniadan.", null, [], 0.75m, false, null, 6000m, 32000m, 3),
            new("Demo M2 Jablko", "g", 0.006m, "fruit", 3, new(52m, 0.3m, 14m, 0.2m, 2.4m), "Owoc do przekasek.", null, [], 0.85m, false, null, 6000m, 32000m, 3),
            new("Demo M2 Awokado", "g", 0.026m, "fruit", 3, new(160m, 2m, 9m, 15m, 7m), "Owoc tluszczowy do salatek.", null, [], 0.72m, false, null, 4000m, 22000m, 3),
            new("Demo M2 Oliwa z oliwek", "g", 0.035m, "processed", 4, new(884m, 0m, 0m, 100m, 0m), "Tluszcz do smazenia i salatek.", "Oliwa z oliwek extra virgin.", [], 1.00m, false, null, 5000m, 28000m, 10),
            new("Demo M2 Maslo klarowane", "g", 0.030m, "dairy", 2, new(892m, 0m, 0m, 99m, 0m), "Tluszcz do smazenia.", "Tluszcz mleczny.", ["LAK"], 1.00m, false, null, 4000m, 22000m, 7),
            new("Demo M2 Pasta curry", "g", 0.040m, "processed", 4, new(180m, 4m, 22m, 8m, 5m), "Produkt przetworzony do sosu curry.", "Chili, czosnek, trawa cytrynowa, przyprawy.", [], 1.00m, false, null, 2500m, 15000m, 14),
            new("Demo M2 Sos sojowy", "g", 0.015m, "processed", 4, new(53m, 8m, 5m, 0m, 0m), "Produkt przetworzony do tofu.", "Woda, soja, pszenica, sol.", ["SOJ", "GLU"], 1.00m, false, null, 2500m, 15000m, 14),
            new("Demo M2 Miod", "g", 0.030m, "processed", 4, new(304m, 0.3m, 82m, 0m, 0m), "Slodzik do deserow.", null, [], 1.00m, false, null, 2500m, 15000m, 14),
            new("Demo M2 Kakao", "g", 0.020m, "dry", 4, new(228m, 20m, 58m, 14m, 33m), "Kakao do deserow.", null, [], 1.00m, false, null, 2000m, 12000m, 14),
            new("Demo M2 Orzechy wloskie", "g", 0.050m, "dry", 4, new(654m, 15m, 14m, 65m, 7m), "Orzechy do granoli i salatek.", null, ["ORZ"], 1.00m, false, null, 2500m, 15000m, 10),
            new("Demo M2 Przyprawa chili", "g", 0.018m, "spice", 4, new(282m, 12m, 50m, 14m, 35m), "Przyprawa ostra jako zasob magazynowy.", "Chili mielone.", [], 1.00m, false, null, 800m, 5000m, 14),
            new("Demo M2 Sol himalajska", "g", 0.004m, "spice", 4, new(0m, 0m, 0m, 0m, 0m), "Sol jako zasob magazynowy.", "Chlorek sodu.", [], 1.00m, false, null, 2000m, 12000m, 14),
        ];

    private static IReadOnlyList<CanonicalComponentSeed> GetCanonicalComponentSeeds()
        =>
        [
            Component("eggs", "Demo M2 Jajecznica ze szczypiorkiem", "breakfast", 15, "Sniadaniowa skladowa jajeczna.", "box250",
                [
                    Version(1, 220m, 205m, new(155m, 13m, 1.5m, 11m, 0.2m), "Usmaz jajka na masle klarowanym i dodaj szczypiorek.", [
                        Ing("Demo M2 Jaja", 150m), Ing("Demo M2 Maslo klarowane", 8m), Ing("Demo M2 Szczypiorek", 12m), Ing("Demo M2 Sol himalajska", 1m)
                    ], "Wersja standardowa."),
                    Version(2, 265m, 245m, new(142m, 17m, 1.4m, 8m, 0.2m), "Wersja high protein z dodatkowym bialkiem z twarogu.", [
                        Ing("Demo M2 Jaja", 150m), Ing("Demo M2 Twarog poltlusty", 70m), Ing("Demo M2 Maslo klarowane", 5m), Ing("Demo M2 Szczypiorek", 12m), Ing("Demo M2 Sol himalajska", 1m)
                    ], "Wersja high protein.")
                ]),
            Component("chia", "Demo M2 Pudding chia jogurtowy", "snack", 10, "Deser/sniadanie na zimno.", "box250",
                [
                    Version(1, 260m, 260m, new(128m, 8m, 14m, 5m, 6m), "Wymieszaj jogurt, chia, miod i borowki, odstaw do napecznienia.", [
                        Ing("Demo M2 Jogurt grecki 2%", 170m), Ing("Demo M2 Nasiona chia", 22m), Ing("Demo M2 Borowki", 50m), Ing("Demo M2 Miod", 8m)
                    ], "Wersja jogurtowa."),
                    Version(2, 260m, 260m, new(118m, 6m, 15m, 5m, 6m), "Wersja bez laktozy na jogurcie bez laktozy.", [
                        Ing("Demo M2 Jogurt bez laktozy", 180m), Ing("Demo M2 Nasiona chia", 22m), Ing("Demo M2 Borowki", 45m), Ing("Demo M2 Miod", 8m)
                    ], "Wersja bez laktozy.")
                ]),
            Component("rice", "Demo M2 Ryz basmati gotowany", "dry", 18, "Baza weglowodanowa.", "box250", [Version(1, 200m, 180m, new(130m, 2.7m, 28m, 0.3m, 0.4m), "Ugotuj ryz na sypko.", [Ing("Demo M2 Ryz basmati", 65m), Ing("Demo M2 Sol himalajska", 1m)], "Wersja bazowa.")]),
            Component("chicken", "Demo M2 Kurczak curry", "meat", 22, "Skladowa bialkowa z kurczaka.", "box500", [Version(1, 190m, 165m, new(160m, 25m, 2m, 5m, 0m), "Podsmaz kurczaka z pasta curry i dopiecz do temperatury kontrolnej.", [Ing("Demo M2 Piers z kurczaka", 170m), Ing("Demo M2 Pasta curry", 12m), Ing("Demo M2 Oliwa z oliwek", 6m), Ing("Demo M2 Sol himalajska", 1m)], "Wersja bazowa.", true, 75m)]),
            Component("curry-sauce", "Demo M2 Sos curry kokosowy", "processed", 18, "Sos do curry.", "sauce80", [Version(1, 130m, 120m, new(96m, 1.6m, 7m, 7m, 1m), "Zredukuj mleko kokosowe z curry i passata.", [Ing("Demo M2 Mleko kokosowe", 75m), Ing("Demo M2 Passata pomidorowa", 45m), Ing("Demo M2 Pasta curry", 8m), Ing("Demo M2 Czosnek", 3m)], "Wersja bazowa.")]),
            Component("broccoli", "Demo M2 Brokul parowany", "veg", 12, "Warzywo gotowane na parze.", "box250", [Version(1, 170m, 150m, new(35m, 3m, 6m, 0.4m, 3m), "Ugotuj brokul na parze, schlodz i dopraw.", [Ing("Demo M2 Brokul", 165m), Ing("Demo M2 Oliwa z oliwek", 3m), Ing("Demo M2 Sol himalajska", 1m)], "Wersja bazowa.")]),
            Component("salmon", "Demo M2 Losos pieczony", "meat", 24, "Ryba pieczona.", "box500", [Version(1, 160m, 140m, new(220m, 22m, 0m, 14m, 0m), "Piecz lososia do temperatury bezpiecznej.", [Ing("Demo M2 Losos filet", 150m), Ing("Demo M2 Oliwa z oliwek", 5m), Ing("Demo M2 Sol himalajska", 1m)], "Wersja bazowa.", true, 63m)]),
            Component("quinoa", "Demo M2 Komosa ryzowa gotowana", "dry", 20, "Baza z komosy.", "box250", [Version(1, 210m, 190m, new(120m, 4.4m, 21m, 1.9m, 2.8m), "Ugotuj komose do miekkosci.", [Ing("Demo M2 Komosa ryzowa", 70m), Ing("Demo M2 Sol himalajska", 1m)], "Wersja bazowa.")]),
            Component("tofu", "Demo M2 Tofu grillowane", "meat", 20, "Skladowa vege bialkowa.", "box500", [Version(1, 175m, 160m, new(160m, 17m, 3m, 9m, 1m), "Zamarynuj tofu w sosie sojowym i grilluj.", [Ing("Demo M2 Tofu naturalne", 155m), Ing("Demo M2 Sos sojowy", 10m), Ing("Demo M2 Oliwa z oliwek", 5m), Ing("Demo M2 Czosnek", 3m)], "Wersja bazowa.")]),
            Component("hummus", "Demo M2 Hummus warzywny", "processed", 8, "Dodatek sosowy do wrapow.", "sauce80", [Version(1, 90m, 90m, new(155m, 7m, 13m, 9m, 5m), "Wymieszaj hummus z czosnkiem i dopraw.", [Ing("Demo M2 Hummus", 80m), Ing("Demo M2 Czosnek", 2m), Ing("Demo M2 Oliwa z oliwek", 3m)], "Wersja bazowa.")]),
            Component("tortilla", "Demo M2 Tortilla warzywna", "processed", 12, "Baza wrapa.", "box250", [Version(1, 180m, 180m, new(190m, 6m, 27m, 6m, 4m), "Zloz tortille z warzywami.", [Ing("Demo M2 Tortilla pszenna", 70m), Ing("Demo M2 Salata rzymska", 35m), Ing("Demo M2 Papryka czerwona", 35m), Ing("Demo M2 Ogorek", 30m)], "Wersja bazowa.")]),
            Component("chili", "Demo M2 Chili wolowe", "lunch", 28, "Sos wolowy z fasola.", "box500", [Version(1, 270m, 245m, new(150m, 15m, 14m, 7m, 5m), "Dus wolowine z fasola, passata i chili.", [Ing("Demo M2 Mieso wolowe mielone", 130m), Ing("Demo M2 Fasola czerwona", 80m), Ing("Demo M2 Passata pomidorowa", 70m), Ing("Demo M2 Cebula", 25m), Ing("Demo M2 Przyprawa chili", 2m)], "Wersja bazowa.", true, 70m)]),
            Component("batat", "Demo M2 Bataty pieczone", "veg", 25, "Dodatek skrobiowy.", "box250", [Version(1, 210m, 180m, new(95m, 2m, 22m, 0.5m, 3.5m), "Upiecz bataty w kostce.", [Ing("Demo M2 Batat", 200m), Ing("Demo M2 Oliwa z oliwek", 6m), Ing("Demo M2 Sol himalajska", 1m)], "Wersja bazowa.")]),
            Component("lentil", "Demo M2 Sos pomidorowy z soczewica", "processed", 25, "Sos vege z bialkiem roslinnym.", "box500", [Version(1, 260m, 235m, new(125m, 8m, 20m, 2m, 7m), "Gotuj soczewice w passacie z warzywami.", [Ing("Demo M2 Soczewica czerwona", 75m), Ing("Demo M2 Passata pomidorowa", 95m), Ing("Demo M2 Marchew", 45m), Ing("Demo M2 Cebula", 25m), Ing("Demo M2 Przyprawa chili", 1m)], "Wersja bazowa.")]),
            Component("choco", "Demo M2 Fit krem czekoladowy", "snack", 10, "Deser czekoladowy.", "box250", [Version(1, 150m, 150m, new(210m, 7m, 23m, 10m, 7m), "Zblenduj banana, kakao, jogurt i czekolade.", [Ing("Demo M2 Banan", 80m), Ing("Demo M2 Kakao", 10m), Ing("Demo M2 Jogurt grecki 2%", 45m), Ing("Demo M2 Czekolada gorzka 70%", 12m)], "Wersja bazowa.")]),
            Component("granola", "Demo M2 Granola chrupiaca", "snack", 8, "Dodatek chrupiacy.", "box250", [Version(1, 35m, 35m, new(465m, 10m, 55m, 20m, 8m), "Odmierz granole do deseru.", [Ing("Demo M2 Granola orzechowa", 30m), Ing("Demo M2 Orzechy wloskie", 5m)], "Wersja bazowa.")]),
            Component("avocado", "Demo M2 Salatka awokado", "veg", 10, "Dodatek warzywny.", "box250", [Version(1, 130m, 130m, new(125m, 2m, 7m, 10m, 5m), "Pokroj warzywa i wymieszaj z oliwa.", [Ing("Demo M2 Awokado", 55m), Ing("Demo M2 Pomidor koktajlowy", 45m), Ing("Demo M2 Salata rzymska", 25m), Ing("Demo M2 Oliwa z oliwek", 4m)], "Wersja bazowa.")]),
            Component("pasta", "Demo M2 Makaron pelnoziarnisty gotowany", "dry", 18, "Baza makaronowa.", "box250", [Version(1, 220m, 200m, new(145m, 5m, 29m, 0.9m, 3m), "Ugotuj makaron al dente.", [Ing("Demo M2 Makaron pelnoziarnisty", 75m), Ing("Demo M2 Sol himalajska", 1m)], "Wersja bazowa.")]),
            Component("cod", "Demo M2 Dorsz z warzywami", "meat", 22, "Lekka skladowa rybna.", "box500", [Version(1, 200m, 175m, new(95m, 18m, 4m, 1m, 1m), "Dus dorsza z cukinia i papryka.", [Ing("Demo M2 Dorsz filet", 140m), Ing("Demo M2 Cukinia", 45m), Ing("Demo M2 Papryka czerwona", 35m), Ing("Demo M2 Oliwa z oliwek", 4m)], "Wersja bazowa.", true, 63m)]),
            Component("yogurt-salsa", "Demo M2 Salsa jogurtowa", "processed", 7, "Sos jogurtowy.", "sauce80", [Version(1, 80m, 80m, new(65m, 7m, 4m, 2m, 0.5m), "Wymieszaj jogurt z czosnkiem i ogorkiem.", [Ing("Demo M2 Jogurt grecki 2%", 60m), Ing("Demo M2 Ogorek", 20m), Ing("Demo M2 Czosnek", 2m)], "Wersja bazowa.")]),
            Component("oatmeal", "Demo M2 Owsianka bananowa", "breakfast", 12, "Sniadanie owsiane.", "box250", [Version(1, 260m, 250m, new(135m, 6m, 23m, 3m, 4m), "Ugotuj platki i dodaj banana.", [Ing("Demo M2 Platki owsiane", 55m), Ing("Demo M2 Banan", 80m), Ing("Demo M2 Jogurt bez laktozy", 80m), Ing("Demo M2 Miod", 5m)], "Wersja bazowa.")]),
        ];

    private static CanonicalComponentSeed Component(
        string key,
        string name,
        string categoryKey,
        int preparationTimeMinutes,
        string description,
        string packagingKey,
        IReadOnlyList<CanonicalComponentVersionSeed> versions)
        => new(key, name, description, categoryKey, preparationTimeMinutes, versions.Select(v => v with { PackagingKey = packagingKey }).ToArray());

    private static CanonicalComponentVersionSeed Version(
        int versionNumber,
        decimal rawWeightGrams,
        decimal cookedWeightGrams,
        CanonicalNutritionSeed nutrition,
        string instructions,
        IReadOnlyList<CanonicalComponentIngredientSeed> ingredients,
        string changeSummary,
        bool requiresTemperature = false,
        decimal? temperature = null)
        => new(
            versionNumber,
            instructions,
            1.0m,
            "portion",
            rawWeightGrams,
            cookedWeightGrams,
            nutrition,
            48,
            requiresTemperature,
            changeSummary,
            true,
            string.Empty,
            ingredients,
            [
                new("Przygotowanie", 1, [
                    new("Odmierz skladniki wedlug zapotrzebowania produkcyjnego.", 1, false, null, null, null, false),
                    new(instructions, 2, requiresTemperature, requiresTemperature ? "CoreTemperature" : null, temperature, requiresTemperature ? "C" : null, requiresTemperature),
                    new("Oznacz partie i przekaz skladowa do kompletacji dania.", 3, false, null, null, null, false),
                ]),
            ]);

    private static CanonicalComponentIngredientSeed Ing(
        string ingredientName,
        decimal weightInGrams,
        decimal yieldFactor = 1.0m,
        bool isOptional = false,
        string? notes = null)
        => new(ingredientName, weightInGrams, yieldFactor, isOptional, notes);

    private static IReadOnlyList<CanonicalMealSeed> GetCanonicalMealSeeds()
        =>
        [
            Meal("eggs-meal", "Demo M2 Jajecznica z salatka", "breakfast", "Sniadanie jajeczne z wariantem high protein.", "box500",
                Base("eggs:v1", "Main", 1, "portion", 1), Base("avocado:v1", "Side", 90, "g", 2),
                [
                    Variant("standard", "standard", "Standard", true, "box500", Base("eggs:v1", "Main", 1, "portion", 1), Base("avocado:v1", "Side", 90, "g", 2)),
                    Variant("high", "high protein", "HighProtein", false, "box500", Base("eggs:v2", "Main", 1, "portion", 1), Base("avocado:v1", "Side", 80, "g", 2)),
                ]),
            Meal("chia-meal", "Demo M2 Pudding chia z granola", "snack", "Pudding z wariantem bez laktozy.", "box250",
                Base("chia:v1", "Main", 1, "portion", 1), Base("granola:v1", "Crunch", 25, "g", 2),
                [
                    Variant("standard", "standard", "Standard", true, "box250", Base("chia:v1", "Main", 1, "portion", 1), Base("granola:v1", "Crunch", 25, "g", 2)),
                    Variant("lactose-free", "bez laktozy", "NoLactose", false, "box250", Base("chia:v2", "Main", 1, "portion", 1), Base("granola:v1", "Crunch", 20, "g", 2)),
                ]),
            Meal("curry-meal", "Demo M2 Kurczak curry z ryzem", "lunch", "Danie wieloskladowe testujace ryz w gramach i komponenty sosu.", "box750",
                Base("chicken:v1", "Protein", 1, "portion", 1), Base("curry-sauce:v1", "Sauce", 1, "portion", 2), Base("rice:v1", "Base", 1, "portion", 3), Base("broccoli:v1", "Vegetable", 1, "portion", 4),
                [
                    Variant("standard", "standard", "Standard", true, "box750", Base("chicken:v1", "Protein", 1, "portion", 1), Base("curry-sauce:v1", "Sauce", 1, "portion", 2), Base("rice:v1", "Base", 1, "portion", 3), Base("broccoli:v1", "Vegetable", 1, "portion", 4)),
                    Variant("high", "high protein", "HighProtein", false, "box750", Base("chicken:v1", "Protein", 1.30m, "portion", 1), Base("curry-sauce:v1", "Sauce", 1, "portion", 2), Base("rice:v1", "Base", 160, "g", 3), Base("broccoli:v1", "Vegetable", 1, "portion", 4)),
                    Variant("vege", "vege tofu", "Vege", false, "box750", Base("tofu:v1", "Protein", 1, "portion", 1), Base("curry-sauce:v1", "Sauce", 1, "portion", 2), Base("rice:v1", "Base", 1, "portion", 3), Base("broccoli:v1", "Vegetable", 1, "portion", 4)),
                ]),
            Meal("salmon-meal", "Demo M2 Losos z quinoa", "lunch", "Ryba z komosa i brokulem.", "box750",
                Base("salmon:v1", "Protein", 1, "portion", 1), Base("quinoa:v1", "Base", 1, "portion", 2), Base("broccoli:v1", "Vegetable", 1, "portion", 3), Base("yogurt-salsa:v1", "Sauce", 1, "portion", 4),
                [
                    Variant("standard", "standard", "Standard", true, "box750", Base("salmon:v1", "Protein", 1, "portion", 1), Base("quinoa:v1", "Base", 1, "portion", 2), Base("broccoli:v1", "Vegetable", 1, "portion", 3), Base("yogurt-salsa:v1", "Sauce", 1, "portion", 4)),
                    Variant("large", "wieksza gramatura", "Large", false, "box750", Base("salmon:v1", "Protein", 1.20m, "portion", 1), Base("quinoa:v1", "Base", 220, "g", 2), Base("broccoli:v1", "Vegetable", 1, "portion", 3), Base("yogurt-salsa:v1", "Sauce", 1, "portion", 4)),
                ]),
            Meal("tortilla-meal", "Demo M2 Tortilla vege z tofu", "dinner", "Wrap vege z hummusem.", "box500",
                Base("tortilla:v1", "Base", 1, "portion", 1), Base("tofu:v1", "Protein", 1, "portion", 2), Base("hummus:v1", "Sauce", 1, "portion", 3),
                [
                    Variant("standard", "standard vege", "Vege", true, "box500", Base("tortilla:v1", "Base", 1, "portion", 1), Base("tofu:v1", "Protein", 1, "portion", 2), Base("hummus:v1", "Sauce", 1, "portion", 3)),
                    Variant("high", "vege high protein", "HighProtein", false, "box500", Base("tortilla:v1", "Base", 1, "portion", 1), Base("tofu:v1", "Protein", 1.30m, "portion", 2), Base("hummus:v1", "Sauce", 1, "portion", 3)),
                ]),
            Meal("chili-meal", "Demo M2 Chili z batatami", "dinner", "Chili z wariantem wolowym i vege.", "box750",
                Base("chili:v1", "Main", 1, "portion", 1), Base("batat:v1", "Side", 1, "portion", 2),
                [
                    Variant("standard", "standard", "Standard", true, "box750", Base("chili:v1", "Main", 1, "portion", 1), Base("batat:v1", "Side", 1, "portion", 2)),
                    Variant("high", "high protein", "HighProtein", false, "box750", Base("chili:v1", "Main", 1.25m, "portion", 1), Base("batat:v1", "Side", 180, "g", 2)),
                    Variant("vege", "vege soczewica", "Vege", false, "box750", Base("lentil:v1", "Main", 1, "portion", 1), Base("batat:v1", "Side", 1, "portion", 2)),
                ]),
            Meal("dessert-meal", "Demo M2 Fit deser czekoladowy", "snack", "Deser z produktem przetworzonym i granola.", "box250",
                Base("choco:v1", "Main", 1, "portion", 1), Base("granola:v1", "Crunch", 20, "g", 2),
                [
                    Variant("standard", "standard", "Standard", true, "box250", Base("choco:v1", "Main", 1, "portion", 1), Base("granola:v1", "Crunch", 20, "g", 2)),
                    Variant("large", "wieksza porcja", "Large", false, "box500", Base("choco:v1", "Main", 1.20m, "portion", 1), Base("granola:v1", "Crunch", 30, "g", 2)),
                ]),
        ];

    private static CanonicalMealSeed Meal(
        string key,
        string name,
        string categoryKey,
        string description,
        string packagingKey,
        CanonicalMealComponentSeed firstComponent,
        CanonicalMealComponentSeed secondComponent,
        IReadOnlyList<CanonicalMealVariantSeed> variants)
        => new(
            key,
            name,
            categoryKey,
            description,
            $"Demo M2: {description}",
            30,
            "Danie agreguje skladowe technologiczne; szczegoly przygotowania sa na kartach skladowych.",
            48,
            false,
            packagingKey,
            [firstComponent, secondComponent],
            variants);

    private static CanonicalMealSeed Meal(
        string key,
        string name,
        string categoryKey,
        string description,
        string packagingKey,
        CanonicalMealComponentSeed firstComponent,
        CanonicalMealComponentSeed secondComponent,
        CanonicalMealComponentSeed thirdComponent,
        IReadOnlyList<CanonicalMealVariantSeed> variants)
        => new(
            key,
            name,
            categoryKey,
            description,
            $"Demo M2: {description}",
            30,
            "Danie agreguje skladowe technologiczne; szczegoly przygotowania sa na kartach skladowych.",
            48,
            false,
            packagingKey,
            [firstComponent, secondComponent, thirdComponent],
            variants);

    private static CanonicalMealSeed Meal(
        string key,
        string name,
        string categoryKey,
        string description,
        string packagingKey,
        CanonicalMealComponentSeed firstComponent,
        CanonicalMealComponentSeed secondComponent,
        CanonicalMealComponentSeed thirdComponent,
        CanonicalMealComponentSeed fourthComponent,
        IReadOnlyList<CanonicalMealVariantSeed> variants)
        => new(
            key,
            name,
            categoryKey,
            description,
            $"Demo M2: {description}",
            35,
            "Danie agreguje skladowe technologiczne; szczegoly przygotowania sa na kartach skladowych.",
            48,
            false,
            packagingKey,
            [firstComponent, secondComponent, thirdComponent, fourthComponent],
            variants);

    private static CanonicalMealVariantSeed Variant(
        string key,
        string nameSuffix,
        string variantType,
        bool isDefault,
        string packagingKey,
        params CanonicalMealComponentSeed[] components)
        => new(
            key,
            $"Demo M2 - {nameSuffix}",
            variantType,
            $"Wariant demo M2: {nameSuffix}.",
            isDefault,
            "Aggregated",
            null,
            packagingKey,
            components);

    private static CanonicalMealComponentSeed Base(
        string componentVersionKey,
        string role,
        decimal quantityPerServing,
        string unit,
        int sortOrder,
        bool isOptional = false)
        => new(componentVersionKey, role, quantityPerServing, unit, sortOrder, isOptional);

    private static IReadOnlyList<CanonicalDietSeed> GetCanonicalDietSeeds()
        =>
        [
            new("Standard", "Dieta standardowa oparta o pelny przekroj demo M2.", "Standardowy plan demo z 5 posilkami.", [
                new("standard-1800", "Standard 1800", 1800, 1.00m, true),
                new("standard-2200", "Standard 2200", 2200, 1.12m, false),
            ]),
            new("High Protein", "Dieta wysokobialkowa testujaca warianty dań.", "Plan demo dla klienta aktywnego.", [
                new("protein-2200", "High Protein 2200", 2200, 1.18m, true),
                new("protein-2600", "High Protein 2600", 2600, 1.30m, false),
            ]),
            new("Vege/Fit", "Dieta roslinna i lekka z wariantami bez laktozy.", "Plan demo vege/fit.", [
                new("vege-1600", "Vege/Fit 1600", 1600, 1.05m, true),
                new("vege-1900", "Vege/Fit 1900", 1900, 1.13m, false),
            ]),
        ];

    private static IReadOnlyList<CanonicalDietVariantMealPlanSeed> GetCanonicalDietVariantMealPlans()
        =>
        [
            new("standard-1800", [
                Slot("Breakfast", 1, "eggs-meal:standard"),
                Slot("Snack1", 2, "chia-meal:standard"),
                Slot("Lunch", 3, "curry-meal:standard"),
                Slot("Snack2", 4, "dessert-meal:standard"),
                Slot("Dinner", 5, "salmon-meal:standard"),
            ]),
            new("standard-2200", [
                Slot("Breakfast", 1, "eggs-meal:standard"),
                Slot("Snack1", 2, "chia-meal:standard", 1.10m),
                Slot("Lunch", 3, "curry-meal:high"),
                Slot("Snack2", 4, "dessert-meal:large"),
                Slot("Dinner", 5, "chili-meal:standard"),
            ]),
            new("protein-2200", [
                Slot("Breakfast", 1, "eggs-meal:high"),
                Slot("Snack1", 2, "chia-meal:standard"),
                Slot("Lunch", 3, "curry-meal:high"),
                Slot("Snack2", 4, "dessert-meal:standard"),
                Slot("Dinner", 5, "chili-meal:high"),
            ]),
            new("protein-2600", [
                Slot("Breakfast", 1, "eggs-meal:high", 1.10m),
                Slot("Snack1", 2, "chia-meal:standard", 1.15m),
                Slot("Lunch", 3, "curry-meal:high", 1.10m),
                Slot("Snack2", 4, "dessert-meal:large"),
                Slot("Dinner", 5, "salmon-meal:large"),
            ]),
            new("vege-1600", [
                Slot("Breakfast", 1, "chia-meal:lactose-free"),
                Slot("Snack1", 2, "dessert-meal:standard"),
                Slot("Lunch", 3, "curry-meal:vege"),
                Slot("Snack2", 4, "tortilla-meal:standard"),
                Slot("Dinner", 5, "chili-meal:vege"),
            ]),
            new("vege-1900", [
                Slot("Breakfast", 1, "chia-meal:lactose-free"),
                Slot("Snack1", 2, "dessert-meal:standard"),
                Slot("Lunch", 3, "curry-meal:vege", 1.10m),
                Slot("Snack2", 4, "tortilla-meal:high"),
                Slot("Dinner", 5, "chili-meal:vege"),
            ]),
        ];

    private static CanonicalDietPlanSlotSeed Slot(
        string mealSlot,
        int sortOrder,
        string mealVariantKey,
        decimal servingSizeMultiplier = 1.0m)
        => new(mealSlot, sortOrder, mealVariantKey, servingSizeMultiplier);

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

    private sealed class CanonicalVariantNutritionRow
    {
        public decimal? RawWeightGrams { get; set; }

        public decimal? CookedWeightGrams { get; set; }

        public decimal? Calories { get; set; }

        public decimal? Protein { get; set; }

        public decimal? Carbohydrates { get; set; }

        public decimal? Fat { get; set; }

        public decimal? Fiber { get; set; }
    }

    private sealed record CanonicalNutritionSeed(
        decimal Calories,
        decimal Protein,
        decimal Carbohydrates,
        decimal Fat,
        decimal Fiber);

    private sealed record CanonicalIngredientSeed(
        string Name,
        string Unit,
        decimal CostPerUnit,
        string CategoryKey,
        int WarehouseCategoryId,
        CanonicalNutritionSeed Nutrition,
        string Description,
        string? ProductComposition,
        IReadOnlyCollection<string> AllergenCodes,
        decimal YieldFactor,
        bool RequiresCoreTemperatureCheck,
        decimal? MinimumCoreTemperatureCelsius,
        decimal MinimumStockLevel,
        decimal TargetBatchQuantity,
        int LeadTimeDays,
        string? Notes = null);

    private sealed record CanonicalComponentSeed(
        string Key,
        string Name,
        string Description,
        string CategoryKey,
        int PreparationTimeMinutes,
        IReadOnlyList<CanonicalComponentVersionSeed> Versions);

    private sealed record CanonicalComponentVersionSeed(
        int VersionNumber,
        string Instructions,
        decimal YieldQuantity,
        string YieldUnit,
        decimal RawWeightGrams,
        decimal CookedWeightGrams,
        CanonicalNutritionSeed Nutrition,
        int ShelfLifeHours,
        bool UseEarliestIngredientExpiry,
        string ChangeSummary,
        bool IsTechnologyChange,
        string PackagingKey,
        IReadOnlyList<CanonicalComponentIngredientSeed> Ingredients,
        IReadOnlyList<CanonicalInstructionSectionSeed> Sections);

    private sealed record CanonicalComponentIngredientSeed(
        string IngredientName,
        decimal WeightInGrams,
        decimal YieldFactor,
        bool IsOptional,
        string? Notes);

    private sealed record CanonicalInstructionSectionSeed(
        string Title,
        int SortOrder,
        IReadOnlyList<CanonicalInstructionStepSeed> Steps);

    private sealed record CanonicalInstructionStepSeed(
        string StepText,
        int SortOrder,
        bool RequiresControl,
        string? ControlType,
        decimal? ExpectedValue,
        string? ExpectedUnit,
        bool IsCritical);

    private sealed record CanonicalMealSeed(
        string Key,
        string Name,
        string CategoryKey,
        string Description,
        string MarketingDescription,
        int PreparationTimeMinutes,
        string PreparationInstructions,
        int ShelfLifeHours,
        bool UseEarliestIngredientExpiry,
        string PackagingKey,
        IReadOnlyList<CanonicalMealComponentSeed> BaseComponents,
        IReadOnlyList<CanonicalMealVariantSeed> Variants);

    private sealed record CanonicalMealVariantSeed(
        string Key,
        string Name,
        string VariantType,
        string Description,
        bool IsDefault,
        string NutritionSource,
        string? NutritionOverrideReason,
        string? PackagingKey,
        IReadOnlyList<CanonicalMealComponentSeed> Components);

    private sealed record CanonicalMealComponentSeed(
        string ComponentVersionKey,
        string Role,
        decimal QuantityPerServing,
        string Unit,
        int SortOrder,
        bool IsOptional);

    private sealed record CanonicalDietSeed(
        string Name,
        string Description,
        string MarketingDescription,
        IReadOnlyList<CanonicalDietVariantSeed> Variants);

    private sealed record CanonicalDietVariantSeed(
        string Key,
        string Name,
        int TargetCalories,
        decimal PriceMultiplier,
        bool IsDefault);

    private sealed record CanonicalDietVariantMealPlanSeed(
        string DietVariantKey,
        IReadOnlyList<CanonicalDietPlanSlotSeed> Slots);

    private sealed record CanonicalDietPlanSlotSeed(
        string MealSlot,
        int SortOrder,
        string MealVariantKey,
        decimal ServingSizeMultiplier);

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

    private sealed record UnifiedDemoCustomerSeed(
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

    private sealed class PublishedMenuPlanBackfillRow
    {
        public int Id { get; set; }

        public DateTime PlanDate { get; set; }
    }

    private sealed class PublishedMenuPlanItemSnapshotBackfillRow
    {
        public int PlanId { get; set; }

        public int DietMenuPlanItemId { get; set; }

        public string SnapshotJson { get; set; } = string.Empty;

        public string SnapshotHash { get; set; } = string.Empty;
    }

    private static Task<int> CountRowsAsync(IDbConnection db, string tableName, CancellationToken cancellationToken)
        => db.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM [{tableName}];",
            cancellationToken: cancellationToken));
}
