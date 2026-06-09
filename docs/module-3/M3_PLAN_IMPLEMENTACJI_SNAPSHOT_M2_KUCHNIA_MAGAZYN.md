# M3: plan implementacji na snapshotach M2

Data: 2026-06-07  
Cel: rozdzielić prace, które możemy wdrażać od razu, od prac zależnych od domknięcia M2 przez Gabriela.

## 0A. Aktualizacja 08.06.2026 — checklista stanu

Na koniec obecnego etapu dokument traktujemy jako checklistę weryfikacyjną, a nie listę nowych dużych zadań. Zakres P0 został domknięty w najważniejszych miejscach:

- [x] `/meals` działa jako katalog z server-side search, filtrami, paginacją i statusem kompletności.
- [x] `/diet-editor/recipes` ma dopracowany widok V1: metryki, lepszy empty state i mniej dominujący formularz tworzenia składowej.
- [x] M1 smoke potwierdza ścieżkę wybór diety -> koszyk -> checkout -> `OrderItems` z referencjami M2.
- [x] `/production/m2-plan` istnieje jako 7-dniowy podgląd planu M2 dla produkcji.
- [x] `WarehouseDemandService` V1 agreguje zapotrzebowanie z opłaconych zamówień i snapshotu M2.
- [x] Etykiety foliowe korzystają ze snapshotu M2 jako źródła danych wariantu.
- [ ] Weryfikacja końcowa flow M2 -> M1 -> M3 na lokalnym Dockerze po uruchomieniu bazy i aplikacji.

Nie zaczynamy teraz ciężkich rzeczy z sekcji "Czekaj na domknięcie M2":

- trwałych sesji gotowania składowych;
- pełnego workflow alertów i potwierdzeń;
- pełnych rezerwacji magazynowych oraz demand 7+ dni.

Priorytet końcowy: weryfikacja, instrukcja smoke i upewnienie się, że przepływ M2 -> M1 -> M3 jest czytelny dla testów ręcznych.

## 0. Aktualizacja 07.06.2026 — etap 1 i etap 2

Etap 1 został częściowo wykonany w kodzie M3:

- istnieje widok `/production/m2-plan` jako podgląd planu M2 na 7+ dni;
- istnieje `WarehouseDemandService` V1 z agregacją składników i opakowań z opłaconych zamówień oraz snapshotu M2;
- karta gotowania pokazuje progres sesji składowych;
- etykiety foliowe korzystają ze snapshotu M2.

Etap 2 dopina operacyjność panelu:

- `/production` pokazuje kompaktowy panel „Plan M2 na 7 dni”;
- pełny widok `/production/m2-plan` pokazuje świeżość snapshotu produkcyjnego względem aktualnego M2;
- admin może odświeżyć plan produkcji z faktycznego M2 + M1 przed startem FEFO/gotowania;
- refresh blokuje się, jeśli plan ma już FEFO, opakowania, gotowanie albo ugotowane ilości;
- repozytorium produkcji podmienia pozycje planu transakcyjnie przez soft-delete starych pozycji i insert nowych.

## 1. Decyzja wykonawcza

M3 może już pracować na obecnym snapshotcie M2 w zakresie V1: karta gotowania, etykiety foliowe, podstawowe spięcie z produkcją i pakowaniem. Nie powinniśmy jednak zaczynać ciężkich migracji sesji gotowania ani pełnego `WarehouseDemandService` 7+ dni, dopóki M2 nie domknie stabilnego UX planu dnia, wariantów i publikacji.

Plan na jutro powinien być pragmatyczny:

1. Uporządkować elementy, które są już wystarczająco oparte na obecnym M2.
2. Zostawić miejsce na pełny Sprint 8 po stabilnej paczce M2.
3. Nie blokować M1, ale jasno wymagać zapisu referencji M2 w `OrderItems`.

Dokument powiązany: [M2_M3_AUDYT_WIDOKOW_I_GOTOWOSC_M1.md](M2_M3_AUDYT_WIDOKOW_I_GOTOWOSC_M1.md).

## 2. Stan wyjściowy

