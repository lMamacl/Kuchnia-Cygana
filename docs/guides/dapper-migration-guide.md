# Migracja warstwy danych na Dapper

## Cel

Projekt odchodzi od starego ORM-a ServiceStack i utrzymuje legalny, jawny dostep do SQL Server przez Dapper. Nie wolno obchodzic mechanizmow licencji bibliotek ani dodawac refleksyjnych obejsc w `Program.cs`, testach lub konfiguracji.

## Zasady

- Nie dodajemy pakietow ServiceStack do `Domain`, `Infrastructure`, `Web` ani testow.
- Encje domenowe uzywaja standardowych atrybutow .NET: `System.ComponentModel.DataAnnotations` oraz `System.ComponentModel.DataAnnotations.Schema`.
- Migracje DDL sa wylacznie we FluentMigrator.
- Repozytoria pisza jawny SQL przez Dapper. Nie ma lazy loadingu, relacje ladujemy przez JOIN albo osobne zapytanie.
- SQL musi miec jawne filtry `IsDeleted = 0` dla encji soft-delete.
- Daty przechowujemy i porownujemy konsekwentnie w UTC; dla `DateOnly` i `TimeOnly` korzystamy z handlerow Dappera z `DapperTypeHandlers`.

## Wzor repozytorium

```csharp
public sealed class ExampleRepository : BaseRepository<Example>, IExampleRepository
{
    public ExampleRepository(IDbConnectionFactory factory)
        : base(factory)
    {
    }

    public async Task<IEnumerable<Example>> GetActiveAsync()
    {
        using var db = Factory.CreateConnection();

        return await db.QueryAsync<Example>(
            """
            SELECT *
            FROM [Examples]
            WHERE [IsDeleted] = 0
            ORDER BY [Id];
            """);
    }
}
```

## Testy

- Test DI ma potwierdzac rejestracje `IDbConnectionFactory` jako `SqlServerConnectionFactory`.
- Testy integracyjne repozytoriow uzywaja SQL Server przez Testcontainers i Dapper.
- Test guard `ForbiddenPersistenceUsageTests` blokuje powrot usunietych zaleznosci i obejsc licencji.

## Kontrola przed PR

```bash
dotnet build KuchniaUCygana.sln --no-restore
dotnet test tests/KuchniaUCygana.Tests/KuchniaUCygana.Tests.csproj --no-restore
rg "ServiceStack[.](OrmLite|DataAnnotations)|License[U]tils|__activated[L]icense" src tests docs README.md
```

## Branch integration rules

- `develop` przyjmuje tylko kompatybilna infrastrukture, dokumentacje i testy bazowe.
- `Mamac` utrzymuje implementacje Modulu 3: Warehouse, Production, Packing, widoki, seeding demo i testy M3.
- Branche deweloperskie po aktualizacji `develop` robia rebase albo merge, a potem migruja wlasne repozytoria wedlug wzorca Dapper.
- Nie robimy pelnego merge'a branchy, ktore usuwaja cudze kontrolery, widoki albo konfiguracje Web. W takich przypadkach wybieramy cherry-pick konkretnych plikow/modulow.
