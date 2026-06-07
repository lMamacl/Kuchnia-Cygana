# M3: plan implementacji na snapshotach M2

Data: 2026-06-07  
Cel: rozdzielić prace, które możemy wdrażać od razu, od prac zależnych od domknięcia M2 przez Gabriela.

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

- `/meals` nie ma jeszcze server-side search, paginacji i filtrów katalogowych.
- `/diet-editor/meals` jest placeholderem albo wymaga przekierowania.
- `/diet-editor/recipe` jest placeholderem albo wymaga przekierowania.
- `/diet-editor/menu-plan/day` używa dużych selectów dla posiłków i wariantów.
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

## 5. Kolejność pracy na jutro

### Krok 1: Dokument i scope

Cel:

- Upewnić się, że zespół zgadza się na podział "implementuj teraz" vs "czekaj na M2".

Koniec kroku:

- Ten dokument zaakceptowany jako plan roboczy.

### Krok 2: `/meals` jako katalog produkcyjny

Cel:

- Dodać search/paginację i filtry.

Koniec kroku:

- `/meals` nadaje się do pracy na dużym katalogu.
- Placeholder `/diet-editor/meals` nie wprowadza w błąd.

### Krok 3: `/diet-editor/recipes` UX V1

Cel:

- Ułatwić pracę na przepisach-składowych bez przebudowy modelu.

Koniec kroku:

- Widać braki publikacji i statusy.
- Empty state i szybkie filtry są czytelne.

### Krok 4: Etykieta ze snapshotu

Cel:

- Domknąć najważniejsze spięcie M2 -> M3 w foliowaniu.

Koniec kroku:

- Etykieta drukuje dane wariantu ze snapshotu.
- Podgląd etykiety odtwarza zapisane `LabelDataJson`.
- Redruk dalej wymaga powodu.

### Krok 5: Smoke

Cel:

- Potwierdzić, że dane demo i flow działają.

Minimalny smoke:

1. M2 ma opublikowany plan na datę.
2. M1 ma opłacone zamówienie z referencjami M2.
3. M3 generuje plan produkcji.
4. Karta gotowania pokazuje snapshot.
5. FEFO zdejmuje składniki.
6. Zatwierdzenie gotowania zdejmuje opakowania.
7. Foliowanie pokazuje pudełka.
8. Etykieta drukuje dane ze snapshotu.
9. Pakowanie blokuje pudełko bez etykiety i pozwala spakować pudełko z etykietą.

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

## 8. Minimalne komendy walidacyjne po późniejszej implementacji

Po kodowym sprincie "implementuj teraz":

```powershell
git diff --check
dotnet build --no-restore -v:q
dotnet test --no-restore --filter "FullyQualifiedName!~Integration" -v:q
```

Po Docker smoke:

```powershell
docker compose up --build
```

Ręcznie sprawdzić:

- `/diet-editor/recipes`;
- `/meals`;
- `/diet-editor/menu-plan`;
- `/production`;
- `/production/cooking-card/{id}`;
- `/production/foil-printing`;
- `/packing`;
- SQL sanity dla `OrderItems.DietMenuPlanItemId`, `MealVariantId` i `ProductionPlanItems.M2SnapshotJson`.

## 9. Podsumowanie

Nie musimy czekać z wszystkim na Gabriela. Możemy już zrobić:

- katalog posiłków gotowy pod M3;
- poprawiony widok przepisów-składowych;
- etykiety foliowe ze snapshotu;
- stabilny smoke M2 -> M1 -> M3.

Musimy natomiast poczekać z:

- pełnym widokiem "Plan z M2";
- trwałymi sesjami gotowania składowych;
- pełnym `WarehouseDemandService` 7+ dni;
- kompletnym workflow alertów;
- zaawansowanymi wariantami klienta.

To jest bezpieczny podział: jutro domykamy rzeczy, które już mają dane, a większą kuchnię budujemy dopiero na stabilnym planie M2.
