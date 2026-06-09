# Generalny audyt repo i plan VolumeDemo

## Cel dokumentu

Ten dokument zbiera aktualny sweep repo po integracjach M2/M3 i ostatnich poprawkach
kuchni, kompletacji oraz staff. Ma pomoc zdecydowac, co mozemy realnie poprawic do
jutra bez pelnego reworka oraz jak przygotowac osobny profil `VolumeDemo` do testow
na duzej bazie.

Zakres:

- Modul 1: strona klienta, logowanie, checkout, zamowienia.
- Modul 2: diety, skladniki, posilki, przepisy-skladowe, plan M2.
- Modul 3: kuchnia, magazyn, produkcja, FEFO, etykiety, kompletacja.
- Modul 4: logistyka, trasy, kierowcy, pojazdy, zaladunek.
- Modul 5: HR, Admin, BOK.
- Seeder wolumenowy `VolumeDemo`.

Wnioski bazuja na lokalnym sweepie oraz raportach 4 agentow read-only:

- M1 + M4: klient i logistyka.
- M2: diety, skladniki, posilki, plan.
- M3: kuchnia, magazyn, kompletacja.
- M5 + seeder: HR, Admin, BOK i profil wolumenowy.

## Najwazniejsze wnioski

1. Modul 3 jest obecnie najblizej operacyjnego demo: produkcja, FEFO, gotowanie
   komponentow, etykiety, packing i loading maja juz dzialajacy backbone oraz coraz
   wiecej SQL paging.
2. Modul 2 ma dobre podstawy pod duze katalogi: SQL paging istnieje dla skladnikow,
   posilkow i przepisow-skladowych. Ryzyko zostalo w starych selectach, planie M2
   i ciezkich kalkulacjach publikacji.
3. Modul 1 potrafi zapisac realne referencje M2 w `OrderItems`, wiec M1 moze
   pracowac na prawdziwych danych. Najwiekszy brak to paginacja historii zamowien
   klienta i katalogu menu.
4. Modul 4 ma domyslnie podpiety provider M4 i loading jest juz czesciowo
   paginowany, ale kierowcy, pojazdy i mapowanie tras nadal opieraja sie na
   `GetAllAsync`.
5. Modul 5 jest najwiekszym dlugiem skalowania: BOK, Admin i HR maja widoczna
   paginacje, ale dane czesto sa najpierw ladowane w calosci do RAM.
6. `VolumeDemo` jeszcze nie istnieje w kodzie. Obecnie `DatabaseSeedingProfile`
   zawiera tylko `MinimalRealistic` i `DemoData`.

## Szczegolowe findings code review

Ta sekcja jest ostrzejsza niz ogolny sweep. Traktuje znalezione punkty jak code
review: co jest problemem, jaki bedzie skutek i jak to naprawic. Priorytety:

- `High` - blokuje bezpieczne duze demo albo moze dac bledne dane operacyjne.
- `Medium` - nie blokuje malego demo, ale uderzy przy VolumeDemo lub dluzszym uzyciu.
- `Low` - ergonomia, drobne ryzyko UI albo latwa optymalizacja.

### Modul 1 - klient, checkout, zamowienia

| Priorytet | Miejsce | Problem | Skutek | Jak naprawic |
| --- | --- | --- | --- | --- |
| High | `OrderRepository.cs:16` | `GetByCustomerIdAsync` robi `SELECT *` bez paginacji. | Historia klienta zwolni wraz z liczba zamowien. | Dodac `SearchByCustomerAsync(customerId, page, pageSize, status, dateFrom, dateTo)` z `COUNT(*)` i `OFFSET/FETCH`. |
| High | `OrderController.cs:27-31` | `Index()` wyswietla wszystkie zamowienia klienta. | UI nie przezyje klienta z dluga historia. | Zmienic akcje na model paged i filtry: status, zakres dat, numer zamowienia. |
| High | `OrderService.cs:94` | Checkout zapisuje zamowienie, pozycje i kalendarz bez jednej transakcji. | Przerwanie zapisu moze zostawic order bez kompletnych `OrderItems` albo `DeliveryCalendar`. | Dodac transakcyjna metode repozytorium/unit-of-work dla calego checkoutu. |
| High | `OrderRepository.cs:69` | Numer zamowienia to `COUNT(1)+1` dla dnia. | Rownolegle checkouty moga wygenerowac ten sam numer. | Dodac sekwencje albo unikalny indeks + retry przy kolizji. |
| High | `OrderService.cs:78` | Cena opiera sie na danych koszyka/sesji przekazanych z warstwy web. | Manipulacja sesja/requestem moze zanizyc cene. | Przed zapisem pobrac warianty/diety z M2 i przeliczyc cene po stronie serwera. |
| High | `OrderService.cs:108` | `AddressId` z checkoutu nie jest walidowany wzgledem `customerId`. | Klient moze probowac zamowic na cudzy adres. | W serwisie pobrac adres metoda scoped do usera i odrzucic brak/obcy adres. |
| High | `M1OrderDataProvider.cs:176` | Legacy order items bez `DeliveryDate` sa laczone z kazdym dniem dostawy. | M3 moze przeszacowac produkcje dla starych zamowien. | Zrobic migracje `DeliveryDate` albo odciac legacy fallback po dacie wdrozenia. |
| Medium | `DietCatalogAdapter.cs:22-48` | Katalog diet pobiera caly katalog i dopiero doczepia warianty. | Menu klienta skaluje sie liniowo. | Rozszerzyc `DietCatalogQuery` o `Page/PageSize`; warianty doczepiac tylko do strony wynikow. |
| Medium | `MenuController.cs:55` | Szczegoly diety pobieraja plan 7 dni dla wszystkich wariantow i filtruja w RAM. | Koszt rosnie z liczba wariantow i pozycji planu. | Dodac `Get7DayPlanAsync(startDate, dietVariantId)` albo filtr SQL w providerze. |
| Medium | `Checkout/Index.cshtml:138` | UI komunikuje brak niedziel, a backend moze generowac dni codziennie. | Klient zobaczy obietnice sprzeczna z faktycznym kalendarzem. | Ujednolicic: pominac niedziele w backendzie albo usunac tekst z UI. |
| Medium | `OrderService.cs:105` | `DeliveryCalendar` budowany po `Max(TotalDays)`. | Przy mieszanych dlugosciach diet powstana dni bez pozycji czesci diet. | Budowac kalendarz z rzeczywistych `OrderItems.DeliveryDate` albo blokowac mieszane dlugosci. |
| Medium | `OrderItemDto.cs:3` i `Order/Details.cshtml:82` | Szczegoly zamowienia ukrywaja dzien, slot i klucze M2. | Klient/staff widzi wiele pozycji jak "diety", nie jadlospis dzienny. | Dodac `DeliveryDate`, `MealSlot`, `MealId`, `DietMenuPlanItemId` i grupowanie po dniu. |
| Medium | `CartService.cs:16-23` | Uszkodzony JSON sesji nie jest obslugiwany; suma `TotalDays` moze przekroczyc limit. | Checkout moze sie wysypac albo koszyk przekroczy ograniczenia. | Lapac `JsonException`, czyscic koszyk; po zsumowaniu wymusic zakres 1-365. |