### 2.1 Co M2 już daje technicznie

Obecny kod M2/M3 daje wystarczający backbone do pracy operacyjnej V1:

- `RecipeComponentVersion` jako wersjonowana składowa przepisu.
- `Meal` jako posiłek/agregat.
- `MealVariant` jako wariant technologiczny dania.
- `DietMenuPlanItems.MealVariantId`, czyli plan może wskazywać konkretny wariant dania.
- `MealVariantResultCalculator`, który liczy wynik wariantu:
  - finalną gramaturę,
  - nutrition na 100 g,
  - nutrition na porcję,
  - alergeny,
  - składniki,
  - opakowania,
  - status kompletności.
- `PublishedDietPlanSnapshotDto`, który przekazuje do M3:
  - `DietMenuPlanItemId`,
  - `MealId`,
  - `MealVariantId`,
  - `MealSlot`,
  - `ServingMultiplier`,
  - finalną gramaturę,
  - nutrition,
  - alergeny,
  - komponenty,
  - składniki agregowane,
  - opakowania,
  - instrukcje,
  - alerty.
- Demo seeder tworzy kompletne składowe, warianty, opakowania i plan z `MealVariantId`.

### 2.2 Co M2 nadal ma niedomknięte widokowo

Te braki nie blokują wszystkiego, ale blokują pełny komfort i docelowy Sprint 8:

- [x] `/meals` ma już server-side search, paginację i filtry katalogowe.
- [x] `/diet-editor/meals` przekierowuje na realny katalog.
- [x] `/diet-editor/recipe` przekierowuje na `/diet-editor/recipes`.
- [x] `/diet-editor/menu-plan/day` używa lookupów dla posiłków i dociągania wariantów wybranego dania zamiast pełnych selectów katalogu.
- Widok posiłku pokazuje dane techniczne, ale nie jest jeszcze czytelnym ekranem dietetycznym agregatu.
- Pełny UX publikacji planu dnia, wyboru wariantów i walidacji braków jest nadal do dopracowania po stronie M2.

### 2.3 Co M3 już ma

M3 ma już fundament:

- generowanie planu produkcji ze snapshotu M2;
- `M2SnapshotJson` i `M2SnapshotHash` na `ProductionPlanItems`;
- dashboard kuchni z search, filtrami i paginacją;
- karta gotowania potrafi czytać składowe i opakowania ze snapshotu;
- FEFO jest idempotentne przez `FefoDeductedAt`;
- opakowania są zdejmowane idempotentnie przez `PackagingDeductedAt`;
- foliowanie ma search, filtry i paginację;
- pakowanie blokuje pudełko bez etykiety foliowej.

## 3. Implementuj teraz

To są zadania, które można robić od razu na obecnym stanie M2. Nie wymagają czekania na pełny redesign planu dnia Gabriela.

### 3.1 Widok posiłków M2/M3-ready

Status 08.06.2026:

- [x] Wykonane jako zakres P0.

Powód:

Katalog posiłków jest miejscem, z którego dietetyk i M3 rozumieją, co faktycznie trafi do planu produkcji. Obecna lista `/meals` działa, ale nie skaluje się i nie pokazuje wystarczająco statusu kompletności.

Zakres:

- Dodać server-side search i paginację dla `/meals`.
- Dodać filtry:
  - tekst,
  - kategoria,
  - status,
  - alergen,
  - ma warianty / brak wariantów,
  - braki publikacji.
- Pokazać wiersze z informacjami:
  - nazwa,
  - kategoria,
  - status,
  - liczba wariantów,
  - finalna gramatura,
  - nutrition status,
  - opakowanie status,
  - status kompletności.
- Usunąć albo przekierować placeholder `/diet-editor/meals` na realny katalog.

Kryterium zakończenia:

- Dietetyk może znaleźć posiłek bez ładowania całego katalogu.
- Lista nie pokazuje tylko opublikowanych pozycji, ale pozwala pracować także na draftach i brakach.
- Puste stany mają jasne CTA.

Priorytet:

- P0 na jutro.

