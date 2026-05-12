# PLAN MODUŁU 3 — Produkcja, Kompletacja i Magazyn (WMS/ERP)

> **Wersja:** 1.0  
> **Data:** 2026-04-27  
> **Autor:** Architect / AI  
> **Status:** Proposed  
> **Deweloper:** DEV 03

---

## 1. Opis Stanu Docelowego

Moduł 3 realizuje pełny cykl operacyjny kuchni cateringowej:

1. **Produkcja (Kuchnia)** — automatyczne generowanie dziennego planu produkcji na podstawie zamówień (M1) i receptur (M2), obliczanie zapotrzebowania materiałowego (Food Cost), zarządzanie kartami gotowania, zatwierdzanie ugotowanych posiłków.

2. **Kompletacja i Załadunek (Magazyn)** — pakowanie skompletowanych diet klientów, generowanie etykiet (QR, alergeny, dane klienta), przypisywanie paczek do tras, załadunek do aut i zatwierdzanie wydania kurierowi.

3. **Smart Inventory (Magazyn Zaawansowany)** — zarządzanie zapasami wg FEFO (First Expired, First Out), śledzenie partii (Traceability), automatyczne alerty o uzupełnieniu, rejestracja dostaw z datami ważności, inwentaryzacja.

4. **Nadzór HACCP** — kontrola temperatur, rejestracja strat, śledzenie partii dla Sanepidu.

### Aktorzy
- **Szef Kuchni** — generuje plany, zatwierdza gotowanie, produkuje półprodukty
- **Magazynier** — kompletacja, etykiety, załadunek, dostawy, inwentaryzacja, straty
- **System (Zadanie w tle)** — automatyczne plany (o 22:00), audyt dat ważności, alerty

### Punkty Styku z Innymi Modułami
- **⬅️ Moduł 1:** Pobiera aktywne zamówienia do planu produkcji
- **⬅️ Moduł 2:** Pobiera 7-dniowy plan diet i receptury posiłków
- **➡️ Moduł 4:** Udostępnia zestawienia paczek, weryfikacja aut przy załadunku

---

## 2. Stan Obecny (co już istnieje)

### Domain Layer ✅
- `BaseEntity<TId>` + `AuditableEntity<TId>` + `ISoftDeletable` — kompletne
- `IDomainEvent` — interfejs gotowy
- `IRepository<T, TId>` — generyczny interfejs z `FindAsync`
- Encje Warehouse: `UnitOfMeasure`, `StockItem`, `Batch`, `InventoryTransaction`, `TemperatureLog`
- `InventoryTransactionType` enum
- `IBatchRepository`, `IInventoryTransactionRepository` — dedykowane interfejsy

### Infrastructure Layer ✅
- `BaseRepository<T, TId>` — generyczne repozytorium z Soft Delete
- `BatchRepository` — z FEFO query
- `InventoryTransactionRepository` — z zapytaniami per partia
- Migration `002_CreateWarehouseTables` — 5 tabel magazynowych

### Braki ⬜
- [ ] DTOs, AutoMapper Profiles, Walidatory (Sprint 3)
- [ ] Serwisy aplikacyjne (Sprint 3)
- [ ] Kontrolery i widoki (Sprint 4)
- [ ] Testy jednostkowe serwisów i walidatorów
- [ ] QuestPDF (Karta Produkcyjna, Etykiety)

---

## 3. Podział na Architektoniczne Etapy Realizacji

### ETAP A — Uzupełnienie Warstwy Domenowej
**Priorytet:** 🔴 KRYTYCZNY  
**Zależności:** Brak (korzysta z istniejącego Domain Core)

#### A.1: Encje Produkcyjne ✅
| Encja | Klasa bazowa | Opis |
|-------|-------------|------|
| `ProductionPlan` | `AuditableEntity` | Dzienny plan produkcji (data, status, powiązanie z zamówieniami) |
| `ProductionPlanItem` | `AuditableEntity` | Pozycja planu: posiłek × ilość × status ugotowania |
| `ProductionBatch` | `AuditableEntity` | Półprodukty (buliony, sosy) — powiązanie plan↔batch |

#### A.2: Encje Kompletacji i Załadunku ✅
| Encja | Klasa bazowa | Opis |
|-------|-------------|------|
| `PackingSession` | `AuditableEntity` | Sesja pakowania (data, magazynier, status) |
| `PackingItem` | `AuditableEntity` | Pozycja paczki: klient × dieta × wariant × status |
| `PackingLabel` | `BaseEntity` | Etykieta QR z metadanymi (klient, alergeny, trasa) |

