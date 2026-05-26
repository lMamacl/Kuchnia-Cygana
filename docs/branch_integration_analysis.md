# Analiza integracji gałęzi deweloperskich i migracji Dapper

Ten dokument przedstawia szczegółową analizę stanu gałęzi deweloperskich (Gabriel, Tomasz, Dawid, Pawciot) względem gałęzi `develop` oraz instrukcję bezpiecznego wdrożenia Dappera i nowoczesnych widoków Tabler z minimalną liczbą konfliktów.

---

## 1. Główne przyczyny rozbieżności i konfliktów

Analiza polecenia `git diff` wykazała, że wszystkie gałęzie deweloperskie divergnęły (rozjechały się) z gałęzią `develop` na wczesnym etapie, zanim do projektu wprowadzono:
1.  **Tabler CSS i nowy layout panelu pracownika** (`_LayoutStaff.cshtml`, `/wwwroot/css/staff.css`).
2.  **Czystą architekturę persystencji opartą na Dapperze** (całkowite usunięcie ServiceStack.OrmLite).

Z tego powodu standardowa próba scalenia (`git merge develop`) wygeneruje ogromne konflikty w widokach, kontrolerach oraz strukturze bazy danych. Wiele plików w diffach gałęzi deweloperskich wygląda na „usunięte”, ponieważ ich gałęzie bazują na commitach, w których te widoki i kontrolery jeszcze nie istniały.

---

## 2. Strategia migracji na Dappera i zachowania logiki

Na gałęzi `develop` usunięto bibliotekę ServiceStack. OrmLite operował na wyrażeniach lambda (`Expression<Func<T, bool>>`), co w Dapperze nie jest natywnie obsługiwane w ten sam sposób. Poniżej znajduje się zestawienie, jak należy przepisać metody repozytoriów na czyste zapytania SQL za pomocą Dappera.

### A. Zamiana zapytań z OrmLite na Dapper

Oto bezpośrednia ściągawka dla deweloperów, jak przekształcić najczęstsze operacje:

#### 1. Pobieranie listy encji z filtrem (Select)
*   **OrmLite:**
    ```csharp
    return await db.SelectAsync<Meal>(m => m.Status == MealStatus.Published && !m.IsDeleted);
    ```
*   **Dapper:**
    ```csharp
    return await db.QueryAsync<Meal>(
        "SELECT * FROM [Meal] WHERE [Status] = @Status AND [IsDeleted] = 0",
        new { Status = MealStatus.Published });
    ```

#### 2. Pobieranie po ID (SingleById)
*   **OrmLite:**
    ```csharp
    var meal = await db.SingleByIdAsync<Meal>(mealId);
    ```
*   **Dapper:**
    Użycie odziedziczonej metody z `BaseRepository`:
    ```csharp
    var meal = await GetByIdAsync(mealId);
    ```

#### 3. Usuwanie rekordów powiązanych (Hard Delete)
*   **OrmLite:**
    ```csharp
    await db.DeleteAsync<MealAllergen>(ma => ma.MealId == mealId);
    ```
*   **Dapper:**
    ```csharp
    await db.ExecuteAsync("DELETE FROM [MealAllergen] WHERE [MealId] = @mealId", new { mealId });
    ```

#### 4. Wstawianie danych (Insert)
*   **OrmLite:**
    ```csharp
    await db.InsertAsync(ma);
    ```
*   **Dapper:**
    ```csharp
    await db.ExecuteAsync(
        "INSERT INTO [MealAllergen] ([MealId], [AllergenId], [IsTrace]) VALUES (@MealId, @AllergenId, @IsTrace)",
        ma);
    ```

---

## 3. Szczegółowe analizy i zalecenia per deweloper

### 👤 Tomasz (Moduł 4 - Logistyka i Geolokalizacja)

Tomasz zaimplementował w pełni sprawny moduł logistyki. Jego logika geokodowania oparta na OpenStreetMap Nominatim API oraz mechanizmy planowania tras i background worker (`DailyGeocodingWorker.cs`) **nie zależą** od technologii bazodanowych i są gotowe do przeniesienia 1:1.

