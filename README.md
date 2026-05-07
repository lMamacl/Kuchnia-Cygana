# 🍽️ Kuchnia u Cygana

[![CI — Build, Test & Coverage](https://github.com/lMamacl/Kuchnia-Cygana/actions/workflows/ci.yml/badge.svg)](https://github.com/lMamacl/Kuchnia-Cygana/actions/workflows/ci.yml)

Platforma webowa dla firmy cateringowej łącząca **portal B2C** (zamawianie diet, płatności online) z **ERP back-office** (produkcja, magazyn, logistyka, HR, helpdesk).

> **Technologie:** ASP.NET MVC 8 · SQLite + OrmLite · FluentMigrator · Clean Architecture · HTMX 2.x · Alpine.js 3.x

---

## 📐 Architektura

Projekt stosuje **N-Tier / Clean Architecture** z czytelnym podziałem odpowiedzialności:

```
KuchniaUCygana.Web              ← ASP.NET MVC (Controllers, Views, wwwroot)
    ↓ używa DTOs
KuchniaUCygana.Application      ← BLL (Services, DTOs, AutoMapper, Validators)
    ↓ używa Interfejsów
KuchniaUCygana.Domain           ← Core (Entities, Repository Interfaces, Events)
    ↑ implementuje Interfejsy
KuchniaUCygana.Infrastructure   ← DAL (OrmLite, Migrations, External APIs, Cache)
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

---

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

### 2. Przywrócenie zależności

```bash
dotnet restore KuchniaUCygana.sln
```

### 3. Konfiguracja sekretów (user-secrets)

Projekt korzysta z `dotnet user-secrets` do przechowywania kluczy API lokalnie (poza repozytorium).

```powershell
# Zainicjuj user-secrets dla projektu Web
dotnet user-secrets init --project src/KuchniaUCygana.Web

# Opcjonalnie — klucze API (możesz pominąć jeśli nie testujesz integracji)
dotnet user-secrets set "Stripe:SecretKey" "sk_test_..." --project src/KuchniaUCygana.Web
dotnet user-secrets set "OpenAI:ApiKey" "sk-..." --project src/KuchniaUCygana.Web
```

> **Alternatywa:** Skopiuj `appsettings.Development.json.example` lub utwórz plik `src/KuchniaUCygana.Web/appsettings.Development.json` z wartościami. Plik ten jest w `.gitignore` i nie zostanie scommitowany.

### 4. Uruchomienie

```bash
dotnet run --project src/KuchniaUCygana.Web
```

Migracje SQLite uruchamiają się **automatycznie** przy starcie aplikacji. Baza `kuchniaucygana_dev.db` zostanie utworzona w katalogu projektu Web.

Aplikacja będzie dostępna pod: `https://localhost:5001` lub `http://localhost:5000`
(lub innym porcie, sprawdź w terminalu po uruchomieniu)

---

## 🐳 Docker

### 1. Przygotowanie zmiennych środowiskowych

```bash
cp .env.example .env
# Edytuj .env i uzupełnij wartości (STRIPE_SECRET, STRIPE_PUBLISHABLE, OPENAI_KEY)
```

### 2. Uruchomienie

```bash
docker compose up --build
```

Aplikacja będzie dostępna pod `http://localhost:8080`.

Dane SQLite persystują w named volume `sqlite_data`. Pliki uploadowane w `uploads_data`.

### 3. Zatrzymanie

```bash
docker compose down            # zatrzymaj kontenery
docker compose down -v         # zatrzymaj i usuń dane (volumes)
```

---

## 🧪 Testy

### Uruchomienie testów

```bash
dotnet test KuchniaUCygana.sln
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

### Zakresy migracji (per deweloper)

| Deweloper | Zakres numerów migracji |
|-----------|------------------------|
| DEV 01 | 100–199 |
| DEV 02 | 200–299 |
| DEV 03 | 001–099 (legacy) + 300–399 |
| DEV 04 | 400–499 |
| DEV 05 | 500–599 |

---

## 📖 Dokumentacja

| Dokument | Opis |
|----------|------|
| [`docs/architecture/ARCHITEKTURA_KuchniaUCygana.html`](docs/architecture/ARCHITEKTURA_KuchniaUCygana.html) | Pełna dokumentacja architektoniczna (otwórz w przeglądarce) |
| [`docs/PLAN_PROJEKTU.md`](docs/PLAN_PROJEKTU.md) | Ogólny roadmap projektu (7 faz) |
| [`docs/module-3/PLAN_MODUL_3.md`](docs/module-3/PLAN_MODUL_3.md) | Szczegółowy plan Modułu 3 |
| [`docs/Decisions_Log.md`](docs/Decisions_Log.md) | Dziennik decyzji architektonicznych |
| [`docs/guides/Developer_Manual.md`](docs/guides/Developer_Manual.md) | Podręcznik dewelopera (OPRÓCZ PLANU NAJWAŻNIEJSZE) |

---

## 🛠️ Narzędzia i Konwencje

- **ORM:** ServiceStack OrmLite (bliżej SQL niż EF Core, brak lazy loading — jawne JOINy)
- **Migracje:** FluentMigrator — jedyne źródło DDL (nie `db.CreateTableIfNotExists`!)
- **Mapowania:** AutoMapper — 1 plik Profile per moduł
- **Walidacja:** FluentValidation (serwer) + jQuery Validation Unobtrusive (klient)
- **Testy:** xUnit + Moq + FluentAssertions + Bogus
- **PDF:** QuestPDF (licencja Community)
- **Jakość kodu:** StyleCop + Roslynator + SonarAnalyzer (via `Directory.Build.props`)
- **Logowanie:** Serilog (Console + File)
- **Frontend:** HTMX 2.x (partial swap) + Alpine.js 3.x (lokalna reaktywność) + Bootstrap 5

---

## 🎨 Architektura Frontendowa

**Wybrany stack: Razor Views + HTMX + Alpine.js** (decyzja 2026-05-04)

```
B2C (klient):           _LayoutPublic.cshtml
                         Navbar | Body | Footer

ERP back-office:        _LayoutAdmin.cshtml
                         Sidebar | Breadcrumb | Body
```

### Zasady

- **Interaktywność bez reload** — HTMX pobiera fragmenty HTML (Partial Views) i wkleja je w DOM bez przeładowania strony
- **Lokalna reaktywność** — Alpine.js (`x-data`, `x-show`, `@click`) do modali, toggleów, potwierdzeń
- **Zero JS boilerplate dla CRUD** — filtry, paginacja, zatwierdzanie pozycji przez atrybuty `hx-*`
- **Walidacja** — FluentValidation (server) + jQuery Validation Unobtrusive (client); HTMX nie wysła formularza przed walidacją kliencką
- **CSRF** — jednorazowa konfiguracja globalna w `htmx-config.js`; wszystkie żądania HTMX automatycznie dostączają token

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
