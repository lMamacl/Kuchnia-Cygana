# Gabriel M2: krytyczne braki pod plan produkcji M3

## Cel

Ten dokument zawiera tylko zakres potrzebny Gabrielowi po stronie M2, zeby M3 moglo poprawnie tworzyc plan produkcji, karte gotowania, zapotrzebowanie magazynowe, foliowanie i etykiety.

To nie jest pelny opis kuchni M3. M3 nie edytuje diet ani receptur. M3 potrzebuje z M2 stabilnego, opublikowanego snapshotu: **co gotujemy, z jakich składowych, w jakiej gramaturze, z jakimi skladnikami, nutrition, alergenami i opakowaniami**.

## 1. Najkrytyczniejszy brak: wynik posilku / wariantu

Obecnie mamy fundament danych:

- `Meal` ma reczne `RawWeightGrams` i `CookedWeightGrams`.
- `MealVariant` ma pola gramatury i nutrition, ale nie jest jeszcze pelnym wariantem technologicznym w UI.
- `RecipeComponentVersion` ma skladniki, yield, raw/cooked weight i nutrition.
- M3 potrafi policzyc zapotrzebowanie skladnikow ze snapshotu.

Brakuje jednak jednego centralnego wyniku M2:

> Dla tego posilku albo wariantu dania: jaki jest finalny produkt, ile ma gramow, jakie ma nutrition, alergeny, opakowania i z jakich wersji przepisow-składowych powstaje?

### Do zrobienia przez Gabriela

Dodac logike typu `MealVariantResultCalculator`, ktora liczy albo sklada wynik z:

- wybranych `RecipeComponentVersion`,
- `QuantityPerServing` kazdej składowej,
- raw/cooked weight składowych,
- nutrition składowych,
- alergenow skladnikow,
- opakowan posilku i składowych.

Wynik musi zawierac:

- finalna gramature porcji,
- nutrition per 100 g,
- nutrition per porcja,
- alergeny,
- skladniki zbiorcze,
- opakowania wymagane do produkcji,
- status kompletności,
- informacje, czy dane sa agregowane automatycznie, czy recznie nadpisane.

Dietetyk moze zrobic reczny override, ale musi podac powod.

## 2. Krytyczny widok: posilek jako agregat składowych

Obecny widok posilku pokazuje przypiete składowe, ale jeszcze nie jest pelnym widokiem dietetycznym. Dla M3 i planu produkcji ten widok jest kluczowy, bo posilek jest jednostka, ktora trafia do planu.

### Widok musi pokazac

- nazwe posilku,
- opis,
- zdjecie glowne, jesli istnieje,
- status publikacji,
- kategorie,
- czas przygotowania,
- warianty dania,
- finalna gramature,
- nutrition per 100 g i per porcja,
- alergeny,
- shelf-life,
- opakowania,
- status kompletności.

### Sekcja składowych

Kazda składowa musi byc widoczna jako osobny wiersz/karta:

- nazwa składowej,
- rola w daniu, np. mieso, sos, baza, dodatek,
- wersja przepisu,
- status wersji,
- ilosc na porcje,
- link do szczegolow przepisu-składowej,
- informacja, czy jest opcjonalna.

### Sekcja podsumowania

Widok musi pokazac:

- zbiorcza liste skladnikow calego dania,
- rozbicie skladnikow per składowa,
- laczne alergeny,
- laczne nutrition,
- laczna gramature,
- wymagane pudełka/pojemniki.

Bez tego M3 nie ma pewnosci, czy snapshot opisuje realny produkt, czy tylko czesciowo wypelniony posilek.

## 3. Krytyczny widok: wariant dania

Wariant dania jest potrzebny, bo nasze diety beda mialy warianty wagowe, alergiczne i specjalne, np. high-protein.

### Wariant dania musi miec

- nazwe,
- typ wariantu, np. Standard, Weight, Allergy, HighProtein, LowCarb,
- status: Draft/Published/Archived,
- konkretne wersje przepisow-składowych,
- mnozniki albo ilosci składowych,
- finalna gramature,
- nutrition per 100 g i per porcja,
- alergeny,
- opakowania,
- powod override, jesli wynik nie jest automatyczny.

