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
2.  **Konto `dev` / `pracownik` (Połączenie DML / Aplikacja):**
    *   Używa connection stringa `DefaultConnection`.
    *   Posiada ograniczone uprawnienia na poziomie roli `db_datareader` oraz `db_datawriter`.
    *   **NIE** posiada roli `db_owner`. Nie ma możliwości wykonywania żadnych poleceń DDL (np. `DROP TABLE`, `ALTER TABLE`, `TRUNCATE TABLE`).
    *   Zapewnia to izolację – nawet w przypadku awarii aplikacji bądź błędu w zapytaniach użytkownik/system nie jest w stanie usunąć struktury bazy danych.

---

## 2. Architektura ORM: Dapper jako ORM i dlaczego nie ciężki EF Core / OrmLite?
**Kluczowa obrona projektu:** Prowadzący preferuje użycie komercyjnego **ServiceStack OrmLite** lub ciężkiego **EF Core**, a my zdecydowaliśmy się na **Dapper**. Musimy dowieść, że Dapper to w pełni zasłużony ORM oraz uzasadnić inżyniersko ten wybór.

### 2.1. Dlaczego Dapper to faktycznie ORM?
ORM (Object-Relational Mapper) to narzędzie, które rozwiązuje problem niedopasowania obiektowo-relacyjnego (*Impedance Mismatch*). Dapper realizuje te zadania jako **Micro-ORM**:
1.  **Automatyczne mapowanie wyników (Object Mapping):** Dapper automatycznie mapuje wiersze i kolumny zapytania SQL na silnie typowane klasy C# za pomocą dynamicznej kompilacji kodu IL (Intermediate Language) w locie, dopasowując nazwy pól.
2.  **Obsługa mapowania relacji (Multi-Mapping):** Umożliwia złączenie wielu tabel w jednym zapytaniu SQL i automatyczne rozbicie ich na zagnieżdżone obiekty (np. mapowanie relacji jeden-do-wielu: `Meal` -> `Category`).
3.  **Własny generyczny mechanizm CRUD (BaseRepository):** W pliku [BaseRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/BaseRepository.cs) zbudowaliśmy własną warstwę generyczną, która za pomocą refleksji odpytuje właściwości encji i automatycznie generuje oraz wykonuje zapytania SQL (`INSERT`, `UPDATE`, `DELETE`). W ten sposób **stworzyliśmy własny, lekki ORM**, który realizuje zadania analogiczne do OrmLite, ale pozostaje pod naszą całkowitą kontrolą.
4.  **Bezpieczna parametryzacja (SQL Injection Protection):** Dapper automatycznie mapuje właściwości obiektów anonimowych na parametry zapytania ADO.NET (`@Param`), chroniąc bazę danych przed atakami SQL Injection na poziomie drivera bazodanowego.

### 2.2. Dlaczego Dapper jest lepszy od ServiceStack OrmLite?
1.  **Brak ograniczeń licencyjnych (100% Open Source):**
    *   **ServiceStack OrmLite** od wersji 4.0 jest biblioteką **komercyjną**. Darmowa wersja posiada krytyczne ograniczenia – pozwala na mapowanie maksymalnie **10 tabel**. Nasz system posiada **80 tabel** – użycie OrmLite w wersji produkcyjnej wymagałoby zakupu drogiej licencji komercyjnej.
    *   **Dapper** jest całkowicie darmowy (licencja Apache 2.0), bez żadnych limitów tabel czy wierszy, rozwijany otwarcie przez inżynierów StackOverflow.
2.  **Maksymalna wydajność (Industry Standard):**
    *   Dapper jest najszybszym micro-ORM-em w świecie .NET, osiągającym prędkości czystego ADO.NET (dzięki optymalnemu buforowaniu planów zapytań i generowaniu kodu IL).
3.  **Brak ukrytego dialektu generowania zapytań:**
    *   OrmLite wprowadza własny, specyficzny język pseudo-LINQ do budowania zapytań SQL.
    *   Dapper bazuje na czystym SQL. Dzięki temu inżynier baz danych ma pełny wgląd w zapytanie, bez niespodzianek ze strony generatora zapytań biblioteki.

