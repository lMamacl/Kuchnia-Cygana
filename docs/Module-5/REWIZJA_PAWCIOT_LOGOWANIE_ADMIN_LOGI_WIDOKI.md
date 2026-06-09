# Rewizja Pawciot: logowanie, admin, logi i widoki po scaleniu

## 1. Zakres i metoda oceny

Ten dokument jest statyczna rewizja kodu po scaleniu zmian z `origin/pawciot_laczy`. Ocena zostala wykonana po kontrolerach, serwisach, widokach Razor, migracjach, seedingu, staff navigation i istniejacych mechanizmach logow/powiadomien.

Nie uruchamiano dodatkowego browser smoke testu i nie przechodzono widokow recznie. Wnioski o UI dotycza tego, co widac w kodzie widokow i tras.

## 2. Co Pawciot wprowadzil

Najwieksza wartosc integracji to wprowadzenie realnego obszaru pracowniczego wokol logowania, profilu, HR, BOK i admina. To nie jest juz tylko makieta nawigacji - sa kontrolery, serwisy aplikacyjne, DTO, walidatory, widoki i seed danych.

Glowny zakres:

- Logowanie pracownikow przez `Users.Email` i BCrypt hash hasla.
- Cookie auth ASP.NET Core z claimami `NameIdentifier`, `Name`, `Email` i rolami.
- Profil pracownika z grafikiem oraz formularzem wniosku urlopowego.
- Role dzialowe: HR, HRManager, BOK, BOKManager, Logistics, LogisticsManager, Driver, DriverManager oraz role M3/M4.
- Sekcje staff navigation widoczne wedlug roli.
- HR: dzialy, pracownicy, urlopy, grafik.
- BOK: dashboard ticketow, lista zgloszen, przypisywanie i zmiana statusu.
- Admin: dashboard uzytkownikow, widok logow `Admin/Logs`, widoki `Users/Roles/Settings`.
- `SystemLogs` jako tabela i serwis audytu administracyjnego.
- Seeding uzytkownikow z BCrypt hashami oraz kartotek HR.
- Migracja `510_AddBaseColumnsToModule5Tables` dodajaca `CreatedAt` i `UpdatedAt` dla `Departments` oraz `SystemLogs`.

Po naszej stronie zachowano tez kluczowe wejscia M3/M4 w staff navigation: magazyn, FEFO, HACCP, historia transakcji, kompletacja, zaladunek, rework kuchni i adminowe awarie kompletacji.

## 3. Logowanie i profil pracownika

Logowanie standardowe dziala w sposob docelowy: formularz przyjmuje email i haslo, kontroler szuka uzytkownika po `Users.Email`, porownuje haslo z `PasswordHash` przez BCrypt, a potem wystawia cookie auth. Po zalogowaniu redirect zalezy od glownej roli: HR idzie do HR, BOK do BOK, kuchnia do produkcji, magazyn do magazynu, logistyka do logistyki, driver do mobile, admin do staff.

Plusy:

- Mechanizm jest zgodny z obecnym cookie auth i nie wymaga osobnego systemu sesji.
- BCrypt w seedingu i weryfikacji hasla jest dobrym kierunkiem.
- Role sa przepinane na claimy, wiec `[Authorize(Roles = "...")]` dziala naturalnie.
- Manager dostaje takze role bazowa, a Admin dostaje role staff, co upraszcza dostep do paneli.
- Profil laczy konto uzytkownika z kartoteka pracownika, grafikiem i wnioskami urlopowymi.

Wady i ryzyka:

- Dev-login nadal istnieje w widokach i kontrolerze. Jest ograniczony do `env.IsDevelopment()`, ale powinien byc osobnym blockerem przed jakimkolwiek srodowiskiem pokazowym/produkcyjnym.
- Brakuje audytu logowania: udane logowanie, bledne haslo, wylogowanie i odmowa dostepu nie trafiaja do `SystemLogs`.
- Brakuje mechanizmu resetu hasla, wymuszenia zmiany hasla i polityki blokady po wielu probach.
- Rozszerzanie roli Admin na wszystkie role jest wygodne, ale w audycie warto rozroznic "admin ma dostep" od "admin dziala jako konkretny dzial".
- Profil pracownika zalezy od dopasowania `UserId` do `Employee`. Jesli kartoteka HR nie istnieje, czesc profilu traci sens i pokazuje blad przy wniosku urlopowym.

