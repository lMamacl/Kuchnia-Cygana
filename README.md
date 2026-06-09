# 🍽️ Kuchnia u Cygana

[![CI - Build, Test i Coverage](https://github.com/lMamacl/Kuchnia-Cygana/actions/workflows/ci.yml/badge.svg)](https://github.com/lMamacl/Kuchnia-Cygana/actions/workflows/ci.yml)

Platforma webowa dla firmy cateringowej łącząca **portal B2C** (zamawianie diet, płatności online) z **ERP back-office** (produkcja, magazyn, logistyka, HR, helpdesk).

> **Technologie:** ASP.NET MVC 8 · MS SQL Server · Dapper · HTMX 2.x · Alpine.js · Clean Architecture

---

## 🏗️ Stan Projektu (Czysta Baza)
Obecnie na gałęzi `develop` znajduje się **Czysty Szablon Architektoniczny**. Został on oczyszczony z logiki testowej, aby służyć jako solidny fundament pod budowę modułów. Posiada wbudowaną konfigurację **Cookie Authentication**, obsługę **HTMX** oraz gotowy pipeline CI/CD.

> [!IMPORTANT]
> Pełna implementacja **Modułu 3 (Magazyn i Produkcja)** znajduje się obecnie na dedykowanej gałęzi `Mamac`. Gałąź `develop` zawiera jedynie niezbędny baseline infrastrukturalny (SQL Server, Migracje).

---

## 📐 Architektura
- `KuchniaUCygana.Web` - UI ASP.NET MVC
- `KuchniaUCygana.Application` - logika aplikacyjna, DTO, mapowania
- `KuchniaUCygana.Domain` - encje i kontrakty domenowe
- `KuchniaUCygana.Infrastructure` - dostęp do danych, migracje, integracje zewnętrzne

Projekt stosuje **N-Tier / Clean Architecture** z czytelnym podziałem odpowiedzialności:

```
KuchniaUCygana.Web              ← ASP.NET MVC (Controllers, Views, wwwroot)
    ↓ używa DTOs
KuchniaUCygana.Application      ← BLL (Services, DTOs, AutoMapper, Validators)
    ↓ używa Interfejsów
KuchniaUCygana.Domain           ← Core (Entities, Repository Interfaces, Events)
    ↑ implementuje Interfejsy
KuchniaUCygana.Infrastructure   ← DAL (Dapper, Migrations, External APIs, Cache)
```

**Zasada:** Web → Application → Domain ← Infrastructure. Żadna warstwa nie może znać szczegółów warstwy powyżej.

---

## 🗂️ Moduły Funkcjonalne

| # | Moduł | Deweloper | Opis |
|---|-------|-----------|------|
| 1 | **E-commerce i Zamówienia** | DEV 01 | Klient zamawia diety, zarządza dostawami, płaci online (Stripe) |
| 2 | **Katalog Diet i Receptur** | DEV 02 | Diety, warianty kaloryczne, posiłki, składniki, alergeny, AI opisy (OpenAI) |
| 3 | **Produkcja, Kompletacja i Magazyn** | DEV 03 | Plan produkcji, FEFO, HACCP, pakowanie, etykiety, Smart Inventory |
| 4 | **Logistyka i Dostawy** | DEV 04 | Trasy, kierowcy, pojazdy, geokodowanie (OpenStreetMap) |
| 5 | **Administracja, HR i Komunikacja** | DEV 05 | Tickety, grafiki pracy, zarządzanie użytkownikami |

## Konta testowe i role

Seeder tworzy poniższe konta aplikacyjne w profilach `MinimalRealistic` i `DemoData`.
Logowanie pracownicze korzysta ze zwykłego formularza `/Account/Login` albo `/staff/login`;
hasło jest weryfikowane przeciwko `PasswordHash` w tabeli `Users`.

Rejestracja klienta działa przez `/Account/Register`: tworzy nowe konto z rolą `Client`,
normalizuje email i zapisuje hasło jako hash BCrypt. Role pracownicze nie mają samodzielnej
rejestracji; do testów korzystają z kont seedowanych poniżej.

### Konta bazowe (`MinimalRealistic`)

| Rola | Email | Hasło | Moduł / użycie |
|------|-------|-------|----------------|
| `Admin` | `admin@kuchnia.local` | `Admin123!` | Administracja, pełny dostęp |
| `Kitchen` | `kitchen@kuchnia.local` | `Kitchen123!` | M3 produkcja |
| `KitchenManager` | `kitchenm@kuchnia.local` | `Kitchen123!` | M3 kierownik produkcji |
| `Warehouse` | `warehouse@kuchnia.local` | `Warehouse123!` | M3 magazyn |
| `WarehouseManager` | `warehousem@kuchnia.local` | `Warehouse123!` | M3 kierownik magazynu |
| `Packing` | `packing@kuchnia.local` | `Packing123!` | M3 kompletacja |
| `PackingManager` | `packingm@kuchnia.local` | `Packing123!` | M3 kierownik kompletacji |
| `Dietitian` | `dietitian@kuchnia.local` | `Diet123!` | M2 katalog diet i receptur |
| `Logistics` | `logistics@kuchnia.local` | `Logistics123!` | M4 logistyka |
| `LogisticsManager` | `logisticsm@kuchnia.local` | `Logistics123!` | M4 kierownik logistyki |
| `Driver` | `driver@kuchnia.local` | `Driver123!` | M4 kierowca |
| `DriverManager` | `driverm@kuchnia.local` | `Driver123!` | M4 koordynator kierowców |
| `HR` | `hr@kuchnia.local` | `HR123!` | M5 HR |
| `HRManager` | `hrm@kuchnia.local` | `HR123!` | M5 kierownik HR |
| `BOK` | `bok@kuchnia.local` | `BOK123!` | M5 obsługa klienta |
| `BOKManager` | `bokm@kuchnia.local` | `BOK123!` | M5 kierownik BOK |

### Konta demo (`DemoData`)

| Rola | Email | Hasło | Użycie |
|------|-------|-------|--------|
| `Client` | `demo-klient-01@kuchnia.local` ... `demo-klient-15@kuchnia.local` | `Demo123!` | Wspólni klienci M1/M2/M3/M4 z seedowanych zamówień |
| `Driver` | `driver2@kuchnia.local` | `Driver123!` | Dodatkowy kierowca scenariusza tras M4 |
| `Driver` | `driver3@kuchnia.local` | `Driver123!` | Dodatkowy kierowca scenariusza tras M4 |

Loginy SQL Server nie są kontami aplikacji. Docker tworzy loginy `admin`, `pracownik` i `klient`
z hasłami z `.env`; testy integracyjne Testcontainers używają odpowiednio `Admin123!Integration`,
`Pracownik123!Integration` i `Klient123!Integration`.

---

## 🚀 Wymagania Systemowe

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (8.0.x)
- [Docker](https://docs.docker.com/get-docker/) (opcjonalnie, do konteneryzacji)
- Visual Studio 2022+ / VS Code / Rider

---

## ⚡ Szybki Start (lokalne uruchomienie)

### 1. Klonowanie repozytorium

```bash
git clone https://github.com/lMamacl/Kuchnia-Cygana.git
cd Kuchnia-Cygana
```

> **⚠️ UWAGA — WAŻNE:** Wszystkie poniższe komendy (`dotnet`, `docker-compose`) **muszą** być uruchamiane z poziomu **głównego katalogu repozytorium** (tam, gdzie znajduje się plik `KuchniaUCygana.sln`), a **NIE** wewnątrz folderu `src/KuchniaUCygana.Web`!

### 2. Przygotowanie zmiennych środowiskowych

```bash
cp .env.example .env
```
W pliku `.env` uzupełnij następujące pola:

```
- `MSSQL_SA_PASSWORD` = Twoje super tajne hasło!
- `MSSQL_DB_NAME` = `KuchniaUCygana`
- opcjonalnie `MSSQL_PORT` = `1433`
- `MSSQL_MEMORY_LIMIT_MB` = `1536` (limit RAM dla silnika SQL)
- `MSSQL_CONTAINER_MEMORY_LIMIT` = `2g` (limit dla kontenera SQL)
- `WEB_CONTAINER_MEMORY_LIMIT` = `768m` (limit dla kontenera Web)
```
# Opcjonalnie — klucze API (możesz pominąć jeśli nie testujesz integracji)
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project src/KuchniaUCygana.Web
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project src/KuchniaUCygana.Web

> **Alternatywa:** Skopiuj `appsettings.Development.json.example` or utwórz plik `src/KuchniaUCygana.Web/appsettings.Development.json` z wartościami. Plik ten jest w `.gitignore` i nie zostanie scommitowany.
> Uwaga: `.env` zawiera sekrety i nie może być commitowany do repozytorium.

### 3. Przywrócenie zależności

```bash
dotnet restore KuchniaUCygana.sln
```

---

## 🐳 Docker


### 4. Uruchomienie w Dockerze (zalecane)

```bash
docker compose up --build
```

Po starcie aplikacja jest dostępna pod `http://localhost:8080`.

Dane SQL Server persystują w named volume `mssql_data`. Pliki uploadowane w `uploads_data`.

### 4a. Visual Studio vs Docker: dlaczego czasem widzisz inny frontend

Visual Studio uruchamia aplikację bezpośrednio z aktualnych plików źródłowych, zwykle pod `https://localhost:7296/`.
Docker pod `http://localhost:8080` uruchamia ostatnio zbudowany obraz kontenera `web`. Jeżeli zmieniły się pliki Razor,
CSS albo JavaScript, ale obraz nie został przebudowany, kontener może nadal pokazywać stary wygląd strony.

Dla zmian frontendowych najczęściej wystarczy przebudować i odtworzyć tylko kontener aplikacji:

```bash
docker compose up -d --build --force-recreate web
```

Nie czyść wtedy bazy danych. `docker compose down -v` usuwa named volume `mssql_data`, czyli kasuje lokalne dane SQL
Server. Używaj go tylko wtedy, gdy celowo potrzebujesz czystej bazy, naprawiasz migracje albo chcesz wykonać pełny
greenfield rebuild. Zmiany w widokach, layoutach, plikach `wwwroot/css` i `wwwroot/js` nie wymagają przebudowy bazy.

### 5. Zatrzymanie

```bash
docker compose down            # zatrzymaj kontenery
docker compose down -v         # zatrzymaj i usuń dane (volumes)
```
### Pełny reset danych (greenfield rebuild)

```bash
docker compose down -v --remove-orphans
docker compose up --build
```

Co robi reset:
- usuwa kontenery i sieci projektu,
- usuwa volume `mssql_data` (tracisz wszystkie dane bazy),
- usuwa osierocone kontenery compose,
- po ponownym starcie tworzy czystą bazę od zera przez migracje.

---

## 💻 Uruchomienie lokalne bez Dockera

Możesz uruchomić aplikację lokalnie, ale baza dalej powinna wskazywać na SQL Server.

1. Przygotuj `src/KuchniaUCygana.Web/appsettings.Development.json` (na bazie `appsettings.Development.json.example`).
2. Ustaw poprawny `ConnectionStrings:DefaultConnection` i opcjonalnie `ConnectionStrings:MigrationConnection` do SQL Server.
3. Uruchom:
```bash
dotnet run --project src/KuchniaUCygana.Web
```

---

## Baza danych, migracje i seeding

- Strategia danych: **greenfield-only** (bez migracji danych ze SQLite).
- Kolejność startu Dockera: `SQL Server healthy -> SQL init loginów -> MigrateUp -> Seed (warunkowo) -> Start aplikacji`.
- Migracje są uruchamiane automatycznie przy starcie aplikacji.
- Seeding w startupie działa tylko dla `Development` i `Test`.
- W `Production` startup seeding jest wyłączony.
- `ConnectionStrings:DefaultConnection` jest runtime Dappera i powinien używać ograniczonego loginu `pracownik`.
- `ConnectionStrings:MigrationConnection` jest używany przez FluentMigrator i powinien używać loginu `admin`.
- Docker tworzy loginy SQL Server `admin`, `pracownik` i `klient` przez `docker/sqlserver-init.sql`; uzupełnij hasła w `.env` na bazie `.env.example`.
- Seed wydajnościowy 100k+ oraz dokument/prezentacja z porównaniem zapytania przed/po optymalizacji są poza bieżącym zakresem.

### Status SBD po domknięciu audytu

- Runtime aplikacji nie używa już `sa`; `sa` zostaje wyłącznie do bootstrapu SQL Server w Dockerze/Testcontainers.
- Migracje używają `MigrationConnection` (`admin`), a runtime Dappera używa `DefaultConnection` (`pracownik`).
- Login `klient` jest tworzony z ograniczonym DML jako przygotowanie infrastrukturalne, ale nie jest jeszcze przełączany per request.
- Fizyczne obiekty T-SQL dla SBD są w migracji `507_AddSbdSqlObjectsAndIndexes`: `tr_Batches_UpdateIsDepleted`, `usp_ArchiveSystemLogs`, `fn_MealNutritionCost` i `SystemLogsArchive`.
- Auth jest świadomie w trybie preview: `Login` i `Register` nie wykonują pełnego produkcyjnego uwierzytelniania, a pracowniczy dev-login jest wydzielony pod `/staff/login` tylko dla `Development`.

### Aktualne przewodniki po zmianach i uruchomieniu

- [Overview zmian 2026-06-02 i show preview](docs/guides/ZMIANY_2026-06-02_OVERVIEW_I_SHOWCASE.md)
- [Windows Docker SQL Server setup checklist](docs/guides/WINDOWS_DOCKER_SQLSERVER_SETUP_CHECKLIST.md)

Ręczne uruchomienie seedingu:
```bash
dotnet run --project src/KuchniaUCygana.Web -- seed
```


## 🧪 Testy

### Uruchomienie testów

```bash
dotnet test KuchniaUCygana.sln
```

### Tylko testy integracyjne:
```bash
dotnet test KuchniaUCygana.sln --filter "Category=Integration"
```
### Tylko testy jednostkowe:
```bash
dotnet test KuchniaUCygana.sln --filter "Category!=Integration"
```

### Z raportem pokrycia kodu

```bash
dotnet test KuchniaUCygana.sln --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

Raport Cobertura XML znajdziesz w `./TestResults/`. Wymagane pokrycie: **≥ 70%** głównej logiki aplikacji.

### Struktura testów
```
tests/KuchniaUCygana.Tests/
├── Unit/           # Testy jednostkowe (Services, Validators, Mappings)
├── Integration/    # Testy integracyjne (Repositories, Controllers)
├── Domain/         # Testy logiki domenowej
├── Fixtures/       # Test fixtures i konfiguracja
└── TestData/       # Seedery testowe (Bogus)
```
---


## 🔄 Workflow Zespołu

```
main ← develop ← feature/devX-opis-zadania
```

1. **Praca na branchach** `feature/devX-*` (np. `feature/dev3-warehouse-entities`) od `develop`
2. **Pull Request:** `feature/* → develop` — wymaga przejścia CI i code review
3. **`main`** aktualizowany tylko przez merge z `develop`
4. **Pliki współdzielone** (`DependencyInjection.cs`, migracje) — uzgodnij z właścicielem

### Standard web/back-office dla branchy modułowych

Panel pracowniczy/back-office korzysta ze wspólnego shellu Tabler. Nowe testowe widoki operacyjne powinny być
dopięte pod `_LayoutStaff`, a nie przez osobne layouty ani przez przebudowę `_Layout.cshtml` klienta B2C.

- Widoki staff/back-office: `Layout = "_LayoutStaff";`.
- Nawigacja: dopisz widok w `StaffNavigationCatalog`, zamiast ręcznie edytować kilka menu naraz.
- Ikony: korzystaj z `StaffIconCatalog`; jeżeli brakuje ikony, dopisz klucz tam.
- Style i skrypty shellu: `wwwroot/css/staff.css` oraz `wwwroot/js/staff-shell.js`.
- Dla samych mocków/widoków nie zmieniaj `DependencyInjection.cs`, `Program.cs`, migracji ani `docker-compose.yml`.
- Jeżeli Twój moduł ma realny serwis/repozytorium, dopisuj rejestrację DI addytywnie. Nie podmieniaj całego pliku.

### Branch integration rules

- `develop` przyjmuje tylko kompatybilną infrastrukturę DB na Dapperze, dokumentację i testy bazowe.
- `Mamac` zostawia implementacje Modułu 3: Warehouse, Production, Packing, widoki, seeding demo i testy M3.
- Branche deweloperskie po aktualizacji `develop` robią rebase albo merge i migrują własne repozytoria według `docs/guides/dapper-migration-guide.md`.
- Nie przenoś pełnym merge'em branchy, które usuwają cudze kontrolery, widoki lub konfigurację Web; wybieraj cherry-pick właściwych plików.

### 🚀 Sygnały poprawnego startu
Po uruchomieniu `docker compose up`, w logach powinieneś zobaczyć:
- `Container kuchnia_sqlserver Healthy` — SQL Server jest gotowy.
- `Starting up database 'KuchniaUCygana'` — Migracje zostały pomyślnie wykonane.
- `Application started. Press Ctrl+C to shut down.` — Aplikacja Web działa.

### Zakresy migracji (per deweloper)

| Deweloper | Zakres numerów migracji |
|-----------|------------------------|
| DEV 01 | 100–199 |
| DEV 02 | 200–299 |
| DEV 03 | 001–099 (legacy) + 300–399 |
| DEV 04 | 400–499 |
| DEV 05 | 500–599 |


## 📖 Dokumentacja

| Dokument | Opis |
|----------|------|
| [`docs/architecture/ARCHITEKTURA_KuchniaUCygana.html`](docs/architecture/ARCHITEKTURA_KuchniaUCygana.html) | Pełna dokumentacja architektoniczna (otwórz w przeglądarce) |
| [`docs/PLAN_PROJEKTU.md`](docs/PLAN_PROJEKTU.md) | Ogólny roadmap projektu (7 faz) |
| [`docs/module-3/PLAN_MODUL_3.md`](docs/module-3/PLAN_MODUL_3.md) | Szczegółowy plan Modułu 3 |
| [`docs/Decisions_Log.md`](docs/Decisions_Log.md) | Dziennik decyzji architektonicznych |
| [`docs/guides/Developer_Manual.md`](docs/guides/Developer_Manual.md) | Podręcznik dewelopera (OPRÓCZ PLANU NAJWAŻNIEJSZE) |
| [`docs/guides/dapper-migration-guide.md`](docs/guides/dapper-migration-guide.md) | Wzorzec migracji repozytoriów i zasady integracji branchy |

## 🛠️ Narzędzia i Konwencje
Wszystkie testy:

- **Dostęp do danych:** Dapper (bliżej SQL niż EF Core, brak lazy loading — jawne JOINy)
- **Migracje:** FluentMigrator — jedyne źródło DDL (nie `db.CreateTableIfNotExists`!)
- **Mapowania:** AutoMapper — 1 plik Profile per moduł
- **Walidacja:** FluentValidation (serwer) + jQuery Validation Unobtrusive (klient)
- **Testy:** xUnit + Moq + FluentAssertions + Bogus
- **PDF:** QuestPDF (licencja Community)
- **Jakość kodu:** StyleCop + Roslynator + SonarAnalyzer (via `Directory.Build.props`)
- **Logowanie:** Serilog (Console + File)
- **Frontend:** HTMX 2.x (partial swap) + Alpine.js 3.x (lokalna reaktywność) + Bootstrap 5

## 🎨 Architektura Frontendowa

**Wybrany stack: Razor Views + HTMX + Alpine.js** (decyzja 2026-05-04)

B2C (klient):           _LayoutPublic.cshtml
                         Navbar | Body | Footer

ERP back-office:        _LayoutAdmin.cshtml
                         Sidebar | Breadcrumb | Body

### Zasady

- **Interaktywność bez reload** — HTMX pobiera fragmenty HTML (Partial Views) i wkleja je w DOM bez przeładowania strony
- **Lokalna reaktywność** — Alpine.js (`x-data`, `x-show`, `@click`) do modali, toggleów, potwierdzeń
- **Zero JS boilerplate dla CRUD** — filtry, paginacja, zatwierdzanie pozycji przez atrybuty `hx-*`
- **Walidacja** — FluentValidation (serwer) + jQuery Validation Unobtrusive (klient); HTMX nie wysyła formularza przed walidacją kliencką
- **CSRF** — jednorazowa konfiguracja globalna w `htmx-config.js`; wszystkie żądania HTMX automatycznie dostarczają token

### Wzorzec kontrolera (dual response)

```csharp
// Akcja zwraca pełną stronę LUB fragment — zależnie od żądania
public async Task<IActionResult> PlanItems(DateOnly date)
{
    var items = await _productionService.GetPlanItemsAsync(date);
    if (Request.Headers.ContainsKey("HX-Request"))
        return PartialView("_PlanItemsTable", items); // HTMX — fragment
    return View(items);                                // przeglądarka — cała strona
}
```

### Wzorzec widoku (HTMX)

```html
<!-- Filtr dat — aktualizuje tylko tabelę -->
<input type="date" hx-get="/Production/PlanItems"
       hx-target="#plan-table" hx-trigger="change" />
<div id="plan-table">
    @await Html.PartialAsync("_PlanItemsTable", Model.Items)
</div>
```

> Pełne porównanie Razor+jQuery vs Razor+HTMX: [`docs/frontend-comparison.html`](docs/frontend-comparison.html)