**Kolejnosc napraw M1 do jutra:**

1. Transakcja checkoutu i serwerowe przeliczenie ceny.
2. Walidacja adresu klienta.
3. SQL paging historii zamowien.
4. Uporzadkowanie widoku szczegolow zamowienia per dzien/slot.
5. Dopiero potem paging katalogu diet.

### Modul 2 - diety, plan M2, przepisy

| Priorytet | Miejsce | Problem | Skutek | Jak naprawic |
| --- | --- | --- | --- | --- |
| High | `DietMenuPlanRepository.cs:455` | Publikacja zmienia status, ale nie utrwala immutable snapshotu M2. | Zmiana receptury po publikacji moze zmienic pozniejszy plan produkcji. | Przy publikacji zapisac JSON/hash pozycji albo zablokowac wersje skladowe uzyte w planie. |
| High | `MealManagementService.cs:149` | Kalkulacja wariantow idzie sekwencyjnie po meal/variant. | Publikacja/overview planu moze robic N+1 przy 500 daniach. | Dodac batch preload komponentow, skladnikow, opakowan i alergenow dla listy kluczy. |
| High | `MealManagementService.cs:580` | Dla komponentu osobno pobierane sa ingredients, packaging, allergens. | Kilka zapytan na komponent mnozy sie przez plan 14 dni. | Dodac `GetVersionDetailsBulkAsync(versionIds)` w repozytorium skladowych. |
| Medium | `DietMenuPlanRepository.cs:493` | `ItemSql` ma wiele `OUTER APPLY` per pozycja planu. | Widok dnia/tygodnia skaluje sie liniowo z ciezkimi podzapytaniami. | Preagregowac counts w CTE po `planId` i dolaczyc po `MealId/MealVariantId`. |
| Medium | `DietMenuPlanRepository.cs:45` | Weekly summary liczy warnings przez correlated `OUTER APPLY`. | Start planera bedzie kosztowny przy 14 dniach i duzej liczbie pozycji. | Zastapic jednym zapytaniem agregujacym komponenty per plan item. |
| Medium | `MealRepository.cs:27` | Lista dan liczy ciezki CTE osobno dla count i strony. | Paginacja jest SQL, ale nadal placi podwojnie. | Count oprzec o lekki ID/filter CTE albo `COUNT(*) OVER()` po odchudzonym SELECT. |
| Medium | `MealRepository.cs:71` | Lookup planera uzywa tej samej ciezkiej sciezki co lista dan. | Search w planie bedzie wolny mimo limitu. | Dodac lekki endpoint SQL tylko dla lookupu: id, nazwa, status, warianty podstawowe. |
| Medium | `IngredientRepository.cs:195` | Search uzywa `%LIKE%` po wielu polach. | Przy 5000 skladnikow SQL moze skanowac tabele. | Dla lookupu ograniczyc do `Name`, docelowo dodac normalized search/full-text. |
| Medium | `RecipeComponentsController.cs:105` | `IngredientLookup` filtruje typ `Food/Spice` po stronie aplikacji. | Strona wynikow moze byc pusta mimo istniejacych pasujacych skladnikow. | Dodac filtr `ResourceType` do SQL query. |
| Medium | `DietEditorController.cs:100` | Legacy details laduje wszystkie opublikowane posilki do selecta. | HTML i pamiec rosna przy 500 posilkach. | Zastapic AJAX lookupiem z planera albo oznaczyc ekran jako legacy read-only. |
| Medium | `DietEditorController.cs:154` | `AssignMeal` ufa `dietId` z hidden field. | Mozna probowac dopisac posilek do wariantu innej diety. | Pobrac wariant z DB i sprawdzic `variant.DietId == dietId`. |
| Low | `DietMenuPlanController.cs:26` | `days` nie jest ograniczony. | Uzytkownik moze wymusic bardzo szeroki zakres. | Clamp do 7/14/31 dni. |
| Low | `MenuPlanDay.cshtml:365` | Lookup dan nie anuluje starszych requestow. | Wolniejsza odpowiedz moze nadpisac nowsza. | Uzyc `AbortController` albo numeru sekwencji requestow. |
| Low | `MenuPlanDay.cshtml:436` | `document.click` dodawany per formularz. | Drobny koszt UI przy wielu pozycjach. | Zrobic jeden delegowany listener. |

**Kolejnosc napraw M2 do jutra:**

1. Walidacja przynaleznosci wariantu w `AssignMeal`.
2. Clamp `days` w planie.
3. Lekki lookup dan do planera.
4. Usuniecie pelnych selectow z legacy details.
5. Dopiero potem batch kalkulatora wariantow i immutable snapshot publikacji.

### Modul 3 - produkcja i karta gotowania