Ocena: dobry fundament realnego auth, ale wymaga security pass. Najpilniejsze sa decyzja o dev-login, audyt zdarzen logowania i procesy zarzadzania haslem.

## 4. Admin i SystemLogs

Admin dashboard pokazuje liczbe uzytkownikow, role w uzyciu, liczbe logow audytu i logi z dzisiaj. Widok `Admin/Logs` pokazuje czas, uzytkownika, akcje, obiekt, stare/nowe wartosci i IP.

Plusy:

- Admin dostal czytelna powierzchnie do ogladania logow.
- `IAuditLogService` ma metody odczytu wszystkich logow, logow po uzytkowniku, po obiekcie oraz tworzenia logu.
- Migracje tworza `SystemLogs`, indeksy i archiwum `SystemLogsArchive` z procedura archiwizacji.
- Admin zachowuje nasze `PackingIncidents`, wiec nie zgubilismy operacyjnej obslugi awarii kompletacji.

Wady i ryzyka:

- `SystemLogs` nie sa jeszcze centralnym audytem. Serwis istnieje, ale nie widac automatycznych wywolan z HR, BOK, logowania, magazynu, produkcji, kompletacji ani logistyki.
- `AuditLogService` pobiera `GetAllAsync()` i filtruje/sortuje w pamieci. Przy wiekszej liczbie logow bedzie to problem wydajnosciowy.
- Widok `Admin/Logs` nie ma paginacji, filtrow, wyszukiwarki, zakresu dat ani filtrowania po module.
- Brakuje maskowania `OldValue/NewValue`. Jesli ktos zacznie zapisywac payloady bez kontroli, mozna przypadkiem pokazac dane wrazliwe.
- Brakuje jawnej taksonomii eventow. `Action`, `TargetEntity`, `TargetId` sa elastyczne, ale bez slownika beda niespojne.
- `Users/Roles/Settings` wygladaja bardziej jak powierzchnie preview niz pelny panel administracyjny.

Ocena: dobry start dla admin audit view, ale jeszcze nie pelny audyt systemowy. Na razie to raczej "miejsce, gdzie mozna pokazac logi", a nie "mechanizm, ktory zbiera kluczowe zdarzenia z calej aplikacji".

## 5. HR

HR sklada sie z dashboardu, pracownikow, dzialow, urlopow i grafiku. Kontroler jest chroniony rolami `HR`, `HRManager`, `Admin`. Czesci operacji, jak dezaktywacja pracownika, review urlopu i usuniecie grafiku, sa ograniczone do `HRManager` albo `Admin`.

Plusy:

- Spina kartoteki pracownikow z uzytkownikami.
- Daje podstawowy dashboard: wnioski do decyzji, aktywni pracownicy, dzisiejsze zmiany, nieaktywni.
- Ma walidacje i serwis aplikacyjny zamiast trzymania logiki bezposrednio w widoku.
- Profil pracownika korzysta z tych samych danych HR, wiec modul nie jest calkiem oderwany.

Wady i braki:

- Widoki sa raczej tabelaryczno-formularzowe i proste. Dla malej liczby danych wystarczy, ale przy realnym zespole zabraknie filtrow, paginacji, wyszukiwania i detail view.
- Brakuje pelnych ekranow edycji pracownika, dzialu i grafiku mimo ze serwisy maja metody update.
- Brakuje audytu operacji HR: utworzenie pracownika, dezaktywacja, decyzja urlopowa i zmiana grafiku nie trafiaja do `SystemLogs`.
- Brakuje powiadomien po decyzji urlopowej albo zmianie grafiku.
- Przy tworzeniu pracownika formularz wymaga emaila pracownika mimo ze wybierany jest `UserId`; warto potem sprawdzic, czy nie grozi to rozjazdem danych.

Ocena: HR jest sensownie podlaczony jako modul wewnetrzny, ale jest na poziomie MVP. Najbardziej brakuje audytu, powiadomien i ergonomii list.

## 6. BOK

BOK sklada sie z dashboardu z kolejka zgloszen oraz widoku ticketow. Kontroler jest chroniony rolami `BOK`, `BOKManager`, `Admin`. Mozna utworzyc ticket, przypisac do pracownika BOK/Admin i zmienic status.

Plusy:

- Prosty, zrozumialy workflow: nowe zgloszenie, kolejka, priorytet, przypisanie, status.
- Dashboard od razu pokazuje oczekujace, nieprzypisane, wysoki priorytet i zamkniete dzisiaj.
- Serwis BOK ma wsparcie dla zalacznikow i mapowania klient/przypisany uzytkownik.

