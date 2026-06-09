# Audyt widoków M2 i gotowość M1/M3

Data audytu: 2026-06-06  
Kontekst: stan po integracjach Gabriela oraz poprawkach seedera M2/M3.

## 1. Wniosek wykonawczy

M2 jest już technicznie wystarczające, żeby M1 i M3 zaczęły używać realnych danych z bazy zamiast mocków. Mamy plan menu, warianty dań, wersjonowane składowe, opakowania, nutrition, alergeny, snapshot M2 -> M3 oraz pola w `OrderItems`, które pozwalają M1 zapisać wybór konkretnej pozycji planu.

Nie jest to jednak jeszcze stan gotowy do wygodnego publicznego demo bez poprawek widoków. Największe ryzyka na pokaz za 3 dni to:

1. Puste lub mylące widoki w `DietEditor`, szczególnie `/diet-editor/meals` i `/diet-editor/recipe`.
2. Brak server-side search/paginacji w katalogu posiłków `/meals`.
3. Zbyt duże selecty w edytorze planu dnia, które nie skalują się przy dużym katalogu dań i wariantów.
4. Techniczny, ciężki layout szczegółów posiłku, który pokazuje dane, ale nie prowadzi dietetyka przez kompletność dania.
5. Brak pełnego widoku M3 "Plan z M2" na 7+ dni z alertami, brakami i potwierdzeniem odbioru.

Priorytet: nie przebudowywać teraz całego modelu. Najpierw ustabilizować widoki katalogowe, ukryć placeholdery, podpiąć M1 do realnych pól planu i sprawdzić przepływ: opublikowany plan M2 -> zamówienie M1 -> produkcja M3.

## 2. Co faktycznie działa w M2

### 2.1 Model i kontrakt danych

Działa:

- `RecipeComponent` i `RecipeComponentVersion` jako wersjonowane przepisy-składowe.
- `Meal` jako danie/posiłek agregujące składowe.
- `MealVariant` jako wariant technologiczny dania.
- `MealVariantComponents` jako przypięcie wersji składowych do wariantu.
- `PackagingRequirements` dla składowych, posiłków i wariantów.
- `DietMenuPlans` i `DietMenuPlanItems` jako datowany plan menu.
- `DietMenuPlanItems.MealVariantId`, czyli plan może wskazać specjalny wariant dania.
- `MealVariantResultCalculator`, który agreguje:
  - finalną gramaturę,
  - nutrition na 100 g,
  - nutrition na porcję,
  - alergeny,
  - składniki,
  - opakowania,
  - status kompletności.
- `PublishedDietPlanSnapshotDto`, który przekazuje M3:
  - `DietMenuPlanItemId`,
  - `MealId`,
  - `MealVariantId`,
  - slot posiłku,
  - multiplier,
  - finalną gramaturę,
  - nutrition,
  - alergeny,
  - składowe,
  - składniki agregowane,
  - opakowania,
  - instrukcje,
  - alerty.

Ocena: kontrakt M2 -> M3 jest już realnym backbone, a nie tylko mockiem.

### 2.2 Seeder/demo dane

Działa po ostatniej poprawce:

- demo posiłki dostają opublikowane składowe;
- składowe mają nutrition, gramaturę, alergeny zatwierdzone, instrukcje i opakowania;
- warianty dań mają komponenty;
- plan menu ma `MealVariantId`;
- SQL sanity powinien pokazywać dane w `RecipeComponentVersions` i `MealVariantComponents`.

Ryzyko:

- legacy `Recipes` nadal istnieje jako fallback. Nie traktować go jako docelowego modelu M2.
- jeżeli baza nie przeszła migracji `306`, `508`, `509`, `510`, `511`, widoki mogą pokazać błędy kolumn albo blokery publikacji.

## 3. Audyt widoków M2

### 3.1 Diety: `/diet-editor`

Stan:

- widok pokazuje aktywne diety i warianty;
- można wejść w szczegóły, edytować dietę i dodać wariant;
- brak paginacji, search bara i filtrów statusu;
- lista opiera się na `GetActiveDietsAsync()`.

Co poprawić:

- dodać `DietSearchFilterDto` z `Search`, `IsActive`, `Page`, `PageSize`;
- dodać repozytoryjne wyszukiwanie diet server-side;
- pokazać liczbę wariantów, kompletność wariantów i informację, czy dieta ma opublikowany plan na najbliższe 7 dni;
- dodać empty state z CTA "Dodaj dietę".

Priorytet:

- P1. Diety zwykle nie będą największym katalogiem, ale dla demo trzeba uniknąć wrażenia starej, statycznej tabeli.