| Priorytet | Miejsce | Problem | Skutek | Jak naprawic |
| --- | --- | --- | --- | --- |
| High | `ProductionService.cs:691` | `ApproveCookingAsync` nie wymaga `FefoDeductedAt` ani ukonczonych sesji komponentow. | Pozycje da sie oznaczyc jako `Cooked` i zdjac opakowania bez realnego przejscia karty gotowania. | Przed zatwierdzeniem wymagac FEFO i kompletu `Completed` dla wymaganych komponentow/krokow. |
| High | `ProductionService.cs:2164` | Korekta `CookedQuantity` akceptuje tez approvale `PackagingQuantity` i `LabelQuantity`. | Approval pudelek/etykiet o tej samej wartosci moze przepuscic inna faktyczna liczbe porcji. | Walidowac dokladnie `AdjustmentType == CookedQuantity`; opakowania i etykiety sprawdzac osobno. |
| Medium | `ProductionService.cs:451` i `2168` | Approvale produkcji sa szukane przez `GetAllAsync`. | Karta gotowania i approval flow beda skanowac cala tabele korekt. | Dodac repo metody `GetByPlanItemAsync` i `FindApprovedAsync(planItemId,type,value)`. |
| Medium | `ProductionService.cs:1752` | FEFO ustawia status pozycji na `Cooking`. | UI pokazuje gotowanie w toku, chociaz zadna sesja gotowania nie musiala ruszyc. | Rozdzielic status magazynowy od statusu gotowania albo zostawic `Planned` do startu pierwszej sesji. |
| Medium | `CookingCard.cshtml:125` | Formularz zatwierdzenia jest aktywny niezaleznie od prerekwizytow. | Operator moze probowac finalizowac bez FEFO/komponentow. | Wylaczyc submit do czasu spelnienia warunkow i pokazac blokery z serwera. |
| Medium | `ProductionPlanGenerator.cs:281` | Legacy fallback liczenia ilosci nadal dziala, gdy order items nie maja `MealId`. | Plan M2 snapshotem moze dostac ilosci policzone starym sposobem po wariancie diety. | Ograniczyc fallback do trybu migracyjnego z ostrzezeniem albo blokowac produkcje bez jawnych referencji M2. |
| Low | `CookingCards.cshtml:22` | UI mapuje status `Pending`, ktorego nie ma w enumie, i nie mapuje `Planned/Failed`. | Kolory/statusy kart sa mylace. | Mapowac `Planned`, `Cooking`, `Cooked`, `Failed` zgodnie z `ProductionItemStatus`. |
| Low | `ProductionController.cs:253` | `CookingCards` przekierowuje do `Index`, a widok wyglada na martwy. | Poprawki UI moga trafic w nieuzywany ekran. | Usunac legacy widok albo przywrocic akcje z poprawnym modelem. |
| Low | `CookingSessionService.cs:264` | Krok kontrolny mozna oznaczyc bez walidacji wartosci/progu. | Test temperatury moze przejsc z pusta albo zbyt niska wartoscia. | Dla `RequiresControl` wymagac wartosci/jednostki i walidowac `ExpectedValue/ControlType`. |

**Kolejnosc napraw M3 produkcja/kuchnia do jutra:**

1. Blokada `ApproveCookingAsync` bez FEFO i bez ukonczonych komponentow.
2. Doprecyzowanie approvali `CookedQuantity` vs `PackagingQuantity` vs `LabelQuantity`.
3. Repozytoryjne odczyty approvali.
4. UI blokery na karcie gotowania.
5. Walidacja wartosci krokow kontrolnych.

### Modul 3 - magazyn, demand i FEFO

| Priorytet | Miejsce | Problem | Skutek | Jak naprawic |
| --- | --- | --- | --- | --- |
| High | `WarehouseDemandService.cs:49` | Demand bierze live opublikowany snapshot M2. | Preview moze nie zgadzac sie z FEFO produkcji, ktore uzywa zamrozonego `M2SnapshotJson`. | Dla istniejacego planu produkcji budowac demand z `ProductionPlanItems.M2SnapshotJson`. |
| High | `WarehouseDemandService.cs:95` | `EnrichAvailabilityAsync` robi N+1 na dostepnosci partii. | VolumeDemo dostanie setki/tysiace zapytan. | Bulk query po `stockItemIds` i `warehouseCategoryIds`: suma dostepna, earliest batch, risk. |
| High | `WarehouseDemandService.cs:320` | Demand skladnikow liczy w gramach, a dostepnosc porownuje z surowym `Batch.CurrentQuantity`. | Falszywe braki/nadwyzki dla jednostek innych niz g. | Wprowadzic konwersje UOM albo wymusic bazowa jednostke FEFO per stock item/category. |
| High | `WarehouseDemandService.cs:414` | Pozycje bez `StockItemId` i `WarehouseCategoryId` sa po cichu pomijane. | Raport zaniza zapotrzebowanie i moze pokazac OK mimo brakow mapowan. | Zbierac `MissingWarehouseMappings` w DTO i blokowac eksport/status bez mapowan. |
| High | `WarehouseDemandService.cs:419` | Stock item i category licza dostepnosc niezaleznie. | Ta sama partia moze byc policzona dwa razy w preview. | Zrobic symulacje alokacji FEFO dla calego demand albo rozdzielic pule kategorii od jawnych stock itemow. |
| Medium | `WarehouseDemandService.cs:109` | Filtrowanie/sortowanie/paginacja sa po zbudowaniu wszystkich dni. | Strona 1 kosztuje tyle co caly raport. | Przeniesc filtry przed enrichment i polaczyc z bulk availability/cache. |
| Medium | `WarehouseDemandService.cs:467` | FEFO preview pokazuje tylko najwczesniejsza partie. | Magazyn nie widzi rozbicia wymaganej ilosci na partie. | Dodac alokacje: batch, expiry, qty used, remaining. |
| Medium | `WarehouseService.cs:64` | Przyjecie dostawy nie ma idempotencji. | Double-submit zdubluje partie i stan. | Dodac operation/idempotency key oraz unikalnosc biznesowa przyjecia. |
| Medium | `WarehouseService.cs:104` | Odpady i reczne wydania nie maja idempotentnego reference. | Ponowienie POST odejmie magazyn drugi raz. | Generowac operation id per formularz i egzekwowac deduplikacje. |
| Medium | `WarehouseCommandRepository.cs:476` | Idempotencja FEFO opiera sie globalnie na `ReferenceDocument`. | Kolizja referencji moze pominac prawidlowy odpis. | Uzyc namespacowanego klucza operacji z typem, stock/category i unikalnym indeksem. |
| Medium | `WarehouseController.cs:603` | Eksport FEFO pobiera caly raport do pamieci. | Duzy eksport CSV/PDF moze zabic request. | Stream CSV, limit PDF albo eksport asynchroniczny. |
| Medium | `InventoryTransactionRepository.cs:86` | Historia transakcji wykonuje CTE dwa razy. | Duze historie skanuja sie podwojnie. | Uzyc temp table albo `COUNT(*) OVER()` w jednym przebiegu. |

**Kolejnosc napraw M3 magazyn/demand do jutra:**

1. Demand z zamrozonego snapshotu produkcji, gdy plan juz istnieje.
2. Bulk availability zamiast N+1.
3. Raport brakow mapowan zamiast cichego pomijania.
4. Prosta symulacja alokacji FEFO bez podwojnego liczenia kategorii i stock itemow.
5. Idempotencja recznych operacji magazynowych.

### Modul 3 - foliowanie, kompletacja i loading

