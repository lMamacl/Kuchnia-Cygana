# Plan dla Gabriela: M2 Diet Editor, receptury i plan 7+ dni

## Cel

Ten dokument jest handoffem dla Gabriela i opisuje zakres M2 gotowy do dalszej implementacji w IDE. M2 jest właścicielem diet, posiłków, receptur-składowych, zakładanego nutrition, opakowań posiłku i datowanego planu menu. M3 nie edytuje planu, tylko go konsumuje, zapisuje snapshot i używa operacyjnie w kuchni, magazynie, etykietach i pakowaniu.

## Stan po branchu `Gabriel-Nuevo`

- Dodano model startowy: `RecipeComponent`, `RecipeComponentVersion`, `MealRecipeComponent`, `PackagingRequirement`, `PlanChangeAlert`.
- Dodano widoki i serwisy dla planu menu: dzień, tydzień, kopiowanie dnia, publikacja, blokada edycji po D-3.
- Dodano DTO snapshotu M2 -> M3: plan, pozycje, wersje składowych, składniki, opakowania, nutrition, shelf-life i alerty.
- Dodano pierwsze widoki składowych oraz podpięcie składowej do widoku posiłku.
- Obecny stan traktujemy jako backbone, nie jako zamknięty moduł: walidacje, alerty, audyt zmian i UX wymagają dalszego dopięcia.

## Granica M2 -> M3

- M2 publikuje plan minimum 7 dni do przodu; może publikować więcej.
- M2 definiuje plan, receptury, zakładany wynik produktu, nutrition, alergeny, shelf-life i wymagane pojemniki.
- M3 generuje produkcję tylko z opłaconych zamówień. Pozycje planu bez zamówień są tylko widoczne w podglądzie.
- M3 zapisuje snapshot planu i wersji składowych. Po utworzeniu produkcji M2 nie zmienia operacyjnie danych M3.
- Jeśli M2 i M3 różnią się w wyliczeniu zapotrzebowania, operacyjnie wygrywa M3.
- Etykieta klienta używa składu i nutrition z M2, a realne stock itemy dobrane przez FEFO zostają w traceability wewnętrznym.

## Model docelowy M2

- `Meal` to gotowy posiłek/oferta dla klienta.
- `Meal` agreguje wiele wersjonowanych składowych, np. mięso, sos, baza, dodatek.
- `RecipeComponent` to reużywalna składowa.
- `RecipeComponentVersion` to produkcyjna wersja składowej z instrukcją, yield, nutrition, składnikami, przyprawami, testami temperatury i opakowaniami.
- `MealRecipeComponent` łączy posiłek z konkretną wersją składowej, ilością na porcję, kolejnością i rolą.
- `PackagingRequirement` opisuje pudełka, pojemniki lub dodatkowe opakowania posiłku/składowej.

## Wersjonowanie

- Zmiana składników, gramatur, yield, nutrition, alergenów, shelf-life lub opakowań tworzy nową wersję składowej.
- Zmiana tekstu, zdjęcia lub instrukcji może być nietechnologiczna tylko po zaznaczeniu checkboxa i wpisaniu powodu.
- Opublikowanej wersji technologicznej nie edytujemy w miejscu.
- Widok wersji musi jasno pokazywać status: Draft, Published, Archived.

## Plan 7+ dni

- Plan dzienny ma datę, wariant diety, slot posiłku, kolejność, posiłek, mnożnik porcji i status `Draft/Published`.
- Edytor musi pozwalać układać dzień, kopiować dzień, publikować i filtrować posiłki po kategorii/alergenach.
- Edycja opublikowanego planu jest dozwolona do północy D-3 w strefie Europe/Warsaw.
- Po D-3 zmiana wymaga override managera/admina z powodem, audytem i alertem, jeśli produkcja jeszcze nie ruszyła.
- Jeśli M3 utworzyło snapshot produkcyjny, M2 nie zmienia już operacyjnie tej produkcji.

## Walidacje publikacji

Publikacja posiłku lub planu ma być blokowana, gdy brakuje:

- opublikowanej wersji składowej;
- składników albo przypraw w składowej;
- kategorii magazynowej `WarehouseCategoryId`;
- alergenów;
- nutrition na 100 g oraz na porcję lub oznaczenia produktu surowego bez obróbki;
- shelf-life albo decyzji o ważności z najwcześniejszego surowca;
- wymaganego opakowania/pojemnika;
- instrukcji przygotowania dla składowej wymagającej pracy kuchni.

`StockItemId` jest opcjonalny tylko wtedy, gdy `WarehouseCategoryId` oznacza kategorię równoważną pod skład i alergeny. Wtedy M3 może dobrać konkretny stock item przez FEFO.

## Zużycie zasobów vs uzysk produktu

M2 musi rozdzielać dwa zestawy danych:

- Zużycie zasobów: składniki, przyprawy, ilości, jednostki, yield/straty, `StockItemId`, `WarehouseCategoryId`, pudełka, pojemniki i inne opakowania posiłku.
- Uzyskany produkt: gotowy posiłek po obróbce, liczba porcji, cooked weight, nutrition po przygotowaniu, alergeny, shelf-life i dane na etykietę.

Dla produktu surowego lub nieobrabianego M2 może oznaczyć, że nutrition po przygotowaniu jest takie jak nutrition surowego produktu.

## Posiłek i widok posiłku

- Widok posiłku pokazuje opis posiłku, finalne nutrition, alergeny, opakowania i shelf-life.
- Widok posiłku pokazuje składowe dania: nazwa, rola, ilość na porcję, wersja, status kompletności.
- Każda składowa jest linkiem do strony szczegółów receptury-składowej.
- Przy składowej widoczne są zasoby jednostkowe i przeliczone przez ilość produkcyjną.
- Posiłek może mieć wiele fizycznych pojemników, np. danie główne + sos osobno.

## Opakowania i QR

- M2 wskazuje wymagane pudełka/pojemniki dla posiłku lub składowej.
- Opakowania są osobną kategorią magazynową bez gramatury; liczymy je ilościowo.
- Torby transportowe nie należą do M2. Torby liczy M3/Packing z zamówień.
- Każdy fizyczny pojemnik dostaje własny QR po stronie M3.
- Posiłek wielopojemnikowy jest wymaganym bundle: wszystkie pojemniki muszą zostać zeskanowane przed spakowaniem torby.

## Kryteria akceptacji M2

- Da się ułożyć, skopiować i opublikować plan na minimum 7 kolejnych dni.
- Publikacja blokuje braki alergenów, nutrition, opakowań, składowych i mapowań magazynowych.
- Posiłek jest agregatem wersjonowanych składowych.
- Każda składowa ma własny widok, instrukcje, yield, nutrition, składniki, przyprawy i opakowania.
- Plan 7+ dni pozwala M3 policzyć składniki, przyprawy i pudełka.
- Dane M2 wystarczają M3 do snapshotu produkcji, karty gotowania, zapotrzebowania, etykiety i traceability.

## Testy dla M2

- Publikacja kompletnego planu przechodzi.
- Publikacja planu z brakiem nutrition/alergenów/opakowań/kategorii magazynowej jest blokowana.
- Zmiana technologiczna tworzy nową wersję.
- Zmiana nietechnologiczna wymaga checkboxa i powodu.
- Widok posiłku pokazuje linki do składowych.
- Snapshot M2 zawiera stabilne `RecipeComponentVersionId`, opakowania, nutrition i shelf-life.
