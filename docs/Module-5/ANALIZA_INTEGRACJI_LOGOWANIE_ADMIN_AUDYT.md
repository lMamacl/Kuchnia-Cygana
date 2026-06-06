# Analiza integracji logowania, admina i audytu

## Zakres integracji

Do obecnego `Mamac` zostaly wciagniete zmiany z `origin/pawciot_laczy` dotyczace logowania pracownikow, profilu, HR, BOK, panelu admina oraz `SystemLogs`. W konfliktach jako baza zostala przyjeta implementacja Pawciota, z minimalnymi dostosowaniami po stronie naszej aplikacji tam, gdzie bylo to konieczne do kompilacji i zachowania istniejacych modulow M3/M4.

Zachowane zostaly istniejace mechanizmy magazynu, kompletacji, zaladunku, powiadomien i awarii kompletacji. Nie podpinano logow magazynowych ani historii transakcji do adminowego `SystemLogs`.

## Logowanie i role

Logowanie Pawciota jest podlaczone do obecnego cookie auth ASP.NET Core. Standardowy formularz logowania szuka uzytkownika po `Users.Email`, weryfikuje haslo przez BCrypt i zapisuje claimy `NameIdentifier`, `Name`, `Email` oraz role. Na tej podstawie dzialaja atrybuty `[Authorize]` w widokach staff/admin/HR/BOK.

Role zostaly rozszerzone o role dzialowe: HR, HRManager, BOK, BOKManager, Logistics/Driver i odpowiednie role managerskie. Administrator dostaje rozszerzony zestaw rol w claimach, co pozwala mu widziec dzialy staff bez przepinania osobnej logiki autoryzacji.

Dev-login Pawciota pozostal w kodzie zgodnie z aktualna decyzja na tym etapie. Mechanizm jest ograniczony do `env.IsDevelopment()`, ale nadal powinien byc jednym z pierwszych punktow do decyzji przy kolejnym security pass.

## Admin, HR i BOK

Panel admina Pawciota wprowadza dashboard z uzytkownikami oraz widok `Admin/Logs` oparty o `IAuditLogService` i tabele `SystemLogs`. Zachowano rowniez nasze adminowe awarie kompletacji przez dopiecie akcji `PackingIncidents` do scalonego `AdminController`.

Widoki HR i BOK sa wpięte jako osobne sekcje staff panelu. HR korzysta z `IHumanResourcesService`, encji pracownikow, dzialow, urlopow i grafikow. BOK korzysta z `ICustomerSupportService`, ticketow i zalacznikow. Na tym etapie wyglada to jak osobna warstwa Module 5, a nie gleboko zintegrowana obsluga operacyjna wszystkich modulow.

Nawigacja staff zostala zlozona jako best-of-both: zostaly sekcje Pawciota dla HR/BOK/Admin Logs, a przywrocone zostaly nasze dzialajace wejscia do FEFO, historii transakcji, HACCP locations, rework kuchni, zaladunku oraz adminowych awarii kompletacji.

## Logi i audyt

`SystemLogs` nalezy traktowac teraz jako ogolny audyt administracyjny Module 5. Zawiera zdarzenia zapisywane przez `AuditLogService`, m.in. akcje, encje docelowe i kontekst uzytkownika.

Logi magazynowe, transakcje magazynowe, HACCP, packing status logi i historia awarii kompletacji pozostaja osobnymi logami operacyjnymi. To jest obecnie poprawny podzial: adminowe `SystemLogs` nie maja jeszcze modelu, ktory bezpiecznie zastapi szczegolowa historie magazynu i kompletacji.

Rekomendacja: nie laczyc teraz tych dwoch systemow. Wspolny model audytu warto projektowac dopiero po scanowaniu calego stanu, z decyzja czy ma to byc warstwa indeksujaca zdarzenia z modulow, czy pelne ujednolicenie logow.

## Migracje i kompatybilnosc

Kolizja migracji Pawciota `508_AddBaseColumnsToModule5Tables` zostala rozdzielona przez przeniesienie tej poprawki do migracji `510_AddBaseColumnsToModule5Tables`. Sama logika migracji zostala zachowana: dodaje brakujace `CreatedAt`/`UpdatedAt` dla `Departments` i `SystemLogs`.

DI zostalo spiete przez rejestracje serwisow Pawciota w warstwie Application oraz repozytoriow Module 5 w Infrastructure. Istniejace serwisy M3/M4 pozostaly na swoich rejestracjach.

## Stan po weryfikacji

`dotnet build KuchniaUCygana.sln --no-restore` przechodzi. Wystepuja liczne istniejace ostrzezenia analyzerow, ale bez bledow kompilacji.

`dotnet test KuchniaUCygana.sln --no-restore` poza sandboxem uruchomil Testcontainers i zakonczyl sie wynikiem 150/156. Szesc porazek dotyczy istniejacych testow M3/M4: wymuszenia gotowosci produkcji przed drukiem etykiety foliowania oraz sortowania lookupu magazynowego. Nie byly poprawiane w tej integracji, bo nie sa blockerem Pawciotowego merge'a i wchodza w logike magazynu/kompletacji.

## Rekomendacje do pelnego audytu

1. Zrobic osobny security pass dla dev-login i skrotow w layoucie.
2. Sprawdzic pelny przeplyw realnego logowania seedowanymi uzytkownikami i BCrypt hashami.
3. Przejrzec uprawnienia HR/BOK/Admin wzgledem rzeczywistych rol biznesowych.
4. Zdecydowac, czy `SystemLogs` ma byc tylko admin audit, czy docelowo centralny indeks zdarzen z modulow.
5. Dopiero po scanowaniu projektowac ewentualny wspolny model audytu dla magazynu, HACCP, kompletacji i admina.