### Najwazniejsza zasada

Jesli plan M2 wskazuje `MealVariantId`, snapshot dla M3 musi uzyc danych tego wariantu, a nie bazowego `Meal`.

To dotyczy szczegolnie:

- gramatury,
- nutrition,
- skladnikow,
- alergenow,
- opakowan,
- listy wersji składowych.

## 4. Krytyczny widok: przepis-składowa

Przepis-składowa to reuzywalna czesc posilku, np. ziemniaki, sos, mieso, baza ryzowa.

### Widok wersji przepisu musi pokazac

- nazwe składowej,
- numer wersji,
- status wersji,
- instrukcje przygotowania,
- sekcje i kroki instrukcji,
- czas przygotowania,
- skladniki,
- gramature skladnikow,
- yield/straty,
- raw weight,
- cooked weight,
- nutrition per 100 g,
- alergeny wynikajace ze skladnikow,
- opakowania składowej,
- shelf-life,
- wymagane testy, np. temperatura dla mies.

### Braki do domkniecia

- Pokazac automatyczne porownanie:
  - suma gramatur skladnikow,
  - raw weight wersji,
  - cooked weight wersji,
  - yield/strata,
  - nutrition wpisane recznie,
  - nutrition wyliczone.
- Pokazac, czy nutrition i gramatura sa agregowane, czy reczne.
- Wymagac powodu przy recznym override.
- Lista składowych musi miec server-side search, paginacje i filtry.

## 5. Krytyczny widok: skladniki i opakowania

Skladniki sa podstawa zapotrzebowania magazynowego. Opakowania sa podstawa foliowania i pakowania.

### Widok skladnika musi miec

- nazwe,
- typ zasobu: skladnik, przyprawa, pudełko/pojemnik, opakowanie,
- kategorie zywnosciowa,
- kategorie magazynowa,
- opcjonalny `StockItemId`,
- nutrition skladnika,
- alergeny skladnika,
- sklad opisowy produktu,
- jednostke,
- yield/straty,
- wymog testu temperatury,
- zdjecie opcjonalne,
- status aktywnosci.

### Lista skladnikow musi miec

- server-side search,
- paginacje,
- filtr typu zasobu,
- filtr kategorii,
- filtr alergenow,
- filtr brakow mapowania magazynowego,
- osobny filtr opakowan/pudelek.

Nie mozemy ladowac calego katalogu skladnikow do jednego selecta, bo to nie przejdzie przy duzych danych.

## 6. Krytyczny widok: plan menu 7+ dni

Plan M2 jest bezposrednim wejsciem do planu produkcji M3.

### Plan musi pokazac

- zakres 7+ dni,
- dzien,
- status dnia: Missing/Draft/Published,
- diete,
- wariant diety,
- slot posilku,
- posilek,
- wariant dania,
- mnoznik porcji,
- finalna gramature,
- status kompletności pozycji,
- braki blokujace publikacje.

### Publikacja musi blokowac

- brak składowych,
- brak opublikowanych wersji składowych,
- brak nutrition,
- brak alergenow,
- brak opakowan,
- brak `WarehouseCategoryId` dla skladnikow,
- brak finalnej gramatury,
- brak kompletnego wariantu dania, jesli pozycja planu uzywa wariantu.

### Ważne dla M3

Plan nie moze byc tylko lista `MealId`. Musi jednoznacznie wskazac:

- `DietMenuPlanItemId`,
- `MealId`,
- `MealVariantId`, jesli dotyczy,
- `DietVariantId`,
- slot,
- sort order,
- `ServingMultiplier`,
- finalny wynik wariantu/posilku.

## 7. Snapshot wymagany dla planu produkcji M3

M3 potrzebuje snapshotu, ktory jest stabilny i nie zmienia sie po utworzeniu planu produkcji.

### Snapshot pozycji planu musi zawierac