| Priorytet | Miejsce | Problem | Skutek | Jak naprawic |
| --- | --- | --- | --- | --- |
| High | `PackingService.cs:762` | `GetTransportLabelsForRouteAsync` buduje pelny board dnia dla jednej trasy. | Druk/redruk etykiet trasy kosztuje tyle co caly dzien. | Dodac route-scoped query `GetRouteBagsForLabelsAsync(date, routeId)`. |
| High | `PackingService.cs:786` i `2199` | Kazda etykieta z trasy przechodzi przez mapper, ktory znowu buduje pelny board. | Druk 1000 toreb moze wykonac 1000 rekonstrukcji dnia. | Przekazywac znaleziony `PackingBagDto`/route context do mappera. |
| High | `PackingService.cs:813` | Missing labels dla trasy generowane sa per torba. | Przy duzej trasie powstaje N razy koszt boardu i print-count. | Batch generowanie etykiet dla routeId z jednym pobraniem trasy. |
| High | `PackingService.cs:668` | Etykieta dla jednej torby laduje pelny board dnia. | Pojedynczy redruk degraduje z liczba dostaw dnia. | Dodac `GetBoardBagByPackingBagIdAsync(packingBagId)`. |
| High | `LoadingService.cs:70` | Manifest control pobiera pelny packing board. | Wejscie w manifest jednej trasy skaluje sie z calym dniem. | Route-scoped board/manifest query. |
| High | `LoadingService.cs:178` | `GenerateManifestAsync` buduje pelny board i potem robi to drugi raz. | Generowanie manifestu dubluje koszt. | Pobrac tylko trase i zrobic walidacje scoped do routeId. |
| High | `LoadingService.cs:1197` | `ResolveRouteIdForSessionAsync` rozwiazuje trase przez pelny board. | Skan pojedynczej torby zalezy od calego dnia kompletacji. | SQL lookup po `PackingSessionId/DeliveryCalendarId` do `DeliveryRouteStops`. |
| High | `LoadingService.cs:592` | Po zaladowaniu jednej torby znowu budowany jest pelny board. | Kazdy skan torby kosztuje caly dzien. | Zwrocic DTO z route-scoped lookup po `PackingBagId`. |
| Medium | `LoadingService.cs:420` | Reset loadingu startuje od pelnego boardu dnia. | Reset jednej trasy czyta wszystkie trasy i torby. | Dla routeId uzyc query po routeId, dla dnia batch update SQL. |
| Medium | `LoadingService.cs:467` | Reset manifestow filtruje `GetAllAsync` w pamieci. | Historia manifestow bedzie pelnym skanem. | `GetCurrentByRoutesAsync(date, routeIds)` albo `SupersedeByRoutesAsync`. |
| Medium | `PackingService.cs:2152`, `2217`, `LoadingService.cs:1031` | Fallbacki etykiet ida przez `GetAllAsync`. | Skan QR/redruk moze przejsc cala tabele etykiet. | Wymagac indeksowanych metod repo albo fail-fast bez repo. |
| Medium | `LoadingController.cs:42` | Ekran loadingu ma strone toreb, ale pelna liste tras. | Przy wielu trasach UI nadal renderuje szeroko. | Dodac paging/search tras albo summary z limitami. |
| Medium | `PackingController.cs:889` | Druk zaznaczonych etykiet bez routeId buduje board i pobiera trasy osobno. | Bulk print etykiet moze kaskadowo ladowac caly dzien. | Dodac `GetShippingLabelsByIdsWithRouteAsync(labelIds)`. |
| Medium | `PackingBagRepository.cs:96` | `SearchBoardBagsAsync` powtarza ciezki CTE trzy razy. | SQL paging dziala, ale query placi za summary/count/page osobno. | Rozwazyc temp table/staging albo lzejsze query dla count/summary. |
| Medium | `PackingBagRepository.cs:225` | Latest label przez `OUTER APPLY` wymaga dobrego indeksu. | Bez indeksu latest label bedzie kosztowny przy tysiacach toreb. | Indeks filtrowany `(LabelType, PackingBagId, PrintNumber DESC, Id DESC) INCLUDE (AttachedAt, AttachedBy)`. |

**Kolejnosc napraw M3 kompletacja/loading do jutra:**

1. Route-scoped query dla etykiet trasy.
2. Lookup torby/trasy po `PackingBagId` i `PackingSessionId`.
3. Usuniecie pelnego boardu z generowania manifestu.
4. Fail-fast zamiast `GetAllAsync` fallbackow etykiet.
5. Indeks/latest label smoke na duzym dniu.

### Modul 4 - logistyka

| Priorytet | Miejsce | Problem | Skutek | Jak naprawic |
| --- | --- | --- | --- | --- |
| High | `LogisticsController.cs:252` | Flota laduje wszystkie pojazdy. | `/logistics/vehicles` rosnie liniowo. | Dodac `SearchVehiclesAsync(search,status,page,pageSize)`. |
| High | `LogisticsController.cs:342` | Kierowcy ladowani pelnym `DriverService.GetAllAsync`. | Brak realnej paginacji i kosztowne mapowanie. | Dodac `SearchDriversAsync` z joinami do users/assignments/vehicles. |
| High | `DriverService.cs:32-35` | Laduje wszystkich drivers, users, assignments i vehicles. | Jeden ekran skaluje sie jak kilka pelnych tabel. | Jedna kwerenda paged z projekcja DTO. |
| High | `RoutingService.cs:525` | Mapowanie tras pobiera wszystkie pojazdy. | Koszt zalezy od calej floty, nie od tras dnia. | Pobierac `VehicleId IN (...)` albo dolaczyc dane w SQL route query. |
| High | `RoutingService.cs:578` | Lookup kierowcow filtruje po `GetAllAsync` i laduje wszystkich userow. | Koszt rosnie z cala baza staff. | Dodac `GetDriverDisplaysByIdsAsync(driverIds)` z joinem do `Users`. |
| Medium | `LogisticsController.cs:518` | Edycja trasy laduje pelne selecty pojazdow i kierowcow. | Formularz bedzie ciezki przy wiekszej flocie. | Lookup/select2 z limitem i filtrem aktywnych. |
| Medium | `LoadingService.cs:72` | Kontrola manifestu pobiera caly packing board dnia. | Manifest jednej trasy kosztuje caly dzien kompletacji. | Dodac `GetRouteBoardAsync(date, routeId)`. |
| Medium | `LoadingService.cs:1202` | `ResolveRouteIdForSessionAsync` szuka przez pelny board. | Skan torby przebudowuje dzienny obraz. | Repo lookup po `PackingSessionId`/`DeliveryCalendarId`. |

**Kolejnosc napraw M4 do jutra:**

1. SQL paging drivers/vehicles.
2. Lookup by ids dla tras.
3. Scoped route board dla loadingu.
4. Dopiero potem rozbudowa UI map/trasy.

### Modul 5 - HR, Admin, BOK

