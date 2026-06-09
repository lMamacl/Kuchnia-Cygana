# Repo sweep 3-4h: widoki, wydajnosc i super seeder 40k

## Cel

Ten dokument zbiera konkretne poprawki, ktore warto wykonac w najblizszym oknie 3-4 godzin. Zakres nie zaklada pelnego reworka dzialajacych modulow. Priorytetem sa widoczne bledy UI, linki do pustych/legacy ekranow, miejsca bez paginacji/search oraz zapytania, ktore nie przetrwaja planowanego super seedera z okolo 40 000 rekordow.

## P0 - zrobic dzisiaj

### 1. Polskie znaki i widoczne teksty

Najpierw zweryfikowac w przegladarce i naprawic tylko mojibake faktycznie widoczny w UI. Nie poprawiac tekstow, ktore wygladaja zle jedynie w konsoli lub przy odczycie pliku innym kodowaniem, jesli runtime renderuje je prawidlowo.

Ekrany do sprawdzenia:

- menu boczne pracownikow;
- kuchnia: pulpit, plan M2, zapotrzebowanie 7 dni, foliowanie;
- diet editor: dashboard, plan menu, przepisy, posilki;
- magazyn/kompletacja: nazwy sekcji i akcji.

Kryterium akceptacji: w UI nie ma tekstow typu `OdĹ›wieĹĽ`, `MĂłj panel`, `skĹ‚adniki`, `zamĂłwieĹ„`; jesli problem wystepuje tylko w terminalu, oznaczamy go jako problem odczytu/kodowania narzedzia, nie jako blad widoku.

### 2. Usuniecie linkow do placeholderow

Nie linkowac z menu do pustych lub historycznych ekranow:

- `/diet-editor/meals` ma prowadzic do realnego katalogu `/meals`;
- `/diet-editor/recipe` nie powinien byc linkowany;
- przepisy maja prowadzic do `/diet-editor/recipes`;
- legacy fallback receptur pokazywac jako ostrzezenie, nie jako normalny stan danych.

Kryterium akceptacji: z menu nie da sie wejsc w pusty ekran edytora diet.

### 3. Gotowanie komponentow bez pelnych skanow tabel

`CookingSessionService` nie powinien uzywac `GetAllAsync()` do szukania sesji i checkboxow krokow. Przy duzym wolumenie sesji powoduje to skan calej tabeli przy kazdym kliknieciu.

Naprawa:

- dodac repozytoryjny odczyt sesji po `ProductionPlanItemId + RecipeComponentVersionId`;
- dodac repozytoryjny odczyt checka po `CookingSessionId + RecipeComponentInstructionStepId`;
- dodac repozytoryjny odczyt wszystkich checkow jednej sesji;
- wykorzystac istniejace indeksy `IX_CookingSessions_Date_Component` oraz `IX_CookingSessionStepChecks_Session_Step`.

Kryterium akceptacji: odznaczanie krokow i reload karty gotowania nie materializuja calej tabeli `CookingSessions` ani `CookingSessionStepChecks`.

### 4. Najwieksze ryzyka paginacji w pamieci

Do poprawy przed seedem 40k:

- `/packing` i `/packing/labels` - nie budowac calej tablicy kompletacji przed paginacja;
- `/loading` - nie budowac pelnego boardu tras przed wybraniem strony/szczegolow;
- Admin/BOK/HR - zastapic `GetAllAsync()` + `PagedList.Create()` repozytoryjnym SQL pagingiem;
- BOK `NewTicket` - nie ladowac wszystkich klientow do selecta, tylko server-side lookup.

Kryterium akceptacji: najwieksze listy tna dane w SQL, a nie w RAM.

## P1 - jesli zostanie czas

### WarehouseDemandService

Obecny kierunek jest dobry, ale przy duzym wolumenie trzeba ograniczyc liniowe wyszukiwanie:

- budowac slowniki snapshotow po `DietMenuPlanItemId` i `MealVariantId`;
- sprawdzanie stanow magazynowych robic batchowo;
- unikac N+1 przy FEFO preview.

### Widoki M2

- `/meals/details` nie powinien ladowac wszystkich wersji komponentow do selecta;
- listy diet, kategorii i alergenow powinny dostac search/paginacje, jesli katalog bedzie rosl;
- lookupi skladnikow, kategorii magazynowych i komponentow powinny byc server-side.

### M1/BOK

- w pozycjach zamowienia pokazac referencje M2/M3: `DietMenuPlanItemId`, `MealId`, `MealVariantId`, `MealSlot`;
- BOK i Admin przeniesc na repozytoryjne search/paging;
- ekran zamowien klienta nie powinien ladowac calej historii bez paginacji.

## Super seeder 40k

Seeder wolumenowy powinien byc osobnym profilem, a nie czescia zwyklego startu aplikacji.

Proponowany kontrakt:

```powershell
dotnet run --project src/KuchniaUCygana.Web -- seed --profile VolumeDemo --orders 40000
```

Zasady:

- profil `VolumeDemo` albo `PerformanceDemo` uruchamiany jawnie;
- parametry: liczba klientow, zamowien, dni, pozycji, ticketow, dostaw;
- dane deterministyczne, z naturalnymi kluczami typu `OrderNumber`, email klienta, zakres dat;
- katalog M2 seedowany raz, a zamowienia/dostawy/pakowanie batchami;
- inserty batchowe przez `SqlBulkCopy`, TVP albo staging tables;
- reset wolumenowy batchami po prefiksach/naturalnych kluczach, bez kasowania katalogow M2.

Sanity SQL po seedzie:

- count per tabela;
- brak osieroconych `OrderItems`;
- kazde zamowienie ma klienta, adres, payment i delivery;
- kazdy `OrderItem` ma referencje M2, jesli pochodzi z planu;
- produkcja ma `M2SnapshotJson`;
- packing ma torby, pozycje i etykiety;
- magazyn ma partie FEFO dla skladnikow i opakowan.

## Widoki do smoke testu przy duzych danych

- `/production`;
- `/production/m2-plan`;
- `/production/warehouse-demand`;
- `/production/foil-printing`;
- `/packing`;
- `/packing/labels`;
- `/loading`;
- `/meals`;
- `/ingredients`;
- `/diet-editor/recipes`;
- `/diet-editor/menu-plan`;
- Admin/BOK/HR.

## Kolejnosc wykonania

1. Naprawic polskie teksty i menu.
2. Usunac pelne skany z gotowania komponentow.
3. Zweryfikowac build i testy jednostkowe.
4. W kolejnym pakiecie zaczac od SQL paging dla kompletacji i zaladunku.
5. Dopiero po tym budowac super seeder 40k.
