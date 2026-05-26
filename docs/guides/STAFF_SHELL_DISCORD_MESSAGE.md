# Wiadomość na Discord: wspólny shell Tabler dla panelu staff

Cześć, wrzuciłem wspólny shell Tabler dla panelu pracowniczego/back-office. To jest warstwa WWW do mocków i ekranów
operacyjnych, a nie pełny merge Modułu 3. Celem jest to, żeby każdy mógł podpiąć swoje testowe widoki pod jeden układ
bez ciągnięcia cudzej domeny, migracji i serwisów.

Najważniejsze zasady:

- widoki back-office ustawiają `Layout = "_LayoutStaff";`;
- linki do menu i wyszukiwarki dopisujemy w `StaffNavigationCatalog`;
- ikony dopisujemy w `StaffIconCatalog`;
- wspólne style i skrypty są w `wwwroot/css/staff.css` oraz `wwwroot/js/staff-shell.js`;
- dla samych mocków/widoków nie ruszamy `DependencyInjection.cs`, `Program.cs`, migracji ani Dockera;
- po zmianach Razor/CSS/JS w Dockerze przebudowujemy tylko `web`: `docker compose up -d --build --force-recreate web`;
- nie róbcie `docker compose down -v` dla zmian frontendowych, bo to kasuje lokalną bazę.

Gabriel:

- katalog, diety, posiłki i przepisy podepnij pod `_LayoutStaff`;
- swoje widoki dopisz do `StaffNavigationCatalog`, żeby działały z menu i wyszukiwarką;
- nie nadpisuj globalnego `_Layout.cshtml`, bo on zostaje dla części klienta/B2C.

Pawciot:

- jeżeli masz widoki admin/helpdesk/HR albo mockupy, podepnij je pod `_LayoutStaff`;
- DI dodawaj tylko dla realnych serwisów, nie dla statycznych ekranów testowych;
- jeśli brakuje pozycji menu, dodaj sekcję lub widok w `StaffNavigationCatalog`.

Tomek:

- zachowaj swoje `VehiclesController` i logikę pojazdów/logistyki;
- dopnij linki i widoki logistyki pod nowy shell staff;
- szczególnie uważaj na konflikty w `_Layout.cshtml`, `Program.cs` i `LogisticsController.cs`;
- jeśli konflikt dotyczy tylko układu strony, preferuj `_LayoutStaff`, a nie przebudowę layoutu klienta.

Dawid:

- moduł zamówień, koszyk, checkout i część B2C zostają przy `_Layout.cshtml`;
- testowe widoki back-office lub operacyjne podpinamy pod `_LayoutStaff`;
- konflikty w `DependencyInjection.cs` rozwiązuj przez dopisanie własnych rejestracji, nie przez zastępowanie całego pliku;
- jeżeli Docker pokazuje stary wygląd, przebuduj obraz `web`, nie bazę.

Docelowo na `develop` najpierw ma trafić lekki web-only shell. Pełne moduły domenowe każdy dociąga i adaptuje na swoim
branchu po zaciągnięciu aktualnego `develop`.