Wady i braki:

- Nie ma detail view ticketu. Przy realnym BOK potrzebna bedzie historia kontaktu, komentarze, timeline, notatki i SLA.
- Zalaczniki sa w serwisie, ale nie widac ich w obecnych widokach.
- Brakuje audytu: utworzenie ticketu, przypisanie i zmiana statusu nie trafiaja do `SystemLogs`.
- Brakuje powiadomien po przypisaniu lub eskalacji ticketu.
- Brakuje filtrow po statusie, priorytecie, przypisanym pracowniku, kliencie i dacie.

Ocena: BOK jest dobrym szkieletem kolejki, ale jeszcze nie pelnym systemem obslugi klienta.

## 7. Ocena widokow jako calosci

Widoki Pawciota dobrze wpasowuja sie w istniejacy staff shell. Uzywaja kart, metryk, tabel i formularzy podobnych do reszty panelu. Nawigacja staff zostala rozszerzona o HR, BOK i Admin Logs, a po scaleniu zachowuje tez nasze wejscia magazynowe i kompletacyjne.

Mocne strony:

- Spójny staff layout i wspolna nawigacja.
- Role kontroluja widocznosc sekcji.
- Dashboardy daja szybki podglad stanu.
- Formularze maja podstawowe walidacje i `AntiForgeryToken`.
- Admin, HR i BOK sa od razu dostepne z panelu pracowniczego.

Slabe strony:

- Czesc widokow jest preview/MVP, szczegolnie `Admin/Users`, `Admin/Roles`, `Admin/Settings`.
- Brak paginacji i filtrow w wielu tabelach.
- Brak detail views dla ticketow, pracownikow, dzialow, urlopow i logow.
- `Admin/Logs` moze stac sie nieczytelny przy dlugich JSON-ach w `OldValue/NewValue`.
- Dev-login w layout/login pozostaje niekomfortowy z perspektywy bezpieczenstwa, nawet jesli jest ograniczony do development.

## 8. Co realnie trafia do logow

Aktualnie `SystemLogs` ma strukture i widoki, ale nie widac szerokiego automatycznego zapisu zdarzen biznesowych.

Realnie dostepne:

- `SystemLogs` jako tabela.
- `SystemLogsArchive` i procedura archiwizacji z migracji SBD.
- `IAuditLogService.CreateSystemLogAsync(...)` jako reczny punkt zapisu.
- `Admin/Index` i `Admin/Logs` jako odczyt ostatnich logow.

Czego nie widac jako automatyczny zapis do `SystemLogs`:

- Udane lub nieudane logowanie.
- Wylogowanie.
- Utworzenie pracownika, dezaktywacja, decyzja urlopowa, zmiana grafiku.
- Utworzenie ticketu, przypisanie, zmiana statusu.
- Przygotowanie posilku, druk etykiety foliowania, wygenerowanie planu produkcji.
- Przyjecie/wydanie magazynowe, korekta inwentaryzacji, waste, HACCP temperature log.
- Wygenerowanie tras, zmiana trasy, przypisanie kierowcy, wygenerowanie manifestu.
- Awarie kompletacji i ich obsluga jako admin audit.

Istniejace operacyjne logi/moduly, ktore sa osobne:

- Magazyn: transakcje magazynowe, partie, FEFO, waste, historia transakcji.
- HACCP: logi temperatur i alerty.
- Kompletacja: packing incidents, statusy i przeplywy rework/waste.
- Zaladunek: manifesty, loading workflow i powiadomienia.
- Powiadomienia: `Notifications` i `UserNotifications`.

Wniosek: `SystemLogs` powinno byc traktowane jako potencjalny przekrojowy indeks audytowy, a nie jako zamiennik szczegolowych logow operacyjnych.

## 9. Czy podpinac magazyn i logistyke do SystemLogs

Nie rekomenduje hurtowego przepinania magazynu ani logistyki do `SystemLogs`.

Powod:

- Magazyn i logistyka maja dane operacyjne o wysokiej szczegolowosci, ktore wymagaja wlasnych tabel i zapytan.
- `SystemLogs` nie ma jeszcze wydajnego modelu filtrowania, paginacji i retencji na duze wolumeny.
- Pelne kopiowanie operacyjnych zdarzen do `SystemLogs` zdubluje dane i moze szybko wygenerowac szum.
- Admin potrzebuje syntetycznego audytu decyzji i milestone'ow, a nie kazdego szczegolu technicznego.