#### A.3: Encje Nadzoru i Korekt ✅
| Encja | Klasa bazowa | Opis | Status |
|-------|-------------|------|--------|
| `TemperatureLog` | `AuditableEntity<long>` | Log temperatur HACCP | ✅ Gotowe |
| `InventoryAdjustment` | `AuditableEntity` | Korekta inwentaryzacyjna | ✅ Gotowe |

#### A.4: Enumy Produkcyjne ✅
| Enum | Wartości |
|------|---------|
| `ProductionPlanStatus` | `Draft`, `Active`, `InProgress`, `Completed`, `Cancelled` |
| `ProductionItemStatus` | `Planned`, `Cooking`, `Cooked`, `Failed` |
| `PackingStatus` | `Pending`, `Packed`, `Labeled`, `Loaded`, `Dispatched` |

#### A.5: Interfejsy Repozytoriów
- `IProductionPlanRepository` — z `GetByDateAsync(DateOnly date)` ✅
- `IPackingSessionRepository` — z `GetActiveByDateAsync(DateOnly date)` ✅
- `IStockItemRepository` — z `GetBelowMinimumAsync()` ✅
- `ITemperatureLogRepository` — z `GetByDateRangeAsync()` ✅

#### A.6: Interfejsy zewnętrzne ✅
Interfejsy w `Domain/Interfaces/External/`:
- `IOrderDataProvider` — pobiera aktywne zamówienia (M1) ✅
- `IDietDataProvider` — pobiera receptury i plan posiłków (M2) ✅
- `IDeliveryManifestProvider` — pobiera przypisania aut (M4) ✅

---

### ETAP B — Migracje Bazodanowe ✅
**Priorytet:** 🔴 KRYTYCZNY  
**Zależności:** ETAP A (encje muszą istnieć)

| # | Migracja | Tabele | Status |
|---|----------|--------|--------|
| B.1 | `004_CreateProductionTables` | `ProductionPlans`, `ProductionPlanItems`, `ProductionBatches` | ✅ |
| B.2 | `005_CreatePackingTables` | `PackingSessions`, `PackingItems`, `PackingLabels` | ✅ |
| B.3 | `006_CreateInventoryAdjustments` | `InventoryAdjustments` | ✅ |

---

### ETAP C — Repozytoria i Warstwa Dostępu do Danych ✅
**Priorytet:** 🟡 WYSOKI  
**Zależności:** ETAP B (tabele muszą istnieć)

| # | Repozytorium | Kluczowe metody | Status |
|---|-------------|-----------------|--------|
| C.1 | `StockItemRepository` | `GetBelowMinimumAsync()`, `GetByIngredientIdAsync()` | ✅ |
| C.2 | `ProductionPlanRepository` | `GetByDateAsync()`, `GetWithItemsAsync()` | ✅ |
| C.3 | `PackingSessionRepository` | `GetActiveByDateAsync()`, `GetWithItemsAsync()` | ✅ |
| C.4 | `TemperatureLogRepository` | `GetByDateRangeAsync()`, `GetByLocationAsync()` | ✅ |
| C.5 | Rejestracja DI — aktualizacja `DependencyInjection.cs` | Pełna rejestracja wszystkich repo | ✅ |

---

### ETAP D — Serwisy Domenowe ✅
**Priorytet:** 🟡 WYSOKI  
**Zależności:** ETAP C (repozytoria muszą istnieć)

| # | Serwis | Odpowiedzialność | Status |
|---|--------|-----------------|--------|
| D.1 | `FefoService` | Algorytm FEFO — wybór partii do zdjęcia wg daty ważności | ✅ |
| D.2 | `FoodCostCalculator` | Obliczanie zapotrzebowania materiałowego (receptury × zamówienia) | ✅ |
| D.3 | `SmartInventoryAnalyzer` | Analiza dat ważności vs. lead-time, generowanie alertów | ✅ |
| D.4 | `ProductionPlanGenerator` | Generowanie planu produkcji z zamówień i receptur | ✅ |

---

### ETAP E — DTOs, AutoMapper i Walidatory
**Priorytet:** 🟡 WYSOKI  
**Zależności:** ETAP A (encje) + ETAP D (serwisy definiują potrzebne DTO)

#### E.1: DTOs w `Application/DTOs/Production/`
- `ProductionPlanDto`, `ProductionPlanItemDto`, `CreateProductionPlanRequest`
- `CookingCardDto` (zbiorcza karta gotowania)
- `FoodCostReportDto`, `ShortageReportDto`