#### Wyzwanie w kodzie Tomasza:
W pliku `GeocodingOrchestrator.cs` Tomasz wywołuje:
```csharp
var pendingAddresses = await _addressRepository.FindAsync(a => a.Latitude == null || a.Longitude == null);
```
Metoda `FindAsync(expression)` korzystała z OrmLite. Dapper nie obsługuje parsowania lambdy na SQL.

#### Rozwiązanie:
1.  Do interfejsu `IAddressRepository.cs` (w warstwie Domain) należy dodać dedykowaną metodę:
    ```csharp
    Task<IEnumerable<Address>> GetPendingAddressesAsync();
    ```
2.  Zaimplementować ją w `AddressRepository.cs` za pomocą Dappera:
    ```csharp
    public async Task<IEnumerable<Address>> GetPendingAddressesAsync()
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<Address>(
            "SELECT * FROM [Address] WHERE ([Latitude] IS NULL OR [Longitude] IS NULL) AND [IsDeleted] = 0");
    }
    ```
3.  W `GeocodingOrchestrator.cs` zmienić wywołanie na:
    ```csharp
    var pendingAddresses = await _addressRepository.GetPendingAddressesAsync();
    ```

#### Dostosowanie widoków i layoutu (Pojazdy / Trasy):
Tomasz dodał widok pojazdów (`Views/Vehicles/Index.cshtml`). Aby wyglądał spójnie z panelem pracownika Tabler z `develop`:
*   Na początku plików `.cshtml` w `Views/Vehicles/` i `Views/Logistics/` musi ustawić layout:
    ```cshtml
    @{
        Layout = "_LayoutStaff";
        ViewData["Title"] = "Pojazdy";
        ViewData["Section"] = "Logistyka";
    }
    ```