### 3.2 Posiłki/potrawy: `/meals`

Stan:

- widok działa jako katalog opublikowanych posiłków;
- szczegóły posiłku mają składowe, warianty, wynik kalkulatora i opakowania;
- lista `/meals` nie ma server-side search, paginacji ani filtrów;
- lista pokazuje tylko opublikowane posiłki, co utrudnia pracę dietetyka nad draftami i brakami;
- brakuje filtrów po kategorii, statusie, alergenach, wariantach, kompletności i brakach publikacji.

Co poprawić:

- dodać `MealSearchFilterDto`:
  - `Search`,
  - `CategoryId`,
  - `Status`,
  - `AllergenId`,
  - `MissingPublicationData`,
  - `HasVariants`,
  - `Page`,
  - `PageSize`;
- dodać `MealRepository.SearchAsync(...)` z `COUNT + OFFSET/FETCH`;
- przerobić `/meals` na `PagedResultDto<MealListItemDto>`;
- pokazać w tabeli:
  - nazwę,
  - kategorię,
  - status,
  - liczbę wariantów,
  - status kompletności,
  - finalną gramaturę,
  - nutrition status,
  - opakowanie status,
  - akcje: szczegóły, edycja, publikuj, archiwizuj;
- dodać quick filters:
  - "Braki publikacji",
  - "Draft",
  - "Published",
  - "Bez wariantu",
  - "Bez opakowania".

Priorytet:

- P0. To jest najważniejszy brak katalogowy M2 przed pokazem. Dietetyk musi móc znaleźć danie bez scrollowania pełnej listy.

### 3.3 Szczegóły posiłku: `/meals/{id}`

Stan:

- widok pokazuje:
  - wynik posiłku bazowego,
  - składowe posiłku,
  - warianty dania,
  - komponenty wariantu,
  - nutrition override,
  - alergeny zatwierdzone,
  - opakowania;
- dane są technicznie wartościowe, ale layout jest ciężki i mało dietetyczny;
- część pól jest blisko siebie, ale nie prowadzi użytkownika przez pytanie: "czy to danie jest gotowe do planu i produkcji?".

Co poprawić:

- dodać górny panel statusu kompletności:
  - "Gotowe do publikacji" albo lista braków;
  - gramatura finalna;
  - nutrition `100 g + porcja`;
  - alergeny;
  - opakowania;
  - liczba składowych;
- rozdzielić ekran na sekcje:
  1. Dane dania.
  2. Wynik finalny.
  3. Składowe bazowe.
  4. Warianty dania.
  5. Opakowania.
  6. Zdjęcia/opis.
- składowe pokazywać jako linki do wersji przepisu;
- przy wariancie pokazać różnice względem bazowego dania;
- dodać czytelne ostrzeżenia: brak nutrition, brak opakowań, brak zatwierdzonych alergenów, nieopublikowana składowa.

Priorytet:

- P0/P1. Dla demo wystarczy czytelny panel statusu i braków. Pełny redesign wariantów może być P1.

### 3.4 Przepisy-składowe: `/diet-editor/recipes`

Stan:

- widok działa i ma:
  - search,
  - paginację,
  - filtr kategorii,
  - filtr statusu wersji,
  - filtr alergenu,
  - filtr "tylko braki publikacji";
- można utworzyć nową składową;
- szczegóły i wersje są obsługiwane przez `RecipeComponentsController`;
- lookup kategorii działa przez endpoint, ale sam endpoint ładuje kategorie i filtruje w pamięci.

Co poprawić:

- poprawić layout listy:
  - dodać panel metryk: wszystkie, z brakami, opublikowane, draft;
  - dodać sortowanie po nazwie, statusie i liczbie braków;
  - oddzielić formularz "Nowa składowa" od listy, żeby nie dominował ekranu;
  - dodać CTA w empty state;
- przenieść lookup kategorii na pełne server-side search w repozytorium kategorii;
- dodać jasny status "ostatnia wersja jest kompletna/niekompletna";
- sprawdzić widoczność polskich znaków w przeglądarce na Windows/Docker, bo w konsoli część tekstów wygląda jak mojibake.

Priorytet:

- P0 dla layoutu i pustych stanów.
- P1 dla pełnego server-side lookup kategorii.

### 3.5 Szczegóły przepisu-składowej i wersji

Stan:

- istnieją widoki:
  - `RecipeComponentDetails`,
  - `RecipeComponentVersion`,
  - `RecipeComponentVersionEdit`;
- model obsługuje:
  - instrukcje,
  - sekcje i kroki,
  - składniki,
  - nutrition,
  - raw/cooked weight,
  - yield,
  - alergeny,
  - opakowania,
  - publikację wersji.

