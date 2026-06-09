# Zmiany 2026-06-02 - Overview i Show Preview

Ten dokument zbiera dzisiejsze zmiany w projekcie, stan gotowości pod większą bazę danych, rzeczy poza zakresem oraz krótki scenariusz pokazowy dla aplikacji i kodu.

## Co zostało zmienione lub dodane

**Infrastruktura SQL Server**

- Runtime aplikacji nie używa już loginu `sa`.
- Docker tworzy loginy SQL Server `admin`, `pracownik` i `klient`.
- `admin` jest kontem migracyjnym z `db_owner` w bazie docelowej.
- `pracownik` jest domyślnym runtime Dappera z rolami `db_datareader` i `db_datawriter`.
- `klient` jest przygotowany jako ograniczony runtime DML pod przyszły osobny kontekst klienta.
- Dodano `ConnectionStrings:MigrationConnection`; FluentMigrator i `MigrationRunner` używają go dla migracji.
- `DefaultConnection` zostaje runtime dla repozytoriów Dapper i w Dockerze wskazuje login `pracownik`.
- Dodano `docker/sqlserver-init.sql` i service `sqlserver-init` w `docker-compose.yml`.
- Naprawiono skrypt init tak, żeby dynamiczne `CREATE LOGIN` i `ALTER LOGIN` działały poprawnie w SQL Server.

**SBD i obiekty bazodanowe**

- Dodano migrację `507_AddSbdSqlObjectsAndIndexes`.
- Migracja tworzy fizyczne obiekty T-SQL: trigger `dbo.tr_Batches_UpdateIsDepleted`, tabelę `dbo.SystemLogsArchive`, procedurę `dbo.usp_ArchiveSystemLogs` i funkcję inline TVF `dbo.fn_MealNutritionCost`.
- Dodano brakujące indeksy dla `Orders`, `OrderItems`, `DeliveryCalendar`, `Payments`, `Addresses`, `SystemLogs` i `Batches`.
- Poprawiono problem z indeksem `IX_Batches_StockItem_Active_Expiry`, usuwając starą wersję i tworząc docelową wersję z `CurrentQuantity`.
- Dostosowano insert batchy w magazynie do triggera SQL Server: `Batches` nie używa już `OUTPUT INSERTED.Id` bez `INTO`.

**UX, role i logowanie preview**

- Dodano rolę aplikacyjną `Client` oraz pełną listę staff w `AppRoles.StaffRoleNames`.
- Publiczny login klienta nie pokazuje już przycisków ról pracowniczych.
- Dodano dev-only ekran `/staff/login` z szybkim wyborem roli pracowniczej.
- Dodano dev-only menu w navbarze: `DEV: Szybkie logowanie`.
- `Panel pracowniczy` w publicznym layoucie pokazuje się tylko zalogowanym rolom staff/admin.
- `StaffController` jest zabezpieczony rolami pracowniczymi/admin.

**Testy i weryfikacja**

- Dodano testy integracyjne SBD dla obiektów SQL, triggera, procedury archiwizacji, funkcji raportowej i least privilege.
- Dodano test jednostkowy potwierdzający osobny `MigrationConnection`.
- Pełny przebieg testów po zmianach był zielony: `110/110`.
- Testy bez integracji były zielone: `72/72`.

## Aktualny stan Docker

Oczekiwany stan po poprawnym starcie:

```text
kuchnia_sqlserver       Up (healthy)   port 1433
kuchnia_sqlserver_init  Exited (0)
kuchnia_web             Up             port 8080
```

`sqlserver-init` ma być `Exited (0)`. To kontener jednorazowy, który tworzy bazę, loginy i użytkowników. Nie powinien działać stale.

Aplikacja jest dostępna pod:

```text
http://localhost:8080
```

## Gotowość pod dużą bazę

**Co już pomaga**

- Dapper i jawny SQL dają kontrolę nad zapytaniami i indeksami.
- Runtime ma ograniczone konto `pracownik`, a migracje idą kontem `admin`.
- Kluczowe tabele zamówień, płatności, adresów, kalendarza dostaw, logów i batchy mają indeksy pod typowe filtry.
- Magazyn i wybrane listy mają server-side filtrowanie, wyszukiwanie lub paginację.
- `SystemLogs` ma procedurę archiwizacji do `SystemLogsArchive`, więc logi można porcjować i przenosić bez ręcznego kopiowania.
- Testy integracyjne sprawdzają least privilege i obiekty T-SQL, czyli część wymagań SBD jest fizycznie w bazie, a nie tylko w dokumentacji.

**Ocena**

To jest solidny etap pod średnią bazę demo i dalszy rozwój. Projekt ma indeksy i separację kont, ale nie jest jeszcze potwierdzony jako zoptymalizowany produkcyjnie pod bardzo duże wolumeny.

## Czego jeszcze nie ma

- Brak seedu wydajnościowego 100k+ rekordów.
- Brak materiału z execution plan przed/po, `STATISTICS IO`, `STATISTICS TIME` i logical reads.
- Brak pełnego produkcyjnego `Login/Register` z realną weryfikacją haseł.
- Brak automatycznej usługi zapisującej wszystkie zmiany biznesowe do `SystemLogs`.
- Brak per-request przełączania SQL loginu na `klient`.
- Brak harmonogramu/job dla `usp_ArchiveSystemLogs`.
- Brak pełnego audytu wszystkich zapytań pod duże wolumeny i konkretne plany wykonania.