- `DietMenuPlanId`,
- `DietMenuPlanItemId`,
- `MealId`,
- `MealVariantId`,
- `DietVariantId`,
- `MealSlot`,
- `SortOrder`,
- `ServingMultiplier`,
- nazwe posilku,
- nazwe wariantu dania,
- finalna gramature,
- nutrition per 100 g,
- nutrition per porcja,
- alergeny,
- shelf-life,
- flage najwczesniejszego surowca,
- liste `RecipeComponentVersionId`,
- skladniki kazdej składowej,
- opakowania posilku i składowych,
- instrukcje/kroki składowych,
- status kompletności,
- ostrzezenia walidacyjne.

### Dlaczego to jest krytyczne

M3 tworzy plan produkcji z opłaconych zamowien. Jesli snapshot nie rozroznia wariantu dania albo nie zawiera finalnego wyniku, M3 moze:

- polaczyc dwa rozne warianty w jedna pozycje produkcyjna,
- policzyc zla gramature,
- wydrukowac zla etykiete,
- zuzyc zle opakowania,
- pokazac kuchni niepelna karte gotowania.

## 8. Co M3 moze robic dopiero po tych zmianach

Po domknieciu powyzszych elementow M3 bedzie moglo poprawnie:

- grupowac produkcje po `DietMenuPlanItemId` albo `MealVariantId`,
- pokazac karte gotowania dla wariantu dania,
- policzyc zapotrzebowanie skladnikow i opakowan,
- pokazac nutrition `100 g + porcja`,
- drukowac etykiete z finalnym wynikiem M2,
- rozroznic wariant wagowy, alergiczny i high-protein,
- utrzymac traceability po wersjach składowych.

## 9. Czego Gabriel nie musi robic

Gabriel nie musi implementowac:

- FEFO,
- zdejmowania magazynu,
- sesji gotowania M3,
- fizycznego drukowania etykiet,
- pakowania toreb,
- redruku etykiet,
- manager approval w kuchni,
- traceability realnych stock itemow po FEFO.

Te elementy sa po stronie M3. Gabriel ma dostarczyc dane, widoki i snapshot M2, ktore pozwola M3 wykonac te operacje.

## 10. Priorytety dla Gabriela

1. **Wynik posilku/wariantu**
   - Agregacja składowych, gramatury, nutrition, alergenow i opakowan.

2. **Widok posilku jako agregatu**
   - Składowe jako linki, zbiorcze skladniki, nutrition, alergeny, gramatura i kompletność.

3. **Widok wariantu dania**
   - Warianty wagowe/specjalne z wlasnymi składowymi, mnoznikami i wynikiem.

4. **Widok przepisu-składowej**
   - Pelna karta technologiczna z instrukcjami, skladnikami, yield, nutrition i opakowaniami.

5. **Widok skladnikow**
   - Search/paginacja, alergeny, nutrition, kategorie, mapowanie magazynowe, opakowania.

6. **Plan menu 7+ dni**
   - Wybor wariantu dania, kompletna walidacja, sumy gramatur i publikacja.

7. **Snapshot M2 dla M3**
   - Stabilny payload pozycji planu z finalnym wynikiem posilku/wariantu.

## 11. Kryteria akceptacji

- Dietetyk moze zbudowac posilek z wielu przepisow-składowych.
- Kazda składowa jest linkiem do wlasnej wersji przepisu.
- Wariant dania moze miec inne składowe lub inne mnozniki niz wariant bazowy.
- System pokazuje finalna gramature i nutrition posilku/wariantu.
- System pokazuje, czy wynik jest automatyczny, czy recznie nadpisany.
- Override wymaga powodu.
- Plan 7+ dni pozwala wybrac wariant dania.
- Publikacja planu blokuje braki krytyczne.
- Snapshot M2 zawiera `MealVariantId`, finalna gramature, nutrition, alergeny, opakowania i `RecipeComponentVersionId`.
- M3 moze utworzyc plan produkcji bez doczytywania live danych M2.