| Priorytet | Miejsce | Problem | Skutek | Jak naprawic |
| --- | --- | --- | --- | --- |
| High | `BaseRepository.cs:70` | `GetAllAsync` to `SELECT *` bez limitu. | Kazdy ekran oparty o metode skaluje sie fatalnie. | Zostawic tylko dla malych slownikow; listy przeniesc na `SearchAsync`. |
| High | `PagedList.cs:32` | `PagedList.Create` robi `source.ToArray()` przed pagingiem. | Paginacja jest tylko wizualna. | Dla list operacyjnych uzyc DTO `PagedResult<T>` z SQL. |
| High | `AdminController.cs:488` | Admin users laduje wszystkich userow. | Panel admina robi pelny dump przy 1200+ kontach. | Dodac `UserRepository.SearchAsync(role,search,page,pageSize)`. |
| High | `CustomerSupportController.cs:186` | BOK laduje wszystkie tickety, open tickety i userow. | Ekran BOK bedzie pierwszym timeoutem VolumeDemo. | `TicketRepository.SearchAsync` + osobne SQL summary licznikow. |
| High | `CustomerSupportService.cs:74` | `GetTicketsAsync` robi `GetAllAsync` i mapuje przez pelna liste userow. | Paging kontrolera nic nie daje. | Zwracac strone `TicketDto` z joinami klient/przypisanie. |
| High | `CustomerSupportService.cs:117` | Operational context budowany w petli per ticket. | N+1 na order, delivery, route, packing. | Batchowac konteksty dla widocznej strony po ids. |
| Medium | `CustomerSupportService.cs:136` | Delivery options pobiera zakres i dopiero potem `Take(200)`. | Formularz ticketu ciezki przy duzej liczbie dostaw. | Server-side lookup `TOP (@limit)` z joinami. |
| High | `HumanResourcesController.cs:499` | HR zawsze laduje departments, employees, leaves, schedules i users. | Kazdy podwidok HR placi koszt calego modulu. | Rozdzielic modele per akcja i dodac SQL paging per lista. |
| High | `HumanResourcesService.cs:99` | Employees opiera sie o pelne `GetAllAsync`. | Kartoteka HR nie skaluje sie. | `SearchEmployeesAsync` z department/status/role/search i joinami. |
| High | `HumanResourcesService.cs:197` | Urlopy ladowane w calosci. | Ekran urlopow puchnie z czasem. | `SearchLeaveRequestsAsync(status,type,search,page,pageSize)`. |
| High | `HumanResourcesService.cs:394` | Mapowanie grafikow laduje wszystkich pracownikow i userow. | Zakres jednego tygodnia kosztuje pelne tabele. | Mapowac przez SQL join tylko dla zwracanej strony. |
| Medium | `CustomerSupport/Tickets.cshtml:51` | Statusy BOK sa po angielsku. | Operatorzy widza mieszanke PL/EN. | Helper `DisplayName` dla statusow i priorytetow. |
| Medium | `HumanResources/Schedules.cshtml:46` | Zmiany `Morning/Evening/Night` bez tlumaczenia. | Niespojnosc jezykowa. | Wspolny translator enumow HR. |

**Kolejnosc napraw M5 do jutra:**

1. BOK tickets SQL page + summary.
2. Admin users SQL page.
3. HR employees/leaves/schedules SQL page.
4. Batch contexts dla BOK.
5. Dopiero potem kosmetyka tlumaczen.

### Seeder i VolumeDemo - findings techniczne

| Priorytet | Miejsce | Problem | Skutek | Jak naprawic |
| --- | --- | --- | --- | --- |
| High | `DatabaseSeedingOptions.cs:23` | Profile to tylko `MinimalRealistic` i `DemoData`. | Nie ma bezpiecznego miejsca na wolumen. | Dodac `VolumeDemo`, tylko command/config, nigdy startup. |
| High | `appsettings.json:6` | Domyslnie seed moze byc `Enabled=true`, `Mode=Both`, `Profile=DemoData`. | Rozszerzenie `DemoData` o wolumen rozwali zwykly dev start. | Zostawic male demo; `VolumeDemo` jawnie przez CLI/config. |
| High | `DatabaseSeeder.cs:45` | `DemoData` jest spiete z normalnym seedem i resetem demo. | Tysiace rekordow zmienia zachowanie standardowego uruchomienia. | Wydzielic `VolumeDemoSeeder` lub orchestrator poza monolit. |
| Medium | `Program.cs:17` | Komenda rozpoznaje tylko `seed`, `--seed`, `db:seed`. | `--profile VolumeDemo` nie jest realnie parsowane przez kod CLI. | Dodac parser opcji albo dokumentowac uruchomienie przez zmienne konfiguracyjne. |
| Medium | `DatabaseSeeder.cs:1106` | Demo logistyczne generuje maly scenariusz w petli po klientach. | Dobry funkcjonalnie, ale nie wolumenowo. | W `VolumeDemo` uzyc batchy SQL/TVP po 500-1000 rekordow. |

**Docelowy kontrakt VolumeDemo:**

```powershell
dotnet run --project src/KuchniaUCygana.Web -- seed --profile VolumeDemo --scale Medium --days 14 --users 1200 --staff 50 --active-customers 300 --peak-orders 500 --seed 20260608
```

Jezeli nie dodajemy parsera CLI w pierwszym kroku, tymczasowy wariant:

```powershell
$env:DatabaseSeeding__Mode='Command'
$env:DatabaseSeeding__Profile='VolumeDemo'
$env:DatabaseSeeding__Enabled='true'
dotnet run --project src/KuchniaUCygana.Web -- seed
```

**Skale:**

- `Small`: 300 users, 50 aktywnych klientow, 7 dni, 1000-2000 batch/log rows.
- `Medium`: 1200 users, 300 aktywnych klientow, 14 dni, 3000-7000 batchy.
- `Large`: 1200 users, piki 500 orders/dzien, 14 dni, 10000-30000 logow.

**RAM:**

- `Small`: 6-8 GB Docker Desktop.
- `Medium`: 10-12 GB.
- `Large`: 12-16 GB i limit pamieci SQL Server.

## Priorytety do jutra

### P0 - zrobic najpierw

- Nie dodawac jeszcze duzego seedera do zwyklego startu aplikacji.
- Dla pokazu uzywac realnych providerow `OrderProvider=M1` i
  `DeliveryManifestProvider=M4`; jesli mock jest aktywny, pokazac to jawnie w UI/adminie.
- Naprawic SQL paging tam, gdzie duzy seeder natychmiast obnazylby problem:
  - historia zamowien klienta;
  - BOK tickets;
  - Admin users;
  - HR employees/leaves/schedules;
  - Drivers/Vehicles w logistyce.
- W M3 nie robic reworka kuchni. Zostawic dzialajacy flow, a poprawki ograniczyc
  do batchowego demand/availability i usuwania najbardziej kosztownych pelnych boardow.