Rekomendowany kierunek:

- Zostawic szczegolowe logi w modulach operacyjnych.
- Dodac cienka warstwe `BusinessAudit`/`AuditEventPublisher`, ktora zapisuje do `SystemLogs` tylko kluczowe milestone'y.
- Ustalic slownik eventow, np. `Production.MealPrepared`, `Production.FoilLabelPrinted`, `Logistics.RoutesGenerated`, `Logistics.RouteChanged`, `Warehouse.StockIssued`, `Warehouse.WasteRegistered`, `Haccp.CriticalTemperatureAlert`, `Packing.IncidentReported`, `Packing.IncidentResolved`, `Loading.RouteManifestGenerated`.
- W `SystemLogs` trzymac krotki opis, module, target, correlation id, user id, IP i bezpieczny JSON bez danych wrazliwych.
- Dla szczegolow linkowac do widoku modulowego zamiast kopiowac caly payload.

Przyklady, ktore warto emitowac do `SystemLogs` w przyszlosci:

- Przygotowano posilek X lub potwierdzono gotowanie pozycji planu produkcji.
- Wydrukowano etykiete foliowania dla produktu/pozycji.
- Wygenerowano trasy dostaw na dzien.
- Zmieniono trase, kierowce lub kolejnosc stopow.
- Wygenerowano manifest zaladunku.
- Zgloszono awarie kompletacji i zamknieto awarie.
- Zarejestrowano rozchod/waste z awarii.
- Wystapil krytyczny alert HACCP.

## 10. Powiadomienia

Powiadomienia nie wygladaja na przejete przez Pawciota jako nowy model. Istnial juz osobny mechanizm `Notifications` i `UserNotifications`, z serwisem, repozytorium, topbarem i widokiem listy powiadomien.

Integracja dotknela powiadomien glownie przez staff navigation i topbar:

- w topbarze nadal jest podsumowanie powiadomien z `INotificationService`;
- w nawigacji staff jest wejscie do `Notifications/Index`;
- powiadomienia sa filtrowane pod zalogowanego uzytkownika;
- istnieja akcje oznaczania jako przeczytane i archiwizacji.

Miejsca, ktore juz tworza powiadomienia, sa poza Pawciotowym HR/BOK:

- `TemperatureService` przy alertach HACCP;
- `PackingIncidentService` przy awariach i rework;
- `LoadingService` przy zdarzeniach zaladunku.

Braki:

- HR nie wysyla powiadomien po decyzji urlopowej lub zmianie grafiku.
- BOK nie wysyla powiadomien po przypisaniu/eskalacji ticketu.
- `SystemLogs` i powiadomienia nie sa skorelowane jednym correlation id.

Wniosek: powiadomienia zostaly zachowane i lekko wyeksponowane w nawigacji, ale Pawciot nie rozbudowal ich logiki domenowej.

## 11. Najwazniejsze zalety implementacji

- Wprowadza realny staff/auth/admin/HR/BOK zamiast samego preview.
- Dobrze wykorzystuje cookie auth i role.
- Daje podstawowy profil pracownika i HR self-service przez wnioski urlopowe.
- Admin zyskuje pierwsza wersje przegladu audytu.
- Moduly sa podzielone przez serwisy aplikacyjne, DTO i walidatory.
- Staff navigation zaczyna odzwierciedlac rzeczywiste role operacyjne.
- Nie rozbija dotychczasowych logow magazynu, HACCP, kompletacji i powiadomien.

## 12. Najwazniejsze wady i ryzyka

- Dev-login pozostaje w development i w UI, co wymaga jasnej decyzji przed pokazem/stagingiem.
- `SystemLogs` ma infrastrukture, ale brakuje automatycznego podpinania zdarzen.
- Brakuje paginacji/filtrow w logach oraz wiekszych listach HR/BOK.
- Brakuje maskowania danych w audycie.
- HR/BOK sa MVP: malo detail views, brak historii, brak pelnych edycji, brak powiadomien.
- Czesci admina sa preview.
- Serwisy audytu i HR/BOK czesto pobieraja cale kolekcje i mapuja w pamieci, co przy duzych danych bedzie slabym punktem.
- Brakuje spojnej taksonomii eventow i correlation id miedzy modulami.

## 13. Braki do dodania w kolejnych etapach

Najpierw:

- Decyzja i usuniecie albo twarde ukrycie dev-login.
- Audyt logowania: sukces, blad, logout, access denied.
- Audit hooks dla HR i BOK.
- Paginacja i filtrowanie `Admin/Logs`.
- Maskowanie `OldValue/NewValue`.
- Slownik eventow audytowych.

Pozniej:

- Cienka warstwa business audit dla kluczowych eventow M3/M4.
- Detail views dla ticketow, pracownikow, urlopow, dzialow i logow.
- Powiadomienia HR/BOK.
- Korelacja `SystemLogs`, powiadomien i logow operacyjnych.
- Retencja i automatyczny job dla archiwizacji `SystemLogs`.
- Dashboard admina z filtrami po module i typie zdarzenia.

## 14. Stan techniczny znany po integracji

Znany stan po scaleniu:

- `dotnet build KuchniaUCygana.sln --no-restore` przechodzil.
- `dotnet test KuchniaUCygana.sln --no-restore` poza sandboxem uruchomil Testcontainers i dal wynik 150/156.
- Szesc porazek testow dotyczylo istniejacej logiki M3/M4: etykiety foliowania/gotowosc produkcji oraz sortowanie lookupu magazynowego.
- Te porazki nie wygladaja na bezposredni skutek Pawciotowego HR/BOK/Admin, ale powinny zostac rozpatrzone osobno przed stabilizacja calej galezi.

## 15. Konkluzja

Implementacja Pawciota jest dobrym krokiem w strone realnego panelu pracowniczego i administracyjnego. Najlepiej traktowac ja jako MVP obszaru Module 5: dziala koncepcyjnie, jest wpięta w role i staff shell, ale wymaga drugiego przejscia pod bezpieczenstwo, audyt, UX tabel i kompletne przeplywy HR/BOK.

Logow magazynowych i logistycznych nie nalezy teraz laczyc hurtowo z adminowym `SystemLogs`. Lepszy kierunek to zostawienie szczegolowych danych w modulach operacyjnych i dodanie przekrojowego audytu milestone'ow do `SystemLogs`, z jawna taksonomia eventow, maskowaniem danych i korelacja zdarzen.

## 16. Uzupełnienie po dodatkowej rewizji

### Rejestracja pracownika i widok pracujacych osob

Formularz HR "Nowy pracownik" nie tworzy nowego konta uzytkownika. Wybiera istniejace konto z `Users`, a potem zaklada kartoteke `Employee`. To jest technicznie poprawne rozdzielenie konta od kartoteki pracownika, ale UI tego nie tlumaczy. Uzytkownik moze miec wrazenie, ze "rejestruje pracownika", a tak naprawde laczy istniejace konto z dzialem, stanowiskiem i danymi HR.

Pracownik po dodaniu powinien pojawic sie w tabeli `HR/Employees`, ale brakuje osobnego widoku "kto faktycznie pracuje dzisiaj / teraz". Istnieje dashboard HR z dzisiejszym grafikiem i widok `HR/Schedules`, lecz nie ma operacyjnego widoku obecnosci albo aktywnej zmiany.

Wniosek: dopisac w przyszlosci jasny przeplyw `konto -> kartoteka pracownika -> grafik`, a dla operacji dziennych dodac widok "dzisiaj pracuja" z filtrami po zmianie i dziale.

### Obsluga wnioskow urlopowych

Po akceptacji lub odrzuceniu wniosku kontroler ustawia `TempData["Success"]`, a staff layout pokazuje alert. Techniczna konfirmacja wiec istnieje. Problem jest w UX: zaakceptowany albo odrzucony wniosek zostaje na tej samej liscie i nadal ma te same akcje decyzyjne. To nie wyglada jak domkniety proces.

Wniosek: widok HR powinien domyslnie pokazywac kolejke `Pending`, a zakonczone wnioski przeniesc do historii albo pokazac bez przyciskow akceptacji/odrzucenia. Status powinien miec czytelne etykiety tekstowe, nie tylko wartosci enum/liczbowe.

### Logout w staff shell

Endpoint `Account/Logout` istnieje i jest uzywany w layoucie kierowcy, ale nie ma widocznego wylogowania w glownym staff topbarze ani w standardowym layoucie klienta po zalogowaniu. Dla HR/BOK/Admin wyglada to jak realny blocker UX, bo uzytkownik nie ma oczywistej drogi wyjscia z sesji.