Co poprawić:

- dodać tryb "karta M3" albo przynajmniej podgląd kroków jako checklisty bez edycji;
- pokazać różnicę raw vs cooked oraz wynik skali per porcja;
- dodać czytelny panel "co blokuje publikację";
- dodać historię wersji z przyciskiem "utwórz nową wersję";
- uporządkować formularze składników/opakowań, żeby nie wyglądały jak tabela techniczna.

Priorytet:

- P1. Technicznie działa, ale wymaga dopracowania UX.

### 3.6 Składniki i opakowania: `/ingredients`

Stan:

- widok działa i ma:
  - search,
  - paginację,
  - typ zasobu,
  - kategorię żywnościową,
  - kategorię magazynową,
  - alergen,
  - status,
  - filtr braku mapowania magazynowego;
- formularz składnika ma:
  - nazwę,
  - typ zasobu,
  - kategorię,
  - opis,
  - skład opisowy,
  - zdjęcie URL,
  - jednostkę,
  - koszt,
  - yield,
  - mapowanie magazynowe,
  - nutrition,
  - alergeny,
  - kontrolę temperatury.

Co poprawić:

- lookup kategorii i alergenów nie powinien ładować pełnego katalogu przy dużych danych;
- zdjęcie jest obecnie URL-em, nie bezpiecznym uploadem z walidacją MIME/rozmiaru;
- usuwanie powinno jasno komunikować, gdzie składnik jest używany, jeśli blokada zadziała;
- opakowania/pudełka powinny mieć bardziej wyspecjalizowany widok lub filtr "opakowania", żeby nie ginęły w składnikach spożywczych;
- dodać kolumnę "używany w X składowych / Y opublikowanych wersjach".

Priorytet:

- P0 dla bezpiecznego i czytelnego empty/error state.
- P1 dla lookupów i wyspecjalizowania opakowań.

### 3.7 Plan menu: `/diet-editor/menu-plan`

Stan:

- widok tygodniowy obsługuje zakres minimum 7 dni;
- widok dnia ma draft, publish, copy day, pozycje planu, walidację publikacji;
- pozycja planu obsługuje `DietVariantId`, `MealId`, `MealVariantId`, slot, multiplier i sort order;
- walidacja korzysta z wyniku kalkulatora posiłku/wariantu.

Co poprawić:

- selecty posiłków i wariantów są pełnymi listami, więc nie skalują się przy dużym katalogu;
- trzeba zastąpić je asynchronicznym search/select:
  - search posiłku po nazwie/kategorii/statusie,
  - po wyborze posiłku dociągnąć warianty tylko tego posiłku,
  - pokazać status kompletności wariantu przed dodaniem;
- dodać szybki filtr w widoku tygodniowym:
  - brak planu,
  - draft,
  - published,
  - ma blokery;
- dodać jasny komunikat, które pozycje blokują publikację i dlaczego;
- dodać ostrzeżenie, że zmiany w opublikowanym planie wpływają na M3.

Priorytet:

- P0/P1. Dla demo krytyczne jest, żeby plan dało się szybko ułożyć i opublikować bez scrollowania ogromnych selectów.

### 3.8 Puste i mylące widoki

Wykryte placeholdery:

- `/diet-editor/meals` -> widok `DietEditor/Meals.cshtml` renderuje `_StaffPlaceholder`.
- `/diet-editor/recipe` -> widok `DietEditor/Recipe.cshtml` renderuje `_StaffPlaceholder`.

Jak naprawić:

- `/diet-editor/meals` powinno przekierować na `/meals` albo zostać zastąpione realnym widokiem katalogu posiłków w module M2;
- `/diet-editor/recipe` powinno zostać usunięte z nawigacji albo przekierować do `/diet-editor/recipes`;
- jeżeli linki istnieją w menu, muszą prowadzić do realnych ekranów, nie do placeholderów;
- placeholdery można zostawić tylko dla modułów oznaczonych jako preview, ale nie dla kluczowych ekranów M2.

Priorytet:

- P0. Puste ekrany są najgorszym ryzykiem demo.

## 4. Gotowość M1 do pracy na realnych danych

M1 może zacząć implementację bez mocków, ale musi używać obecnego kontraktu M2.

### 4.1 Co M1 może już czytać

M1 może czytać:

- opublikowany plan M2 na datę;
- pozycje planu `DietMenuPlanItems`;
- wariant diety `DietVariantId`;
- posiłek `MealId`;
- wariant dania `MealVariantId`;
- slot posiłku;
- finalną gramaturę i nutrition ze snapshotu;
- status kompletności i publikacji.