### P1 - jezeli zostanie czas

- Async lookupi zamiast pelnych selectow w starych ekranach M2.
- Optymalizacja `WarehouseDemandService` przez slowniki snapshotow i batchowe
  sprawdzanie dostepnosci.
- Ograniczenie `GetPackingBoardAsync(date)` w akcjach etykiet, redrukow, manifestow
  i replacement bag.
- Tlumaczenie enumow BOK typu `New`, `Open`, `Critical` w widokach.

### P2 - po VolumeDemo lub rownolegle z nim

- Indeksy i smoke SQL dla najciezszych zapytan `PackingBagRepository.SearchBoardBagsAsync`.
- Dalsza redukcja legacy fallbackow kuchni i BOK.
- Pelny workflow alertow M2, manager approval i bundle QR.

## Modul 1 - strona klienta

### Co dziala

- Checkout w `OrderService` materializuje zamowienie z opublikowanego snapshotu M2.
- `OrderItems` sa uzupelniane o `MealId`, `MealVariantId`,
  `DietMenuPlanItemId`, `MealSlot` i `DeliveryDate`.
- `M1OrderDataProvider` czyta realne `Orders`, `OrderItems` i `DeliveryCalendar`.
- Konfiguracja domyslna wskazuje realnego providera: `OrderProvider=M1`.

### Ryzyka skali i UI

- `OrderController.Index` pobiera cala historie zamowien klienta.
- `OrderRepository.GetByCustomerIdAsync` robi pelne `SELECT * ... ORDER BY CreatedAt DESC`,
  bez `OFFSET/FETCH`.
- `MenuController.Index` ma search, ale nie ma `page/pageSize`.
- `DietCatalogAdapter` filtruje diety po `LIKE '%term%'` i nie ma paginacji.
- Fallback legacy w `M1OrderDataProvider` jest potrzebny kompatybilnosciowo, ale
  dla VolumeDemo powinien byc traktowany jako ostrzezenie danych, nie normalna sciezka.

### Poprawki do jutra

1. Dodac `OrderRepository.SearchByCustomerAsync(customerId, page, pageSize, status?, dateFrom?, dateTo?)`.
2. Przebudowac `OrderController.Index` na SQL paging i filtry.
3. Dodac `page/pageSize` do `MenuController.Index`.
4. Pokazac w zamowieniu klienta referencje M2 w trybie staff/debug:
   `DietMenuPlanItemId`, `MealId`, `MealVariantId`, `MealSlot`.

### Wymagania dla VolumeDemo

- Kazde aktywne zamowienie musi miec `PaymentStatus=Paid` albo rownowazny status
  widoczny dla M3.
- Kazdy `OrderItem` z planu musi miec referencje M2, inaczej produkcja spadnie
  do fallbacku po wariancie diety.
- Rozklad dni powinien byc zmienny: np. 200 zamowien, potem 500, potem 260, zeby
  kuchnia i logistyka dostaly realne piki.

## Modul 2 - diety, skladniki i plan M2

### Co dziala

- `IngredientRepository.SearchAsync` ma SQL paging, `COUNT`, `OFFSET/FETCH`
  i limit `PageSize <= 100`.
- `MealRepository.SearchAsync` ma SQL paging oraz filtry statusu, kategorii,
  alergenow, wariantow i brakow publikacji.
- `RecipeComponentRepository.SearchComponentsAsync` ma server-side paging i filtry.
- Plan M2 uzywa lookupu posilkow `SearchPlanningMealsAsync(query, 20)`.
- `ProductionSnapshotPayloadFactory` tworzy stabilny JSON i hash snapshotu.
- Snapshot M2 zawiera dane potrzebne M3: plan item, wariant, komponenty,
  skladniki, opakowania, nutrition i instrukcje.

### Ryzyka skali i UI

- `DietEditorController.Details` nadal laduje opublikowane posilki do pelnego selecta.
- `Views/DietEditor/Details.cshtml` ma pelny `<select>` posilkow.
- `MealsController.Details` laduje wszystkie `GetPublishedVersionOptionsAsync`.
- `Views/Meals/Details.cshtml` ma pelne selecty wersji komponentow.
- `IngredientRepository.SearchAsync` uzywa `LIKE '%term%'` po kilku polach, co
  przy 5000 skladnikow moze skanowac mimo paginacji.
- `MealRepository.SearchAsync` ma ciezkie CTE i wiele `OUTER APPLY`, liczone osobno
  dla `COUNT` i strony wynikow.
- Publikacja planu w `DietMenuPlanManagementService` waliduje wiele itemow przez
  swieze kalkulacje.
- `GetM2PlanOverviewAsync` buduje overview szeroko i potem filtruje/paginuje w RAM.

### Poprawki do jutra

1. Zastapic pelne selecty w `DietEditor/Details` i `Meals/Details` async lookupami.
2. Dodac minimalne ostrzezenie UI, kiedy posilek/receptura wpada w legacy fallback.
3. Ograniczyc `GetM2PlanOverviewAsync`: najpierw zakres/dzien/status, dopiero potem
   szczegoly pozycji.
4. Przygotowac plan indeksow/search dla skladnikow: prefix search albo full-text
   jako etap po VolumeDemo.

### Wymagania dla VolumeDemo

- Katalog M2 powinien byc seedowany raz i reuzywany przez plan:
  - 5 diet;
  - warianty kaloryczne;
  - 40-60 dan;
  - 100-150 wariantow dan;
  - 120-200 przepisow-skladowych;
  - 1000 skladnikow, z czego 150-250 aktywnie uzywanych.
- Produkty przetworzone, np. czekolada, jogurt, tortilla, hummus, powinny byc
  `Ingredient` z `ProductComposition`, nutrition, alergenami, kategoria magazynowa
  i batchami.
- Opakowania powinny byc osobna kategoria zasobow, nie skladniki zywieniowe.

## Modul 3 - kuchnia, magazyn i kompletacja

### Co dziala

- `ProductionPlanGenerator` generuje produkcje z opublikowanego snapshotu M2.
- Brak snapshotu M2 blokuje generacje; nie ma cichego tworzenia produkcji z legacy
  `GetPlanForDateAsync`.
- `ProductionService.ProduceSemiFinishedAsync` wykonuje FEFO ze snapshotu i blokuje
  produkcje bez snapshotu.
- `ProductionPlanRepository.SearchPlanItemsAsync` ma SQL paging, search i filtry.
- Karty gotowania komponentow nie powinny juz skanowac pelnych tabel sesji:
  `CookingSessionRepository` ma odczyt po `ProductionPlanItemId + RecipeComponentVersionId`,
  a `CookingSessionStepCheckRepository` ma odczyt po sesji/kroku.