Wniosek: przy kolejnym pass nalezy dodac wylogowanie do dropdownu uzytkownika w staff topbarze i ewentualnie do glownego layoutu, bez zmiany logiki auth.

### Seeder

Dane Pawciota nie sa "webowym" seedem. Sa dodane w glownym `DatabaseSeeder` w infrastrukturze. Minimalny seeding tworzy uzytkownikow dla M3/M4/M5, w tym kuchnie, magazyn, kompletacje, dietetyka, logistyke, drivera, HR, BOK i admina. Ten sam seeder tworzy dzialy, pracownikow, grafiki i przykladowe wnioski urlopowe.

Wniosek: Pawciot korzysta z glownego mechanizmu seedowania projektu, ale warto opisac konta testowe i role w osobnej sekcji dokumentacji, bo teraz sa rozsiane w duzym seederze.

### Puste widoki administracji

`Admin/Users`, `Admin/Roles` i `Admin/Settings` renderuja tylko `_StaffPlaceholder`. Sa widoczne w nawigacji jako gotowe pozycje, ale funkcjonalnie sa puste.

Wniosek: do czasu implementacji nalezy je ukryc z nawigacji albo jawnie oznaczyc jako "w budowie". W aktualnym stanie tworza falszywe wrazenie gotowego panelu administracyjnego.

### BOK tickets a awarie kompletacji

BOK `Tickets` i `PackingIncidents` sa dwoma oddzielnymi mechanizmami. BOK obsluguje zgloszenia klienta, a `PackingIncidents` obsluguja operacyjne awarie kompletacji, rework kuchni, waste i blokady zaladunku/manifestu. Mechanicznie nie sa razem i nie maja wspolnej kolejki ani relacji.

Nie powinny byc bezrefleksyjnie scalane, bo maja inne zrodla i inna odpowiedzialnosc. Powinny jednak miec wspolny widok lub korelacje tam, gdzie awaria kompletacji generuje kontakt z klientem albo ticket BOK.

Wniosek: docelowo warto dodac "sprawy operacyjne" albo relacje `Ticket -> PackingIncident`, ale zostawic osobne modele domenowe.

### Migracja 507: trigger Batches i procedura archiwizacji SystemLogs

Migracja `507_AddSbdSqlObjectsAndIndexes` rejestruje kilka mechanizmow bazodanowych:

- `tr_Batches_UpdateIsDepleted` - trigger na tabeli `Batches`, ktory automatycznie ustawia `IsDepleted` po zmianie `CurrentQuantity`;
- `usp_ArchiveSystemLogs` - procedura archiwizacji starych rekordow z `SystemLogs` do `SystemLogsArchive` w paczkach przez parametr `@BatchSize`;
- indeks `IX_SystemLogs_Timestamp_Action_TargetEntity_UserId`;
- indeksy dla `Batches` i innych tabel;
- funkcje/obiekty SBD niezalezne od Module 5.

Pawciot nie wykorzystuje aktywnie tych mechanizmow w HR/BOK/Admin. To jest zgodne z obecnym zakresem, bo HR/BOK nie dotykaja tabeli `Batches`, a `usp_ArchiveSystemLogs` jest procedura utrzymaniowa, a nie mechanizm do generowania logow.

Wazne rozroznienie:

- Trigger `tr_Batches_UpdateIsDepleted` dziala automatycznie na poziomie bazy, kiedy magazyn zmienia `Batches`. Pawciot nie musi go wolac i nie powinien go uzywac w HR/BOK.
- Procedura `usp_ArchiveSystemLogs` nie generuje logow, tylko archiwizuje juz istniejace logi. Pawciot nie powinien jej wolac przy akcjach uzytkownika; powinna byc uruchamiana przez job utrzymaniowy albo reczny proces administracyjny.
- `BatchExpiryChangeLogs` to osobny operacyjny log magazynu dotyczacy zmian terminow partii. Pawciot nie powinien go uzywac jako ogolnego audytu HR/BOK/Admin.

Wniosek: Pawciot nie korzysta z tej mechaniki i nie powinien jej uzywac bezposrednio. Powinien natomiast emitowac wlasne zdarzenia do `SystemLogs` przez warstwe aplikacyjna, a procedura z migracji `507` powinna pozniej obslugiwac retencje/archiwizacje tych logow. Mechanika batchy magazynowych powinna pozostac w magazynie, a adminowy audyt powinien dostac tylko przekrojowe milestone'y, np. `Warehouse.BatchDepleted`, jezeli chcemy je pokazac administratorowi.