### 3.2 Widok przepisów-składowych

Status 08.06.2026:

- [x] Wykonane jako zakres V1.

Powód:

Przepisy-składowe są już technicznie dobrze zbudowane, ale widok `/diet-editor/recipes` powinien lepiej prowadzić przez braki i status publikacji.

Zakres:

- Poprawić layout `/diet-editor/recipes`.
- Dodać metryki nad tabelą:
  - wszystkie,
  - draft,
  - published,
  - z brakami publikacji.
- Dopracować empty states.
- Dodać szybkie filtry braków.
- Oddzielić formularz "Nowa składowa" od głównej tabeli, żeby lista była pierwszym ekranem roboczym.
- Zostawić model danych bez przebudowy.

Kryterium zakończenia:

- Użytkownik od razu widzi, które składowe blokują publikację.
- Widok jest czytelny jako katalog dużych danych, a nie tylko techniczna tabela.

Priorytet:

- P0/P1 na jutro.

### 3.3 Etykiety foliowe M3 ze snapshotu

Status 08.06.2026:

- [x] Wykonane jako spięcie V1 ze snapshotem M2.

Powód:

To jest najważniejsze spięcie M2 -> M3, które możemy zrobić teraz. Obecnie etykieta foliowa korzysta częściowo z legacy danych po `MealId`, a powinna bazować na `ProductionPlanItem.M2SnapshotJson`.

Zakres:

- `PrintFoilLabelAsync` ma pobierać `ProductionPlanItem` i deserializować `M2SnapshotJson`.
- Etykieta ma drukować:
  - nazwę dania,
  - wariant dania,
  - skład z M2,
  - alergeny z M2,
  - nutrition na 100 g,
  - nutrition na porcję,
  - gramaturę,
  - QR pudełka,
  - numer wydruku,
  - powód redruku.
- `LabelDataJson` ma zapisywać pełny snapshot etykiety, żeby `GetLatestFoilLabelAsync` nie musiał ponownie czytać legacy danych.
- Zachować twardą blokadę:
  - pozycja produkcji musi być `Cooked`;
  - `PackagingDeductedAt` musi być ustawione;
  - redruk wymaga powodu.

Kryterium zakończenia:

- Etykieta nie używa `GetMealIngredientsAsync`, `GetMealAllergensAsync` ani kalorii po bazowym `MealId` jako źródła prawdy.
- Etykieta wariantu dania pokazuje finalny wynik wariantu ze snapshotu.
- Podgląd ostatniej etykiety działa z zapisanego `LabelDataJson`.

Priorytet:

- P0 na jutro.

### 3.4 Seeder i smoke flow

Status 08.06.2026:

- [x] Dodany smoke M1 dla wyboru diety, koszyka, checkoutu i referencji M2 w `OrderItems`.
- [ ] Do wykonania ręcznie po stronie lokalnego środowiska: pełny smoke Docker M2 -> M1 -> M3.

Powód:

Bez stabilnych danych demo nie da się ocenić, czy M2/M1/M3 faktycznie działają razem.

Zakres:

- Utrzymać albo doprecyzować seeder demo:
  - plan M2 minimum 7 dni;
  - opublikowane składowe;
  - warianty dań;
  - opakowania;
  - opłacone zamówienia;
  - `OrderItems.DietMenuPlanItemId`;
  - `OrderItems.MealId`;
  - `OrderItems.MealVariantId`;
  - `OrderItems.MealSlot`;
  - partie magazynowe dla składników i opakowań.
- Dodać lub utrzymać SQL sanity:
  - `RecipeComponentVersions > 0`;
  - `MealVariantComponents > 0`;
  - demo plan ma `MealVariantId`;
  - demo order items mają referencje M2;
  - zasoby magazynowe mają dostępne ilości.

Kryterium zakończenia:

- Na czystej bazie Docker można przejść:
  - opublikowany plan M2,
  - zamówienie M1,
  - generowanie produkcji M3,
  - karta gotowania,
  - zatwierdzenie gotowania,
  - etykieta foliowa,
  - pakowanie.

Priorytet:

