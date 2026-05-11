# PLAN PROJEKTU — Kuchnia u Cygana

> **Wersja:** 1.0  
> **Data:** 2026-04-27  
> **Autor:** Architect / AI  
> **Status:** Proposed

---

## Legenda Statusów

| Emoji | Status | Znaczenie |
|-------|--------|-----------|
| ⬜ | Do zrobienia | Nie istnieje w żadnym branchu |
| 🔨 | W feature | Działa na branchu `feature/*`, jeszcze nie zmergowane |
| ✅ | W develop | Zmergowane do `develop`, działa z resztą projektu |

> Statusy mapują się 1:1 na git flow. Prawda o stanie kodu żyje w gicie, nie w markdownie.

---

## 1. Kontekst i Cel

Projekt „Kuchnia u Cygana" to platforma webowa (B2C e-commerce + ERP back-office) dla firmy cateringowej. System składa się z **5 modułów domenowych** rozwijanych przez 5 deweloperów, osadzonych w architekturze N-Tier / Clean Architecture (ASP.NET MVC 8, SQLite + OrmLite, FluentMigrator).

**Decyzja frontendowa (2026-05-04):** Wybrany stack to **Razor Views + HTMX + Alpine.js** (Opcja B). Szczegółowe porównanie z klasycznym jQuery w [`docs/frontend-comparison.html`](./frontend-comparison.html).

Ten dokument definiuje **kolejność realizacji komponentów** — od infrastruktury po wdrożenie — z uzasadnieniem opartym na współzależnościach.

---

## 2. Mapa Modułów i Współzależności

```
         ┌──────────────────────────────────────────────────────────────┐
         │                     INFRASTRUKTURA BAZOWA                     │
         │  Docker · CI/CD · DB Schema · Auth · DI · Bazowe Repozytoria │
         └──────────────────┬───────────────────────────────────────────┘
                            │ blokuje wszystko
         ┌──────────────────▼───────────────────────────────────────────┐
         │          MODUŁ 0: Wspólna Warstwa Domenowa (Domain Core)     │
         │  BaseEntity · AuditableEntity · IRepository · Enums · Events │
         └──────┬───────────┬────────────┬────────────┬────────────────┘
                │           │            │            │
        ┌───────▼──┐  ┌─────▼─────┐ ┌────▼────┐ ┌────▼──────────┐
        │ MODUŁ 1  │  │ MODUŁ 2   │ │MODUŁ 5  │ │  MODUŁ 3      │
        │E-commerce│  │ Katalog   │ │Admin/HR │ │Produkcja/WMS  │
        │Zamówienia│  │ Diet      │ │Helpdesk │ │Magazyn        │
        └────┬─────┘  └─────┬─────┘ └─────────┘ └───┬───────────┘
             │              │                         │
             │  dane diet   │  receptury ──▶ zapotrzebowanie   
             └──────┬───────┘                         │
                    │                          ┌──────▼────────┐
                    └──────────────────────────▶│   MODUŁ 4     │
                           zamówienia ──▶      │  Logistyka     │
                                               │  i Dostawy    │
                                               └───────────────┘
```

### Kluczowe zależności kierunkowe

| Moduł źródłowy | Moduł docelowy | Co przekazuje |
|---|---|---|
| Moduł 1 (Zamówienia) | Moduł 3 (Produkcja) | Aktywne zamówienia → generowanie planu produkcji |
| Moduł 2 (Katalog) | Moduł 3 (Produkcja) | Receptury posiłków, składniki → zapotrzebowanie materiałowe |
| Moduł 3 (Produkcja) | Moduł 4 (Logistyka) | Skompletowane paczki, zestawienia → załadunek i trasy |
| Moduł 1 (Zamówienia) | Moduł 4 (Logistyka) | Adresy dostaw, kalendarz → planowanie tras |
| Moduł 5 (Admin) | Wszystkie | Zarządzanie użytkownikami, rolami, ticketami |

---

## 3. Fazy Realizacji