- Packing/loading dostaly repozytoryjne paging boardow.

### Ryzyka skali i UI

- `WarehouseDemandService.GetDemandAsync` agreguje dostawy per dzien, dopasowuje
  snapshoty i robi FEFO preview liniowo/N+1.
- `ProductionPlanRepository.GetPlanItemsAsync` nadal laduje caly plan; uzywane w
  refresh, overview M2 i FEFO.
- `PackingService` nadal ma akcje, ktore wolaja pelny `GetPackingBoardAsync(date)`
  przy etykietach tras, redrukach, manifestach i replacement bag.
- `PackingBagRepository.SearchBoardBagsAsync` ma SQL paging, ale opiera sie na
  ciezkim CTE z `OUTER APPLY`; wymaga testu na duzym dniu.
- `LoadingController.Index` trzyma liste tras bez pelnej paginacji.
- `ProductionController.Rework` pobiera incydenty i paginuje je w pamieci.
- `GetCookingCardAsync` ma legacy odczyt receptury przy braku `M2SnapshotJson`;
  dla demo operacyjnego brak snapshotu powinien byc traktowany jako blad danych.

### Poprawki do jutra

1. Nie przebudowywac kuchni od nowa.
2. Dodac w dokumencie/kodzie widoczny status: brak snapshotu M2 = problem danych.
3. Przygotowac batch availability dla `WarehouseDemandService`.
4. Ograniczyc uzycie `GetPackingBoardAsync(date)` w najciezszych akcjach.
5. Zrobic smoke na jednym dniu z duza liczba paczek zanim odpalimy pelny VolumeDemo.

### Wymagania dla VolumeDemo

- Produkcja powinna byc generowana tylko dla dni smoke, np. dzisiaj i jutro, a
  demand moze widziec 7-14 dni.
- Zamowienia musza miec pelne referencje M2, zeby kuchnia nie grupowala po legacy
  wariancie diety.
- Magazyn musi miec partie skladnikow i opakowan wystarczajace na piki 500 zamowien.
- FEFO powinno miec dane z roznymi datami waznosci, nie jedna plaska partie.

## Modul 4 - logistyka

### Co dziala

- Konfiguracja domyslna wskazuje realnego providera `DeliveryManifestProvider=M4`.
- Trasy po dacie maja indeksy w migracji logistycznej.
- `LoadingController` ma paging dla szczegolu trasy.
- `PackingBagRepository.SearchBoardBagsAsync` jest dobrym wzorcem dla boardow
  pod duzy seeder.

### Ryzyka skali i UI

- `LogisticsController.Vehicles` i `LogisticsController.Drivers` uzywaja `GetAllAsync`.
- `DriverService.GetAllAsync` laduje wszystkich kierowcow, userow, assignmenty i pojazdy.
- `VehicleService.GetAllAsync` laduje wszystkie pojazdy.
- `RoutingService` filtruje trasy data, ale mapowanie dociaga wszystkie vehicles,
  users i drivers przez `GetAllAsync`.
- Bazowe `BaseRepository.GetAllAsync` to `SELECT *`, bez limitu.

### Poprawki do jutra

1. Dodac `SearchDrivers` i `SearchVehicles` z SQL paging.
2. Dodac lookup by ids dla driver/vehicle/user mappingu w routingu.
3. Dla list logistycznych dodac filtry: data, status, driver, vehicle, route.
4. W UI pokazac jasny status, czy dane pochodza z realnego M4, a nie mocka.

### Wymagania dla VolumeDemo

- Dane musza isc sciezka: M2 plan -> M1 checkout -> M4 routes -> loading.
- Dni z 200/500 zamowieniami musza tworzyc rozna liczbe tras i stopow.
- Pojazdy i kierowcy musza miec sensowne limity pojemnosci i grafiki.
- Nie generowac tras w pamieci z pelnym `GetAllAsync` dla calego wolumenu.

## Modul 5 - HR, Admin i BOK

### Co dziala

- System logs maja dobry wzorzec: `SystemLogRepository.SearchAsync` z `COUNT`,
  `OFFSET/FETCH` i filtrami SQL.
- Sa indeksy pod system logs oraz tickety:
  - user/action/time/target dla logow;
  - client/order/delivery/status dla ticketow.
- HR ma czesc tlumaczen statusow urlopow w widokach.
- Ostatnie zmiany staff poprawily czesc list, ale nie zamykaja problemu SQL paging
  dla wszystkich danych.

### Ryzyka skali i UI

- `PagedList.Create` materializuje `source.ToArray()` przed `Skip/Take`.
- `AdminController` laduje wszystkich uzytkownikow przez `userService.GetAllAsync`.
- `CustomerSupportController` laduje tickety i userow, potem paginuje w pamieci.
- `CustomerSupportService.GetTicketsAsync` pobiera wszystkie tickety.
- `CustomerSupportService.MapTicketsAsync` pobiera wszystkich userow.
- `HumanResourcesController.BuildModel` laduje wszystkie departments, employees,
  leaves, schedules i users.
- `HumanResourcesService` robi kolejne pelne odczyty przy mapowaniu HR.
- Widok BOK pokazuje enumy typu `New`, `Open`, `Critical` bez tlumaczenia.

### Poprawki do jutra

1. Admin: dodac `UserRepository.SearchAsync` i przeniesc `AdminController.Users`
   na SQL paging.
2. BOK: dodac `TicketRepository.SearchAsync` zwracajacy total count i strone
   wynikow z dolaczonym klientem/order/delivery context.
3. HR: dodac repozytoryjne listy `SearchEmployees`, `SearchLeaveRequests`,
   `SearchSchedules`.
4. Zostawic `PagedList.Create` tylko dla malych slownikow i statycznych list.
5. Przetlumaczyc widoczne enumy BOK.

### Wymagania dla VolumeDemo

- `1200` uzytkownikow nie powinno byc problemem dla SQL, ale bedzie problemem dla
  ekranow, ktore pobieraja wszystkich userow na raz.
- HR moze dostac 50 staff, ale grafiki i urlopy powinny byc generowane tak, aby
  listy byly filtrowane po zakresie dat.
- BOK powinien dostac setki/tysiace ticketow dopiero po wdrozeniu SQL paging.

## VolumeDemo - projekt profilu seedera

### Kontrakt uruchomienia

Docelowo dodac trzeci profil:

```powershell
dotnet run --project src/KuchniaUCygana.Web -- seed --profile VolumeDemo --days 14 --users 1200 --active-customers 300 --peak-orders 500 --seed 20260608
```