- P0 na jutro, ale tylko jeśli smoke wykaże dziury w danych.

### 3.5 Dokumentacja i audyt

Powód:

Musimy mieć jasny podział pracy, żeby nie zacząć ciężkiej przebudowy M3 przed stabilną paczką M2.

Zakres:

- Ten dokument traktować jako plan pracy.
- Utrzymać powiązanie z:
  - `M2_M3_AUDYT_WIDOKOW_I_GOTOWOSC_M1.md`,
  - `PLAN_SPRINTOW_M3.md`,
  - `GABRIEL_M2_KRYTYCZNE_WIDOKI_PLAN_PRODUKCJI.md`.
- Po wykonaniu prac P0 dopisać wynik smoke testu albo osobny release note.

Kryterium zakończenia:

- Każdy zespół wie, co robić teraz i czego nie zaczynać przed M2.

Priorytet:

- P0.

## 4. Czekaj na domknięcie M2

Te rzeczy mają sens docelowo, ale nie powinny być pierwszym krokiem jutro, bo zależą od stabilnej paczki M2 lub mogą wymagać powtórnej przebudowy.

### 4.1 Pełny widok planu dnia M2

Czekamy na:

- wygodne układanie planu dnia;
- search/select posiłków i wariantów;
- czytelną walidację publikacji;
- stabilne zachowanie edycji planu po publikacji;
- potwierdzenie, że `MealVariantId` jest używane konsekwentnie.

Nie robić teraz:

- docelowego redesignu M3 "Plan z M2" z pełnymi alertami i odbiorem;
- głębokiego workflow korekt planu po stronie M3.

Można przygotować:

- prosty opis docelowego widoku;
- listę danych wymaganych od M2;
- testy kontraktu snapshotu.

### 4.2 Pełne sesje gotowania składowych

Status 08.06.2026:

- [ ] Odroczone. Nie zaczynać przed stabilnym kontraktem M2.

Czekamy na:

- stabilny kontrakt składowych;
- pewność, że `RecipeComponentVersionId` i instrukcje kroków nie zmienią kształtu;
- decyzję, czy sesja składowej może zasilać kilka dni i jak obsłużyć 3-dniowe okno.

Nie robić teraz:

- migracji trwałych tabel sesji gotowania;
- pełnego modelu rezerwacji półproduktów na 3 dni;
- manager approval dla odchyleń zużycia.

Można zrobić teraz:

- wizualnie poprawić kartę gotowania;
- pokazać komponenty ze snapshotu;
- przygotować checklistę kroków tylko jako UI V1, bez trwałego modelu sesji.

### 4.3 Pełne alerty `PlanChangeAlerts`

Status 08.06.2026:

- [ ] Odroczone. Nie zaczynać pełnego workflow acknowledgement i blokad.

Czekamy na:

- realne generowanie alertów przez M2;
- reguły, kiedy alert powstaje;
- decyzję, które alerty wymagają potwierdzenia przez kuchnię, a które przez magazyn.

Nie robić teraz:

- pełnego workflow acknowledgement w M3;
- twardych blokad produkcji na podstawie alertów.

Można przygotować:

- miejsce w docelowym widoku "Plan z M2";
- testy adaptera, jeśli alerty już są w snapshotcie.

### 4.4 `WarehouseDemandService` 7+ dni

Status 08.06.2026:

- [x] V1 wykonane dla obecnego zapotrzebowania ze snapshotu i opłaconych zamówień.
- [ ] Pełne rezerwacje, eksport i demand 7+ dni odroczone.

Czekamy na:

- stabilny plan 7+ dni w M2;
- wygodną publikację i walidację planu;
- pewność, że plan ma dane wariantów, opakowań i składników w docelowym kształcie.

Nie robić teraz:

- pełnego FEFO preview 7+ dni;
- eksportu zapotrzebowania;
- twardych rezerwacji magazynowych pod przyszłe dni.

Można przygotować:

- projekt DTO;
- opis agregacji;
- test kontraktu snapshotu dla jednego dnia.

### 4.5 Zaawansowane warianty klienta i M1

