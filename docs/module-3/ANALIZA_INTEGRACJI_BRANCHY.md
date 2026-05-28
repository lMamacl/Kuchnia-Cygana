# Analiza Integracji Branchy Zewnętrznych z Modułem 3 (M3)
**Projekt: Kuchnia u Cygana**  
*Autor: Antigravity*  
*Data analizy: 27.05.2026*  
*Status: Zgodne z gałęzią `Mamac` (HEAD) po zmigrowaniu innych modułów na Dappera*

---

## 1. Wstęp i Cel Analizy

Celem niniejszego dokumentu jest analiza kodu źródłowego na gałęziach deweloperskich pozostałych modułów:
1.  **M1 (Zamówienia / E-commerce):** Branch `Dawid`
2.  **M2 (Menu / Diety):** Branch `Gabriel-Nueva`
3.  **M4 (Logistyka):** Branch `Mod4`
4.  **M5 (HR / Administracja):** Branch `Pawciot_tym_razem_moze_dzialac_bedzie`

Analiza określa, w jakim stopniu te gałęzie rozwiązują luki zidentyfikowane w planie [PLAN_SPRINTOW_M3.md](file:///c:/Users/cyunc/source/repos/Projekt%20Platforma%20Cateringowa/Kuchnia%20Cygana/docs/module-3/PLAN_SPRINTOW_M3.md), czy występują konflikty architektoniczne (w szczególności dotyczące ORM i Migracji) oraz w jaki sposób ich import wpłynie na strukturę planowanych sprintów.

---

## 2. Ocena Kompatybilności Technicznej (ORM & Migracje)

### 2.1 Standard Dostępu do Danych (Dapper)
*   **Wszystkie gałęzie są w 100% zmigrowane na Dappera.**
*   W kodzie **nie stwierdzono obecności Entity Framework Core (DbContext, DbSet)** ani starego standardu ServiceStack OrmLite w nowo zaimplementowanych repozytoriach.
*   Wszystkie repozytoria opierają się na generycznym `BaseRepository<T>` i parametryzowanych zapytaniach SQL z użyciem Dappera, co gwarantuje pełną spójność techniczną z naszą gałęzią `Mamac`.

### 2.2 Brak Konfliktów w Migracjach (FluentMigrator)
Struktura numeracji migracji w projekcie została podzielona na zakresy setkowe, co zapobiega jakimkolwiek konfliktom (nie ma pokrywających się numerów wersji):
*   **M3 (Nasza gałąź - Magazyn/Produkcja/Pakowanie):** `001` - `009` (i kolejne `010+`)
*   **M1 (Dawid - Zamówienia):** `100` - `107`
*   **M2 (Gabriel - Menu):** `301` - `303`
*   **M4 (Mod4 - Logistyka):** `400`
*   **M5 (Pawciot - HR/Admin):** `500` - `506`

*Wniosek:* Wszystkie pliki migracyjne z innych modułów mogą zostać zaimportowane do naszego folderu migracji bez modyfikacji ich atrybutów `[Migration(X)]`.

---

## 3. Wpływ Poszczególnych Modułów na Luki M3

### 3.1 Moduł M1 (Dawid) — E-commerce & Zamówienia
Nasza gałąź `Mamac` posiada już w repozytorium pliki migracji `100-107` oraz kod M1, jednak istnieją istotne różnice funkcjonalne.

*   **Rozwiązane luki M3:**
    *   **Geolokalizacja adresów:** Na gałęzi `Dawid` migracja `100_CreateAddressesTable.cs` oraz encja `Address` zawierają pola `Latitude` i `Longitude` (typu `Double?`). W naszej lokalnej wersji `Mamac` pól tych brakuje. Zaciągnięcie wersji Dawida jest krytyczne dla trasowania w Module 4.
    *   **Prawdziwy `OrderDataProvider`:** Dawid zaimplementował ten dostawca z użyciem zoptymalizowanego batch loadingu w Dapperze (osobne szybkie zapytania dla adresów, pozycji i klientów), zastępując nasz tymczasowy adapter `M1OrderDataProvider` oparty o ciężkie JOINy.
*   **Pozostałe luki:**
    *   **Brak `CustomerProfile.PublicId`:** Wizja M3 wymaga pola `PublicId` w profilu klienta do weryfikacji i anonimizacji etykiet wysyłkowych. Analiza encji `CustomerProfile` u Dawida potwierdza, że to pole tam **nie istnieje**. M3 musi samodzielnie rozszerzyć tę tabelę i encję o to pole w dedykowanej migracji (np. `010`).

### 3.2 Moduł M2 (Gabriel-Nueva) — Menu & Diety
W naszej gałęzi mamy tylko pliki migracji dla Menu (`301-303`) oraz uproszczone definicje encji (np. `Recipe`). Brakuje nam jednak całej logiki biznesowej Menu.

*   **Rozwiązane luki M3 (Krytyczne bloki dla Produkcji):**
    *   **Silnik Receptur (`IRecipeEngine`):** Wersja Gabriela zawiera kompletną logikę sprawdzania poprawności receptur oraz przeliczania wartości odżywczych (`INutritionCalculator`) i alergenów (`IAllergenPropagationService`).
    *   **Serwisy i DTOs Menu:** Otrzymujemy kompletny backend do zarządzania posiłkami, dietami i ich wariantami (`MealManagementService`, `DietManagementService`, `IngredientManagementService`).
    *   **Spójność bazy:** Prawdziwe powiązania receptury (`Recipes` -> `Ingredients`) będą działać automatycznie z naszym modułem magazynowym (FEFO) w scenariuszach produkcyjnych.
*   **Wykryte błędy (Do poprawy przy imporcie):**
    *   **Brak rejestracji repozytoriów w DI:** Gabriel stworzył repozytoria (np. `DietRepository`, `MealRepository`), ale **nie zarejestrował ich** w kontenerze DI w `DependencyInjection.cs`. Zarejestrował tylko serwisy domenowe i adaptery. Spowoduje to błąd uruchomienia aplikacji. Musimy ręcznie dodać te rejestracje podczas integracji.

### 3.3 Moduł M4 (Mod4) — Logistyka & Trasowanie
Moduł ten dostarcza kompletne zarządzanie trasami, autami, kierowcami i torbami termicznymi.

*   **Rozwiązane luki M3 (Krytyczne bloki dla Kompletacji i Załadunku):**
    *   **Baza danych kierowców i aut:** Tabele `Vehicles`, `Drivers` oraz trasy `DeliveryRoutes` i przystanki `DeliveryRouteStops` zastępują nasze mockowane dane tras.
    *   **Prawdziwe trasowanie:** Otrzymujemy integrację z OpenStreetMap do wyznaczania tras kurierskich, co pozwoli na poprawne wdrożenie procesu kompletacji.
*   **Pozostałe luki:**
    *   **Brak `IDeliveryManifestProvider`:** Moduł 4 nie implementuje tego kontraktu dla M3. Korzysta z własnych repozytoriów tras. M3 musi zaimplementować adapter `M4DeliveryManifestProvider` odpytujący tabele M4 przez Dappera, aby zasilić nasz proces kompletacji i załadunku.
    *   **Placeholdery DriverMobile:** Widoki dla kierowców na gałęzi M4 to wciąż puste pliki HTML wywołujące partial `_DriverPlaceholder`. Logika mobilnego ekranu kierowcy musi zostać dokończona w ramach M3 (Sprint 7.3).

### 3.4 Moduł M5 (Pawciot) — HR & Administracja
Moduł dostarcza ewidencję pracowników, urlopów i ticketów administracyjnych.

*   **Rozwiązane luki M3:**
    *   **Ewidencja personelu:** Tabele `Employees` i `Departments` pozwalają powiązać akcje pakowania (`PackedBy` w `PackingSession`) oraz zatwierdzenia gotowania (`ApprovedBy`) z rzeczywistymi pracownikami z bazy danych, a nie z ciągami tekstowymi wpisywanymi z ręki.
*   **Wykryte błędy (Do poprawy przy imporcie):**
    *   **Brak rejestracji repozytoriów w DI:** Paweł nie dodał rejestracji dla `TicketRepository` i `WorkScheduleRepository` do pliku `DependencyInjection.cs`. Wymaga to ręcznego uzupełnienia.

---

## 4. Wpływ na Plan Sprintów M3 (`PLAN_SPRINTOW_M3.md`)

Importy te znacznie **upraszczają i przyspieszają realizację planu**, eliminując konieczność pisania mocków i tymczasowych struktur.

### 4.1 Zmiany w poszczególnych sprintach:

1.  **Sprint 4 (Domain + Infra Gaps):**
    *   *Ułatwienie:* Nie musimy sami projektować encji dla Menu (M2) czy Logistyki (M4). Możemy bezpośrednio zaimportować pliki klas i migracji z tamtych branchy.
    *   *Zadanie dodatkowe:* Musimy stworzyć migrację rozszerzającą tabelę `CustomerProfiles` o kolumnę `PublicId` (brakujące u Dawida) oraz dopisać rejestracje DI dla repozytoriów Menu (M2) i HR (M5).
2.  **Sprint 7 (Refaktor Kompletacja i Załadunek):**
    *   *Ułatwienie:* Zamiast opierać się na `MockDeliveryManifestProvider`, możemy od razu napisać produkcyjny adapter `M4DeliveryManifestProvider` pobierający dane z tabel `DeliveryRoutes` i `DeliveryRouteStops`.
    *   *Zadanie do wykonania:* Moduł 4 nie posiada logiki ekranów kierowcy (tylko mock-karty), więc cały flow `DriverMobile` (wyświetlanie przypisanej trasy, oznaczanie dostarczenia, zgłaszanie problemów) pozostaje po stronie M3 (Sprint 7.3).
3.  **Sprint 8 (Produkcja — Uzupełnienia):**
    *   *Ułatwienie:* Całkowite odblokowanie zależności od M2. Karta gotowania (`CookingCard.cshtml`) oraz wydruki etykiet mogą w 100% korzystać z rzeczywistych składników, receptur oraz automatycznie wyliczonych wartości odżywczych i alergenów za pomocą `RecipeEngine` od Gabriela.

### 4.2 Czego importy NIE rozwiązują? (Nasza praca własna w M3)
Poniższe kluczowe zadania refaktoryzacyjne i wizualne są specyficzne dla M3 i nie występują na żadnej innej gałęzi – musimy je wykonać samodzielnie:
*   **Rozbicie God Service `PackingService.cs` (41.6 KB):** Wydzielenie `LoadingService.cs` i uporządkowanie odpowiedzialności (SRP).
*   **Rozbicie kontrolera:** Wydzielenie `LoadingController.cs` z `PackingController.cs`.
*   **4 Nowe widoki magazynowe:** `BatchDetails`, `Issue` (wydanie ręczne), `FefoReport`, `TransactionHistory`.
*   **Filtrowanie i dynamiczne kolorowanie (HTMX):** Dynamiczna tabela w `Warehouse/Index`, lazy-loading w historii partii, Chart.js w temperaturach i raportach HACCP.
*   **Eksport raportów:** PDF (QuestPDF) i CSV (HACCP) z magazynu.

---

## 5. Rekomendowana Strategia Integracji (Krok po Kroku)

Aby uniknąć konfliktów w kodzie i bazie, zaleca się importowanie gałęzi w kolejności zależności architektonicznych (Phase-Gated Integration):

### Faza 1: Baza i Zamówienia (Dawid - M1)
1.  Pobierz encję `Address` i powiązaną migrację `100` zawierającą kolumny `Latitude` i `Longitude`.
2.  Zaimplementuj nową migrację rozszerzającą `CustomerProfiles` o `PublicId`.
3.  Zastąp `M1OrderDataProvider` zoptymalizowanym `OrderDataProvider` Dawida.
4.  *Gate:* Uruchom migracje i zweryfikuj testy integracyjne bazy (`dotnet test`).

### Faza 2: Menu i Receptury (Gabriel - M2)
1.  Zaimportuj DTOs, Serwisy i Repozytoria Menu z gałęzi `Gabriel-Nueva`.
2.  **Krok naprawczy:** Dodaj brakujące rejestracje `services.AddScoped<IDietRepository, DietRepository>()` (oraz pozostałych 9 repozytoriów) w pliku `DependencyInjection.cs`.
3.  *Gate:* Przetestuj weryfikację receptury przez `RecipeEngine` w teście jednostkowym.

### Faza 3: Logistyka i Trasowanie (Mod4 - M4)
1.  Zaimportuj migrację `400` oraz encje tras, pojazdów i toreb termicznych.
2.  Zaimportuj serwisy routingowe i pracownika w tle.
3.  Stwórz adapter `M4DeliveryManifestProvider` w M3 w oparciu o tabele M4.
4.  *Gate:* Sprawdź, czy geokodowanie i trasowanie poprawnie odczytuje współrzędne z adresów (Krok 1) przy użyciu OpenStreetMap.

### Faza 4: Administracja i HR (Pawciot - M5)
1.  Zaimportuj tabele pracowników i zgłoszeń serwisowych.
2.  **Krok naprawczy:** Dodaj brakujące rejestracje repozytoriów HR/Admin w DI.
3.  Powiąż sesje pakowania i zatwierdzenia produkcji z zalogowanym pracownikiem pobranym z `EmployeeRepository`.

---

## 6. Podsumowanie i Rekomendacja dla Użytkownika

Importy z gałęzi zewnętrznych **w ok. 35-40% pokrywają brakujące elementy domenowo-infrastrukturalne M3**, eliminując konieczność pisania skomplikowanych mocków receptur (M2) i tras (M4). 

Integracja ta **nie jest jednak prostym "kopiuj-wklej"** ze względu na:
1.  Konieczność naprawy błędów rejestracji DI w modułach M2 i M5.
2.  Konieczność rozbudowy bazy o brakujące pole `PublicId` (którego nie ma u Dawida).
3.  Konieczność napisania własnego adaptera tras (`IDeliveryManifestProvider`) do tabel Modułu 4.

Zalecam rozpoczęcie od **Fazy 1 (M1 - Dawid)** w celu uzupełnienia współrzędnych geograficznych w adresach, co odblokuje geokodowanie dla logistyki, a następnie przejście do **Fazy 2 (M2 - Gabriel)** w celu wdrożenia receptur.