Najbezpieczniejszy kierunek:

- M1 nie powinien sam składać danych posiłku z legacy tabel.
- M1 powinien używać opublikowanego planu M2 jako katalogu dostępnych opcji na konkretną datę.

### 4.2 Co M1 musi zapisać w `OrderItems`

Dla każdego wybranego posiłku M1 powinien zapisać:

- `DietMenuPlanItemId` - najważniejszy identyfikator pozycji planu M2;
- `MealId` - posiłek bazowy;
- `MealVariantId` - wariant dania, jeśli wybrany albo wynikający z planu;
- `MealSlot` - slot, np. `Breakfast`, `Lunch`;
- `DietVariantId` - wariant diety klienta.

Jeżeli klient nie wybiera pojedynczych dań, tylko kupuje standardową dietę:

- M1 może zapisać tylko `DietVariantId`;
- M3 użyje fallbacku po wariancie diety;
- to jest dobre dla prostego demo, ale niewystarczające dla docelowego wyboru konkretnych dań.

### 4.3 Co blokuje pełny M1

Brakuje jeszcze pełnego, wygodnego widoku/endpointu M1 typu "opublikowane opcje menu dla klienta":

- data;
- dieta;
- wariant kaloryczny;
- slot;
- posiłek;
- wariant dania;
- cena/dopłata, jeśli dotyczy;
- alergeny;
- nutrition;
- gramatura;
- informacja o dostępności.

Jak naprawić:

- dodać aplikacyjny query service dla M1, który zwraca tylko opublikowane i kompletne pozycje planu;
- nie wystawiać klientowi pozycji z `ValidationWarnings`;
- zapisać snapshot wyboru klienta w `OrderItems` albo w tabeli pomocniczej, jeśli cena/nazwa mają być odporne na późniejsze zmiany katalogu.

Priorytet:

- P0 dla zapisu `DietMenuPlanItemId`, `MealId`, `MealVariantId`, `MealSlot`.
- P1 dla wygodnego query service pod frontend klienta.

## 5. Gotowość M3 do pracy na realnym M2

M3 może już konsumować realny snapshot M2.

Działa lub jest wystarczające do V1:

- `ProductionPlanGenerator` pobiera opublikowany snapshot M2;
- plan produkcji korzysta z zamówień M1;
- explicit `OrderItems` mogą sterować ilościami po `DietMenuPlanItemId` i `MealVariantId`;
- M3 zapisuje `M2SnapshotJson` i `M2SnapshotHash`;
- karta gotowania może czytać składowe, składniki, opakowania i nutrition ze snapshotu;
- dashboard kuchni ma search, filtry i paginację;
- foliowanie ma search, filtry i paginację.

Brakuje do pełnego modelu:

- widoku "Plan z M2" na 7+ dni z alertami, brakami i potwierdzeniem odbioru;
- `WarehouseDemandService` dla 7+ dni: składniki, przyprawy, opakowania, FEFO preview, braki, eksport;
- pełnych sesji gotowania `RecipeComponentVersion + ProductionDate`;
- checklisty kroków per składowa w sesji gotowania;
- manager approval dla odchyleń i zmian liczby pojemników/etykiet;
- etykiety w pełni opartej o snapshot: skład, alergeny, nutrition `100 g + porcja`, gramatura, data ważności, QR;
- pełnej obsługi `PlanChangeAlerts`: generowanie, widok, potwierdzanie odbioru.

Priorytet:

- P0: potwierdzić przepływ M1 order -> M3 production na realnych `OrderItems`.
- P1: widok "Plan z M2" i zapotrzebowanie magazynowe 7+ dni.
- P2: sesje składowych, QR, pełne alerty i approval.

## 6. Plan napraw w kolejności ważności

### P0 - publiczne demo za 3 dni

1. Usunąć lub przekierować placeholdery:
   - `/diet-editor/meals` -> `/meals`;
   - `/diet-editor/recipe` -> `/diet-editor/recipes`.

2. Dodać server-side search/paginację dla `/meals`:
   - filtr tekstowy,
   - status,
   - kategoria,
   - alergen,
   - braki publikacji,
   - warianty,
   - `Page/PageSize`.

3. Uporządkować `/diet-editor/recipes`:
   - metryki nad tabelą,
   - lepszy empty state,
   - szybkie filtry braków,
   - przenieść "Nowa składowa" do mniej dominującej sekcji lub osobnego przycisku.

4. Uprościć plan dnia:
   - dodać search select dla posiłku;
   - warianty dociągać po wybranym posiłku;
   - pokazać status kompletności przed zapisaniem pozycji.