*   Zgodnie z wytycznymi [sprint4b_team_integration_guide.md](file:///c:/Users/cyunc/source/repos/Projekt%20Platforma%20Cateringowa/Kuchnia%20Cygana/docs/guides/sprint4b_team_integration_guide.md#L35-L45) tabele w widokach powinny być opakowane w strukturę klas `.card` oraz `.table-vcenter card-table`.

---

### 👤 Gabriel (Moduł 2 - Edytor Diet & Menu)

Gabriel dodał definicje tabel menu (`301_CreateMenuTables.cs`, `302_CreateRelationshipTables.cs`, itp.) oraz serwisy przeliczania wartości odżywczych (`NutritionCalculator.cs`, `RecipeEngine.cs`).

#### Wyzwanie w kodzie Gabriela:
Jego repozytoria (np. `MealRepository`, `MealAllergenRepository`) intensywnie korzystają z zapytań lambda OrmLite do wyszukiwania powiązań dań i alergenów.

#### Rozwiązanie:
*   Musi przepisać zapytania w repozytoriach na SQL Dapper (zgodnie ze ściągawką w Sekcji 2).
*   Np. w `MealAllergenRepository.RecalculateForMealAsync` należy zastąpić:
    *   `db.DeleteAsync<MealAllergen>(ma => ma.MealId == mealId)` -> `db.ExecuteAsync` z zapytaniem `DELETE`
    *   `db.SelectAsync<Recipe>(r => r.MealId == mealId)` -> `db.QueryAsync` z zapytaniem `SELECT`
*   Layout w widokach edytora diet (`Views/DietEditor/*`) musi zostać ustawiony na `Layout = "_LayoutStaff";` ze wskazaniem odpowiedniej sekcji (`ViewData["Section"] = "Diety";`).

---

### 👤 Dawid (Moduł 1 - Koszyk, Klienci i Zamówienia)

Dawid wdrożył proces zamawiania, koszyk, obsługę adresów oraz płatności.

#### Ważna uwaga dotycząca widoków Dawida:
*   Widoki Dawida są przeznaczone **dla klientów końcowych** (e-commerce).
*   **Nie może on zmieniać layoutu** na `_LayoutStaff`. Widoki koszyka (`Cart/Index.cshtml`), checkoutu (`Checkout/Index.cshtml`) oraz widoki adresów i zamówień klienta muszą dziedziczyć po standardowym klienckim layoucie `Layout = "_Layout";`.

#### Migracja na Dappera:
*   Repozytoria adresów (`AddressRepository.cs`), zamówień (`OrderRepository.cs`) oraz kalendarza dostaw muszą zostać przepisane na zapytania SQL za pomocą Dappera.
*   Należy zachować szczególną ostrożność przy pobieraniu pozycji zamówień (Eager loading w `OrderRepository.cs` przy pomocy Dappera można zrealizować poprzez zapytanie z `JOIN` i mapowanie typu `QueryAsync<Order, OrderItem, Order>(...)`).

---

### 👤 Pawciot (Moduł 5 - HR, RBAC i Pracownicy)

Pawciot wdrożył tabele biletów i pracowników oraz logowanie bypassowe.

#### Kluczowy problem w kodzie Pawciota:
Na jego zdalnej gałęzi brakuje plików interfejsów repozytoriów domenowych (np. `ITicketRepository.cs`, `IWorkScheduleRepository.cs`). Z tego powodu kod na jego gałęzi się nie kompiluje.

#### Rozwiązanie:
1.  Pawciot musi upewnić się, że dodał do repozytorium git i wypchnął wszystkie pliki interfejsów w `src/KuchniaUCygana.Domain/Interfaces/`.
2.  Zgodnie z wytycznymi w [sprint4b_team_integration_guide.md](file:///c:/Users/cyunc/source/repos/Projekt%20Platforma%20Cateringowa/Kuchnia%20Cygana/docs/guides/sprint4b_team_integration_guide.md#L50-L89), musi zintegrować akcję `AccountController.POST Login` w celu odpytywania bazy danych i przypisywania rzeczywistych ról z `AppRoles.cs`, zamiast twardo zakodowanych danych testowych.
3.  Zapytania w `TicketRepository.cs` i `WorkScheduleRepository.cs` muszą zostać przepisane na Dappera.

---

## 4. Rekomendowana procedura łączenia (Reconstruction/Cherry-Pick)

Z uwagi na rozległe konflikty plików (usunięcia całych katalogów widoków/kontrolerów na gałęziach deweloperów wynikające ze starego punktu startowego), **odradza się bezpośrednie użycie `git merge develop` na ich gałęziach**. Może to doprowadzić do utraty nowo wdrożonych elementów Tablera.

### Zalecana procedura integracji dla każdego programisty:

1.  **Zabezpieczenie zmian (kopia robocza)**:
    Stworzenie tymczasowego backupu swoich plików lub lokalnej gałęzi.
2.  **Stworzenie nowej gałęzi z najnowszego develop**:
    Pobranie najnowszego `develop` i utworzenie nowej gałęzi (np. `feature/m4-logistics-dapper` dla Tomasza):
    ```bash
    git checkout develop
    git pull origin develop
    git checkout -b feature/m4-logistics-dapper
    ```
3.  **Wydobycie zmian (Cherry-Pick)**:
    Przeniesienie swoich commitów z oryginalnej gałęzi na nową czystą gałąź za pomocą:
    ```bash
    git cherry-pick <SHA-commitów>
    ```
    *Wskazówka: Dzięki temu przeniesione zostaną wyłącznie faktycznie zmienione pliki (migracje, serwisy, modele, dedykowane kontrolery), bez ryzyka nadpisania i usunięcia struktury widoków z najnowszego develop.*
4.  **Wdrożenie standardu Dappera i dostosowanie kodu**:
    *   Usunięcie zależności od `ServiceStack` z kodu.
    *   Refaktoryzacja repozytoriów na Dapper (według wzorca).
    *   Weryfikacja kompilacji i działania testów (`dotnet build` + `dotnet test`).
5.  **Dopasowanie layoutu**:
    Ustawienie odpowiedniego layoutu (`_LayoutStaff` lub `_Layout`) w widokach Razor.
6.  **Weryfikacja działania w Dockerze**:
    Uruchomienie bazy danych i aplikacji w kontenerze deweloperskim i wizualna weryfikacja.

---
*Opracowano na podstawie wytycznych z [dapper-migration-guide.md](file:///c:/Users/cyunc/source/repos/Projekt%20Platforma%20Cateringowa/Kuchnia%20Cygana/docs/guides/dapper-migration-guide.md) oraz [sprint4b_team_integration_guide.md](file:///c:/Users/cyunc/source/repos/Projekt%20Platforma%20Cateringowa/Kuchnia%20Cygana/docs/guides/sprint4b_team_integration_guide.md).*