### 2.3. Dlaczego Dapper jest lepszy od EF Core?
1.  **Brak narzutu Change Trackera:** EF Core śledzi stan wszystkich pobranych obiektów w pamięci podręcznej. Przy masowym przetwarzaniu wierszy logów HACCP (`TemperatureLogs`) lub statusów pakowania (`PackingItems`), Change Tracker zatyka pamięć serwera aplikacyjnego. Dapper jest bezstanowy (stateless) – mapuje dane i natychmiast uwalnia zasoby.
2.  **Przewidywalność planów zapytań:** EF Core często generuje skomplikowane zapytania ze zagnieżdżonymi złączeniami, co utrudnia ich optymalizację. W Dapperze zapytanie SQL jest pisane wprost, co pozwala bezpośrednio aplikować specyficzne hinty optymalizatora (np. `OPTION (RECOMPILE)`, `WITH (NOLOCK)`).

### 2.4. Dowód w Kodzie Aplikacyjnym (Faktyczna Implementacja ORM w Projekcie)
W celu wykazania, że Dapper nie jest jedynie „wykonawcą surowego SQL”, wskazujemy na rzeczywisty kod produkcyjny w naszym projekcie, który realizuje zaawansowane cechy mapowania obiektowo-relacyjnego. Co kluczowe, wdrożone w projekcie testy integracyjne w pliku [DapperOrmProofTests.cs](../tests/KuchniaUCygana.Tests/Integration/Infrastructure/DapperOrmProofTests.cs) **nie są testami na pokaz na makietach danych**, lecz bezpośrednio testują i uruchamiają produkcyjne klasy repozytoriów w środowisku Docker z bazą MS SQL Server:

1.  **Dynamiczny CRUD bez pisania SQL (Własny ORM w BaseRepository):**
    W pliku [BaseRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/BaseRepository.cs#L80-L123) zaimplementowaliśmy generyczne repozytorium, które na podstawie refleksji metadanych encji automatycznie generuje SQL i parametry dla operacji `InsertAsync` i `UpdateAsync`. Klasy pochodne (np. `BatchRepository`, `SystemLogRepository`) nie posiadają w swoim kodzie ani jednej linijki SQL dla podstawowych operacji zapisu i edycji:
    ```csharp
    // Fragment z BaseRepository.cs — dynamiczny INSERT generowany na podstawie właściwości klasy:
    var insertedId = await db.ExecuteScalarAsync<TId>(
        $"""
        INSERT INTO {Metadata.TableName} ({Metadata.InsertColumns})
        OUTPUT INSERTED.{Metadata.KeyColumn}
        VALUES ({Metadata.InsertParameters});
        """,
        entity);
    ```
    *   **Dowód w testach:** Metoda testowa `BaseRepository_ShouldPerformCrudDynamicallyUsingProductionImplementation` uruchamia produkcyjne repozytorium `BaseRepository<UnitOfMeasure>`, wykonując pełny cykl zapisu (Insert), odczytu (GetById), aktualizacji (Update) i usuwania (Delete) bez ani jednej ręcznie napisanej instrukcji SQL w teście.

2.  **Mapowanie dynamicznych projekcji i złączeń na DTO:**
    W pliku [SystemLogRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs#L86-L107) Dapper automatycznie mapuje złożoną unię zapytań wykorzystującą tabele CTE (`WITH LogSource AS ...`) i złączenie `LEFT JOIN Users` na dedykowany obiekt DTO `SystemLogRow` (w tym kolumny wyliczeniowe i tekstowe złączenia, które nie istnieją w tabeli logów):
    ```csharp
    // Fragment z SystemLogRepository.cs — Dapper automatycznie mapuje wynik zapytania na wiersze DTO:
    var items = (await db.QueryAsync<SystemLogRow>(
        $"""
        WITH LogSource AS (
            {sourceSql}
        )
        SELECT [Id], [UserId], [UserFullName], [Action], [TargetEntity], [TargetId], [Timestamp]
        FROM LogSource
        WHERE {dataWhereSql}
        ORDER BY [Timestamp] DESC, [Id] DESC
        OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
        """,
        dataParameters)).ToList();
    ```
    *   **Dowód w testach:** Metoda testowa `SystemLogRepository_ShouldPerformComplexCteAndJoinMappingOnProductionCode` tworzy tymczasowego użytkownika z określonym imieniem i nazwiskiem, dodaje log audytowy powiązany z tym użytkownikiem, a następnie wywołuje produkcyjną metodę `SystemLogRepository.SearchAsync`. Test weryfikuje, że Dapper poprawnie zmapował złączenie `LEFT JOIN` i przypisał wyliczone pole `UserFullName` (połączenie `FirstName` i `LastName`) bezpośrednio do właściwości DTO.

3.  **Obsługa wielu zbiorów wynikowych w jednym zapytaniu (Query Multiple):**
    W pliku [BatchRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/Warehouse/BatchRepository.cs#L221-L223) zaimplementowaliśmy masowy raport FEFO. Zamiast otwierać osobne połączenia do zliczenia rekordów i pobrania wyników, Dapper pobiera i rozdziela dwa odrębne zbiory wynikowe (wynik typu `int` oraz listę `FefoReportRow`) przesłane jednym strumieniem z SQL Server:
    ```csharp
    // Fragment z BatchRepository.cs — Dapper zarządza odczytem wielu zestawów danych w jednej transakcji:
    using var multi = await db.QueryMultipleAsync(sql, parameters);
    var totalCount = await multi.ReadSingleAsync<int>();                // Odczyt pierwszego zestawu (COUNT)
    var items = (await multi.ReadAsync<FefoReportRow>()).ToList();    // Odczyt drugiego zestawu (ROWS)
    ```
    *   **Dowód w testach:** Metoda testowa `BatchRepository_ShouldMapMultipleResultSetsUsingQueryMultipleAsync` wywołuje produkcyjną funkcję `BatchRepository.GetFefoReportPageAsync` i weryfikuje, że Dapper poprawnie zmapował w jednej transakcji sieciowej zarówno liczbę elementów raportu, jak i strukturę wierszy raportu FEFO.

Te rzeczywiste przykłady dowodzą, że Dapper w naszym projekcie pełni rolę kompletnego systemu ORM, eliminując ręczną obsługę kursorów czy czytników danych ADO.NET (DataReader) i zajmując się automatycznym mapowaniem typów, relacji oraz struktur danych na obiekty w pamięci.

---

## 3. Programowanie po stronie Bazy Danych (T-SQL / Procedury, Funkcje, Triggery)
Zamiast przenosić całą logikę relacyjną do kodu aplikacji, krytyczne operacje relacyjne i systemowe zaimplementowano bezpośrednio na silniku MS SQL Server.

### Konkretny plik w projekcie:
Wszystkie obiekty bazodanowe są wdrażane przez system migracji w pliku [507_AddSbdSqlObjectsAndIndexes.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs).

### 3.1. Wyzwalacz (Trigger) — Automatyczne oznaczanie partii jako wyczerpanej
*   **Nazwa:** `[dbo].[tr_Batches_UpdateIsDepleted]`
*   **Lokalizacja w kodzie:** [507_AddSbdSqlObjectsAndIndexes.cs:L69-L85](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs#L69-L85)
*   **Jak działa:** Nasłuchuje na operacje `INSERT` i `UPDATE` w tabeli `Batches`. Jeśli ilość surowca spadnie do zera (`CurrentQuantity <= 0`), wyzwalacz automatycznie przestawia flagę `IsDepleted` na `1` (oraz z powrotem na `0` przy uzupełnieniu stanu).
*   **Uzasadnienie (Rationale):** Zapewnia spójność na poziomie silnika bazy danych. Niezależnie od tego, czy stan modyfikuje aplikacja, czy administrator wykonujący ręczną korektę, wyczerpana partia zostanie natychmiast oznaczona w indeksach.

### 3.2. Procedura Składowana (Stored Procedure) — Transakcyjna archiwizacja logów
*   **Nazwa:** `[dbo].[usp_ArchiveSystemLogs]`
*   **Lokalizacja w kodzie:** [507_AddSbdSqlObjectsAndIndexes.cs:L87-L147](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs#L87-L147)
*   **Jak działa:** Przenosi stare wpisy logów audytowych z tabeli operacyjnej `SystemLogs` do tabeli historycznej `SystemLogsArchive` w paczkach o określonym rozmiarze (parametr `@BatchSize`).
*   **Kluczowe mechanizmy:**
    *   Stosuje podpowiedzi blokowania `WITH (READPAST, UPDLOCK)` – pobiera tylko te wiersze, które nie są zablokowane przez inne zapytania i blokuje je na czas zapisu do archiwum, co eliminuje ryzyko deadlocków (zakleszczeń) przy ciągłym zapisie logów przez serwer aplikacji.
    *   Działa w bezpiecznej transakcji bazodanowej (`BEGIN TRANSACTION ... COMMIT TRANSACTION`) z klauzulą `SET XACT_ABORT ON` (natychmiastowe wycofanie transakcji w przypadku błędu runtime).

### 3.3. Funkcja Bazodanowa (Inline Table-Valued Function) — Wyliczanie kosztów i wartości odżywczych
*   **Nazwa:** `[dbo].[fn_MealNutritionCost]`
*   **Lokalizacja w kodzie:** [507_AddSbdSqlObjectsAndIndexes.cs:L149-L172](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs#L149-L172)
*   **Jak działa:** Dla wybranego `@MealId` zwraca wirtualną tabelę zawierającą sumaryczny koszt surowców (wyliczony na podstawie wag składników i cen jednostkowych z tabeli `Ingredients`) oraz zsumowane makroskładniki (białko, tłuszcze, węglowodany, kalorie, błonnik) z tabeli `NutritionFacts`.
*   **Uzasadnienie (Rationale):** Użycie funkcji typu "Inline" zamiast skalarnej pozwala optymalizatorowi MS SQL Server na zintegrowanie kodu funkcji z planem zapytania nadrzędnego, co znacząco skraca czas wykonania.

---

## 4. Optymalizacja Bazy Danych (Indeksy i Plany Zapytań)
W celu zagwarantowania szybkiego czasu odpowiedzi przy dużych danych testowych, wdrożono indeksy nieklastrowane (Non-Clustered Indexes) pokrywające najczęstsze filtry wyszukiwania.

### 4.1. Indeksy Filtrowane i Pokrywające (Covering Indexes):
*   **Przykład z optymalizacji FEFO:** W pliku [507_AddSbdSqlObjectsAndIndexes.cs:L185-L191](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs#L185-L191) wdrożono indeks:
    ```sql
    CREATE INDEX [IX_Batches_StockItem_Active_Expiry]
    ON [dbo].[Batches] ([StockItemId], [IsDeleted], [IsDepleted], [CurrentQuantity], [ExpiryDate]);
    ```
    *   **Dlaczego composite?** Podczas wyszukiwania najstarszej partii do pobrania surowca, zapytania filtrują po `StockItemId`, wykluczają usunięte (`IsDeleted = 0`), wykluczają wyczerpane (`IsDepleted = 0`) i sortują po `ExpiryDate`. Indeks ten pozwala optymalizatorowi na wykonanie natychmiastowej operacji **Index Seek** zamiast kosztownego przeszukiwania całej tabeli (Table Scan) i sortowania w pamięci RAM.

### 4.2. Przeciwdziałanie "Parameter Sniffing":
*   Pokaż wyszukiwanie logów systemowych w [SystemLogRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs#L77-L107).
*   Dynamiczne zapytania wyszukiwania (filtrowanie po dacie, frazie, użytkowniku) wykorzystują klauzulę **`OPTION (RECOMPILE)`** na końcu zapytania. Zmusza to silnik do przeliczenia planu zapytania przy każdym wykonaniu w oparciu o aktualne wartości filtrów, co zapobiega powstawaniu nieoptymalnych planów pamięci podręcznej (Parameter Sniffing).

### 4.3. Stronicowanie na bazie OFFSET / FETCH:
*   W [SystemLogRepository.cs:L106](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs#L106) zastosowano nowoczesną paginację SQL Server:
    ```sql
    ORDER BY [Timestamp] DESC, [Id] DESC
    OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
    ```
    Jest to zalecane rozwiązanie, o wiele wydajniejsze od przestarzałych konstrukcji opartych na `ROW_NUMBER()`.

---

## 5. Obsługa Dużego Wolumenu Danych (Masowy Seeder i SqlBulkCopy)
W celu weryfikacji wydajności bazy danych przy obciążeniu produkcyjnym, zaprojektowany został mechanizm seeder-a o nazwie **`VolumeDemo`**, opisany szczegółowo w dokumencie [plan_seeder_volumedemo.md](../../../.gemini/antigravity-ide/brain/404f5464-176b-4220-83e3-60e36626d4d4/plan_seeder_volumedemo.md).

*   Zwykły zapis w pętli `INSERT` dla 100 000 rekordów generowałby ogromną liczbę zapytań sieciowych (Network Roundtrip).
*   **Rozwiązanie:** Zastosowanie klasy **`SqlBulkCopy`** z przestrzeni `Microsoft.Data.SqlClient`. Pozwala ona na ładowanie całych tabel z pamięci RAM bezpośrednio na dysk serwera bazodanowego za pomocą zoptymalizowanego strumienia binarnego.
*   Zastosowano optymalny rozmiar paczek (Batch Size) równy **2000 wierszy** na transakcję.
*   **Zasada podziału czasowego:** Aby umożliwić testy dynamiczne w aplikacji, plany produkcyjne i menu są seedowane w przyszłość (do 14 dni), natomiast kompletacja, logistyka i logi systemowe są seedowane wyłącznie w przeszłość (`date < today`), pozostawiając dzień dzisiejszy wolny na testowanie procesów w czasie rzeczywistym.

---

## 6. Scenariusz Pokazowy / Prezentacja Krok po Kroku (Live Demo)
Podczas obrony projektu przed komisją wykonaj następujące kroki:

### Krok 1: Konta Bazodanowe i Połączenia
1.  Otwórz plik [appsettings.json](../src/KuchniaUCygana.Web/appsettings.json).
2.  Pokaż dwa oddzielne parametry połączenia: `DefaultConnection` (użytkownik `dev` z uprawnieniami DML - czytanie/zapis) oraz `MigrationConnection` (użytkownik `admin` z pełnymi uprawnieniami `db_owner` do migracji schematu).

### Krok 2: Obrona Dappera jako ORM
1.  Otwórz plik [BaseRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/BaseRepository.cs).
2.  Zademonstruj metodę `InsertAsync` lub `UpdateAsync`. Wskaż, jak za pomocą refleksji właściwości klasy automatycznie generowane są zapytania SQL, co dowodzi, że zbudowaliśmy własny lekki ORM w oparciu o Dappera.
3.  Wyjaśnij argumenty przeciwko OrmLite (licencja komercyjna, limit 10 tabel) oraz EF Core (ciężki Change Tracker i nieprzewidywalny SQL).

### Krok 3: Wyzwalacze, Funkcje i Procedury w Kodzie Migracji
1.  Otwórz plik migracji [507_AddSbdSqlObjectsAndIndexes.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs).
2.  Pokaż wyzwalacz `tr_Batches_UpdateIsDepleted` i wyjaśnij, że odpowiada za automatyczną spójność flagi `IsDepleted` przy zmianie stanu partii.
3.  Pokaż funkcję `fn_MealNutritionCost`. Zademonstruj, jak wylicza dynamicznie makro i koszt dania na podstawie receptury.
4.  Pokaż procedurę `usp_ArchiveSystemLogs` – zwróć uwagę na użycie transakcji bazodanowej oraz podpowiedzi blokad `WITH (READPAST, UPDLOCK)`.

### Krok 4: Optymalizacja i Indeksy
1.  Wskaż indeks pokrywający `IX_Batches_StockItem_Active_Expiry` w pliku migracji i wyjaśnij, w jaki sposób optymalizuje on wyszukiwanie partii w algorytmie FEFO (Index Seek zamiast Table Scan).
2.  Otwórz [SystemLogRepository.cs](../src/KuchniaUCygana.Infrastructure/Persistence/Repositories/SystemLogRepository.cs). Pokaż metodę `SearchAsync` – wskaż użycie paginacji `OFFSET / FETCH` oraz podpowiedzi `OPTION (RECOMPILE)` przeciwdziałającej zjawisku *Parameter Sniffing*.

### Krok 5: Masowy Zapis (SqlBulkCopy)
1.  Otwórz plik [plan_seeder_volumedemo.md](../../../.gemini/antigravity-ide/brain/404f5464-176b-4220-83e3-60e36626d4d4/plan_seeder_volumedemo.md).
2.  Wyjaśnij strategię masowego ładowania danych za pomocą `SqlBulkCopy` w paczkach po 2000 wierszy dla tabel o dużym natężeniu (np. logi, statusy pakowania, pozycje zamówień), co skraca czas seedowania z kilkunastu minut do kilkunastu sekund.