Czekamy na:

- stabilny sposób wyboru wariantu dania przez klienta;
- decyzję, czy M1 wybiera konkretny `DietMenuPlanItemId`, czy tylko standardową dietę;
- finalny UX M1 koszyka i zamówienia.

Nie robić teraz:

- zaawansowanych korekt M3 pod wszystkie warianty klienta;
- rozbudowanych fallbacków zgadujących posiłek po nazwie.

Wymóg już teraz:

- M1 musi zapisywać w `OrderItems`:
  - `DietMenuPlanItemId`,
  - `MealId`,
  - `MealVariantId`,
  - `MealSlot`,
  - `DietVariantId`.

## 5. Końcowa instrukcja weryfikacji flow M2 -> M1 -> M3

Cel tej sekcji: potwierdzić ręcznie, że wykonane elementy spinają się operacyjnie. To nie jest lista nowych dużych funkcji.

### Krok 1: Przygotowanie środowiska

1. Uruchomić lokalne środowisko Docker z bazą i aplikacją.
2. Upewnić się, że seed/demo zawiera:
   - opublikowany plan M2 na minimum jeden dzień;
   - aktywne dania i warianty dań;
   - opakowania i składniki magazynowe;
   - możliwość złożenia zamówienia M1.
3. Jeśli środowisko było już uruchomione przed zmianą limitu pamięci SQL, odtworzyć kontenery zgodnie z lokalną instrukcją Docker.

### Krok 2: Weryfikacja M2

1. Wejść w `/meals`.
2. Sprawdzić wyszukiwanie, filtry, quick filters i paginację na większym katalogu.
3. Wejść w `/diet-editor/recipes`.
4. Sprawdzić metryki nad tabelą, empty state oraz formularz "Nowa składowa".
5. Wejść w `/diet-editor/menu-plan/day/{date}`.
6. Wybrać danie przez lookup, dociągnąć warianty tylko wybranego dania i sprawdzić status wariantu przed zapisem.
7. Upewnić się, że plan dnia można opublikować albo że widoczne są konkretne braki publikacji.

### Krok 3: Weryfikacja M1

1. Przejść ścieżkę klienta: wybór diety -> koszyk -> checkout.
2. Upewnić się, że zamówienie jest opłacone lub oznaczone jako gotowe do produkcji zgodnie z obecnym flow.
3. Sprawdzić w danych, że `OrderItems` mają:
   - `DietMenuPlanItemId`;
   - `MealId`;
   - `MealVariantId`;
   - `MealSlot`;
   - `DietVariantId`.
4. Jeżeli któryś identyfikator jest pusty, traktować to jako blokadę smoke M2 -> M1 -> M3.

### Krok 4: Weryfikacja M3

1. Wejść w `/production/m2-plan`.
2. Sprawdzić 7-dniowy widok: daty, status publikacji M2, konkretne dania, sloty, warianty diet, warianty dań, gramaturę, składniki, opakowania, alerty i braki.
3. Wejść w `/production`.
4. Wygenerować lub odświeżyć plan produkcji, jeżeli aktualny stan na to pozwala.
5. Sprawdzić, czy pozycje produkcji mają `M2SnapshotJson` i `M2SnapshotHash`.
6. Wejść w kartę gotowania dla pozycji produkcji.
7. Potwierdzić, że karta pokazuje dane ze snapshotu: komponenty, składniki, opakowania i instrukcje.
8. Przejść FEFO i zatwierdzenie gotowania.
9. Upewnić się, że opakowania są zdejmowane idempotentnie po zatwierdzeniu gotowania.

### Krok 5: Weryfikacja foliowania i pakowania

1. Wejść w `/production/foil-printing`.
2. Sprawdzić, czy pudełka do foliowania są widoczne po ugotowaniu i rozliczeniu opakowań.
3. Wydrukować etykietę foliową.
4. Upewnić się, że etykieta pokazuje dane wariantu ze snapshotu M2:
   - nazwę dania;
   - wariant dania;
   - gramaturę;
   - skład;
   - alergeny;
   - nutrition;
   - QR pudełka.