5. Zweryfikować M1 na realnych polach:
   - utworzyć zamówienie z `DietMenuPlanItemId`, `MealId`, `MealVariantId`, `MealSlot`;
   - wygenerować plan produkcji M3;
   - sprawdzić, że produkcja grupuje po konkretnej pozycji planu/wariancie.

6. Sprawdzić demo smoke:
   - `/diet-editor/recipes`,
   - `/diet-editor/menu-plan`,
   - `/meals`,
   - `/ingredients`,
   - `/production`,
   - `/production/foil-printing`.

### P1 - stabilizacja operacyjna

1. Dodać search/paginację diet.
2. Dodać server-side lookup kategorii, alergenów i kategorii magazynowych.
3. Przebudować szczegóły posiłku na ekran agregatu:
   - status kompletności,
   - wynik finalny,
   - warianty,
   - składowe,
   - opakowania,
   - alergeny,
   - nutrition.
4. Dodać widok "Plan z M2" w M3 na 7+ dni.
5. Dodać `WarehouseDemandService` V1.
6. Dodać czytelne komunikaty blokad publikacji i braków magazynowych.

### P2 - pełny model docelowy

1. Sesje gotowania per `RecipeComponentVersion + ProductionDate`.
2. Checklisty kroków składowych w M3.
3. Pełne alerty `PlanChangeAlerts` z potwierdzeniem odbioru.
4. QR per fizyczny pojemnik i bundle wielopojemnikowy.
5. Redruk etykiety z powodem i audytem.
6. Zaawansowane warianty klienta: alergiczne, high-protein, gramaturowe.
7. Bezpieczny upload zdjęć zamiast samego URL.

## 7. Testy akceptacyjne dla tego audytu

### Testy widoków M2

- `/diet-editor/recipes`:
  - search działa server-side;
  - filtr braków pokazuje tylko niekompletne składowe;
  - paginacja nie gubi filtrów.
- `/ingredients`:
  - search działa server-side;
  - filtry typ/kategoria/alergen/status działają razem;
  - filtr braku mapowania pokazuje składniki/opakowania bez mapowania.
- `/meals`:
  - po poprawce search i paginacja działają server-side;
  - można filtrować draft/published/braki;
  - lista nie ładuje pełnego katalogu.
- `/diet-editor/menu-plan`:
  - można utworzyć plan na 7+ dni;
  - można wybrać `MealVariantId`;
  - publikacja blokuje braki;
  - plan opublikowany jest widoczny dla M3.

### Testy M1

- M1 tworzy zamówienie z `DietMenuPlanItemId`, `MealId`, `MealVariantId`, `MealSlot`.
- M1 potrafi utworzyć prostą dietę bez wyboru posiłków, zapisując minimum `DietVariantId`.
- M3 generuje produkcję inaczej dla dwóch wariantów tego samego `MealId`, jeśli `MealVariantId` jest różny.

### Testy M3

- Brak opublikowanego snapshotu M2 daje czytelny błąd.
- Opublikowany plan M2 + opłacone zamówienia generują plan produkcji.
- Karta gotowania pokazuje dane ze snapshotu.
- Foliowanie widzi ugotowane pozycje.
- Etykieta nie może być wydrukowana/spakowana wbrew blokadom.

## 8. Decyzje dla zespołu

1. M2 jest właścicielem planu, posiłków, wariantów, składowych, nutrition i opakowań.
2. M1 nie tworzy własnego mock-katalogu dań. Czyta opublikowany plan M2.
3. M1 zapisuje referencje M2 w `OrderItems`.
4. M3 używa snapshotu M2 jako źródła danych kuchni.
5. Legacy `Recipes` zostają tylko jako fallback/historyczne dane, nie jako docelowa ścieżka.
6. Na demo za 3 dni ważniejsze są działające i czytelne listy niż pełna historia wersji i idealny layout.

## 9. Minimalny zakres gotowy do upublicznienia

Żeby pokazać backbone bez wstydu, minimalnie trzeba mieć:

- brak placeholderów w kluczowym M2;
- `/meals` z search/paginacją;
- `/diet-editor/recipes` i `/ingredients` z czytelnymi filtrami i empty state;
- plan 7+ dni z wyborem wariantu dania;
- M1 zapisujący `DietMenuPlanItemId`, `MealId`, `MealVariantId`, `MealSlot`;
- M3 generujące produkcję z realnego snapshotu;
- karta gotowania i foliowanie bez regresji.

To wystarczy, żeby przejść z "mamy fragmenty" na "mamy działający backbone danych M2 -> M1 -> M3".