### FAZA 0 — Infrastruktura i Środowisko Deweloperskie
**Priorytet:** 🔴 KRYTYCZNY — blokuje całą pracę  
**Czas:** Tydzień 1

| # | Zadanie | Status |
|---|---------|--------|
| 0.1 | Docker: Dockerfile + docker-compose z named volumes (MSSQL + uploads) | ✅ |
| 0.2 | `.editorconfig`, `stylecop.json`, `Directory.Build.props` — jakość kodu | ✅ |
| 0.3 | GitHub Actions CI (`ci.yml`): build + test + coverage | ✅ (XPlat Code Coverage + artifact upload) |
| 0.4 | `.env` / `user-secrets` — zarządzanie sekretami | ✅ (UserSecretsId + .env.example + appsettings.Development.json.example) |
| 0.5 | `README.md` — instrukcja uruchomienia projektu | ✅ (pełna instrukcja: dotnet, Docker, user-secrets, testy, workflow) |

### FAZA 1 — Domain Core + Baza Danych
**Priorytet:** 🔴 KRYTYCZNY — fundament wszystkich modułów  
**Czas:** Tydzień 1–2

| # | Zadanie | Status |
|---|---------|--------|
| 1.1 | `BaseEntity<TId>`, `AuditableEntity<TId>`, `ISoftDeletable` | ✅ |
| 1.2 | `IRepository<T, TId>` generyczny + `BaseRepository` z Soft Delete | ✅ |
| 1.3 | `IDbConnectionFactory` + `SqliteConnectionFactory` | ✅ |
| 1.4 | `MigrationRunner` — automatyczne uruchamianie migracji | ✅ |
| 1.5 | Migration `001_CreateUsersTable` | ✅ |
| 1.6 | DI rejestracja (`AddInfrastructure`) | ✅ |
| 1.7 | Domain Events: `IDomainEvent`, infrastruktura dispatching | ✅ |
| 1.8 | `UserRoles` enum + `ClaimsHelper` + konfiguracja Cookie Authentication (bez JWT) | ✅ |

### FAZA 2 — Moduły Niezależne (równoległy rozwój)
**Priorytet:** 🟡 WYSOKI  
**Czas:** Tydzień 2–5

Moduły 1, 2 i 5 mogą być rozwijane **równolegle**, ponieważ mają minimalne zależności wzajemne na etapie DB i repozytoriów.

#### Moduł 1: E-commerce i Zamówienia (DEV 01)
| # | Zadanie |
|---|---------|
| 2.1.1 | Encje: `Order`, `OrderItem`, `DeliveryCalendar`, `Address` |
| 2.1.2 | Migracje dla tabel zamówieniowych |
| 2.1.3 | Repozytoria: `IOrderRepository`, `OrderRepository` |
| 2.1.4 | DTOs + AutoMapper Profile (`OrderProfile`) |
| 2.1.5 | Serwisy: `IOrderService`, `OrderService` |
| 2.1.6 | Walidatory FluentValidation |
| 2.1.7 | Stripe: `IPaymentService` → `StripePaymentService` |
| 2.1.8 | Controllers + Views (Cart, Order, Account) |

#### Moduł 2: Katalog Diet i Receptur (DEV 02)
| # | Zadanie |
|---|---------|
| 2.2.1 | Encje: `Diet`, `DietVariant`, `Meal`, `Ingredient`, `Recipe`, `Allergen`, `MealAllergen`, `DietVariantMeal` |
| 2.2.2 | Migracje (ManyToMany: Recipe, MealAllergen, DietVariantMeal) |
| 2.2.3 | Repozytoria z JOINami (OrmLite explicit loading) |
| 2.2.4 | DTOs + AutoMapper (`DietProfile`) |
| 2.2.5 | Serwisy: `IDietService`, `IMealService` |
| 2.2.6 | OpenAI: `IAiDescriptionService` → generowanie opisów posiłków |
| 2.2.7 | Controllers + Views (Diet, Meal) |