5. Sprawdzić podgląd ostatniej etykiety z `LabelDataJson`.
6. Przejść do `/packing`.
7. Potwierdzić, że pakowanie blokuje pudełko bez etykiety i pozwala spakować pudełko z etykietą.

### Krok 6: Wynik smoke

Smoke uznajemy za zaliczony, jeżeli:

- M2 publikuje plan z konkretnymi daniami i wariantami.
- M1 zapisuje opłacone zamówienie z referencjami M2.
- M3 pokazuje `/production/m2-plan` oraz generuje produkcję ze snapshotem.
- Karta gotowania i etykieta foliowa czytają dane ze snapshotu.
- Magazyn V1 pokazuje zapotrzebowanie z opłaconych zamówień.
- Pakowanie wymusza etykietę foliową.

Jeżeli smoke nie przejdzie, dopisujemy konkretny punkt awarii do dokumentu albo issue, ale nie rozszerzamy od razu zakresu o trwałe sesje gotowania, pełne alerty ani rezerwacje 7+ dni.

## 6. Kryteria akceptacji dokumentu

Dokument spełnia cel, jeśli:

- jasno mówi, co robimy jutro;
- jasno mówi, czego nie zaczynamy przed domknięciem M2;
- nie miesza odpowiedzialności M2, M1 i M3;
- wskazuje, że M3 może już używać snapshotu do kart gotowania i etykiet;
- wskazuje, że pełne sesje gotowania i demand 7+ dni czekają na stabilny M2;
- daje konkretną smoke checklistę.

## 7. Odpowiedzialności modułów

### M2

M2:

- edytuje posiłki, warianty, składowe i składniki;
- publikuje plan;
- wylicza lub zapisuje finalny wynik dania;
- dostarcza snapshot.

### M1

M1:

- pokazuje klientowi opublikowane opcje;
- przyjmuje zamówienie;
- zapisuje referencje M2 w `OrderItems`;
- oznacza zamówienie jako opłacone.

### M3

M3:

- generuje produkcję z opłaconych zamówień;
- snapshotuje dane M2;
- wykonuje FEFO;
- prowadzi kartę gotowania;
- rozlicza opakowania;
- drukuje etykiety foliowe;
- pakuje pudełka i torby.

## 8. Minimalne komendy walidacyjne po obecnym etapie

Po kodowym sprincie "implementuj teraz":

```powershell
git diff --check
dotnet build --no-restore -v:q
dotnet test --no-restore --filter "FullyQualifiedName!~Integration" -v:q
```

Po Docker smoke:

```powershell
docker compose up -d --force-recreate sqlserver web
```

Ręcznie sprawdzić:

- `/diet-editor/recipes`;
- `/meals`;
- `/diet-editor/menu-plan`;
- `/production`;
- `/production/m2-plan`;
- `/production/warehouse-demand`;
- `/production/cooking-card/{id}`;
- `/production/foil-printing`;
- `/packing`;
- SQL sanity dla `OrderItems.DietMenuPlanItemId`, `MealVariantId` i `ProductionPlanItems.M2SnapshotJson`.

## 9. Podsumowanie

Nie musimy czekać z wszystkim na Gabriela. W obecnym etapie jest już zrobione:

- katalog posiłków gotowy pod M3;
- poprawiony widok przepisów-składowych;
- 7-dniowy podgląd `/production/m2-plan`;
- `WarehouseDemandService` V1;
- etykiety foliowe ze snapshotu;
- smoke M1 potwierdzający krytyczne referencje M2 w `OrderItems`.

Musimy natomiast poczekać z:

- docelowym workflow "Plan z M2" z pełnymi alertami, odbiorem i potwierdzeniami;
- trwałymi sesjami gotowania składowych;
- pełnymi rezerwacjami magazynowymi i demand 7+ dni;
- kompletnym workflow alertów;
- zaawansowanymi wariantami klienta.

To jest bezpieczny podział: teraz domykamy weryfikację rzeczy, które już mają dane, a większą kuchnię budujemy dopiero na stabilnym planie M2.