#### E.2: DTOs w `Application/DTOs/Warehouse/`
- `StockItemDto`, `BatchDto`, `InventoryTransactionDto`
- `ReceiveDeliveryRequest`, `RegisterWasteRequest`
- `InventoryAdjustmentRequest`, `TemperatureLogDto`
- `PackingSessionDto`, `PackingItemDto`, `PackingLabelDto`

#### E.3: AutoMapper Profiles
- `ProductionProfile.cs` — mapowania produkcyjne
- `WarehouseProfile.cs` — mapowania magazynowe

#### E.4: Walidatory FluentValidation
- `ReceiveDeliveryValidator` — data ważności > dziś, ilość > 0
- `RegisterWasteValidator` — ilość ≤ stan partii
- `TemperatureLogValidator` — zakres -30°C do +50°C
- `CreateProductionPlanValidator` — data ≥ dziś

---

### ETAP F — Serwisy Aplikacyjne
**Priorytet:** 🟡 WYSOKI  
**Zależności:** ETAP C + D + E

| # | Serwis | Kluczowe operacje |
|---|--------|-------------------|
| F.1 | `IProductionService` | `GenerateDailyPlanAsync()`, `GetCookingCardAsync()`, `ApproveCookingAsync()`, `ProduceSemiFinishedAsync()` |
| F.2 | `IWarehouseService` | `ReceiveDeliveryAsync()`, `RegisterWasteAsync()`, `PerformInventoryAsync()`, `GetSmartAlertsAsync()` |
| F.3 | `IPackingService` | `StartPackingSessionAsync()`, `PackClientDietAsync()`, `GenerateLabelsAsync()`, `ApproveDispatchAsync()` |
| F.4 | `ITemperatureService` | `LogTemperatureAsync()`, `GetHaccpReportAsync()` |

---

### ETAP G — QuestPDF: Dokumenty
**Priorytet:** 🟢 STANDARD  
**Zależności:** ETAP E (wymaga DTOs)

| # | Dokument | Opis |
|---|----------|------|
| G.1 | `ProductionPlanDocument` | Karta produkcyjna: tabela posiłków na dzień, ilości, składniki |
| G.2 | `CookingCardDocument` | Zbiorcza karta gotowania per posiłek |
| G.3 | `PackingLabelDocument` | Etykieta na paczkę: QR, alergeny, klient, dieta |
| G.4 | `ShortageReportDocument` | Raport braków magazynowych |

---

### ETAP H — Kontrolery i Widoki (Web Layer)
**Priorytet:** 🟢 STANDARD  
**Zależności:** ETAP F (serwisy aplikacyjne muszą istnieć)

| # | Kontroler | Widoki | Role |
|---|-----------|--------|------|
| H.1 | `ProductionController` | Index (plan dnia), CookingCard, ApproveCooking | Kitchen, Admin |
| H.2 | `WarehouseController` | Stock, ReceiveDelivery, Waste, Inventory, Temperatures | Kitchen, Admin |
| H.3 | `PackingController` | Sessions, PackClient, Labels, LoadAndDispatch | Kitchen, Admin |

---

### ETAP I — Testy
**Priorytet:** 🟡 WYSOKI  
**Zależności:** ETAP D + F (musi być co testować)

| # | Kategoria | Co testujemy |
|---|-----------|-------------|
| I.1 | Unit: Serwisy domenowe | `FefoService`, `FoodCostCalculator`, `SmartInventoryAnalyzer` |
| I.2 | Unit: Serwisy aplikacyjne | `ProductionService`, `WarehouseService`, `PackingService` |
| I.3 | Unit: Walidatory | Wszystkie FluentValidation validators |
| I.4 | Unit: AutoMapper | Testy mapowań (brak null, poprawne pola) |
| I.5 | Integration: Repozytoria | SQL Server, FEFO query, Soft Delete | ✅ |
| I.6 | Seedery testowe | `WarehouseDataSeeder` (Bogus) | ✅ |

---

## 4. Rozważane Alternatywy i Uzasadnienie Wyborów

### Alternatywa 1: Monolityczny serwis vs. podział na serwisy domenowe i aplikacyjne

| Kryterium | Opcja A: Jeden `WarehouseService` | Opcja B: Podział na serwisy ✅ |
|-----------|----------------------------------|-------------------------------|
| Testowalność | Niski — duży serwis, trudno mockować | Wysoka — małe, skupione serwisy |
| Czytelność | Niska — 500+ linii kodu | Wysoka — osobne odpowiedzialności |
| Reużywalność | Niska | Wysoka — `FefoService` można użyć z wielu miejsc |
| Złożoność DI | Prosta — 1 rejestracja | Średnia — więcej rejestracji |