#### Moduł 5: Administracja, HR i Komunikacja (DEV 05)
| # | Zadanie |
|---|---------|
| 2.5.1 | Encje: `Ticket`, `TicketAttachment`, `WorkSchedule` |
| 2.5.2 | Migracje |
| 2.5.3 | Serwisy: `ITicketService`, `IWorkScheduleService` |
| 2.5.4 | Upload plików: `IFileStorageService` (załączniki ticketów) |
| 2.5.5 | Controllers + Views (Admin, Ticket) |

### FAZA 3 — Moduł 3: Produkcja, Kompletacja i Magazyn (DEV 03)
**Priorytet:** 🟡 WYSOKI  
**Czas:** Tydzień 3–6  
**Zależności:** Wymaga mocków/interfejsów z Modułu 1 (zamówienia) i Modułu 2 (receptury)

> ⚠️ Szczegółowy plan w [PLAN_MODUL_3.md](./module-3/PLAN_MODUL_3.md)

| # | Zadanie | Status |
|---|---------|--------|
| 3.1 | Encje magazynowe: `StockItem`, `Batch`, `InventoryTransaction`, `TemperatureLog`, `UnitOfMeasure` | 🔨 |
| 3.2 | Migration `002_CreateWarehouseTables` | 🔨 |
| 3.3 | Repozytoria: `IBatchRepository`, `IInventoryTransactionRepository` | 🔨 |
| 3.4 | Encje produkcyjne: `ProductionPlan`, `ProductionPlanItem`, `ProductionBatch` | ⬜ |
| 3.5 | Encje kompletacji: `PackingSession`, `PackingItem`, `PackingLabel` | ⬜ |
| 3.6 | Serwisy domenowe: FEFO, Food Cost, Smart Inventory | ⬜ |
| 3.7 | DTOs + AutoMapper (`ProductionProfile`, `WarehouseProfile`) | ⬜ |
| 3.8 | Serwisy aplikacyjne: `IProductionService`, `IWarehouseService` | ⬜ |
| 3.9 | QuestPDF: Karta Produkcyjna (PDF) | ⬜ |
| 3.10 | Walidatory + testy | ⬜ |
| 3.11 | Controllers + Views (Production, Warehouse) | ⬜ |

### FAZA 4 — Moduł 4: Logistyka i Dostawy (DEV 04)
**Priorytet:** 🟡 WYSOKI  
**Czas:** Tydzień 5–7  
**Zależności:** Wymaga danych z Modułu 1 (adresy) i Modułu 3 (paczki)

| # | Zadanie |
|---|---------|
| 4.1 | Encje: `Vehicle`, `Driver`, `DeliveryRoute`, `DeliveryRouteStop` |
| 4.2 | Migracje |
| 4.3 | OpenStreetMap: geokodowanie adresów, OSRM (opcjonalnie) |
| 4.4 | QuestPDF: Etykiety przewozowe |
| 4.5 | Serwisy: `IRouteService`, `IDeliveryService` |
| 4.6 | Controllers + Views (Logistics) |

### FAZA 5 — Integracja i Testy End-to-End
**Priorytet:** 🟢 STANDARD  
**Czas:** Tydzień 7–8

| # | Zadanie |
|---|---------|
| 5.1 | Zastąpienie mocków rzeczywistymi interfejsami między modułami |
| 5.2 | Testy integracyjne (WebApplicationFactory) |
| 5.3 | Seedowanie: `UserSeeder`, `DietSeeder`, `OrderSeeder` (Bogus) |
| 5.4 | Testy pokrycia ≥ 70% (coverlet) |
| 5.5 | Audyt StyleCop / Roslynator / SonarAnalyzer |

### FAZA 6 — Frontend, Finalizacja i Wdrożenie
**Priorytet:** 🟢 STANDARD  
**Czas:** Tydzień 8–9  
**Stack frontendowy:** Razor Views + **HTMX 2.x** + **Alpine.js 3.x** + Bootstrap 5

> Wybrana Opcja B — partial swap bez przeładowania strony, zero JS boilerplate dla CRUD.
> Porównanie z jQuery: [`docs/frontend-comparison.html`](./frontend-comparison.html)

