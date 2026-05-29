# 🍽️ Kuchnia u Cygana

[![CI - Build, Test i Coverage](https://github.com/lMamacl/Kuchnia-Cygana/actions/workflows/ci.yml/badge.svg)](https://github.com/lMamacl/Kuchnia-Cygana/actions/workflows/ci.yml)

Platforma webowa dla firmy cateringowej łącząca **portal B2C** (zamawianie diet, płatności online) z **ERP back-office** (produkcja, magazyn, logistyka, HR, helpdesk).

> **Technologie:** ASP.NET MVC 8 · MS SQL Server · Dapper · FluentMigrator · HTMX 2.x · Alpine.js · Clean Architecture

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
- `KuchniaUCygana.Infrastructure` - dostep do danych, migracje, integracje zewnetrzne

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

## 🔐 Role w Systemie

| Rola | Dostęp |
|------|--------|
| `Client` | Portal B2C — zamówienia, profil, płatności |
| `Kitchen` | Panel kuchni — produkcja, kompletacja, magazyn |
| `Driver` | Panel kierowcy — trasy, dostawy |
| `Admin` | Pełny dostęp — administracja, raporty, HR |

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

### 2. Przygotowanie zmiennych srodowiskowych

```bash
cp .env.example .env
```
W pliku `.env` uzupelnij nastepujace pola:

```
- `MSSQL_SA_PASSWORD` = Twoje super tajne hasło!
- `MSSQL_DB_NAME` = `KuchniaUCygana`
- opcjonalnie `MSSQL_PORT` = `1433`
- `MSSQL_MEMORY_LIMIT_MB` = `768` (limit RAM dla silnika SQL)
- `MSSQL_CONTAINER_MEMORY_LIMIT` = `1g` (limit dla kontenera SQL)
- `WEB_CONTAINER_MEMORY_LIMIT` = `512m` (limit dla kontenera Web)
```
# Opcjonalnie — klucze API (możesz pominąć jeśli nie testujesz integracji)
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project src/KuchniaUCygana.Web
dotnet user-secrets set "Stripe:PublishableKey" "pk_test_..." --project src/KuchniaUCygana.Web
dotnet user-secrets set "Stripe:WebhookSecret" "whsec_..." --project src/KuchniaUCygana.Web
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project src/KuchniaUCygana.Web

> **Alternatywa:** Skopiuj `appsettings.Development.json.example` lub utwórz plik `src/KuchniaUCygana.Web/appsettings.Development.json` z wartościami. Plik ten jest w `.gitignore` i nie zostanie scommitowany.
> Uwaga: `.env` zawiera sekrety i nie moze byc commitowany do repozytorium.

### 3. Przywrocenie zaleznosci

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

## Uruchomienie lokalne bez Dockera

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
- po ponownym starcie tworzy czysta baze od zera przez migracje.

### 6. Uruchomienie lokalne bez Dockera


Mozesz uruchomic aplikacje lokalnie, ale baza dalej powinna wskazywac na SQL Server.

1. Przygotuj `src/KuchniaUCygana.Web/appsettings.Development.json` (na bazie `appsettings.Development.json.example`).
2. Ustaw poprawny `ConnectionStrings:DefaultConnection` do SQL Server.
3. Uruchom:
```bash
dotnet run --project src/KuchniaUCygana.Web
```

---

## Baza danych, migracje i seeding

- Strategia danych: **greenfield-only** (bez migracji danych ze SQLite).
- Kolejnosc startu runtime: `MigrateUp -> Seed (warunkowo) -> Start aplikacji`.
- Migracje sa uruchamiane automatycznie przy starcie aplikacji.
- Seeding w startupie dziala tylko dla `Development` i `Test`.
- W `Production` startup seeding jest wylaczony.

Reczne uruchomienie seedingu:
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

## 🛠️ Narzędzia i Konwencje
Wszystkie testy:

- **Dostęp do danych:** Dapper (blisko SQL, brak lazy loading — jawne JOINy)
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
- **Walidacja** — FluentValidation (server) + jQuery Validation Unobtrusive (client); HTMX nie wysła formularza przed walidacją kliencką
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
