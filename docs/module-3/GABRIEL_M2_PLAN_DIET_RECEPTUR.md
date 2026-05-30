# Plan dla Gabriela: M2 Diet Editor, receptury i plan 7 dni

**Cel dokumentu:** przekazać Gabrielowi pełny, decyzyjny koncept M2 gotowy do implementacji. M2 jest źródłem prawdy dla diet, posiłków, przepisów-składowych, zakładanego nutrition i datowanego planu menu. M3 nie edytuje planu, tylko go wczytuje, waliduje i operacjonalizuje w kuchni, magazynie, etykietach foliowych i pakowaniu.

## 1. Granica M2 -> M3

- M2 publikuje codzienny plan diet/menu minimum 7 dni do przodu; może publikować więcej niż 7 dni.
- M3 konsumuje tylko opublikowany plan i nie ma osobnego edytora planu w kuchni.
- M2 definiuje plan, receptury, zakładany wynik produktu, nutrition, alergeny i wymagane opakowania.
- M3 liczy operacyjne zapotrzebowanie, FEFO i wykonanie produkcji; jeśli wyliczenia M2 i M3 się różnią, operacyjnie wygrywa M3.
- M3 generuje produkcję tylko z opłaconych zamówień. Posiłek z planu M2 bez opłaconych zamówień jest widoczny w podglądzie, ale nie trafia do produkcji.
- M3 zapisuje snapshot planu produkcji, a receptury czyta po wersjach M2.

## 2. Co jest już w backbone

- Dodano `DietMenuPlans` i `DietMenuPlanItems` jako datowany plan menu.
- Dodano status planu `Draft/Published`.
- Dodano most składnika do magazynu: `Ingredient.StockItemId`, `Ingredient.WarehouseCategoryId`, `YieldFactor`, ustawienia testu temperatury.
- Dodano pola kuchenne w posiłku: instrukcja przygotowania, raw/cooked weight, wymagany test temperatury.
- `IDietDataProvider` po stronie M3 czyta plan z datami przez `Get7DayPlanAsync(startDate)` i `GetPlanForDateAsync(date)`.
- Karta gotowania M3 czyta live dane M2: zdjęcie, kategorię, alergeny, instrukcję, nutrition i wymagania temperatury.

## 3. Model M2 do wdrożenia

- `Meal` = gotowy posiłek/oferta dla klienta. Agreguje składowe, nutrition końcowe, alergeny, wymagane pojemniki i dane etykiety.
- `RecipeComponent` = reużywalny przepis-składowa, np. sos, mięso, baza, dodatek. Może występować w wielu posiłkach.
- `RecipeComponentVersion` = niemutowalna wersja produkcyjna składowej z instrukcją, yield, nutrition, składnikami, przyprawami i wymaganiami temperatury.
- `MealRecipeComponent` = relacja posiłku do wersji składowej, z ilością na porcję, kolejnością i rolą w daniu.
- `PackagingRequirement` / `MealContainerRequirement` = wymagane pojemniki/opakowania posiłku lub składowej.
- Zmiany składu, yield, nutrition, alergenów i opakowań tworzą nową wersję. Literówki, zdjęcia i instrukcje mogą zostać poprawione bez nowej wersji tylko po oznaczeniu checkboxem “zmiana nietechnologiczna”.

## 4. Plan diet 7 dni

- Widok planu dziennego i tygodniowego opiera się o `DietMenuPlans` / `DietMenuPlanItems`.
- Pozycja planu ma datę, wariant diety, slot posiłku, kolejność, posiłek, mnożnik porcji i status publikacji.
- Edytor pozwala układać dzień, kopiować dzień, wyszukiwać posiłki oraz filtrować po kategoriach i alergenach.
- Plan można edytować po publikacji tylko do północy D-3 według daty produkcji w strefie Europe/Warsaw.
- Po blokadzie D-3 zmiana wymaga override managera/admina z powodem, audytem i alertem, o ile produkcja jeszcze nie ruszyła.
- Jeśli plan został już użyty do produkcji, M2 nie może zmienić go operacyjnie dla M3.
- Kuchnia i magazyn widzą aktualny stan planu oraz alerty zmian; nie wymagamy widoku pełnej historii wersji dla kuchni/magazynu.

## 5. Posiłek, przepisy-składowe i widoki

- Widok posiłku pokazuje, jak przygotować danie oraz z jakich składowych się składa.
- Każda składowa na widoku posiłku jest linkiem do strony szczegółów przepisu-składowej.
- Przy składowej widać zużycie zasobów jednostkowe i przeliczone przez ilość produkcyjną.
- Składowa ma własne instrukcje, yield, nutrition, alergeny i wymagane testy temperatury.
- Posiłek agreguje składowe do finalnego nutrition, listy składników, alergenów i danych etykiety.
- Posiłek może składać się z wielu fizycznych pojemników, np. danie główne + osobny sos.

## 6. Zużycie zasobów vs uzysk produktu