## Wprowadzone, ale jeszcze niedopracowane

- Auth jest świadomie w trybie preview: formularze `Login` i `Register` nie są jeszcze produkcyjne, a szybkie logowanie rolami działa tylko w `Development`.
- SQL login `klient` istnieje infrastrukturalnie, ale aplikacja nie używa go jeszcze osobno dla requestów klienta.
- `SystemLogsArchive` i procedura archiwizacji istnieją, ale nie ma automatycznego procesu archiwizacji.
- Starsze dokumenty HTML w `docs/architecture` i `docs/guides` mogą zawierać historyczne wzmianki o SQLite albo starszej konfiguracji.
- Docker wymaga poprawnej konfiguracji `.env`, portów i uprawnień do Docker engine na każdym urządzeniu.

## Show preview - aplikacja

Przed pokazem:

```powershell
docker compose --env-file .env.example ps --all
```

Oczekiwane:

- `kuchnia_sqlserver` - `Up (healthy)`,
- `kuchnia_sqlserver_init` - `Exited (0)`,
- `kuchnia_web` - `Up`,
- `http://localhost:8080` zwraca stronę aplikacji.

Scenariusz demo:

1. Otwórz `http://localhost:8080`.
2. Pokaż publiczny navbar i menu `DEV: Szybkie logowanie`.
3. Kliknij rolę `Admin`.
4. Po zalogowaniu pokaż, że pojawił się link `Panel pracowniczy`.
5. Przejdź do `/staff`.
6. Przejdź do `/warehouse`, żeby pokazać panel magazynu i obszar objęty indeksami/testami.
7. Przejdź do `/notifications`, żeby pokazać listę z filtrowaniem/paginacją.
8. Pokaż `/staff/login` jako pełny ekran ról pracowniczych.
9. Wejdź na `/account/login` i pokaż, że publiczny login klienta nie pokazuje już przycisków ról staff.

## Show preview - kod

Najlepsza kolejność pokazu w kodzie:

1. `docker-compose.yml` - kolejność startu `sqlserver -> sqlserver-init -> web`.
2. `docker/sqlserver-init.sql` - tworzenie loginów `admin`, `pracownik`, `klient`.
3. `src/KuchniaUCygana.Infrastructure/DependencyInjection.cs` - `DefaultConnection` dla runtime i `MigrationConnection` dla migracji.
4. `src/KuchniaUCygana.Infrastructure/Persistence/Migrations/MigrationRunner.cs` - migracje przez `MigrationConnection`.
5. `src/KuchniaUCygana.Infrastructure/Persistence/Migrations/507_AddSbdSqlObjectsAndIndexes.cs` - trigger, procedura, funkcja i indeksy SBD.
6. `tests/KuchniaUCygana.Tests/Integration/Infrastructure/SqlServerSbdAuditTests.cs` - testy obiektów SQL i least privilege.
7. `src/KuchniaUCygana.Domain/Constants/AppRoles.cs` - lista ról staff.
8. `src/KuchniaUCygana.Web/Controllers/AccountController.cs` - dev login tylko w `Development`.
9. `src/KuchniaUCygana.Web/Views/Shared/_Layout.cshtml` - dev-only dropdown `DEV: Szybkie logowanie`.
10. `src/KuchniaUCygana.Web/Views/Account/StaffLogin.cshtml` - pełny ekran ról pracowniczych.

Komendy demo:

```powershell
docker compose --env-file .env.example ps --all
docker compose --env-file .env.example logs --tail 80 web sqlserver-init
dotnet test KuchniaUCygana.sln --no-restore --filter "FullyQualifiedName~SqlServerSbdAuditTests"
dotnet test KuchniaUCygana.sln --no-restore --filter "FullyQualifiedName!~Integration"
```

## Show preview - uprawnienia bazy danych (Least Privilege)

Różnicę w uprawnieniach pomiędzy kontem runtime (`pracownik`) a kontem administracyjnym (`admin`) można łatwo zademonstrować, łącząc się bezpośrednio z bazą danych (np. przez SSMS, Azure Data Studio lub narzędzie CLI `sqlcmd`):

### 1. Próba wykonania DDL jako `pracownik`
1. Połącz się z bazą danych używając loginu `pracownik` i hasła określonego w pliku `.env`.
2. Spróbuj utworzyć nową tabelę, wykonując zapytanie:
   ```sql
   CREATE TABLE TestPrivileges (Id INT);
   ```
3. **Wynik**: Operacja zostanie zablokowana przez SQL Server. Baza zwróci błąd uprawnień wskazujący na brak uprawnień do wykonania operacji `CREATE TABLE` (zgodnie z zasadą Least Privilege).

### 2. Wykonanie tego samego zapytania jako `admin`
1. Połącz się z tą samą bazą danych jako `admin` (używając odpowiedniego hasła z `.env`).
2. Wykonaj to samo zapytanie:
   ```sql
   CREATE TABLE TestPrivileges (Id INT);
   ```
3. **Wynik**: Tabela zostanie pomyślnie utworzona. Konto `admin` posiada rolę `db_owner`, która pozwala na pełne zarządzanie schematem bazy (DDL).

