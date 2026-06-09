# M3 Release Notes - Sweep Kuchnia, Magazyn, Kompletacja

Data aktualizacji: 2026-06-05

## Status Ogolny

M3 jest utrzymywany jako modul operacyjny oparty o SQL Server, Dapper i indeksy bazodanowe. Nie wprowadzamy Elasticsearch ani osobnej wyszukiwarki. Przy zalozeniu do ok. 1000 zamowien dziennie kompletacja dzienna pozostaje bez duzego refaktoru; optymalizacje skupiaja sie na magazynie i na twardym konsumowaniu snapshotu M2 przez kuchnie.

## Magazyn

### Dziala Samodzielnie

- Tabele magazynowe stock/inventory/lookup/FEFO korzystaja z zoptymalizowanych zapytan i indeksow SQL Server.
- FEFO produkcyjne przechodzi przez transakcyjna sciezke `WarehouseCommandRepository` z blokadami `UPDLOCK` dla partii.
- Dedukcja FEFO obsluguje teraz zarowno `StockItemId`, jak i `WarehouseCategoryId`.
- Ponowienie tej samej operacji produkcyjnej jest idempotentne po `ReferenceDocument`, ale referencje sa rozbite per zasob, zeby kilka skladnikow tej samej pozycji planu nie blokowalo sie wzajemnie.
- Historia transakcji pokazuje operatora z `InventoryTransactions.CreatedBy/UpdatedBy`, a dla starych rekordow bez `StockItemId` filtruje po partii przez `COALESCE(it.StockItemId, b.StockItemId)`.

### Zmiany Bazodanowe

- `031_OptimizeWarehouseQueryIndexes` przebudowuje indeksy pod stany, inwentaryzacje, FEFO i prefiks numeru partii.
- `032_AddInventoryTransactionHistoryIndexes` dodaje nullable `CreatedBy`/`UpdatedBy` do `InventoryTransactions` oraz indeksy pod dokument referencyjny i historie.

### Ryzyka Pozostale

- Integracyjne testy SQL Server wymagaja dzialajacego Docker/Testcontainers.
- Raporty HACCP/FEFO PDF zostaja do polerki po zamknieciu P0/P1.

## Kompletacja I Loading

### Dziala Samodzielnie

- Aktywnym ekranem manifestu jest `Loading/Manifest.cshtml`.
- Nowe operacje loadingu pozostaja w `LoadingController`.
- Stare adresy `/packing/loading/...` sa zachowane jako aliasy, ale nie wykonuja logiki zaladunku ani dispatchu. Przekierowuja do `LoadingController`.
- Usunieto martwa akcje `PackClient`, ktora zawsze konczyla sie wyjatkiem.
- Usunieto stare widoki loadingu z `Views/Packing` i martwe widoki preview/redruku z `Views/Loading`.

### Swiadomie Zostawione

- `PackingSynchronizationService.RefreshFromRoutesAsync` zostaje, bo spina M4 z kompletacja i oznacza manifesty do regeneracji po zmianach tras.
- `PackingService` nie jest teraz przepisywany szeroko; zmiany ograniczono do starych tras, testow i punktow ryzyka.

## Kuchnia

### Dziala Samodzielnie

- Generowanie planu produkcji wymaga opublikowanego snapshotu M2. M3 nie generuje juz operacyjnego planu z legacy `GetPlanForDateAsync`.
- `ProduceSemiFinishedAsync` blokuje FEFO, gdy nie ma snapshotu M2 w planie ani opublikowanego snapshotu dla dnia.
- `ApproveCookingAsync` blokuje zatwierdzenie gotowania, gdy pozycja nie ma `M2SnapshotJson`; nie pomija juz cicho zdejmowania opakowan.
- Zdejmowanie opakowan po gotowaniu dziala po `StockItemId` albo `WarehouseCategoryId`, z idempotencja per zasob.

### Czeka Na M2

- Docelowe widoki i kontrakty M2 opisane w `PLAN_SPRINTOW_M3.md` oraz `GABRIEL_M2_BRAKI_WIDOKI_I_KONTRAKTY.md`.
- Pelny flow sesji gotowania po tabelach `CookingSessions` / `CookingSessionStepChecks`.

### Poza Aktywnym Zakresem Release

- `CookingSessionService` nie jest juz rejestrowany w DI. Kod moze zostac jako baza pod pozniejszy flow, ale nie jest aktywna funkcja release.
- Legacy receptury pozostaja technicznie w kodzie jako awaryjna historia, ale normalny flow produkcyjny jest blokowany bez snapshotu M2.

## Testy Dodane Lub Utrzymane

- Unit: FEFO uzywa transakcyjnych komend magazynowych, w tym wariantu po kategorii i idempotentnego skipu.
- Unit: stare aliasy `/packing/loading/...` nie odpalaja `ILoadingService`.
- Unit: kuchnia blokuje approve bez snapshotu M2 i nie spada do legacy receptur przy FEFO bez opublikowanego snapshotu.
- Integration: historia transakcji filtruje stare rekordy po `Batch.StockItemId`, gdy `InventoryTransactions.StockItemId` jest puste.

## Rekomendacja Przed Oddaniem

1. Uruchomic build i testy nieintegracyjne.
2. Przy dzialajacym Docker/Testcontainers uruchomic `WarehouseRepositoriesSqlServerTests`.
3. Potwierdzic z M2 termin dostarczenia widokow snapshotu i wariantow skladnikow.
4. Dopiero po P0/P1 poprawiac wyglad raportow PDF i drobne komunikaty UI.