| # | Zadanie | Status |
|---|---------|--------|
| 6.1 | `_Layout.cshtml` + `_LayoutAdmin.cshtml` — dwa layouty (B2C / ERP), responsywność | ⬜ |
| 6.2 | `wwwroot/lib/`: dodanie `htmx.min.js` + `alpine.min.js` (CDN lub lokalne) | ⬜ |
| 6.3 | `wwwroot/js/htmx-config.js` — globalna konfiguracja HTMX (CSRF token, error handling) | ⬜ |
| 6.4 | `wwwroot/css/site.css` — design system (kolory, typografia, komponenty) | ⬜ |
| 6.5 | Walidacja front-end: jQuery Validation Unobtrusive (kompatybilna z HTMX) | ⬜ |
| 6.6 | Partial Views dla modułów ERP — fragmenty HTML zwracane przez akcje HTMX | ⬜ |
| 6.7 | Upload plików: `IFormFile` + `hx-encoding="multipart/form-data"` | ⬜ |
| 6.8 | Wdrożenie: Azure App Service (Free F1 / Basic B1) via GitHub Actions | ⬜ |
| 6.9 | Zmienne środowiskowe na produkcji (Azure App Settings) | ⬜ |
| 6.10 | Dokumentacja finalna |  ⬜ |

---

## 4. Uzasadnienie Kolejności

### Dlaczego ta kolejność?

1. **FAZA 0+1 (Infra + Domain Core) MUSZĄ być pierwsze** — bez nich żaden moduł nie może:
   - Połączyć się z bazą
   - Zdefiniować encji (brak BaseEntity)
   - Zarejestrować zależności (brak DI)
   - Uruchomić migracji (brak FluentMigrator)

2. **Moduły 1, 2, 5 równolegle (FAZA 2)** — te moduły mają minimalne zależności wzajemne:
   - Moduł 1 definiuje zamówienia (niezależne)
   - Moduł 2 definiuje katalog diet (niezależne)
   - Moduł 5 zarządza użytkownikami (niezależne od domen biznesowych)

3. **Moduł 3 po Modułach 1 i 2 (FAZA 3)** — produkcja konsumuje dane z obu:
   - Plan produkcji wymaga aktywnych zamówień (Moduł 1)
   - Zapotrzebowanie materiałowe wymaga receptur (Moduł 2)
   - Dopóki M1/M2 nie istnieją, M3 może pracować z mockami (obecne podejście)

4. **Moduł 4 po Modułach 1 i 3 (FAZA 4)** — logistyka jest końcem łańcucha:
   - Potrzebuje adresów z zamówień (M1)
   - Potrzebuje skompletowanych paczek (M3)

5. **Integracja (FAZA 5)** — dopiero gdy wszystkie moduły mają stabilne interfejsy

6. **Frontend i wdrożenie (FAZA 6)** — szlifowanie po ukończeniu logiki

### Ryzyka

| Ryzyko | Mitigacja |
|--------|-----------|
| Moduł 3 zaczyna pracę bez gotowych M1/M2 | Mocki/interfejsy w Domain (obecne podejście) |
| SQLite write-lock przy testach równoległych | In-memory SQLite per test, osobne bazy |
| OrmLite brak lazy loading | Jawne JOINy, dokumentacja zapytań przed implementacją |
| Braki w pokryciu testami | CI blokuje merge poniżej 70% |

---

## 5. Timeline (orientacyjny)

```
Tydzień:  1    2    3    4    5    6    7    8    9
          ├────┤
          FAZA 0+1 (Infra + Domain)
               ├─────────────────┤
               FAZA 2 (Moduły 1, 2, 5 — równolegle)
                    ├──────────────────┤
                    FAZA 3 (Moduł 3 — Produkcja/WMS)
                                   ├─────────┤
                                   FAZA 4 (Moduł 4 — Logistyka)
                                        ├─────┤
                                        FAZA 5 (Integracja + Testy)
                                              ├─────┤
                                              FAZA 6 (Frontend + Deploy)
```