M2 musi rozróżniać dwa zestawy informacji:

- **Zużycie zasobów:** składniki, przyprawy, ilości towarów, jednostki, yield/straty, `StockItemId`, `WarehouseCategoryId`, pudełka, pojemniki i inne opakowania posiłku.
- **Uzyskany produkt:** gotowy posiłek po obróbce, liczba porcji, cooked weight, wartości odżywcze po przygotowaniu, alergeny, shelf-life i dane wymagane na etykietę foliową.

Zasada: etykieta klienta korzysta ze składu i nutrition z planu M2, a M3 przechowuje realne stock itemy użyte przez FEFO tylko wewnętrznie w traceability.

## 7. Składniki, przyprawy i magazyn

- Składnik lub przyprawa musi mieć kategorię magazynową.
- `StockItemId` jest zalecany, ale nie zawsze obowiązkowy; jeśli brakuje `StockItemId`, M3 może dobrać aktywny towar z równoważnej kategorii magazynowej przez FEFO.
- Kategoria magazynowa użyta jako zamiennik musi być równoważna pod skład i alergeny.
- Składniki opcjonalne biorą udział w zapotrzebowaniu magazynu.
- Brak alergenów, nutrition, opakowania albo mapowania magazynowego blokuje publikację posiłku lub planu.
- Nutrition po przygotowaniu jest obowiązkowe dla produktów obrabianych. Dla produktu surowego można użyć nutrition surowego produktu, jeśli M2 oznaczy brak obróbki.

## 8. Opakowania, pudełka i QR

- M2 wskazuje wymagane pudełko/opakowanie dla posiłku, składowej lub porcji jako zasób do zużycia.
- Opakowania są osobną kategorią magazynową bez gramatury; pudełka i pojemniki są liczone ilościowo.
- Torby nie należą do planu M2. Torby liczy M3/Packing z zamówień.
- Każdy fizyczny pojemnik ma własny QR i może być zeskanowany w pakowaniu.
- Posiłek wielopojemnikowy jest wymaganym zestawem: wszystkie pojemniki muszą zostać zeskanowane, zanim posiłek trafi do torby.
- Każdy pojemnik drukuje nutrition całego posiłku oraz oznaczenie części, np. sos, dodatek, danie główne.

## 9. Wartości odżywcze, etykieta i ważność

- Nutrition na etykiecie: wartości na 100 g oraz na całą porcję/posiłek.
- Etykieta zawiera skład z M2, alergeny, wartości odżywcze, datę ważności, numer partii/traceability i QR pojemnika.
- Realna nazwa handlowa stock itemu dobranego przez FEFO nie trafia na etykietę klienta; jest widoczna wewnętrznie w audycie/traceability.
- Domyślna data ważności pochodzi z shelf-life posiłku z M2.
- Checkbox na posiłku przełącza ważność na najwcześniejszy surowiec użyty w produkcji, np. dla posiłków nisko przetworzonych.
- Po korekcie zużycia nutrition na etykiecie pozostaje z M2; klient nie dostaje osobnej informacji o korekcie ilościowej.

## 10. Kryteria akceptacji M2

- Gabriel może ułożyć i opublikować plan na minimum 7 kolejnych dni.
- Nie da się opublikować planu z brakiem alergenów, nutrition, opakowania lub mapowania magazynowego.
- Posiłek jest agregatem wersjonowanych składowych, a każda składowa ma własny widok i link z widoku posiłku.
- Plan 7 dni pozwala policzyć składniki, przyprawy oraz pudełka/pojemniki.
- Posiłek wielopojemnikowy definiuje wymagany zestaw pojemników.
- Każda technologiczna zmiana receptury tworzy nową wersję; zmiana nietechnologiczna wymaga checkboxa.
- Dane M2 wystarczają M3 do wygenerowania produkcji, zapotrzebowania, karty gotowania, etykiety i traceability.

## 11. Czego M2 nie robi

- M2 nie generuje produkcji i nie zdejmuje magazynu FEFO.
- M2 nie wykonuje operacyjnego pakowania, skanowania pudełek ani drukowania etykiet.
- M2 nie liczy toreb transportowych; to robi M3/Packing na podstawie zamówień.
- M2 nie rozwiązuje braków magazynowych operacyjnie; dostarcza tylko dane i mapowania potrzebne M3.

## 12. Testy do zaplanowania

- Publikacja planu na 7+ dni i blokada publikacji przy brakach.
- Wersjonowanie składowej po zmianie składników/yield/nutrition/opakowań.
- Poprawka tekstu/zdjęcia/instrukcji bez wersji tylko z checkboxem zmiany nietechnologicznej.
- Posiłek z wieloma pojemnikami i QR dla każdego pojemnika.
- Nutrition na 100 g i porcję oraz data ważności z shelf-life lub najwcześniejszego surowca.
- FEFO po kategorii równoważnej bez zmiany składu drukowanego dla klienta.
