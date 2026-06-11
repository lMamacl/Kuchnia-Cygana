# Przewodnik po Architekturze Bazy Danych i Scenariusz Prezentacji (SBD)
**Projekt:** System Zarządzania Platformą Cateringową „Kuchnia u Cygana”  
**Technologia:** .NET 8 (Clean Architecture) + MS SQL Server (Docker) + Dapper (Micro-ORM)

---

## 1. Konfiguracja Kont i Połączeń Bazodanowych (Separacja Połączeń DDL i DML)
W projekcie zastosowano rozdział odpowiedzialności na poziomie kont bazodanowych i połączeń (Connection Strings), oddzielając operacje modyfikacji schematu (DDL) od codziennych operacji na danych (DML).

### Konkretne pliki w projekcie:
*   **Definicja loginów i uprawnień bazodanowych:** [sqlserver-init.sql](../docker/sqlserver-init.sql)
*   **Konfiguracja połączeń (Connection Strings):** [appsettings.json](../src/KuchniaUCygana.Web/appsettings.json#L2-L5)
*   **Rejestracja w DI:** [DependencyInjection.cs](../src/KuchniaUCygana.Infrastructure/DependencyInjection.cs#L128-L130)

### Jak to działa w praktyce:
1.  **Konto `admin` (Połączenie DDL / Migracje):**
    *   Używa connection stringa `MigrationConnection`.
    *   Posiada uprawnienia właściciela bazy danych (`db_owner`).
    *   Używane wyłącznie w procesie wdrażania aplikacji przez narzędzie FluentMigrator do wykonywania skryptów strukturalnych (tworzenie tabel, modyfikacja kolumn, dodawanie indeksów, procedur, triggerów).
2.  **Konto `pracownik` (Połączenie DML / Aplikacja):**
    *   Używa connection stringa `DefaultConnection`.
    *   Posiada ograniczone uprawnienia na poziomie roli `db_datareader` oraz `db_datawriter`.
    *   **NIE** posiada roli `db_owner`. Nie ma możliwości wykonywania żadnych poleceń DDL (np. `DROP TABLE`, `ALTER TABLE`, `TRUNCATE TABLE`).
    *   Zapewnia to izolację – nawet w przypadku awarii aplikacji bądź błędu w zapytaniach użytkownik/system nie jest w stanie usunąć struktury bazy danych.

### Jak udowodnić prowadzącemu:
1.  **Pokazać plik inicjalizujący SQL Server:** [sqlserver-init.sql](../docker/sqlserver-init.sql)
    *   Tworzone są loginy SQL Server: `admin` oraz `pracownik`.
    *   `admin` jest dodawany do roli `db_owner`.
    *   `pracownik` jest dodawany tylko do `db_datareader` i `db_datawriter`.
    *   Stare konto `klient`, jeśli istnieje po poprzednich wersjach bazy, jest usuwane.

2.  **Pokazać konfigurację połączeń aplikacji:** [appsettings.json](../src/KuchniaUCygana.Web/appsettings.json) albo zmienne środowiskowe w [docker-compose.yml](../docker-compose.yml)
    *   `DefaultConnection` używa loginu `pracownik`.
    *   `MigrationConnection` używa loginu `admin`.

3.  **Pokazać użycie połączeń w kodzie:** [DependencyInjection.cs](../src/KuchniaUCygana.Infrastructure/DependencyInjection.cs)
    *   `IDbConnectionFactory` dla zwykłych repozytoriów jest tworzony z `DefaultConnection`, czyli działa jako `pracownik`.
    *   FluentMigrator używa `MigrationConnection`, czyli działa jako `admin`.
    *   Seeder demonstracyjny również korzysta z połączenia migracyjnego, bo wykonuje zadania inicjalizacyjne/utrzymaniowe, a nie zwykłe operacje użytkownika aplikacji.

4.  **Wykonać zapytanie SQL pokazujące role użytkowników:**
    ```sql
    SELECT
        dp.name AS UserName,
        rp.name AS RoleName
    FROM sys.database_role_members drm
    JOIN sys.database_principals rp ON rp.principal_id = drm.role_principal_id
    JOIN sys.database_principals dp ON dp.principal_id = drm.member_principal_id
    WHERE dp.name IN (N'admin', N'pracownik')
    ORDER BY dp.name, rp.name;
    ```

    Oczekiwany wynik:
    *   `admin` -> `db_owner`
    *   `pracownik` -> `db_datareader`
    *   `pracownik` -> `db_datawriter`

5.  **Pokazać, że aplikacja faktycznie loguje się jako `pracownik`:**
    ```sql
    SELECT
        SUSER_SNAME() AS LoginName,
        USER_NAME() AS DatabaseUser;
    ```

    Po wykonaniu tego zapytania na połączeniu aplikacyjnym/`DefaultConnection` powinno być widać `pracownik`. Po wykonaniu na połączeniu migracyjnym powinno być widać `admin`.

6.  **Opcjonalny test ograniczeń konta `pracownik`:**
    Po połączeniu się do bazy jako `pracownik` można wykonać:
    ```sql
    CREATE TABLE dbo.TestPracownikDdlPermission (Id int NOT NULL);
    ```

    Oczekiwany wynik: błąd uprawnień. To pokazuje, że konto aplikacyjne może czytać i zapisywać dane, ale nie powinno zmieniać schematu bazy.

### Połączenie z serwerem - jak pokazać:
Ten punkt nie wymaga osobnej, rozbudowanej implementacji. Wystarczy pokazać, że aplikacja i narzędzia administracyjne łączą się z rzeczywistym serwerem MS SQL Server uruchomionym w Dockerze.

1.  **Pokazać konfigurację serwera w Dockerze:** [docker-compose.yml](../docker-compose.yml)
    *   Usługa `sqlserver` uruchamia obraz `mcr.microsoft.com/mssql/server:2022-latest`.
    *   Port SQL Servera jest wystawiony jako `${MSSQL_PORT:-1433}:1433`.
    *   `healthcheck` wykonuje proste `SELECT 1`, czyli kontener jest uznawany za gotowy dopiero wtedy, gdy SQL Server faktycznie odpowiada.
    *   Usługa `web` czeka na zdrowy `sqlserver` i zakończony `sqlserver-init`, więc aplikacja startuje dopiero po przygotowaniu bazy i kont.

2.  **Wykonać krótkie zapytanie potwierdzające połączenie:**
    ```sql
    SELECT
        @@SERVERNAME AS ServerName,
        DB_NAME() AS DatabaseName,
        SUSER_SNAME() AS LoginName,
        USER_NAME() AS DatabaseUser,
        SYSDATETIME() AS ServerTime;
    ```

    Tym jednym zapytaniem pokazujemy nazwę serwera, aktualną bazę, login SQL, użytkownika bazodanowego oraz czas po stronie serwera.

3.  **Pokazać, że połączenie widzi obiekty projektu:**
    ```sql
    SELECT COUNT(*) AS ProjectTables
    FROM sys.tables
    WHERE is_ms_shipped = 0;
    ```

    Jeżeli wynik pokazuje tabele projektu, to znaczy, że nie łączymy się z pustym serwerem, tylko z właściwą bazą aplikacji.

4.  **Powiedzieć jednym zdaniem:**
    „Serwer bazy danych działa jako osobny kontener MS SQL Server. Aplikacja łączy się z nim przez connection stringi, migracje uruchamiają się przez konto `admin`, a codzienna praca aplikacji odbywa się przez konto `pracownik`.”

---

## 2. Analiza Biznesowa - co powiedzieć i co pokazać
Ten punkt jest opisany głównie w [Sprawozdanie Bazy Danych.md](Sprawozdanie%20Bazy%20Danych.md), szczególnie w sekcjach:
*   **1. Wstęp i Podział Merytoryczny Zespołu**
*   **2. Szczegółowe Wymagania Klienta i Zakres Merytoryczny Modułów**
*   **3. Wymagania Funkcjonalne i Niefunkcjonalne**

Nie trzeba tu długo klikać po aplikacji. Najważniejsze jest pokazanie, że baza danych nie powstała jako losowy zestaw tabel, tylko wynika z analizy procesu biznesowego cateringu dietetycznego.

### Krótka narracja:
„Analiza biznesowa zaczyna się od procesu cateringowego: klient kupuje dietę i wybiera dni dostaw, katalog diet definiuje posiłki i receptury, produkcja oraz magazyn przygotowują i kompletują zamówienia, logistyka grupuje dostawy w trasy, a moduł administracyjny obsługuje pracowników, grafiki i reklamacje. Z tego procesu wynikają główne obszary danych: klienci, zamówienia, menu, składniki, partie magazynowe, kompletacja, trasy, kierowcy, pojazdy, pracownicy i zgłoszenia.”

### Co pokazać prowadzącemu:
1.  **Sprawozdanie, sekcje 1-3**
    *   pokazują podział systemu na 5 modułów,
    *   opisują zakres biznesowy każdego modułu,
    *   pokazują wymagania funkcjonalne i niefunkcjonalne.

2.  **Przepływ biznesowy end-to-end**
    *   `M1 E-commerce`: klient, adres, zamówienie, kalendarz dostaw,
    *   `M2 Katalog`: dieta, wariant, posiłek, receptura, wartości odżywcze,
    *   `M3 Magazyn/Produkcja`: partie, FEFO, kompletacja, manifest,
    *   `M4 Logistyka`: trasy, przystanki, pojazdy, kierowcy, torby termiczne,
    *   `M5 Admin/HR`: pracownicy, grafiki, reklamacje, logi audytowe.

3.  **Powiązanie analizy biznesowej z bazą**
    *   encje domenowe mają swoje odpowiedniki w tabelach,
    *   wymagania niefunkcjonalne przełożyły się na audyt, indeksy, transakcje i archiwizację,
    *   integracje między modułami są widoczne w relacjach logicznych, np. `DeliveryCalendar` -> `DeliveryRouteStop` -> manifest załadunkowy.

### Jedno zdanie do obrony:
„Analiza biznesowa została wykorzystana jako podstawa modelu danych: najpierw opisaliśmy realny proces cateringu dietetycznego i odpowiedzialności modułów, a potem przełożyliśmy go na encje, relacje, indeksy i procedury bazodanowe.”

---

## 3. Projekt BD, encje i ERD - co pokazać
Ten punkt jest opisany w [Sprawozdanie Bazy Danych.md](Sprawozdanie%20Bazy%20Danych.md), głównie w sekcjach:
*   **8. Relacyjny Model Logiczny i Diagram Fizyczny (ERD)**
*   **9. Logiczny Model Danych i Wymagania dotyczące przechowywania**
*   **10. Wymagania dotyczące przetwarzania danych**

Najlepiej pokazać go jako połączenie trzech poziomów: analiza -> encje domenowe -> fizyczne tabele z migracji.

### Co pokazać prowadzącemu:
1.  **ERD w sprawozdaniu**
    *   Otworzyć rozdział 8 i pokazać diagram Mermaid `erDiagram`.
    *   Powiedzieć, że diagram pokazuje główne encje i relacje między modułami, bez przeciążania go wszystkimi kolumnami technicznymi.

2.  **Grupy danych w modelu logicznym**
    *   Otworzyć rozdział 9.1 w sprawozdaniu.
    *   Pokazać tabelę grup danych: konta, zamówienia, katalog diet, magazyn, HACCP, produkcja, kompletacja, logistyka, HR/admin.
    *   To jest dobry moment, żeby pokazać, że projekt BD wynika z domeny, a nie z przypadkowych ekranów aplikacji.

3.  **Encje w kodzie**
    *   Pokazać katalog [Entities](../src/KuchniaUCygana.Domain/Entities).
    *   Przykłady:
        *   `Orders`: `Order`, `OrderItem`, `DeliveryCalendar`, `Address`, `Payment`,
        *   `Menu`: `Diet`, `DietVariant`, `Meal`, `MealVariant`, `RecipeComponentVersion`,
        *   `Warehouse`: `StockItem`, `Batch`, `InventoryTransaction`,
        *   `Packing`: `PackingSession`, `PackingItem`, `PackingManifest`,
        *   `Logistics`: `Vehicle`, `Driver`, `DeliveryRoute`, `DeliveryRouteStop`, `ThermalBag`,
        *   `Admin/HR`: `SystemLog`, `Ticket`, `Employee`, `WorkSchedule`.

4.  **Fizyczny projekt bazy w migracjach**
    *   Pokazać katalog [Migrations](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations).
    *   Migracje tworzą tabele, kolumny, klucze obce, indeksy, procedury, funkcje i triggery.
    *   Przykład dla logistyki: [400_CreateLogisticsTables.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/400_CreateLogisticsTables.cs).

### Zapytania SQL do udowodnienia:
Liczba tabel projektu:
```sql
SELECT COUNT(*) AS ProjectTables
FROM sys.tables
WHERE is_ms_shipped = 0;
```

Lista tabel użytkowych:
```sql
SELECT
    s.name AS SchemaName,
    t.name AS TableName
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name;
```

Lista kluczy obcych:
```sql
SELECT
    fk.name AS ForeignKeyName,
    OBJECT_SCHEMA_NAME(fk.parent_object_id) AS ChildSchema,
    OBJECT_NAME(fk.parent_object_id) AS ChildTable,
    OBJECT_SCHEMA_NAME(fk.referenced_object_id) AS ParentSchema,
    OBJECT_NAME(fk.referenced_object_id) AS ParentTable
FROM sys.foreign_keys fk
ORDER BY ChildTable, ForeignKeyName;
```

### Ważne wyjaśnienie:
Nie każda relacja z ERD musi być fizycznym kluczem obcym w SQL Server. W projekcie część relacji między modułami jest celowo logiczna, np. przez identyfikatory integracyjne, żeby moduły nie były zbyt mocno sprzężone. Przykład: logistyka łączy przystanek z zamówieniem przez `DeliveryCalendarId`, ale nie każda integracja między modułami musi wymuszać twardy FK.

### Jedno zdanie do obrony:
„Projekt bazy danych został przygotowany na trzech poziomach: mamy ERD w dokumentacji, encje domenowe w kodzie oraz fizyczny schemat tworzony przez migracje FluentMigrator w SQL Server.”

---

## 4. Implementacja fizyczna BD - co pokazać
Ten punkt dotyczy tego, jak projekt logiczny został faktycznie wdrożony w MS SQL Server. Najważniejszy przekaz: fizyczny schemat bazy nie jest tworzony ręcznie w SSMS, tylko przez wersjonowane migracje aplikacji.

### Gdzie to jest w projekcie:
*   **Migracje:** [Migrations](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations)
*   **Uruchamianie migracji:** [MigrationRunner.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/MigrationRunner.cs)
*   **Rejestracja FluentMigratora:** [DependencyInjection.cs](../src/KuchniaUCygana.Infrastructure/DependencyInjection.cs)
*   **SQL Server w Dockerze:** [docker-compose.yml](../docker-compose.yml)
*   **Inicjalizacja bazy i kont:** [sqlserver-init.sql](../docker/sqlserver-init.sql)

### Co fizycznie tworzymy:
1.  **Tabele**
    *   np. `Users`, `Orders`, `DeliveryCalendar`, `StockItems`, `Batches`, `PackingSessions`, `DeliveryRoutes`, `Vehicles`, `Drivers`, `SystemLogs`.

2.  **Kolumny z typami SQL Server**
    *   np. `decimal(18,4)` dla ilości magazynowych,
    *   `datetimeoffset` dla dat audytowych i zdarzeń,
    *   `bigint` dla tabel o dużym przyroście, np. logów.

3.  **Klucze główne i obce**
    *   `Id` jako identity w większości tabel,
    *   fizyczne FK tam, gdzie relacja jest wewnątrz domeny,
    *   relacje logiczne między modułami tam, gdzie nie chcemy zbyt mocnego sprzężenia.

4.  **Indeksy i ograniczenia**
    *   indeksy wyszukiwawcze i pokrywające,
    *   indeksy unikalne dla naturalnie unikalnych danych,
    *   indeksy pod konkretne zapytania raportowe i operacyjne.

5.  **Obiekty programowalne**
    *   trigger `tr_Batches_UpdateIsDepleted`,
    *   procedura `usp_ArchiveSystemLogs`,
    *   funkcja `fn_MealNutritionCost`,
    *   pakiet logistyczny jako schemat `logistics_pkg`.

### Jak pokazać prowadzącemu:
1.  **Otworzyć folder migracji**
    Pokazać, że projekt ma wiele migracji z numerami, np. `001`, `100`, `300`, `400`, `500`, `519`. To pokazuje ewolucję fizycznego schematu w czasie.

2.  **Pokazać przykładową migrację tabel**
    Dla M4 najlepiej otworzyć [400_CreateLogisticsTables.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/400_CreateLogisticsTables.cs), bo tworzy fizyczne tabele logistyki: `Vehicles`, `Drivers`, `DeliveryRoutes`, `DeliveryRouteStops`, `ThermalBags`, `BagMovementLogs`.

3.  **Pokazać migrację techniczną SQL Server**
    [003_SqlServerSchemaHardening.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/003_SqlServerSchemaHardening.cs) pokazuje dopasowanie typów do SQL Servera, np. precyzję `decimal` i `datetimeoffset`.

4.  **Pokazać, że migracje są wykonywane automatycznie**
    [Program.cs](../src/KuchniaUCygana.Web/Program.cs) wywołuje `MigrationRunner.RunMigrations(...)` przy starcie aplikacji, a [DependencyInjection.cs](../src/KuchniaUCygana.Infrastructure/DependencyInjection.cs) rejestruje FluentMigratora z `MigrationConnection`.

### SQL do udowodnienia fizycznej implementacji:
Historia wykonanych migracji:
```sql
SELECT *
FROM dbo.VersionInfo
ORDER BY Version DESC;
```

Liczba i lista tabel:
```sql
SELECT COUNT(*) AS UserTables
FROM sys.tables
WHERE is_ms_shipped = 0;

SELECT
    s.name AS SchemaName,
    t.name AS TableName
FROM sys.tables t
JOIN sys.schemas s ON s.schema_id = t.schema_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name;
```

Kolumny wybranych tabel logistycznych:
```sql
SELECT
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS SqlType,
    c.max_length,
    c.precision,
    c.scale,
    c.is_nullable
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE t.name IN (N'Vehicles', N'Drivers', N'DeliveryRoutes', N'DeliveryRouteStops')
ORDER BY t.name, c.column_id;
```

Indeksy:
```sql
SELECT
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc,
    i.is_unique
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
WHERE t.is_ms_shipped = 0
  AND i.name IS NOT NULL
ORDER BY t.name, i.name;
```

Klucze obce:
```sql
SELECT
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS ChildTable,
    OBJECT_NAME(fk.referenced_object_id) AS ParentTable
FROM sys.foreign_keys fk
ORDER BY ChildTable, ForeignKeyName;
```

### Jedno zdanie do obrony:
„Implementacja fizyczna bazy danych jest wykonywana przez wersjonowane migracje FluentMigrator, które tworzą w SQL Serverze realne tabele, kolumny, typy, klucze, indeksy oraz obiekty programowalne, a tabela `VersionInfo` pozwala pokazać, które migracje zostały faktycznie wykonane.”

---

## 5. Skrypt tworzący bazę - jak to obronić
W projekcie nie utrzymujemy jednego ogromnego pliku `.sql`, który ręcznie tworzy całą bazę od zera. Zamiast tego mamy bezpieczniejszy i bardziej produkcyjny podział:

1.  **Skrypt bootstrapujący SQL Server:** [sqlserver-init.sql](../docker/sqlserver-init.sql)
    *   tworzy bazę danych, jeśli jeszcze nie istnieje,
    *   tworzy/aktualizuje loginy `admin` i `pracownik`,
    *   zakłada użytkowników bazodanowych,
    *   nadaje role `db_owner`, `db_datareader`, `db_datawriter`,
    *   usuwa stare, niepotrzebne konto `klient`.

2.  **Wersjonowany skrypt struktury bazy:** migracje FluentMigrator w katalogu [Migrations](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations)
    *   tworzą tabele,
    *   tworzą kolumny i typy,
    *   dodają klucze główne i obce,
    *   dodają indeksy,
    *   tworzą procedury, funkcje, triggery i schemat `logistics_pkg`.

### Dlaczego tak jest lepiej niż jeden ręczny plik SQL:
*   Migracje są wersjonowane i wykonywane tylko raz.
*   Każda zmiana schematu ma numer, np. `001`, `100`, `400`, `519`.
*   SQL Server przechowuje historię wykonania w tabeli `VersionInfo`.
*   Ten sam mechanizm działa przy starcie aplikacji, więc baza może zostać odtworzona automatycznie po `docker compose down -v`.
*   Unikamy sytuacji, w której ręczny skrypt tworzący bazę rozjeżdża się z kodem aplikacji.

### Co pokazać prowadzącemu:
1.  **docker-compose**
    W [docker-compose.yml](../docker-compose.yml) usługa `sqlserver-init` wykonuje:
    ```bash
    sqlcmd ... -i /init/sqlserver-init.sql
    ```
    To jest moment utworzenia bazy i kont.

2.  **Skrypt SQL tworzący bazę**
    W [sqlserver-init.sql](../docker/sqlserver-init.sql) pokazać fragment:
    ```sql
    IF DB_ID(@dbName) IS NULL
    BEGIN
        DECLARE @createDatabaseSql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@dbName) + N';';
        EXEC(@createDatabaseSql);
    END;
    ```

3.  **Migracje tworzące schemat**
    Pokazać [400_CreateLogisticsTables.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/400_CreateLogisticsTables.cs) jako przykład migracji tworzącej fizyczne tabele.

4.  **Automatyczne wykonanie migracji**
    W [Program.cs](../src/KuchniaUCygana.Web/Program.cs) pokazać `MigrationRunner.RunMigrations(...)`.

### SQL do pokazania:
Aktualna baza:
```sql
SELECT
    name,
    database_id,
    create_date
FROM sys.databases
WHERE name = DB_NAME();
```

Historia migracji:
```sql
SELECT *
FROM dbo.VersionInfo
ORDER BY Version DESC;
```

Przykład potwierdzenia, że migracje utworzyły tabele:
```sql
SELECT
    t.name AS TableName,
    t.create_date,
    t.modify_date
FROM sys.tables t
WHERE t.is_ms_shipped = 0
ORDER BY t.name;
```

### Jedno zdanie do obrony:
„Skrypt tworzący bazę jest rozdzielony na część inicjalizacyjną i część strukturalną: `sqlserver-init.sql` tworzy bazę oraz konta SQL Server, a wersjonowane migracje FluentMigrator tworzą cały schemat tabel, indeksów i obiektów programowalnych.”

---

## 6. Użycie ORM: Dapper jako micro-ORM
**Kluczowa obrona projektu:** w projekcie używamy Dappera, czyli micro-ORM-a. To oznacza, że SQL pozostaje jawny i kontrolowany przez programistę, ale mapowanie wyników, parametrów i typów na obiekty C# wykonuje biblioteka ORM, a nie ręczny kod ADO.NET.

### 6.1. Odpowiedź na zarzut: „to jest czysty SQL”
Najbezpieczniej odpowiedzieć tak:

„Tak, zapytania SQL są u nas jawne, bo Dapper jest micro-ORM-em. Natomiast nie jest to ręczne ADO.NET ani składanie obiektów z `SqlDataReader`. Dapper mapuje wynik zapytania na klasy C# (`QueryAsync<T>`), mapuje parametry z obiektów C# (`new { id }`, `DynamicParameters`), obsługuje wiele zestawów wyników (`QueryMultipleAsync`) i pozwala budować repozytoria pracujące na encjach domenowych. SQL jest jawny, ale warstwa mapowania obiektowo-relacyjnego jest realizowana przez Dappera.”

### 6.2. Co dokładnie spełnia rolę ORM:
1.  **Biblioteka ORM w projekcie**
    *   [KuchniaUCygana.Infrastructure.csproj](../src/KuchniaUCygana.Infrastructure/KuchniaUCygana.Infrastructure.csproj) zawiera `PackageReference Include="Dapper"`.
    *   Repozytoria w warstwie Infrastructure używają `QueryAsync<T>`, `QuerySingleOrDefaultAsync<T>`, `ExecuteAsync`, `ExecuteScalarAsync<T>` i `QueryMultipleAsync`.

2.  **Mapowanie rekordów na obiekty**
    *   Dapper tworzy obiekty C# na podstawie kolumn zwróconych przez SQL.
    *   Przykład: [SystemLogRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs) mapuje wynik CTE i `LEFT JOIN Users` na `SystemLogRow`, w tym pole `UserFullName`.

3.  **Parametryzacja**
    *   W repozytoriach parametry przekazywane są jako obiekty C#, np. `new { id }`, `new { RouteDate = routeDate }`, `DynamicParameters`.
    *   To nie jest konkatenowanie wartości użytkownika do SQL-a, tylko parametryzowane zapytania wykonywane przez provider SQL Server.

4.  **Generyczne repozytorium**
    *   [BaseRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/BaseRepository.cs) działa na `TEntity`.
    *   Na podstawie refleksji metadanych encji generuje podstawowy `INSERT`, `UPDATE`, `SELECT`, `DELETE`.
    *   Odczyt i zapis pracują na obiektach domenowych, np. `User`, `Vehicle`, `UnitOfMeasure`, `SystemLog`, a nie na tablicach kolumn.

5.  **Własne handlery typów**
    *   [DapperTypeHandlers.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Dapper/DapperTypeHandlers.cs) rejestruje obsługę typów `DateTimeOffset`, `DateOnly`, `TimeOnly`.
    *   To jest kolejny element mapowania między światem SQL Server i typami C#.

### 6.3. Dowody w kodzie:
1.  **BaseRepository - generyczny CRUD**
    [BaseRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/BaseRepository.cs) generuje zapytania na podstawie metadanych encji i przekazuje cały obiekt jako parametry Dappera:
    ```csharp
    var insertedId = await db.ExecuteScalarAsync<TId>(
        $"""
        INSERT INTO {Metadata.TableName} ({Metadata.InsertColumns})
        OUTPUT INSERTED.{Metadata.KeyColumn}
        VALUES ({Metadata.InsertParameters});
        """,
        entity);
    ```

2.  **SystemLogRepository - mapowanie złożonego wyniku na DTO**
    [SystemLogRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs) używa CTE, złączenia z `Users`, paginacji i mapowania na `SystemLogRow`:
    ```csharp
    var items = (await db.QueryAsync<SystemLogRow>(
        sql,
        dataParameters)).ToArray();
    ```

3.  **BatchRepository - wiele zestawów wyników**
    [BatchRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/Warehouse/BatchRepository.cs) używa `QueryMultipleAsync`:
    ```csharp
    using var multi = await db.QueryMultipleAsync(sql, parameters);
    var totalCount = await multi.ReadSingleAsync<int>();
    var items = (await multi.ReadAsync<FefoReportRow>()).ToList();
    ```

4.  **LogisticsSbdReportRepository - procedura SQL mapowana na obiekty**
    [LogisticsSbdReportRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/LogisticsSbdReportRepository.cs) wywołuje procedurę `logistics_pkg.usp_GetDailyDispatchBoard`, a Dapper mapuje dwa zestawy wyników na `LogisticsDailyDispatchRouteRow` i `LogisticsDailyDispatchSummaryRow`.

### 6.4. Testy potwierdzające:
W [DapperOrmProofTests.cs](../tests/KuchniaUCygana.Tests/Integration/Infrastructure/DapperOrmProofTests.cs) są testy integracyjne uruchamiane na prawdziwym SQL Serverze:
*   `BaseRepository_ShouldPerformCrudDynamicallyUsingProductionImplementation` - pełny CRUD przez generyczne repozytorium.
*   `SystemLogRepository_ShouldPerformComplexCteAndJoinMappingOnProductionCode` - mapowanie wyniku `JOIN`/CTE na DTO.
*   `BatchRepository_ShouldMapMultipleResultSetsUsingQueryMultipleAsync` - mapowanie wielu zbiorów wynikowych.

### 6.5. Jedno zdanie do obrony:
„Dapper nie ukrywa SQL-a, bo jest micro-ORM-em, ale nadal jest ORM-em: automatycznie mapuje relacyjne wyniki zapytań na obiekty C#, obsługuje parametry i typy, a u nas jest dodatkowo opakowany w generyczne repozytoria pracujące na encjach domenowych.”

---

## 7. Endpointy aplikacyjne
Ten punkt nie wymaga długiej obrony. Najważniejsze jest pokazanie, że baza danych nie jest tylko zbiorem tabel, ale jest obsługiwana przez warstwę aplikacyjną wystawiającą konkretne adresy HTTP.

### 7.1. Co dokładnie mamy:
1.  **Endpointy MVC/Razor**
    *   Projekt używa klasycznych kontrolerów ASP.NET Core MVC, czyli endpointami są akcje kontrolerów oznaczone atrybutami `[Route]`, `[HttpGet]`, `[HttpPost]`.
    *   Nie wszystkie endpointy muszą zwracać JSON. W tym projekcie większość zwraca widoki Razor, przekierowania albo pliki/partial views, bo aplikacja jest systemem webowym z panelem pracownika.

2.  **Rejestracja endpointów**
    *   [Program.cs](../src/KuchniaUCygana.Web/Program.cs) rejestruje MVC przez `AddControllersWithViews`.
    *   Ten sam plik mapuje routing:
        ```csharp
        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Home}/{action=Index}/{id?}");
        ```

3.  **Zabezpieczenie endpointów**
    *   `Program.cs` włącza `UseAuthentication()` i `UseAuthorization()`.
    *   Kontrolery mają atrybuty `[Authorize(...)]`, np.:
        *   `LogisticsController` -> role `Logistics`, `LogisticsManager`, `Admin`,
        *   `DriverMobileController` -> rola `Driver`,
        *   `LoadingController` -> role `Packing`, `PackingManager`, `Admin`.
    *   Dla formularzy MVC globalnie dodano `AutoValidateAntiforgeryTokenAttribute`, więc operacje `POST` są chronione tokenem anty-CSRF.

### 7.2. Najlepsze endpointy do pokazania dla M4:
1.  **Logistyka / dyspozytor**
    *   `GET /logistics` - dashboard logistyki.
    *   `GET /logistics/routes` - lista tras na dzień.
    *   `POST /logistics/routes/generate` - wygenerowanie tras z zaplanowanych dostaw.
    *   `GET /logistics/routes/{id}` - szczegóły trasy.
    *   `GET /logistics/routes/{id}/map` albo `GET /logistics/routes/map` - mapa tras.
    *   `GET /logistics/vehicles`, `POST /logistics/vehicles/create`, `POST /logistics/vehicles/edit/{id}` - zarządzanie pojazdami.
    *   `GET /logistics/drivers`, `POST /logistics/drivers/create`, `POST /logistics/drivers/{id}/vehicle` - zarządzanie kierowcami i przypisaniem kierowcy do auta.

2.  **Widok kierowcy**
    *   `GET /driver` - aktualna trasa kierowcy.
    *   `POST /driver/start` - rozpoczęcie trasy.
    *   `GET /driver/stop/{id}` - szczegóły przystanku.
    *   `POST /driver/stop/{id}/confirm` - potwierdzenie dostawy.
    *   `POST /driver/stop/{id}/problem` - zgłoszenie problemu z dostawą.
    *   `GET /driver/summary` - podsumowanie trasy.

3.  **Integracja z magazynem / manifest**
    *   `GET /loading/{routeId}/manifest` - manifest załadunkowy dla trasy.
    *   `POST /loading/{routeId}/manifest/generate` - wygenerowanie manifestu.
    *   `POST /loading/{routeId}/manifest/worker-approve` - zatwierdzenie przez magazyniera.
    *   `POST /loading/{routeId}/manifest/supervisor-approve` - zatwierdzenie przez koordynatora.
    *   `POST /loading/{routeId}/dispatch` - wydanie trasy do dostawy.

### 7.3. Jak to pokazać prowadzącemu:
1.  Otworzyć [Program.cs](../src/KuchniaUCygana.Web/Program.cs) i wskazać `AddControllersWithViews`, `UseAuthentication`, `UseAuthorization` oraz `MapControllerRoute`.
2.  Otworzyć [LogisticsController.cs](../src/KuchniaUCygana.Web/Controllers/LogisticsController.cs) i pokazać `[Route("logistics")]`, `[Authorize(...)]` oraz akcje `[HttpGet]`/`[HttpPost]`.
3.  W przeglądarce przejść na `/logistics/routes`, wygenerować trasę albo otworzyć szczegóły istniejącej trasy.
4.  Otworzyć [DriverMobileController.cs](../src/KuchniaUCygana.Web/Controllers/DriverMobileController.cs) i pokazać, że kierowca ma oddzielne endpointy do rozpoczęcia trasy, obsługi przystanku, potwierdzenia dostawy i zgłoszenia problemu.

### 7.4. Jedno zdanie do obrony:
„Endpointy są wystawione przez kontrolery ASP.NET Core MVC. Obsługują zarówno widoki panelu pracownika, jak i operacje POST zmieniające stan danych, są powiązane z serwisami aplikacyjnymi oraz zabezpieczone autoryzacją ról i tokenami anty-CSRF.”

---

## 8. Role systemowe
Ten punkt dotyczy ról aplikacyjnych użytkowników, a nie ról bazodanowych SQL Server typu `db_owner`, `db_datareader`, `db_datawriter`. Role bazodanowe były pokazane przy kontach połączeniowych, natomiast role systemowe sterują dostępem użytkowników do modułów aplikacji.

### 8.1. Gdzie są zdefiniowane role:
1.  **Centralna lista ról pracowniczych**
    *   [AppRoles.cs](../src/KuchniaUCygana.Domain/Constants/AppRoles.cs) definiuje role pracownicze: `Kitchen`, `KitchenManager`, `Warehouse`, `WarehouseManager`, `Packing`, `PackingManager`, `Dietitian`, `Logistics`, `LogisticsManager`, `Driver`, `DriverManager`, `Admin`, `HR`, `HRManager`, `BOK`, `BOKManager`.
    *   `AppRoles.StaffRoleNames` grupuje role pracownicze i jest używane np. w filtrze zmian pracowniczych.

2.  **Starsza lista kompatybilna z resztą kodu**
    *   [UserRoles.cs](../src/KuchniaUCygana.Domain/Enums/UserRoles.cs) zawiera te same role oraz rolę `Client`.
    *   Ta klasa nadal jest szeroko używana w kontrolerach, seederze i widokach. To jest obecny stan projektu, więc na prezentacji najlepiej powiedzieć, że `AppRoles` jest nowszym centralnym katalogiem ról pracowniczych, a `UserRoles` pozostaje używany dla kompatybilności istniejącego kodu.

3.  **Przechowywanie roli użytkownika**
    *   Encja [User.cs](../src/KuchniaUCygana.Domain/Entities/Auth/User.cs) ma pole `Role`.
    *   W bazie danych rola jest zapisana w tabeli `Users`, w kolumnie `[Role]`.

### 8.2. Jak role są używane:
1.  **Logowanie i claims**
    *   [AccountController.cs](../src/KuchniaUCygana.Web/Controllers/AccountController.cs) po zalogowaniu tworzy cookie uwierzytelniające i dodaje role jako `ClaimTypes.Role`.
    *   Metoda `ExpandRoles` rozszerza uprawnienia:
        *   `Admin` dostaje wszystkie role pracownicze,
        *   rola managerska, np. `LogisticsManager`, dostaje także bazową rolę `Logistics`.

2.  **Autoryzacja endpointów**
    *   Kontrolery używają `[Authorize(Roles = "...")]`.
    *   Przykłady:
        *   [LogisticsController.cs](../src/KuchniaUCygana.Web/Controllers/LogisticsController.cs) -> `Logistics`, `LogisticsManager`, `Admin`,
        *   [DriverMobileController.cs](../src/KuchniaUCygana.Web/Controllers/DriverMobileController.cs) -> `Driver`,
        *   [LoadingController.cs](../src/KuchniaUCygana.Web/Controllers/LoadingController.cs) -> `Packing`, `PackingManager`, `Admin`,
        *   [AdminController.cs](../src/KuchniaUCygana.Web/Controllers/AdminController.cs) -> tylko `Admin`.

3.  **Nawigacja według ról**
    *   [StaffNavigationCatalog.cs](../src/KuchniaUCygana.Web/Models/StaffNavigationCatalog.cs) pokazuje sekcje panelu pracownika zależnie od roli użytkownika.
    *   Dzięki temu użytkownik widzi tylko moduły, które mają sens dla jego stanowiska.

4.  **Reguły operacyjne**
    *   [StaffShiftGuardFilter.cs](../src/KuchniaUCygana.Web/Filters/StaffShiftGuardFilter.cs) używa `AppRoles.StaffRoleNames`.
    *   Zwykły pracownik może wykonywać operacje modyfikujące dane tylko podczas aktywnej zmiany, natomiast `Admin` i role managerskie są z tego ograniczenia wyłączone.

### 8.3. Jak to pokazać prowadzącemu:
1.  Otworzyć [AppRoles.cs](../src/KuchniaUCygana.Domain/Constants/AppRoles.cs) i pokazać katalog ról pracowniczych.
2.  Otworzyć [User.cs](../src/KuchniaUCygana.Domain/Entities/Auth/User.cs) i wskazać pole `Role`.
3.  Otworzyć [AccountController.cs](../src/KuchniaUCygana.Web/Controllers/AccountController.cs) i pokazać dodawanie `ClaimTypes.Role` oraz `ExpandRoles`.
4.  Otworzyć dowolny kontroler, np. [LogisticsController.cs](../src/KuchniaUCygana.Web/Controllers/LogisticsController.cs), i pokazać `[Authorize(Roles = "Logistics,LogisticsManager,Admin")]`.
5.  Opcjonalnie otworzyć w aplikacji panel admina `/admin/roles`, który prezentuje role i ich użycie od strony UI.

### 8.4. SQL do pokazania:
Lista ról faktycznie występujących w bazie:
```sql
SELECT
    COALESCE(NULLIF([Role], ''), N'Brak roli') AS RoleName,
    COUNT(*) AS UserCount
FROM dbo.Users
WHERE IsDeleted = 0
GROUP BY COALESCE(NULLIF([Role], ''), N'Brak roli')
ORDER BY RoleName;
```

Przykładowi użytkownicy z rolami:
```sql
SELECT TOP (30)
    Id,
    Email,
    FirstName,
    LastName,
    [Role],
    IsDeleted
FROM dbo.Users
ORDER BY [Role], Email;
```

### 8.5. Jedno zdanie do obrony:
„Role systemowe są zapisane przy użytkownikach w tabeli `Users`, po zalogowaniu trafiają do claims cookie, a potem są używane przez `[Authorize]`, nawigację panelu pracownika i dodatkowe filtry bezpieczeństwa, np. kontrolę pracy poza aktywną zmianą.”

---

## 9. Uwierzytelnianie
Uwierzytelnianie odpowiada na pytanie „kim jest użytkownik?”. Dopiero po nim działa autoryzacja, czyli sprawdzanie ról i dostępu do konkretnych modułów. W projekcie używamy standardowego mechanizmu ASP.NET Core Cookie Authentication.

### 9.1. Gdzie jest konfiguracja:
1.  **Konfiguracja cookie authentication**
    *   [Program.cs](../src/KuchniaUCygana.Web/Program.cs) rejestruje:
        *   `AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)`,
        *   `AddCookie(...)`,
        *   `UseAuthentication()`,
        *   `UseAuthorization()`.

2.  **Parametry bezpieczeństwa cookie**
    *   Nazwa cookie: `KuchniaUCygana.Auth`.
    *   `HttpOnly = true`, więc JavaScript po stronie przeglądarki nie powinien czytać cookie sesyjnego.
    *   `SameSite = Lax`, co ogranicza typowe scenariusze CSRF.
    *   `SecurePolicy = Always` poza środowiskiem developerskim, więc w produkcji cookie wymaga HTTPS.
    *   `ExpireTimeSpan = 30 minut` i `SlidingExpiration = true`, więc sesja wygasa po bezczynności, ale aktywny użytkownik nie jest wylogowywany w trakcie pracy.

3.  **Data Protection**
    *   `AddDataProtection().SetApplicationName("KuchniaUCygana").PersistKeysToFileSystem(new DirectoryInfo("./Keys"))`.
    *   Dzięki temu klucze do ochrony cookie są jawnie konfigurowane dla aplikacji i mogą przetrwać restart kontenera/aplikacji.

4.  **Ścieżki logowania i odmowy dostępu**
    *   `LoginPath = "/Account/Login"`.
    *   `AccessDeniedPath = "/Account/AccessDenied"`.
    *   Dla endpointów `/api` i żądań AJAX aplikacja zwraca `401` albo `403`, zamiast robić HTML redirect do formularza logowania.

### 9.2. Jak działa logowanie:
1.  Użytkownik wpisuje email i hasło w widoku [Login.cshtml](../src/KuchniaUCygana.Web/Views/Account/Login.cshtml).
2.  [AccountController.cs](../src/KuchniaUCygana.Web/Controllers/AccountController.cs) odbiera formularz w akcji `Login`.
3.  Email jest normalizowany przez `Trim()`, a użytkownik pobierany przez [UserRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/UserRepository.cs), metodą `FindByEmailAsync`.
4.  Hasło nie jest porównywane jako tekst jawny. Metoda `VerifyPassword` używa `BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)`.
5.  Po poprawnym haśle `SignInAsync` tworzy claims:
    *   `NameIdentifier`,
    *   `Name`,
    *   `Email`,
    *   `ClaimTypes.Role`.
6.  Następnie `HttpContext.SignInAsync(...)` zapisuje zaszyfrowane cookie uwierzytelniające.

### 9.3. Jak działa rejestracja i hasła:
1.  Encja [User.cs](../src/KuchniaUCygana.Domain/Entities/Auth/User.cs) przechowuje `PasswordHash`, a nie hasło jawne.
2.  Przy rejestracji klienta `AccountController.Register` zapisuje:
    ```csharp
    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password)
    ```
3.  Seeder demonstracyjny także używa `BCrypt.Net.BCrypt.HashPassword(...)`, więc nawet konta testowe mają hasła zapisane jako hash.

### 9.4. Wylogowanie i ochrona formularzy:
1.  Wylogowanie odbywa się przez `HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)`.
2.  Globalnie w MVC dodano `AutoValidateAntiforgeryTokenAttribute`, więc formularze `POST` są chronione tokenem anty-CSRF.
3.  Login i rejestracja używają modele z walidacją DataAnnotations, np. `[Required]`, `[EmailAddress]`, `[DataType(DataType.Password)]`.

### 9.5. Jak to pokazać prowadzącemu:
1.  Otworzyć [Program.cs](../src/KuchniaUCygana.Web/Program.cs) i pokazać `AddAuthentication`, `AddCookie`, parametry cookie oraz `UseAuthentication`.
2.  Otworzyć [AccountController.cs](../src/KuchniaUCygana.Web/Controllers/AccountController.cs) i pokazać:
    *   akcję `Login`,
    *   `VerifyPassword`,
    *   `SignInAsync`,
    *   `Logout`.
3.  Otworzyć [User.cs](../src/KuchniaUCygana.Domain/Entities/Auth/User.cs) i pokazać `PasswordHash`.
4.  W przeglądarce wylogować się i spróbować wejść na `/logistics/routes`. Aplikacja powinna przekierować do `/Account/Login`.
5.  Zalogować się na konto pracownika i pokazać, że po zalogowaniu użytkownik trafia do właściwego modułu.
6.  Opcjonalnie spróbować wejść na `/admin` kontem bez roli `Admin`, żeby pokazać różnicę między brakiem uwierzytelnienia a brakiem autoryzacji.

### 9.6. SQL do pokazania:
Pokazujemy, że w bazie nie ma haseł jawnych, tylko hashe:
```sql
SELECT TOP (20)
    Email,
    [Role],
    LEFT(PasswordHash, 4) AS HashPrefix,
    LEN(PasswordHash) AS HashLength,
    CASE
        WHEN PasswordHash LIKE '$2%' THEN N'BCrypt'
        ELSE N'Inny format'
    END AS HashType
FROM dbo.Users
WHERE IsDeleted = 0
ORDER BY Email;
```

### 9.7. Czego nie obiecywać:
Nie warto mówić, że mamy pełny system klasy enterprise IAM. Obecnie mamy poprawne uwierzytelnianie formularzowe z cookie, hashowaniem BCrypt, claims i ochroną formularzy. Nie ma osobnego MFA, resetu hasła przez email ani blokady konta po wielu nieudanych próbach.

### 9.8. Jedno zdanie do obrony:
„Uwierzytelnianie jest oparte o ASP.NET Core Cookie Authentication: użytkownik loguje się emailem i hasłem, hasło jest weryfikowane przez BCrypt względem `PasswordHash`, a po poprawnym logowaniu aplikacja wystawia bezpieczne cookie z claims użytkownika i rolami.”

---

## 10. Bezpieczeństwo danych
Bezpieczeństwo danych w projekcie jest realizowane warstwowo. Nie opiera się na jednym mechanizmie, tylko na połączeniu uprawnień bazodanowych, uwierzytelniania, autoryzacji, parametryzacji SQL, ochrony formularzy i audytu zmian.

### 10.1. Separacja kont bazodanowych
Pierwsza warstwa ochrony jest na poziomie SQL Server:
*   `admin` służy do migracji i ma `db_owner`.
*   `pracownik` jest kontem aplikacyjnym i ma tylko `db_datareader` oraz `db_datawriter`.
*   `pracownik` nie powinien wykonywać DDL, czyli np. `DROP TABLE`, `ALTER TABLE`, `TRUNCATE TABLE`.
*   Stare konto `klient` jest usuwane w [sqlserver-init.sql](../docker/sqlserver-init.sql), bo klient platformy cateringowej nie powinien mieć bezpośredniego konta w bazie danych.

To realizuje zasadę najmniejszych uprawnień: aplikacja do codziennej pracy nie działa na koncie właściciela bazy.

### 10.2. Ochrona haseł użytkowników aplikacji
Hasła użytkowników nie są przechowywane jawnie:
*   [User.cs](../src/KuchniaUCygana.Domain/Entities/Auth/User.cs) przechowuje `PasswordHash`.
*   [AccountController.cs](../src/KuchniaUCygana.Web/Controllers/AccountController.cs) przy rejestracji używa `BCrypt.Net.BCrypt.HashPassword(...)`.
*   Logowanie używa `BCrypt.Net.BCrypt.Verify(...)`, czyli porównuje hasło z hashem.
*   Seeder demonstracyjny również generuje hashe BCrypt dla kont testowych.

### 10.3. Ochrona przed SQL Injection
W projekcie używany jest Dapper, ale zapytania są wykonywane parametryzowanie:
*   parametry przekazywane są przez obiekty C#, np. `new { id }`, `new { Email = email }`, `DynamicParameters`,
*   wartości użytkownika nie powinny być doklejane bezpośrednio do tekstu SQL,
*   [BaseRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/BaseRepository.cs) generuje nazwy kolumn z metadanych encji, a wartości przekazuje jako parametry,
*   identyfikatory tabel i kolumn są cytowane nawiasami `[]`, co ogranicza ryzyko błędów przy generowaniu SQL.

Najprostszy argument do obrony: jawny SQL w Dapperze nie oznacza konkatenacji danych użytkownika do zapytania. Dane wejściowe idą jako parametry.

### 10.4. Autoryzacja i ograniczenie dostępu do modułów
Dane są chronione również na poziomie aplikacji:
*   kontrolery używają `[Authorize(Roles = "...")]`,
*   role użytkowników są zapisywane w tabeli `Users`, a po logowaniu trafiają do `ClaimTypes.Role`,
*   `Admin` ma dostęp szeroki, role managerskie mają dostęp do obszaru i roli bazowej, a zwykli pracownicy widzą tylko swoje moduły,
*   [StaffShiftGuardFilter.cs](../src/KuchniaUCygana.Web/Filters/StaffShiftGuardFilter.cs) dodatkowo blokuje operacje modyfikujące dane poza aktywną zmianą zwykłego pracownika.

### 10.5. Bezpieczne cookie i ochrona formularzy
W [Program.cs](../src/KuchniaUCygana.Web/Program.cs) skonfigurowano:
*   cookie `KuchniaUCygana.Auth`,
*   `HttpOnly = true`,
*   `SameSite = Lax`,
*   `SecurePolicy = Always` poza developmentem,
*   wygaśnięcie sesji po 30 minutach bezczynności,
*   `DataProtection` dla ochrony cookie,
*   globalny `AutoValidateAntiforgeryTokenAttribute` dla formularzy MVC.

To zabezpiecza typowe operacje formularzowe przed przypadkowym wykonaniem z zewnętrznej strony i ogranicza ryzyko wykradania cookie przez JavaScript.

### 10.6. Audyt i maskowanie danych w logach
Projekt posiada audyt zmian:
*   [BaseRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/BaseRepository.cs) zapisuje operacje `Insert`, `Update`, `Delete` do tabeli `SystemLogs`,
*   log zawiera użytkownika, typ akcji, encję, identyfikator rekordu, czas i adres IP,
*   pola o nazwach sugerujących dane wrażliwe, np. `password`, `token`, `secret`, `apikey`, są maskowane jako `********`,
*   [SystemLogRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs) pozwala przeglądać logi z paginacją i filtrami,
*   procedura `usp_ArchiveSystemLogs` pozwala archiwizować starsze logi do `SystemLogsArchive`.

### 10.7. SQL do pokazania:
Role kont bazodanowych:
```sql
SELECT
    dp.name AS UserName,
    rp.name AS RoleName
FROM sys.database_role_members drm
JOIN sys.database_principals rp ON rp.principal_id = drm.role_principal_id
JOIN sys.database_principals dp ON dp.principal_id = drm.member_principal_id
WHERE dp.name IN (N'admin', N'pracownik')
ORDER BY dp.name, rp.name;
```

Hashowanie haseł:
```sql
SELECT TOP (20)
    Email,
    [Role],
    LEFT(PasswordHash, 4) AS HashPrefix,
    LEN(PasswordHash) AS HashLength,
    CASE
        WHEN PasswordHash LIKE '$2%' THEN N'BCrypt'
        ELSE N'Inny format'
    END AS HashType
FROM dbo.Users
WHERE IsDeleted = 0
ORDER BY Email;
```

Przykładowe wpisy audytu:
```sql
SELECT TOP (20)
    Id,
    UserId,
    Action,
    TargetEntity,
    TargetId,
    [Timestamp],
    IPAddress
FROM dbo.SystemLogs
ORDER BY [Timestamp] DESC, Id DESC;
```

### 10.8. Czego nie obiecywać:
Nie warto twierdzić, że projekt ma pełne produkcyjne bezpieczeństwo klasy enterprise. W obecnym zakresie nie pokazujemy TDE, szyfrowania kolumn, MFA, rotacji sekretów ani rozbudowanego systemu blokowania kont po wielu nieudanych próbach. Na obronie lepiej powiedzieć uczciwie: mamy sensowne zabezpieczenia aplikacyjne i bazodanowe dla projektu akademickiego, a pełne hardeningi produkcyjne byłyby kolejnym etapem.

### 10.9. Jedno zdanie do obrony:
„Bezpieczeństwo danych realizujemy warstwowo: aplikacja nie działa na koncie właściciela bazy, hasła są hashowane BCryptem, zapytania są parametryzowane przez Dappera, dostęp do modułów kontrolują role i `[Authorize]`, formularze mają ochronę anty-CSRF, a zmiany danych są audytowane w `SystemLogs`.”

---

## 11. Programowanie po stronie Bazy Danych (T-SQL / Procedury, Funkcje, Triggery)
Zamiast przenosić całą logikę relacyjną do kodu aplikacji, krytyczne operacje relacyjne i systemowe zaimplementowano bezpośrednio na silniku MS SQL Server.

### Konkretny plik w projekcie:
Podstawowe obiekty bazodanowe są wdrażane przez system migracji w pliku [507_AddSbdSqlObjectsAndIndexes.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs). Logiczny pakiet raportowy dla logistyki znajduje się w migracji [519_AddLogisticsSbdPackage.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/519_AddLogisticsSbdPackage.cs).

### Rekomendowany sposób prezentacji:
Na obronie najlepiej zacząć od części logistycznej, bo widać ją zarówno w kodzie SQL, jak i w aplikacji:

1.  **Funkcja M4:** `[logistics_pkg].[fn_RouteLoadSummary]`
    *   Zwraca podsumowanie tras na wybrany dzień: liczba przystanków, przypisany pojazd/kierowca, obciążenie auta, brakujące współrzędne, status manifestu.
    *   To jest funkcja tabelaryczna inline, więc można jej używać jak tabeli w zapytaniach `SELECT`.

2.  **Procedura M4:** `[logistics_pkg].[usp_GetDailyDispatchBoard]`
    *   Wywołuje funkcję `fn_RouteLoadSummary`.
    *   Zwraca dwa zbiory wyników: listę tras oraz zbiorcze podsumowanie dnia.
    *   Jest używana realnie przez aplikację, a nie tylko przez ręczne zapytanie SQL.

3.  **Użycie w aplikacji**
    *   [LogisticsSbdReportRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/LogisticsSbdReportRepository.cs) wywołuje procedurę przez Dappera i `QueryMultipleAsync`.
    *   [LogisticsController.cs](../src/KuchniaUCygana.Web/Controllers/LogisticsController.cs) pobiera raport na dashboard.
    *   [Index.cshtml](../src/KuchniaUCygana.Web/Views/Logistics/Index.cshtml) renderuje kartę „Raport SBD z pakietu SQL”.

4.  **Trigger jako trzeci typ obiektu bazodanowego**
    *   Trigger nie jest stricte logistyczny, ale spełnia wymaganie projektu dotyczące programowania po stronie bazy.
    *   Najlepiej pokazać `[dbo].[tr_Batches_UpdateIsDepleted]`, bo ma jasne uzasadnienie biznesowe: automatycznie oznacza partię jako wyczerpaną, gdy jej ilość spadnie do zera.

### SQL do pokazania obiektów i ich źródła:
Lista obiektów pakietu logistycznego:
```sql
SELECT
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType
FROM sys.objects o
JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE s.name = N'logistics_pkg'
ORDER BY o.type_desc, o.name;
```

Kod źródłowy funkcji i procedury:
```sql
SELECT OBJECT_DEFINITION(OBJECT_ID(N'logistics_pkg.fn_RouteLoadSummary')) AS FunctionSource;
SELECT OBJECT_DEFINITION(OBJECT_ID(N'logistics_pkg.usp_GetDailyDispatchBoard')) AS ProcedureSource;
```

Kod źródłowy triggera:
```sql
SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.tr_Batches_UpdateIsDepleted')) AS TriggerSource;
```

Uruchomienie procedury na dacie, dla której istnieją trasy:
```sql
DECLARE @DeliveryDate date =
(
    SELECT TOP (1) CAST(RouteDate AS date)
    FROM dbo.DeliveryRoutes
    WHERE IsDeleted = 0
    ORDER BY RouteDate DESC
);

EXEC logistics_pkg.usp_GetDailyDispatchBoard
    @DeliveryDate = @DeliveryDate,
    @EstimatedDeliveryWeightKg = 1.20;
```

Bezpośrednie użycie funkcji jako tabeli:
```sql
DECLARE @DeliveryDate date =
(
    SELECT TOP (1) CAST(RouteDate AS date)
    FROM dbo.DeliveryRoutes
    WHERE IsDeleted = 0
    ORDER BY RouteDate DESC
);

SELECT *
FROM logistics_pkg.fn_RouteLoadSummary(@DeliveryDate, 1.20);
```

### Jedno zdanie do obrony:
„Wymaganie PL/SQL realizujemy w SQL Server jako T-SQL: mamy trigger, funkcję tabelaryczną i procedurę składowaną. Najważniejsze jest to, że procedura i funkcja logistyczna nie są sztucznym przykładem, bo dashboard logistyki faktycznie używa ich przez Dappera.”

### 11.1. Wyzwalacz (Trigger) — Automatyczne oznaczanie partii jako wyczerpanej
*   **Nazwa:** `[dbo].[tr_Batches_UpdateIsDepleted]`
*   **Lokalizacja w kodzie:** [507_AddSbdSqlObjectsAndIndexes.cs:L69-L85](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs#L69-L85)
*   **Jak działa:** Nasłuchuje na operacje `INSERT` i `UPDATE` w tabeli `Batches`. Jeśli ilość surowca spadnie do zera (`CurrentQuantity <= 0`), wyzwalacz automatycznie przestawia flagę `IsDepleted` na `1` (oraz z powrotem na `0` przy uzupełnieniu stanu).
*   **Uzasadnienie (Rationale):** Zapewnia spójność na poziomie silnika bazy danych. Niezależnie od tego, czy stan modyfikuje aplikacja, czy administrator wykonujący ręczną korektę, wyczerpana partia zostanie natychmiast oznaczona w indeksach.

### 11.2. Procedura Składowana (Stored Procedure) — Transakcyjna archiwizacja logów
*   **Nazwa:** `[dbo].[usp_ArchiveSystemLogs]`
*   **Lokalizacja w kodzie:** [507_AddSbdSqlObjectsAndIndexes.cs:L87-L147](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs#L87-L147)
*   **Jak działa:** Przenosi stare wpisy logów audytowych z tabeli operacyjnej `SystemLogs` do tabeli historycznej `SystemLogsArchive` w paczkach o określonym rozmiarze (parametr `@BatchSize`).
*   **Kluczowe mechanizmy:**
    *   Stosuje podpowiedzi blokowania `WITH (READPAST, UPDLOCK)` – pobiera tylko te wiersze, które nie są zablokowane przez inne zapytania i blokuje je na czas zapisu do archiwum, co eliminuje ryzyko deadlocków (zakleszczeń) przy ciągłym zapisie logów przez serwer aplikacji.
    *   Działa w bezpiecznej transakcji bazodanowej (`BEGIN TRANSACTION ... COMMIT TRANSACTION`) z klauzulą `SET XACT_ABORT ON` (natychmiastowe wycofanie transakcji w przypadku błędu runtime).

### 11.3. Funkcja Bazodanowa (Inline Table-Valued Function) — Wyliczanie kosztów i wartości odżywczych
*   **Nazwa:** `[dbo].[fn_MealNutritionCost]`
*   **Lokalizacja w kodzie:** [507_AddSbdSqlObjectsAndIndexes.cs:L149-L172](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs#L149-L172)
*   **Jak działa:** Dla wybranego `@MealId` zwraca wirtualną tabelę zawierającą sumaryczny koszt surowców (wyliczony na podstawie wag składników i cen jednostkowych z tabeli `Ingredients`) oraz zsumowane makroskładniki (białko, tłuszcze, węglowodany, kalorie, błonnik) z tabeli `NutritionFacts`.
*   **Uzasadnienie (Rationale):** Użycie funkcji typu "Inline" zamiast skalarnej pozwala optymalizatorowi MS SQL Server na zintegrowanie kodu funkcji z planem zapytania nadrzędnego, co znacząco skraca czas wykonania.

### 11.4. Logiczny pakiet SQL Server — Raport dyspozytorski logistyki
*   **Nazwa schematu:** `[logistics_pkg]`
*   **Lokalizacja w kodzie:** [519_AddLogisticsSbdPackage.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/519_AddLogisticsSbdPackage.cs)
*   **Dlaczego tak:** MS SQL Server nie posiada konstrukcji `CREATE PACKAGE` z Oracle PL/SQL. Zastosowano więc naturalny odpowiednik w SQL Server: dedykowany schemat grupujący powiązane procedury i funkcje jednego obszaru biznesowego.
*   **Zawartość pakietu:**
    *   `[logistics_pkg].[fn_RouteLoadSummary]` — funkcja tabelaryczna inline zwracająca dzienne podsumowanie tras, przystanków, kierowców, pojazdów, obciążenia auta i statusu manifestu.
    *   `[logistics_pkg].[usp_GetDailyDispatchBoard]` — procedura raportowa dla dyspozytora, zwracająca szczegóły tras oraz zbiorcze podsumowanie dnia.
*   **Uzasadnienie biznesowe:** Procedura łączy dane modułów M1/M3/M4: kalendarz dostaw i adresy, trasy i pojazdy, kierowców oraz manifesty kompletacji. Dzięki temu raport jest realnym punktem integracyjnym, a nie sztucznym przykładem do spełnienia wymagania.
*   **Użycie w aplikacji:** Pakiet jest wywoływany przez [LogisticsSbdReportRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/LogisticsSbdReportRepository.cs), które używa Dappera i metody `QueryMultipleAsync` do odczytu dwóch zestawów wyników procedury. Dane są następnie przekazywane przez [LogisticsController.cs](../src/KuchniaUCygana.Web/Controllers/LogisticsController.cs) do dashboardu logistyki i renderowane w sekcji „Raport SBD z pakietu SQL” w [Index.cshtml](../src/KuchniaUCygana.Web/Views/Logistics/Index.cshtml). Dzięki temu można pokazać zarówno ręczne wykonanie procedury w SQL Server, jak i jej praktyczne wykorzystanie przez aplikację.

Przykładowe zapytania do pokazania:
```sql
SELECT 
    s.name AS SchemaName,
    o.name AS ObjectName,
    o.type_desc AS ObjectType
FROM sys.objects o
JOIN sys.schemas s ON s.schema_id = o.schema_id
WHERE s.name = N'logistics_pkg';

EXEC logistics_pkg.usp_GetDailyDispatchBoard
    @DeliveryDate = '2026-06-09',
    @EstimatedDeliveryWeightKg = 1.20;
```

---

## 12. Optymalizacja Bazy Danych (indeksy, paginacja, predykaty)
Optymalizacja w projekcie nie polega na losowym dodaniu indeksów do wszystkich kolumn. Indeksy są dodawane pod konkretne zapytania: filtry w `WHERE`, kolumny używane w `JOIN`, sortowanie `ORDER BY` oraz paginację.

Warto mówić precyzyjnie: większość pokazanych indeksów to **złożone indeksy nieklastrowane**. Część z nich jest też **covering**, bo ma `INCLUDE`. Nie należy ich nazywać indeksami filtrowanymi, jeśli w definicji nie ma klauzuli `WHERE`.

### 12.1. Przykład 1: FEFO i partie magazynowe
Najbardziej czytelny przykład to wyszukiwanie aktywnych partii surowca zgodnie z FEFO, czyli najpierw zużywamy partie z najbliższą datą ważności.

Indeks:
```sql
CREATE INDEX [IX_Batches_StockItem_Active_Expiry]
ON [dbo].[Batches] ([StockItemId], [IsDeleted], [IsDepleted], [CurrentQuantity], [ExpiryDate]);
```

Gdzie w kodzie:
*   [BatchRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/Warehouse/BatchRepository.cs) pobiera aktywne partie po `StockItemId`, odrzuca usunięte i wyczerpane, a następnie sortuje po `ExpiryDate`.
*   [031_OptimizeWarehouseQueryIndexes.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/031_OptimizeWarehouseQueryIndexes.cs) oraz [507_AddSbdSqlObjectsAndIndexes.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs) pokazują ewolucję tego indeksu.

Dlaczego te kolumny:
*   `StockItemId` - najpierw zawężamy do konkretnego składnika albo listy składników.
*   `IsDeleted` - prawie wszystkie zapytania domenowe pomijają rekordy soft-delete.
*   `IsDepleted` - FEFO nie powinno brać partii wyczerpanych.
*   `CurrentQuantity` - wspiera zapytania dostępności i raporty zapasu.
*   `ExpiryDate` - pozwala szybciej znaleźć partie do zużycia jako pierwsze.

### 12.2. Przykład 2: trasy logistyki
Dla M4 najlepszy przykład to indeksy dodane w [401_AddLogisticsQueryIndexes.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/401_AddLogisticsQueryIndexes.cs).

Indeksy:
```sql
IX_DeliveryRoutes_Date_Deleted
    ON DeliveryRoutes(RouteDate, IsDeleted)

IX_DeliveryRouteStops_Route_Sequence
    ON DeliveryRouteStops(RouteId, SequenceNumber, IsDeleted)

IX_DeliveryRouteStops_Calendar
    ON DeliveryRouteStops(DeliveryCalendarId, IsDeleted)
```

Dlaczego akurat te kolumny:
*   `DeliveryRoutes(RouteDate, IsDeleted)` pasuje do widoku tras na dzień. Repozytorium pobiera trasy z zakresu daty i pomija usunięte rekordy.
*   `DeliveryRouteStops(RouteId, SequenceNumber, IsDeleted)` pasuje do szczegółów trasy: najpierw filtrujemy po `RouteId`, potem wyświetlamy przystanki w kolejności `SequenceNumber`.
*   `DeliveryRouteStops(DeliveryCalendarId, IsDeleted)` jest ważne dla integracji M3/M4, bo magazyn rozwiązuje trasę i stop przez `DeliveryCalendarId`.

W kodzie poprawiono też predykat daty w [DeliveryRouteRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/DeliveryRouteRepository.cs): zamiast `CAST(RouteDate AS date) = @date` używany jest zakres:
```sql
WHERE [RouteDate] >= @RouteDateStart
  AND [RouteDate] < @RouteDateEnd
  AND [IsDeleted] = 0
```

To jest ważne, bo funkcja na kolumnie (`CAST`) może utrudnić użycie indeksu. Zakres po surowej kolumnie jest predykatem sargable, czyli optymalizator może efektywniej wykonać seek po indeksie.

### 12.3. Przykład 3: logi systemowe i panel administracyjny
Logi systemowe mogą rosnąć najszybciej, dlatego mają osobne indeksy pod najczęstsze sposoby przeglądania. W [513_HardenSystemLogQueryIndexes.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/513_HardenSystemLogQueryIndexes.cs) dodano między innymi:
```sql
IX_SystemLogs_Window
    ON SystemLogs(Timestamp DESC, Id DESC)

IX_SystemLogs_User_Window
    ON SystemLogs(UserId, Timestamp DESC, Id DESC)

IX_SystemLogs_Target_Window
    ON SystemLogs(TargetEntity, TargetId, Timestamp DESC, Id DESC)

IX_SystemLogs_Action_Window
    ON SystemLogs(Action, Timestamp DESC, Id DESC)
```

Te same indeksy są tworzone także dla `SystemLogsArchive`, żeby wyszukiwanie działało podobnie dla logów aktywnych i zarchiwizowanych.

Dlaczego akurat te kolumny:
*   `Timestamp DESC, Id DESC` pasuje do listy najnowszych logów i paginacji.
*   `UserId` pasuje do historii działań konkretnego użytkownika.
*   `TargetEntity, TargetId` pasuje do audytu konkretnego rekordu.
*   `Action` pasuje do filtrowania po typie zdarzenia.

W [SystemLogRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs) użyto też:
```sql
ORDER BY [Timestamp] DESC, [Id] DESC
OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
OPTION (RECOMPILE);
```

`OFFSET/FETCH` realizuje paginację po stronie bazy, a `OPTION (RECOMPILE)` pomaga przy dynamicznych filtrach, bo plan zapytania jest dobierany do aktualnych parametrów.

### 12.4. SQL do pokazania indeksów:
```sql
SELECT
    t.name AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    ic.key_ordinal AS KeyOrdinal,
    c.name AS ColumnName,
    ic.is_included_column AS IsIncludedColumn
FROM sys.indexes i
JOIN sys.tables t ON t.object_id = i.object_id
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
WHERE i.name IN
(
    N'IX_Batches_StockItem_Active_Expiry',
    N'IX_DeliveryRoutes_Date_Deleted',
    N'IX_DeliveryRouteStops_Route_Sequence',
    N'IX_DeliveryRouteStops_Calendar',
    N'IX_SystemLogs_Window',
    N'IX_SystemLogs_User_Window',
    N'IX_SystemLogs_Target_Window',
    N'IX_SystemLogs_Action_Window'
)
ORDER BY t.name, i.name, ic.is_included_column, ic.key_ordinal, ic.index_column_id;
```

### 12.5. SQL do krótkiego pokazania użycia indeksu:
Najlepiej włączyć w SSMS/Azure Data Studio rzeczywisty plan wykonania albo użyć `SET STATISTICS IO ON`.

```sql
SET STATISTICS IO ON;

DECLARE @RouteDateStart datetimeoffset = '2026-06-09T00:00:00+02:00';
DECLARE @RouteDateEnd datetimeoffset = DATEADD(day, 1, @RouteDateStart);

SELECT *
FROM dbo.DeliveryRoutes
WHERE RouteDate >= @RouteDateStart
  AND RouteDate < @RouteDateEnd
  AND IsDeleted = 0
ORDER BY Id;

DECLARE @RouteId int =
(
    SELECT TOP (1) Id
    FROM dbo.DeliveryRoutes
    WHERE RouteDate >= @RouteDateStart
      AND RouteDate < @RouteDateEnd
      AND IsDeleted = 0
    ORDER BY Id
);

SELECT *
FROM dbo.DeliveryRouteStops
WHERE RouteId = @RouteId
  AND IsDeleted = 0
ORDER BY SequenceNumber;

SET STATISTICS IO OFF;
```

Oczekiwany przekaz: zapytania filtrują po tych samych kolumnach, które są początkiem indeksu, a sortowanie przystanków odpowiada kolejności `SequenceNumber`.

### 12.6. Jedno zdanie do obrony:
„Indeksy zostały dobrane pod rzeczywiste zapytania aplikacji: trasy filtrujemy po dniu, przystanki po trasie i kolejności, partie magazynowe po składniku i statusie aktywności, a logi po czasie, użytkowniku, akcji i obiekcie audytu.”

---

## 13. Duży Wolumen Danych / Dane Fakerowe (VolumeDemo)
Wymaganie dużej liczby danych spełniamy przez profil seedowania **`VolumeDemo`** w [DatabaseSeeder.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Seeding/DatabaseSeeder.cs). To nie jest kilka ręcznie wpisanych rekordów pokazowych, tylko syntetyczny zestaw danych tworzony dla całego przepływu aplikacji: M1 zamówienia i kalendarz dostaw, M2 katalog składników/opakowań i menu, M3 produkcja oraz kompletacja, M4 trasy/flota/torby, M5 pracownicy, zgłoszenia, powiadomienia i logi audytowe.

Co warto powiedzieć:

*   Dane są „fakerowe” w sensie biznesowym: są sztuczne, masowe i generowane automatycznie. Główny profil `VolumeDemo` jest przy tym deterministyczny, bo używa serii `VOL-M1-*`, `VOL-M2-*`, `VOL-M3-*`, `VOL-M4-*`, `VOL-M5-*` oraz parametru `Seed`. Dzięki temu po `docker compose down -v` można odtworzyć porównywalny zestaw danych.
*   Biblioteka **Bogus** jest dołączona do projektu i używana w testowym seederze magazynu, natomiast pełny seeder aplikacyjny `VolumeDemo` celowo nie opiera się na losowaniu w runtime. Dane są generowane set-based SQL-em przez Dappera, żeby wynik był powtarzalny, szybki i łatwy do wyczyszczenia prefiksami `VOL-*`.
*   Przy domyślnych ustawieniach z [DatabaseSeedingOptions.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Seeding/DatabaseSeedingOptions.cs) (`Days = 14`, `ActiveCustomers = 300`, `PeakOrders = 500`) seeder tworzy m.in. syntetycznych klientów i pracowników, 2000-4500 zamówień zależnie od parametru `PeakOrders`, dostawy w `DeliveryCalendar`, pozycje zamówień, 1000 składników, opakowania i pojemniki, partie magazynowe, plany menu/produkcji, sesje pakowania, trasy, manifesty, 300 toreb termicznych oraz tysiące logów audytowych.
*   Nie mówimy, że zaimplementowano `SqlBulkCopy`, bo aktualny kod tego nie robi. Wydajność uzyskano przez hurtowe zapytania `INSERT INTO ... SELECT`, CTE, `CROSS APPLY`, generatory liczb z `sys.all_objects`, transakcje i idempotentne warunki `WHERE NOT EXISTS`.
*   Profil ma ograniczenia bezpieczeństwa: `Days` jest ograniczone do 1-45, `ActiveCustomers` do 1-1150, a `PeakOrders` do 1-2000. Dodatkowo [DatabaseSeedingBootstrapper.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Seeding/DatabaseSeedingBootstrapper.cs) nie uruchamia ciężkiego `VolumeDemo` przy zwykłym starcie aplikacji, tylko używa go jako profilu komendowego.

SQL do pokazania liczebności tabel po seedowaniu:

```sql
SELECT
    OBJECT_SCHEMA_NAME(ps.object_id) AS SchemaName,
    OBJECT_NAME(ps.object_id) AS TableName,
    SUM(ps.row_count) AS [Rows]
FROM sys.dm_db_partition_stats ps
WHERE ps.index_id IN (0, 1)
  AND OBJECTPROPERTY(ps.object_id, 'IsUserTable') = 1
GROUP BY ps.object_id
ORDER BY [Rows] DESC;
```

SQL do pokazania, że dane pochodzą z `VolumeDemo`:

```sql
SELECT N'Klienci VolumeDemo' AS Obszar, CAST(COUNT_BIG(*) AS bigint) AS Rekordy
FROM dbo.Users
WHERE Email LIKE N'vol-klient-%@kuchnia.local'
UNION ALL
SELECT N'Pracownicy VolumeDemo', CAST(COUNT_BIG(*) AS bigint)
FROM dbo.Users
WHERE Email LIKE N'vol-staff-%@kuchnia.local'
UNION ALL
SELECT N'Zamówienia VOL-M1', CAST(COUNT_BIG(*) AS bigint)
FROM dbo.Orders
WHERE OrderNumber LIKE N'VOL-M1-%'
UNION ALL
SELECT N'Dostawy VOL-M1', CAST(COUNT_BIG(*) AS bigint)
FROM dbo.DeliveryCalendar dc
INNER JOIN dbo.Orders o ON o.Id = dc.OrderId
WHERE o.OrderNumber LIKE N'VOL-M1-%'
UNION ALL
SELECT N'Składniki i opakowania VOL-M2', CAST(COUNT_BIG(*) AS bigint)
FROM dbo.Ingredients
WHERE Name LIKE N'VOL-M2-%'
UNION ALL
SELECT N'Partie magazynowe VOL-M2', CAST(COUNT_BIG(*) AS bigint)
FROM dbo.Batches
WHERE SupplierBatchNumber LIKE N'VOL-M2-%'
UNION ALL
SELECT N'Trasy VOL-M4', CAST(COUNT_BIG(*) AS bigint)
FROM dbo.DeliveryRoutes
WHERE Name LIKE N'VOL-M4-%'
UNION ALL
SELECT N'Przystanki tras VOL-M4', CAST(COUNT_BIG(*) AS bigint)
FROM dbo.DeliveryRouteStops rs
INNER JOIN dbo.DeliveryRoutes r ON r.Id = rs.RouteId
WHERE r.Name LIKE N'VOL-M4-%'
UNION ALL
SELECT N'Torby termiczne VOL-M4', CAST(COUNT_BIG(*) AS bigint)
FROM dbo.ThermalBags
WHERE SerialNumber LIKE N'VOL-M4-%'
UNION ALL
SELECT N'Logi audytu VOL', CAST(COUNT_BIG(*) AS bigint)
FROM dbo.SystemLogs
WHERE IPAddress = N'VOL-SEED';
```

Jedno zdanie do obrony:
„Duży wolumen danych realizujemy przez powtarzalny profil `VolumeDemo`, który generuje syntetyczne dane dla wszystkich modułów, oznacza je prefiksami `VOL-*`, pozwala je zresetować i pokazuje, że aplikacja działa na znacznie większym zbiorze niż ręczne dane demonstracyjne.”

---

## 14. Paginacja
Paginacja jest zrealizowana w kilku miejscach aplikacji, szczególnie tam, gdzie liczba rekordów może szybko rosnąć: logi audytowe, użytkownicy, zgłoszenia BOK, pracownicy, magazyn, kompletacja oraz listy logistyki.

Najważniejsze przykłady do pokazania:

*   [SystemLogRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs) - paginacja po stronie SQL Servera dla logów audytu. Repozytorium liczy `TotalCount`, normalizuje `Page` i `PageSize`, a dane pobiera przez `ORDER BY ... OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY`.
*   [VehicleRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/VehicleRepository.cs) i [DriverRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/DriverRepository.cs) - paginacja w module logistyki dla pojazdów i kierowców.
*   [StockItemRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/Warehouse/StockItemRepository.cs), [BatchRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/Warehouse/BatchRepository.cs) i [InventoryTransactionRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/Warehouse/InventoryTransactionRepository.cs) - paginacja danych magazynowych, czyli obszaru szczególnie narażonego na duży wolumen.
*   [PagedList.cs](../src/KuchniaUCygana.Web/Models/PagedList.cs) oraz [_StaffPagination.cshtml](../src/KuchniaUCygana.Web/Views/Shared/_StaffPagination.cshtml) - wspólny model i wspólny komponent widoku pokazujący zakres `FirstItem-LastItem`, liczbę rekordów, aktualną stronę oraz przyciski poprzednia/następna.
*   Widoki [Vehicles.cshtml](../src/KuchniaUCygana.Web/Views/Logistics/Vehicles.cshtml), [Drivers.cshtml](../src/KuchniaUCygana.Web/Views/Logistics/Drivers.cshtml), [Logs.cshtml](../src/KuchniaUCygana.Web/Views/Admin/Logs.cshtml) i [Users.cshtml](../src/KuchniaUCygana.Web/Views/Admin/Users.cshtml) pokazują paginację w UI oraz wybór liczby rekordów na stronie.

Schemat działania:

1.  Kontroler przyjmuje `Page` i `PageSize` z query stringa lub formularza filtra.
2.  Serwis/repozytorium normalizuje wartości, np. nie pozwala na stronę mniejszą niż 1 i ogranicza `PageSize`, najczęściej do 100 lub 200 rekordów.
3.  Repozytorium wykonuje osobne `COUNT(*)`, aby znać całkowitą liczbę rekordów.
4.  Dane właściwe są pobierane tylko dla jednej strony przez `OFFSET/FETCH`.
5.  Widok pokazuje użytkownikowi zakres wyników oraz linki do kolejnych stron, zachowując filtry.

Przykład SQL do pokazania mechanizmu:

```sql
DECLARE @Page int = 2;
DECLARE @PageSize int = 25;
DECLARE @Offset int = (@Page - 1) * @PageSize;

SELECT COUNT(1) AS TotalCount
FROM dbo.SystemLogs;

SELECT
    Id,
    UserId,
    Action,
    TargetEntity,
    TargetId,
    [Timestamp],
    IPAddress
FROM dbo.SystemLogs
ORDER BY [Timestamp] DESC, Id DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
```

Dlaczego to spełnia wymaganie:

*   Nie pobieramy całej tabeli do aplikacji, tylko konkretny wycinek danych.
*   Paginacja działa razem z filtrowaniem i sortowaniem.
*   `PageSize` jest ograniczany, więc użytkownik nie może przypadkowo wymusić pobrania tysięcy rekordów jednym żądaniem.
*   Przy dużym seederze `VolumeDemo` można realnie pokazać różnicę: tabela ma wiele rekordów, ale widok ładuje tylko jedną stronę.

Jedno zdanie do obrony:
„Paginacja jest realizowana głównie po stronie bazy danych przez `COUNT(*)` oraz `OFFSET/FETCH`, a warstwa UI zachowuje aktualne filtry i pokazuje tylko bieżącą stronę wyników, co jest konieczne przy danych generowanych przez `VolumeDemo`.”

---

## 15. Scenariusz Pokazowy / Prezentacja Krok po Kroku (Live Demo)
Podczas obrony projektu przed komisją wykonaj następujące kroki:

### Krok 1: Konta Bazodanowe i Połączenia
1.  Otwórz plik [appsettings.json](../src/KuchniaUCygana.Web/appsettings.json).
2.  Pokaż dwa oddzielne parametry połączenia: `DefaultConnection` (użytkownik `pracownik` z uprawnieniami DML - czytanie/zapis) oraz `MigrationConnection` (użytkownik `admin` z pełnymi uprawnieniami `db_owner` do migracji schematu).
3.  W SQL Server Management Studio / Azure Data Studio wykonaj zapytanie z sekcji „Połączenie z serwerem”, pokazujące `@@SERVERNAME`, `DB_NAME()`, `SUSER_SNAME()` i `USER_NAME()`.

### Krok 2: Projekt i implementacja fizyczna BD
1.  Otwórz rozdział 8 i 9 w [Sprawozdanie Bazy Danych.md](Sprawozdanie%20Bazy%20Danych.md), pokaż ERD oraz grupy danych.
2.  Otwórz katalog [Entities](../src/KuchniaUCygana.Domain/Entities) i pokaż, że encje domenowe są podzielone na moduły.
3.  Otwórz [sqlserver-init.sql](../docker/sqlserver-init.sql) i pokaż fragment `CREATE DATABASE`, czyli skrypt bootstrapujący bazę oraz konta.
4.  Otwórz katalog [Migrations](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations) i pokaż, że fizyczny schemat jest tworzony przez wersjonowane migracje.
5.  Dla M4 pokaż [400_CreateLogisticsTables.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/400_CreateLogisticsTables.cs), czyli fizyczne tabele logistyki.
6.  W SQL Server wykonaj zapytanie z sekcji „Implementacja fizyczna BD”, np. `SELECT * FROM dbo.VersionInfo ORDER BY Version DESC;` oraz listę tabel z `sys.tables`.

### Krok 3: Obrona Dappera jako ORM
1.  Otwórz plik [BaseRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/BaseRepository.cs).
2.  Zademonstruj metodę `InsertAsync` lub `UpdateAsync`. Wskaż, jak metadane encji są zamieniane na parametryzowane polecenia SQL, a Dapper mapuje obiekt C# na parametry zapytania.
3.  Wyjaśnij różnicę między micro-ORM-em a ręcznym ADO.NET: SQL jest jawny, ale Dapper mapuje wyniki na obiekty, obsługuje parametry, typy i wiele zbiorów wynikowych.
4.  Otwórz [DapperOrmProofTests.cs](../tests/KuchniaUCygana.Tests/Integration/Infrastructure/DapperOrmProofTests.cs) i pokaż testy potwierdzające CRUD na `BaseRepository`, mapowanie `JOIN/CTE` na DTO oraz `QueryMultipleAsync`.

### Krok 4: Endpointy aplikacyjne
1.  Otwórz [Program.cs](../src/KuchniaUCygana.Web/Program.cs) i pokaż `AddControllersWithViews`, `UseAuthentication`, `UseAuthorization` oraz `MapControllerRoute`.
2.  Otwórz [LogisticsController.cs](../src/KuchniaUCygana.Web/Controllers/LogisticsController.cs) i pokaż `[Route("logistics")]`, `[Authorize(...)]` oraz przykładowe akcje `[HttpGet]` i `[HttpPost]`.
3.  W przeglądarce pokaż `/logistics/routes` albo `/logistics/routes/{id}` jako endpoint panelu dyspozytora działający na realnych danych z bazy.
4.  Opcjonalnie otwórz [DriverMobileController.cs](../src/KuchniaUCygana.Web/Controllers/DriverMobileController.cs) i pokaż `/driver`, `/driver/start`, `/driver/stop/{id}/confirm` jako endpointy procesu dostawy.

### Krok 5: Role systemowe
1.  Otwórz [AppRoles.cs](../src/KuchniaUCygana.Domain/Constants/AppRoles.cs) i pokaż katalog ról pracowniczych.
2.  Otwórz [User.cs](../src/KuchniaUCygana.Domain/Entities/Auth/User.cs) i pokaż pole `Role`, czyli miejsce przechowywania roli użytkownika.
3.  Otwórz [AccountController.cs](../src/KuchniaUCygana.Web/Controllers/AccountController.cs) i pokaż `ClaimTypes.Role` oraz `ExpandRoles`.
4.  Wykonaj SQL z sekcji „Role systemowe”, żeby pokazać liczebność użytkowników według ról.
5.  Otwórz [LogisticsController.cs](../src/KuchniaUCygana.Web/Controllers/LogisticsController.cs) albo [AdminController.cs](../src/KuchniaUCygana.Web/Controllers/AdminController.cs) i pokaż, że role są używane w `[Authorize]`.

### Krok 6: Uwierzytelnianie
1.  Wyloguj się i spróbuj wejść na `/logistics/routes`, żeby pokazać przekierowanie do `/Account/Login`.
2.  Otwórz [Program.cs](../src/KuchniaUCygana.Web/Program.cs) i pokaż `AddAuthentication`, `AddCookie`, `LoginPath`, `AccessDeniedPath`, `HttpOnly`, `SameSite`, `ExpireTimeSpan` i `UseAuthentication`.
3.  Otwórz [AccountController.cs](../src/KuchniaUCygana.Web/Controllers/AccountController.cs) i pokaż przepływ: `FindByEmailAsync` -> `VerifyPassword` -> `SignInAsync` -> claims.
4.  Otwórz [User.cs](../src/KuchniaUCygana.Domain/Entities/Auth/User.cs) i pokaż `PasswordHash`.
5.  Wykonaj SQL z sekcji „Uwierzytelnianie”, żeby pokazać, że w bazie są hashe BCrypt, a nie hasła jawne.
6.  Zaloguj się poprawnym kontem i pokaż, że aplikacja wpuszcza użytkownika do właściwego modułu.

### Krok 7: Bezpieczeństwo danych
1.  Pokaż [sqlserver-init.sql](../docker/sqlserver-init.sql): `admin` ma `db_owner`, a `pracownik` tylko `db_datareader` i `db_datawriter`.
2.  Wykonaj SQL z sekcji „Bezpieczeństwo danych”, żeby pokazać role kont bazodanowych i hashe BCrypt.
3.  Otwórz [BaseRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/BaseRepository.cs) i pokaż parametryzację wartości oraz maskowanie pól wrażliwych w audycie.
4.  Otwórz [Program.cs](../src/KuchniaUCygana.Web/Program.cs) i pokaż `AutoValidateAntiforgeryTokenAttribute`, `HttpOnly`, `SameSite` i `SecurePolicy`.
5.  Otwórz [SystemLogRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs) albo widok `/admin/logs`, żeby pokazać, że zmiany danych są audytowane.
6.  Powiedz uczciwie, że nie pokazujemy pełnego produkcyjnego hardeningu typu TDE/MFA/rotacja sekretów, tylko warstwowe zabezpieczenia odpowiednie dla zakresu projektu.

### Krok 8: Wyzwalacze, Funkcje i Procedury w Kodzie Migracji
1.  Otwórz migrację [519_AddLogisticsSbdPackage.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/519_AddLogisticsSbdPackage.cs) i pokaż schemat `logistics_pkg` jako odpowiednik pakietu w SQL Server.
2.  Pokaż funkcję `logistics_pkg.fn_RouteLoadSummary` i wyjaśnij, że zwraca dzienne podsumowanie tras: przystanki, pojazdy, kierowców, obciążenie i status manifestu.
3.  Pokaż procedurę `logistics_pkg.usp_GetDailyDispatchBoard`, która używa tej funkcji i zwraca dwa zestawy wyników: szczegóły tras oraz podsumowanie dnia.
4.  Wykonaj zapytanie z sekcji „SQL do pokazania obiektów i ich źródła”, najlepiej z automatycznym wyborem daty z istniejących tras.
5.  Otwórz dashboard logistyki (`/logistics`) i pokaż kartę „Raport SBD z pakietu SQL”. Wyjaśnij, że ta sekcja nie buduje raportu własnym SQL-em w widoku, tylko korzysta z repozytorium Dappera wywołującego procedurę z pakietu `logistics_pkg`.
6.  Otwórz [LogisticsSbdReportRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/LogisticsSbdReportRepository.cs) i pokaż `QueryMultipleAsync`, które mapuje dwa zbiory wyników procedury na obiekty C#.
7.  Na koniec otwórz [507_AddSbdSqlObjectsAndIndexes.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs), pokaż trigger `tr_Batches_UpdateIsDepleted` i wyjaśnij, że jest to trzeci wymagany typ obiektu bazodanowego: automatyzuje spójność flagi `IsDepleted` przy zmianie stanu partii.

### Krok 9: Optymalizacja i Indeksy
1.  Pokaż `IX_Batches_StockItem_Active_Expiry` i wyjaśnij, że odpowiada zapytaniom FEFO: `StockItemId`, aktywność partii i `ExpiryDate`.
2.  Pokaż indeksy logistyczne z [401_AddLogisticsQueryIndexes.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/401_AddLogisticsQueryIndexes.cs): `IX_DeliveryRoutes_Date_Deleted`, `IX_DeliveryRouteStops_Route_Sequence`, `IX_DeliveryRouteStops_Calendar`.
3.  Otwórz [DeliveryRouteRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/DeliveryRouteRepository.cs) i pokaż predykat zakresowy `RouteDate >= @RouteDateStart AND RouteDate < @RouteDateEnd`, czyli wersję przyjazną dla indeksu.
4.  Otwórz [SystemLogRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs). Pokaż metodę `SearchAsync` - wskaż paginację `OFFSET / FETCH` oraz `OPTION (RECOMPILE)`.
5.  W SQL Server wykonaj zapytanie z sekcji „SQL do pokazania indeksów”, a przy czasie można też uruchomić przykład z `SET STATISTICS IO ON`.

### Krok 10: Duży Wolumen Danych / Faker
1.  Otwórz [DatabaseSeeder.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Seeding/DatabaseSeeder.cs) i pokaż profil `VolumeDemo`: `SeedVolumeDemoFullAsync`, `SeedVolM1EcommerceAsync`, `SeedVolM3ProductionAsync`, `SeedVolM4LogisticsAsync`.
2.  Otwórz [DatabaseSeedingOptions.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Seeding/DatabaseSeedingOptions.cs) i pokaż parametry `Days`, `ActiveCustomers`, `PeakOrders`, `Seed` oraz ich domyślne wartości.
3.  Otwórz [DatabaseSeedingBootstrapper.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Seeding/DatabaseSeedingBootstrapper.cs) i pokaż, że ciężki profil `VolumeDemo` jest traktowany jako seed komendowy, a nie coś odpalane bez kontroli przy każdym starcie.
4.  W SQL Server wykonaj zapytania z sekcji „Duży Wolumen Danych / Dane Fakerowe”, żeby pokazać liczebność tabel i prefiksy `VOL-*`.
5.  Jeżeli prowadzący zapyta o Faker/Bogus: powiedz, że dane są fakerowe, czyli syntetyczne i automatycznie generowane; Bogus jest używany w testowym seederze, a profil `VolumeDemo` generuje dane deterministycznie set-based SQL-em przez Dappera, bo do prezentacji i powtarzalnych testów jest to praktyczniejsze.

### Krok 11: Paginacja
1.  Otwórz `/admin/logs`, `/logistics/vehicles` albo `/logistics/drivers` i pokaż wybór liczby rekordów na stronie oraz przechodzenie między stronami.
2.  Otwórz [SystemLogRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs) i pokaż `SearchAsync`: `COUNT(1)`, obliczenie `offset`, `ORDER BY [Timestamp] DESC, [Id] DESC` oraz `OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY`.
3.  Otwórz [PagedList.cs](../src/KuchniaUCygana.Web/Models/PagedList.cs) i [_StaffPagination.cshtml](../src/KuchniaUCygana.Web/Views/Shared/_StaffPagination.cshtml), żeby pokazać, że UI zna `TotalCount`, `Page`, `PageSize`, `TotalPages`, `FirstItem` i `LastItem`.
4.  W SQL Server wykonaj przykład z sekcji „Paginacja”, aby pokazać, że baza zwraca tylko jedną stronę wyników.
5.  Powiedz: „To jest ważne szczególnie po uruchomieniu `VolumeDemo`, bo wtedy tabele mają tysiące rekordów, a użytkownik nadal pracuje na krótkich, filtrowanych listach.”
