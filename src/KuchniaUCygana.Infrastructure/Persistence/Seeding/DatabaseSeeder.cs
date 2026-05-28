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
        await SeedMenuAsync(db, now, auditUser, cancellationToken);
        await SeedProductionPlansAsync(db, now, auditUser, cancellationToken);
        await SeedPackingSessionsAsync(db, now, auditUser, cancellationToken);
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
                (1, 1, 'Jajecznica z szczypiorkiem na maśle', 'Klasyczna jajecznica z 3 jaj na prawdziwym maśle ze świeżym szczypiorkiem i pieczywem.', 'Active', 10, 1, @now, @auditUser),
                (2, 2, 'Pudding chia z jagodami i śmietanką', 'Kremowy deser chia na bazie jogurtu i śmietanki ze słodkim musem z mrożonych jagód.', 'Active', 15, 1, @now, @auditUser),
                (3, 3, 'Pikantna zupa pomidorowa z makaronem', 'Rozgrzewająca, aromatyczna zupa ze słodkich pomidorów krojonych z makaronem penne i nutą śmietanki.', 'Active', 25, 1, @now, @auditUser),
                (4, 3, 'Pieczony filet z łososia z ryżem i brokułami', 'Delikatny łosoś pieczony w ziołach, podawany z sypkim ryżem jaśminowym i gotowanymi brokułami.', 'Active', 35, 1, @now, @auditUser),
                (5, 5, 'Bowl z wołowiną, dynią i batatami', 'Pożywna kolacja z pieczonym mięsem wołowym, batatami i słodką dynią piżmową z przyprawami.', 'Active', 30, 1, @now, @auditUser);
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
                INSERT INTO [PackingSessions] ([PackingDate], [OrderId], [ClientName], [PackedBy], [Status], [RouteId], [StopNumber], [CreatedBy], [CreatedAt])
                VALUES (@packingDate, @orderId, @clientName, @packedBy, @status, @routeId, @stopNumber, @auditUser, @now);
                SELECT CAST(SCOPE_IDENTITY() as int);
                """,
                new
                {
                    packingDate = today.ToDateTime(TimeOnly.MinValue),
                    orderId,
                    clientName,
                    packedBy = sessionStatus == PackingStatus.Pending ? null : "DemoPacker",
                    status = (int)sessionStatus,
                    routeId = 1,
                    stopNumber = i + 1,
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

    private static Task<int> CountRowsAsync(IDbConnection db, string tableName, CancellationToken cancellationToken)
        => db.ExecuteScalarAsync<int>(new CommandDefinition(
            $"SELECT COUNT(1) FROM [{tableName}];",
            cancellationToken: cancellationToken));
}