**Wybór:** Opcja B — podział na serwisy domenowe (`FefoService`, `FoodCostCalculator`) + aplikacyjne (`IWarehouseService`, `IProductionService`). Zgodne z Clean Architecture: logika biznesowa w Domain, orkiestracja w Application.

### Alternatywa 2: Mocki M1/M2 w Domain vs. Adapter Pattern w Infrastructure

| Kryterium | Opcja A: Mocki w Domain ✅ | Opcja B: Adaptery w Infrastructure |
|-----------|--------------------------|-----------------------------------|
| Niezależność rozwoju | Wysoka — M3 nie czeka na M1/M2 | Niska — wymaga gotowych interfejsów |
| Zgodność z DIP | Wysoka — interfejsy w Domain | Wysoka |
| Wysiłek refaktoryzacji | Minimalny — zamiana implementacji w DI | Brak — ale trzeba czekać |
| Realistyczność danych | Niska — statyczne/fakeowe | Wysoka — prawdziwe dane |

**Wybór:** Opcja A (teraz) → Opcja B (FAZA 5 integracji). Interfejsy `IOrderDataProvider` i `IDietDataProvider` w Domain, implementacje mock w `Domain/Mocks`, docelowe adaptery w Infrastructure.

### Alternatywa 3: FEFO w kodzie aplikacyjnym vs. SQL query

| Kryterium | Opcja A: FEFO w serwisie | Opcja B: FEFO w SQL (repozytorium) ✅ |
|-----------|-------------------------|--------------------------------------|
| Wydajność | Niska — załadowanie wszystkich partii do pamięci | Wysoka — SQL Server robi ORDER BY/LIMIT |
| Atomowość | Ryzyko race condition | Lepsza — transakcja DB |
| Testowalność | Łatwiejsza | Wymaga bazy SQL (Integration Test) |

**Wybór:** Opcja B — kluczowe zapytania FEFO (`ORDER BY ExpiryDate ASC, ReceivedDate ASC WHERE IsDepleted = false`) wykonywane w repozytorium (`BatchRepository.GetAvailableFefoAsync()`). Logika decyzji (ile zdjąć) w serwisie domenowym. Sprawdzone w testach integracyjnych na SQL Server.

### Alternatywa 4: Encje kompletacji jako osobna grupa vs. osadzenie w produkcji

| Kryterium | Opcja A: Encje pakowania w `Production/` | Opcja B: Osobny namespace `Packing/` ✅ |
|-----------|------------------------------------------|----------------------------------------|
| Separacja | Niska — mieszanie konceptów | Wysoka — czytelna granica |
| Skalowalność | Trudna | Łatwa — osobne repozytorium, serwis |
| Spójność z use cases | Niska — different actors | Wysoka — Magazynier vs Szef Kuchni |

**Wybór:** Opcja B — osobny namespace `Entities/Packing/`, osobne repozytoria, osobny serwis `IPackingService`. Aktorzy (Szef Kuchni vs Magazynier) mają różne odpowiedzialności.

---

## 5. Graf Zależności Etapów

```
ETAP A (Domain: Encje, Enumy, Interfejsy, Mocki)
    │
    ├──▶ ETAP B (Migracje bazodanowe)
    │        │
    │        └──▶ ETAP C (Repozytoria)
    │                 │
    │                 └──▶ ETAP D (Serwisy domenowe)
    │                          │
    ├──────────────────────────┤
    │                          │
    └──▶ ETAP E (DTOs, AutoMapper, Walidatory)
                   │           │
                   └───────────┤
                               │
                        ETAP F (Serwisy aplikacyjne)
                               │
                    ┌──────────┼──────────┐
                    │          │          │
             ETAP G (PDF) ETAP H (Web) ETAP I (Testy)
```

---

## 6. Kryteria Ukończenia Modułu

- [x] Wszystkie encje zdefiniowane i zmigowane
- [x] Repozytoria z testami integracyjnymi (SQL Server)
- [ ] Serwisy domenowe z testami jednostkowymi (≥ 80% coverage)
- [ ] Serwisy aplikacyjne z testami jednostkowymi (Moq)
- [ ] AutoMapper profiles z testami konfiguracji
- [ ] Walidatory z testami edge cases
- [ ] Kontrolery z autoryzacją (role: Kitchen, Admin)
- [ ] Widoki Razor z walidacją front-end
- [ ] QuestPDF dokumenty działające
- [ ] Seedery testowe (Bogus)
- [ ] Brak błędów StyleCop / Roslynator
