# 🤝 Instrukcja Integracji Zespołowej i Przejścia na Prawdziwe Logowanie

Ten dokument zawiera wytyczne dla członków zespołu (Dawid, Gabriel, Tomasz, Pawciot) po scaleniu zmian infrastruktury logowania oraz szablonu Tabler z gałęzi `develop` (lub `Mamac`).

---

## 1. Instrukcje ogólne dla wszystkich (Tomasz, Gabriel, Dawid)

Po pobraniu najnowszych zmian z `develop`:

1.  **Przebudowanie kontenerów**:
    Wszyscy muszą przebudować i zrestartować środowisko Docker, aby zastosować konfigurację deweloperską i skompilować kontrolery:
    ```bash
    docker-compose down
    docker-compose up -d --build web
    ```
    *Uwaga: W pliku `docker-compose.yml` zmieniono `ASPNETCORE_ENVIRONMENT` na `Development`, co włącza panel deweloperski lokalnie u każdego.*

2.  **Jak testować swoje ekrany**:
    *   Wchodzimy na `http://localhost:8080/Account/Login`.
    *   Pod formularzem logowania znajduje się sekcja **Panel Programisty (Bypass HR)**.
    *   Wystarczy jedno kliknięcie odpowiedniej roli, aby system zalogował nas i automatycznie przekierował do właściwego widoku.

3.  **Spójność Layoutu (Tabler)**:
    *   **Gabriel (M2 — Edytor Diet)** i **Tomasz (M4 — Logistyka & Pojazdy)**: Wasze widoki panelu pracowniczego (back-office) muszą dziedziczyć po layoucie Tablera.
    *   **Tomasz (Uwaga dotycząca widoków Pojazdów)**: Na swojej gałęzi dodałeś `VehiclesController` i widok `Views/Vehicles/Index.cshtml` bez zdefiniowanego layoutu. Domyślnie załadują się one na layoucie klienckim. Aby to poprawić, dodaj na początku każdego widoku pod `Views/Vehicles/` oraz `Views/Logistics/`:
        ```cshtml
        @{
            Layout = "_LayoutStaff";
            ViewData["Title"] = "Pojazdy"; // lub odpowiednio "Trasy", "Edycja"
            ViewData["Section"] = "Logistyka";
            ViewData["Description"] = "Zarządzanie flotą pojazdów dostawczych.";
        }
        ```
    *   **Stylizacja tabel (Wskazówka Premium)**:
        Standardowe tabele Bootstrapa, które masz w `Vehicles/Index.cshtml`, będą działać, ale aby uzyskać pełen "look & feel" premium z Tablera, opakuj tabelę w kartę i nadaj jej odpowiednie klasy:
        ```html
        <div class="card">
            <div class="table-responsive">
                <table class="table table-vcenter card-table">
                    <!-- zawartość tabeli -->
                </table>
            </div>
        </div>
        ```
    *   **Dawid (M1 — Koszyk, Zamówienia)**: Twoje widoki są przeznaczone **dla klienta indywidualnego**. Dlatego **nie zmieniasz layoutu** – Twoje widoki nadal powinny korzystać z domyślnego `Layout = "_Layout";` (standardowy e-commerce z górnym paskiem, bez bocznego menu Tablera).

---

## 2. Szczegółowe instrukcje dla Pawciota (HR / RBAC — Moduł 5)

Pawciot, Twoja gałąź `Pawciot_tym_razem_moze_dzialac_bedzie` ma za zadanie dostarczyć pełne zarządzenie uprawnieniami i pracownikami. Mock Auth został zaprojektowany tak, abyś mógł go łatwo zastąpić prawdziwą bazodanową tożsamością.

### Krok 1: Co musisz najpierw naprawić u siebie
Przed mergowaniem do `develop` upewnij się, że Twój branch się kompiluje. Obecnie brakuje encji (np. `Employee.cs`, `Department.cs`, `Ticket.cs`) w katalogu `Domain/Entities` oraz interfejsów repozytoriów w `Domain/Interfaces`. Musisz te pliki dodać do projektu i je zacommitować.

### Krok 2: Jak działa obecny bypass (Mock Auth)
W `AccountController.cs` w akcji `DevLogin` wstrzykujemy uprawnienia bezpośrednio jako stałe stringi:
```csharp
var claims = new List<Claim>
{
    new Claim(ClaimTypes.Name, $"dev-{role.ToLower()}@kuchniaucygana.pl"),
    new Claim(ClaimTypes.Role, role) // np. "KitchenManager", "WarehouseManager"
};
```
Dzięki temu filtry `[Authorize(Roles = "KitchenManager")]` na naszych kontrolerach działają prawidłowo, chociaż baza danych HR jeszcze nie funkcjonuje.

### Krok 3: Jak przejść na prawdziwe logowanie
Gdy Twoja baza danych i serwisy będą gotowe, musisz:

1.  **Zaimplementować standardową akcję POST Login**:
    W `AccountController.cs` (linie 40-46) znajduje się placeholder akcji logowania formularzem. Musisz go zmodyfikować tak, aby:
    *   Pobrał użytkownika z bazy danych po e-mailu.
    *   Zweryfikował hasło.
    *   Pobrał powiązanego pracownika: `var employee = await employeeRepository.GetByUserIdAsync(user.Id);`.
    *   Odczytał jego rolę / dział (np. na podstawie tabeli `Employee` i jego przypisania).
    *   Wygenerował claimsy z rolą odpowiadającą poziomowi dostępu:
        ```csharp
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Role, employee.Role.ToString()) // Np. "KitchenManager"
        };
        ```
    *   Zalogował przez `HttpContext.SignInAsync`.

2.  **Pozostawienie Panelu Programisty w trybie Development**:
    Nasz `DevLogin` i przyciski na widoku logowania są zabezpieczone warunkiem `env.IsDevelopment()`. Zaleca się **pozostawienie** tego panelu na środowiskach deweloperskich. Pozwoli to każdemu na szybkie testowanie aplikacji bez konieczności zakładania kont w bazie danych za każdym razem.
    *W środowisku produkcyjnym (Production) panel automatycznie znika, a wywołanie endpointu `/account/dev-login` zwraca błąd `400 Bad Request`.*

3.  **Mapowanie nazw ról**:
    Upewnij się, że nazwy ról zapisywane w bazie danych/tokenach są dokładnie takie same jak w pliku `Domain/Constants/AppRoles.cs`. W kontrolerach używamy m.in.:
    *   `KitchenManager` oraz `Kitchen`
    *   `WarehouseManager` oraz `Warehouse`
    *   `PackingManager` oraz `Packing`
    *   `Admin`
    *   `Dietitian`