Parametry:

- `--days 14`
- `--users 1200`
- `--staff 50`
- `--active-customers 300`
- `--peak-orders 500`
- `--ingredients 1000`
- `--batches 5000`
- `--logs 20000`
- `--dry-run`
- `--reset-volume-demo`
- `--seed 20260608`

### Dane docelowe

- 1200 uzytkownikow, w tym 50 pracownikow.
- 200-300 aktywnych klientow dziennie, z pikami do 500 zamowien.
- 5 diet z wariantami kalorycznymi.
- Plan M2 na 14 dni.
- 40-60 realnych dan.
- 100-150 wariantow dan.
- 120-200 przepisow-skladowych.
- 350 pozycji planu M2 na 14 dni, reuzywujacych dania i warianty.
- 1000 skladnikow, z czego 150-250 aktywnie uzywanych.
- 3000-7000 batchy magazynowych.
- 10000-30000 logow magazynu, FEFO, temperatur i dostaw.
- Kilka tysiecy pudelek oraz toreb.

### Realistyczny rozklad zamowien

Przykladowy rozklad 14 dni:

| Dzien | Aktywni klienci | Cel |
| --- | ---: | --- |
| D+0 | 220 | dzien normalny |
| D+1 | 500 | pik produkcji i logistyki |
| D+2 | 280 | spadek po piku |
| D+3 | 320 | mocny dzien roboczy |
| D+4 | 240 | sredni dzien |
| D+5 | 180 | weekend / nizszy popyt |
| D+6 | 170 | weekend / nizszy popyt |
| D+7 | 300 | powrot do tygodnia |
| D+8 | 460 | drugi pik |
| D+9 | 260 | dzien normalny |
| D+10 | 340 | dzien mocny |
| D+11 | 280 | dzien normalny |
| D+12 | 190 | weekend |
| D+13 | 210 | weekend |

### Zasady kompatybilnosci miedzy modulami

- Katalog M2 seedowac jako pierwszy.
- Plan M2 publikowac na 14 dni.
- M1 zamowienia tworzyc z pozycji planu, nie z mockow.
- `OrderItems` musza miec `DietMenuPlanItemId`, `MealId`, `MealVariantId`,
  `MealSlot` i `DeliveryDate`.
- Produkcje generowac dla wybranych dni smoke, niekoniecznie dla calego zakresu.
- Magazyn seedowac po skladnikach i opakowaniach uzytych w planie.
- M4 tworzy trasy z realnych dostaw.
- Packing i loading maja dostac paczki powiazane z realnymi order/delivery.

### Technika seedowania

- Nie dodawac `VolumeDemo` do zwyklego startu dev.
- Dodac `DatabaseSeedingProfile.VolumeDemo`.
- Uzyc osobnej klasy/orchestratora, np. `VolumeDemoSeeder`, zamiast dalszego
  rozpychania `DatabaseSeeder`.
- Inserty robic batchami przez `SqlBulkCopy`, TVP albo staging tables.
- Batch size: 500-2000 rekordow.
- Nie trzymac calego grafu danych w pamieci.
- Dane identyfikowac naturalnymi prefiksami:
  - `VOL-USER-...`
  - `VOL-ORDER-...`
  - `VOL-M2-...`
  - `VOL-BATCH-...`
  - `VOL-ROUTE-...`
- Reset wolumenowy robic po prefiksach i batchami, bez kasowania katalogu M2
  uzywanego przez zwykle demo.

### Pamiec i Docker

- Zwykle demo: 4-6 GB Docker Desktop.
- `VolumeDemo`: minimum 10-12 GB dla Docker Desktop.
- Komfortowo: 12-16 GB, jezeli jednoczesnie dziala SQL Server, aplikacja, build
  i przegladarka.
- SQL Server powinien miec jawny limit pamieci, zeby nie zabral calego hosta.
- Aplikacja powinna miec 1-2 GB zapasu.
- Seeder powinien logowac postep per batch i nie budowac list 30k+ obiektow naraz.

## Smoke test po VolumeDemo

### SQL sanity

- Liczba users, staff, orders, order items, delivery calendar.
- Brak osieroconych `OrderItems`.
- Kazde aktywne zamowienie ma klienta, adres, payment i delivery.
- Kazdy `OrderItem` z planu ma referencje M2.
- Produkcja ma `M2SnapshotJson`.
- Packing ma torby, itemy i etykiety.
- Loading ma trasy, stopy i manifesty.
- Magazyn ma partie FEFO dla skladnikow i opakowan.

### UI smoke

- `/Account/Login`
- `/Account/Register`
- `/Menu`
- `/Order`
- `/Checkout`
- `/diet-editor/menu-plan`
- `/diet-editor/recipes`
- `/ingredients`
- `/meals`
- `/production`
- `/production/m2-plan`
- `/production/warehouse-demand`
- `/production/foil-printing`
- `/packing`
- `/packing/labels`
- `/loading`
- `/logistics`
- `/admin`
- `/customer-support`
- `/human-resources`

### Kryteria sukcesu

- Najwieksze listy nie pobieraja calej tabeli.
- Paginacja jest realizowana w SQL tam, gdzie tabela moze urosnac.
- Kuchnia generuje plan z realnych zamowien M1 i snapshotu M2.
- FEFO nie zdejmuje stanow podwojnie.
- Etykieta i kompletacja dzialaja na realnych paczkach.
- BOK/Admin/HR nie timeoutuja przy 1200 userach i tysiacu+ rekordow operacyjnych.

## Kolejnosc prac

1. SQL paging dla M1 historii zamowien.
2. SQL paging dla BOK tickets i Admin users.
3. SQL paging dla HR employees/leaves/schedules.
4. Search/paging dla M4 drivers/vehicles.
5. Ograniczenie `WarehouseDemandService` i batch availability.
6. Ograniczenie pelnych boardow w packing/loading.
7. Async lookupi w starych ekranach M2.
8. Dopiero potem implementacja `VolumeDemo`.

## Lista kontrolna przed implementacja VolumeDemo

- [ ] `DatabaseSeedingProfile.VolumeDemo` istnieje, ale nie odpala sie przy zwyklym starcie.
- [ ] Admin/BOK/HR nie uzywaja `GetAllAsync` dla glownych list.
- [ ] M1 historia zamowien ma SQL paging.
- [ ] M4 kierowcy i pojazdy maja SQL paging.
- [ ] Demand M3 ma batchowe sprawdzanie stanow.
- [ ] Packing/loading nie buduja pelnych boardow dla typowych akcji.
- [ ] Sa sanity queries po seedzie.
- [ ] Jest smoke script albo checklista dla glownego flow.
