# Podrecznik Dewelopera - Kuchnia u Cygana

Dokument opisuje praktyczne zasady pracy po migracji na SQL Server.

## 1. Kontekst techniczny

- Runtime bazy: **MS SQL Server** uruchamiany w Dockerze.
- ORM: **ServiceStack.OrmLite**.
- Schemat bazy: **FluentMigrator**.
- Strategia danych: **greenfield-only** (nie migrujemy danych ze SQLite).

## 2. Szybki start dla dewelopera

1. Skopiuj `.env.example` do `.env` i uzupelnij `MSSQL_SA_PASSWORD`, `MSSQL_DB_NAME`.
2. Uruchom SQL + aplikacje:

```bash
docker compose up --build
```

3. Sprawdz logi:

- `Container kuchnia_sqlserver Healthy`
- `Starting up database 'KuchniaUCygana'`

4. Opcjonalnie uruchom seeding recznie:

```bash
dotnet run --project src/KuchniaUCygana.Web -- seed
```

## 3. Jak tworzyc encje (klasy domenowe)

### Zasady bazowe

- Dla danych biznesowych dziedzicz po `AuditableEntity<TId>`.
- Dla prostych slownikow dziedzicz po `BaseEntity<TId>`.
- Uzywaj `DateTimeOffset` dla pol czasowych.
- Dla mapowania tabel stosuj `[Alias("NazwaTabeli")]`, gdy nazwa klasy i tabeli nie pokrywa sie 1:1.
- Nie opieraj sie na relacjach nawigacyjnych EF Core; w OrmLite trzymaj jawne klucze i zapytania.

### Typowy szablon encji

1. Klasa w `Domain/Entities/...`.
2. Klucz i pola audytowe zapewnia klasa bazowa.
3. Relacje przez identyfikatory i atrybuty OrmLite (`[References]`).

## 4. Jak tworzyc migracje (FluentMigrator)

### Reguly

- Nie edytuj starych, wykonanych migracji produkcyjnych.
- Kazda zmiana schematu to nowy plik migracji z kolejnym numerem.
- DDL jest tylko w migracjach (nie w kodzie startupowym repozytoriow).

### Proces krok po kroku

1. Dodaj nowa klase migracji w `src/KuchniaUCygana.Infrastructure/Persistence/Migrations`.
2. W `Up()` opisz zmiane (tabela/kolumna/indeks/fk).
3. W `Down()` dodaj bezpieczny rollback, jesli jest wykonalny.
4. Uruchom aplikacje i sprawdz, czy migracja przechodzi na czystej bazie.
5. Zweryfikuj typy SQL (szczegolnie `decimal` i `datetimeoffset`).

## 5. Jak uruchomic cala baze od zera

Pelny reset (usuniecie danych i odbudowa):

```bash
docker compose down -v --remove-orphans
docker compose up --build
```

Efekt:

- tworzony jest nowy, pusty wolumen SQL,
- migracje odtwarzaja schemat,
- seeding uruchamia sie zgodnie z konfiguracja srodowiska.

## 6. Jak pisac repozytoria magazynu

Dotyczy m.in. `BatchRepository` i `InventoryTransactionRepository`.

### Wymagania implementacyjne

- Filtry soft-delete musza byc spojne (`IsDeleted`, `IsDepleted` tam, gdzie ma sens).
- Logika FEFO:
  - daty waznosci rosnaco,
  - `ExpiryDate == null` zawsze na koncu.
- Porownania czasu prowadzone w `DateTimeOffset`, bez niepotrzebnego przejscia na `DateTime`.

## 7. Testy integracyjne (obowiazkowe dla warstwy danych)

Repozytoria magazynu musza byc weryfikowane na **Testcontainers + SQL Server**.

Minimalny zestaw scenariuszy:

1. FEFO sort + `ExpiryDate == null` na koncu.
2. `GetExpiringBeforeAsync` z granica (wartosc rowna cutoff ma byc uwzgledniona).
3. `GetByDateRangeAsync` z granicami `from/to` wlacznie.
4. Spojnosc typow SQL po migracjach (`datetimeoffset`, `decimal`).

Uruchamianie:

```bash
dotnet test KuchniaUCygana.sln --filter "Category=Integration"
```

## 8. Seeder danych testowych (`WarehouseDataSeeder`)

Wymagania:

- dane deterministyczne (staly `seed`),
- realistyczne zakresy dat i temperatur,
- dane wspierajace scenariusze FEFO i testy graniczne,
- brak przypadkowych flakow zaleznych od aktualnej daty systemowej.

## 9. Lista kontrolna przed PR

1. `dotnet build` przechodzi.
2. `dotnet test --filter "Category!=Integration"` przechodzi.
3. `dotnet test --filter "Category=Integration"` przechodzi.
4. Dokumentacja jest aktualna i po polsku.
5. Brak nowych krytycznych warningow w plikach dotknietych zmianami.
