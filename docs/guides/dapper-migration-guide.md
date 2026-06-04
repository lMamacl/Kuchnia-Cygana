# Migracja Dostępu Do Danych Na Dapper

Ten projekt używa teraz Dappera jako wspólnej biblioteki dostępu do danych.
Migracje schematu zostają w FluentMigrator, a repozytoria piszą jawny,
parametryzowany SQL.

## Zasady

- Nie dodawaj pakietów dawnego ServiceStack ORM ani jego adnotacji mapujących.
- Encje domenowe mogą używać standardowych atrybutów .NET, np. `Table`, `Key`
  i `NotMapped`.
- Zapytania niestandardowe zapisuj jako SQL w repozytoriach infrastruktury.
- Nie buduj tłumacza `Expression<Func<T, bool>>`; jeśli moduł potrzebuje filtra,
  dodaj dedykowaną metodę repozytorium.
- Dane biznesowe dziedziczące po `ISoftDeletable` są ukrywane przez
  `BaseRepository.GetAllAsync` i usuwane logicznie przez `DeleteAsync`.

## Wzorzec Repozytorium

```csharp
public sealed class ExampleRepository : BaseRepository<Example>, IExampleRepository
{
    public ExampleRepository(IDbConnectionFactory factory) : base(factory)
    {
    }

    public async Task<Example?> GetByCodeAsync(string code)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<Example>(
            "SELECT * FROM [Examples] WHERE [Code] = @code AND [IsDeleted] = 0;",
            new { code });
    }
}
```

## Weryfikacja

Przed PR uruchom:

```bash
dotnet build KuchniaUCygana.sln --no-restore
dotnet test tests/KuchniaUCygana.Tests/KuchniaUCygana.Tests.csproj --no-restore
rg "ServiceStack\\.(OrmLite|DataAnnotations)|License[U]tils|__activated[L]icense"
```

Ostatnia komenda nie powinna zwrócić wyników.

## Integracja Branchy

- `develop`: tylko wspólna infrastruktura, dokumentacja i testy bazowe.
- `Mamac`: implementacje Modułu 3 zostają na branchu modułowym do czasu pełnej
  migracji Warehouse/Production/Packing.
- Pozostałe branche: po aktualizacji `develop` migrują swoje repozytoria do
  Dappera i nie przenoszą masowych usunięć cudzych plików Web.
