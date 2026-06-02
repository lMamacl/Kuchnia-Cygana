# Braki M2/M3: widoki, dane i kontrakt dla Gabriela

## Cel dokumentu

Ten dokument rozbija brakujące elementy M2/M3 na niezależne sekcje, żeby Gabriel mógł analizować i dowozić je etapami. Nie chodzi o jedną dużą paczkę zmian, tylko o czytelne moduły: przepisy-składowe, dania, składniki, plan diet, kuchnia M3, wydajność i kryteria akceptacji.

Najważniejsze doprecyzowanie terminologii:

- **Przepis** = reużywalna składowa technologiczna, np. ziemniaki w mundurkach, sos, mięso, baza ryżowa.
- **Danie / posiłek** = agregat przepisów-składowych, który trafia do klienta jako oferta/menu.
- **Wariant dania** = wersja wagowa, alergiczna, high-protein albo inna odmiana posiłku. Wariant wybiera konkretne wersje składowych, mnożniki, gramaturę, nutrition i opakowania.
- **Nutrition i gramatura finalna** = system może agregować je ze składowych, ale dietetyk może ręcznie nadpisać finalny wynik z powodem override.

## 1. Przepisy-składowe M2

### Obecny stan

Backbone już istnieje: `RecipeComponent`, `RecipeComponentVersion`, `RecipeComponentIngredient`, `PackagingRequirement` oraz widoki szczegółów wersji. Wersja przepisu-składowej ma instrukcje, yield, raw/cooked weight, nutrition per 100 g, składniki i opakowania.

To jest dobry fundament, ale jeszcze bardziej techniczny niż produktowy. Brakuje danych i UI, które pozwalają dietetykowi oraz kuchni traktować przepis jako gotową, wersjonowaną instrukcję produkcyjną.

### Braki danych

- Kategoria przepisu-składowej, np. baza, mięso, sos, dodatek, warzywa, deser.
- Opcjonalne zdjęcie przepisu-składowej.
- Czas przygotowania przepisu-składowej.
- Alergeny widoczne jako wynik składników oraz możliwość jawnego zatwierdzenia/override, jeśli agregacja nie wystarcza.
- Instrukcje rozbite na sekcje i kroki, zamiast jednego pola tekstowego.
- Oznaczenie kroków wymagających kontroli, np. temperatura rdzenia mięsa.
- Dane dla widoku M3: kroki możliwe do odhaczania, status kroku, kto i kiedy odhaczył.
- Czytelna różnica między zmianą technologiczną a nietechnologiczną.

### Wymagany widok szczegółów przepisu-składowej

Widok powinien mieć sekcje w tej kolejności:

1. Nagłówek: nazwa, opis opcjonalny, zdjęcie opcjonalne, kategoria, status, aktualna wersja, przycisk „utwórz nową wersję”.
2. Parametry produkcyjne: czas przygotowania, yield, raw weight, cooked weight, shelf-life, czy ważność z najwcześniejszego surowca.
3. Wartości odżywcze: per 100 g, per porcja/uzysk, źródło wartości, informacja czy wynik jest automatyczny czy ręczny override.
4. Alergeny: zaciągnięte ze składników, widoczne ostrzeżenia i możliwość ręcznego zatwierdzenia.
5. Składniki i zapotrzebowanie: składnik, ilość, jednostka, yield/strata, `StockItemId`, `WarehouseCategoryId`, optional/required, notatka.
6. Opakowania przepisu-składowej: pojemniki, folie albo inne zasoby przypisane do składowej.
7. Instrukcja przygotowania: sekcje i kroki w kolejności wykonania.
8. Historia wersji: wersje draft/published/archived, data publikacji, autor, powód zmiany.
9. Tryb M3: podgląd kroków z checkboxami, bez edycji danych M2.

### Lista przepisów-składowych

Lista musi być przygotowana pod duże zbiory danych:

- server-side search po nazwie i opisie;
- paginacja;
- filtry kategorii, statusu, alergenów, braków publikacji;
- sortowanie po nazwie, dacie aktualizacji, statusie i kategorii;
- akcje: dodaj, edytuj, dezaktywuj, utwórz nową wersję, otwórz aktualną wersję.

Nie ładować całego katalogu składników ani wszystkich przepisów do jednego selecta, jeśli lista może rosnąć.

### Kryteria akceptacji

- Dietetyk może utworzyć przepis-składową i opublikować wersję tylko wtedy, gdy ma wymagane składniki, mapowanie magazynowe, nutrition, alergeny i instrukcje.
- Przepis-składowa pokazuje alergeny wynikające ze składników.
- Nowa zmiana technologiczna tworzy nową wersję.
- Zmiana nietechnologiczna wymaga checkboxa i powodu.
- M3 może otworzyć wersję przepisu-składowej w trybie tylko do odczytu z checklistą kroków.

## 2. Dania / posiłki M2

### Obecny stan

Widok dania potrafi pokazać przypięte przepisy-składowe i link do wersji przepisu. To już potwierdza kierunek: danie jest agregatem składowych. Obecny widok jest jednak nadal bardziej techniczny niż dietetyczny i nie pokazuje pełnego wyniku posiłku.

### Braki danych

- Warianty dania: wagowy, alergiczny, high-protein, low-carb albo inny wariant technologiczny.
- Dla każdego wariantu: konkretne wersje przepisów-składowych, mnożniki, gramatura finalna i opakowania.
- Zbiorcze składniki dania z rozbiciem na przepisy-składowe.
- Zbiorcze alergeny dania z rozbiciem na źródło alergenu.
- Zbiorcze nutrition dania: per 100 g i per porcję.
- Informacja, czy nutrition jest agregowane automatycznie, czy ręcznie nadpisane przez dietetyka.
- Podgląd pudełek/pojemników wymaganych dla dania.
- Status kompletności dania i wariantów.

### Wymagany widok dania

Widok dania powinien mieć sekcje:

1. Nagłówek: nazwa dania, opis, zdjęcie główne, kategoria, status, czas przygotowania sumaryczny.
2. Warianty dania: lista wariantów wagowych/specjalnych, aktywny wariant, status kompletności.
3. Przepisy-składowe: każda składowa jako karta lub wiersz z nazwą, zdjęciem opcjonalnym, rolą, ilością, wersją i linkiem do szczegółów przepisu.
4. Składniki zbiorcze: suma składników całego dania oraz rozbicie, z której składowej pochodzi dana ilość.
5. Alergeny zbiorcze: lista alergenów i ich źródła.
6. Wartości odżywcze i gramatura: agregacja składowych, finalny cooked weight, wartości per 100 g i per porcja, override z powodem.
7. Opakowania: pudełka/pojemniki wymagane dla wariantu dania.
8. Walidacja: braki blokujące publikację.

### Lista dań

Lista dań musi mieć:

- server-side search;
- paginację;
- filtry kategorii, statusu, diety, alergenów, braków walidacji;
- sortowanie po nazwie, kategorii, statusie, aktualizacji;
- akcje: dodaj, edytuj, dezaktywuj/usuń, publikuj, wersjonuj wariant, otwórz widok szczegółowy.

### Kryteria akceptacji

- Danie może składać się z wielu przepisów-składowych.
- Każdy przepis-składowa w widoku dania jest linkiem do własnego widoku szczegółów.
- Danie pokazuje skumulowane składniki, alergeny, nutrition i gramaturę z rozbiciem na składowe.
- Wariant dania może używać innej wersji składowej albo innego mnożnika.
- Nie można opublikować dania ani wariantu, jeśli brakuje składowych, nutrition, alergenów, opakowań albo mapowania magazynowego.

## 3. Składniki i opakowania M2

### Obecny stan

Składnik ma nazwę, jednostkę, koszt, notatki, `StockItemId`, `WarehouseCategoryId`, yield i wymogi temperatury. Formularz pokazuje jednak tylko podstawowe pola, więc część danych istnieje w modelu, ale nie jest wygodnie zarządzana z UI.

### Braki danych i UI

- Wartości odżywcze składnika.
- Alergeny składnika, nawet jeśli wydaje się to oczywiste, np. orzeszek ma alergen orzeszki.
- Skład opisowy produktu, np. jogurt: mleko, kultury bakterii.
- Zdjęcie opcjonalne.
- Opis opcjonalny.
- Kategorie spożywcze: owoce, warzywa, ryby, mięso, nabiał, przyprawy itd.
- Wyraźne oddzielenie zasobów spożywczych od opakowań/pudełek.
- Widoczne mapowanie do magazynu: `StockItemId`, `WarehouseCategoryId`, status mapowania.
- Pełne pola temperatury i yield w formularzu.

### Wymagany widok składnika

Widok składnika powinien zawierać:

1. Dane bazowe: nazwa, opis, zdjęcie, status aktywności.
2. Typ zasobu: składnik spożywczy, przyprawa, pudełko/pojemnik, inne opakowanie.
3. Kategorie: kategoria spożywcza i kategoria magazynowa.
4. Jednostki i gramatura: jednostka bazowa, przeliczniki, yield/straty.
5. Nutrition: wartości odżywcze składnika.
6. Alergeny: lista alergenów przypiętych do składnika.
7. Skład opisowy: tekstowa lista składników produktu z etykiety dostawcy.
8. Mapowanie magazynowe: `StockItemId`, `WarehouseCategoryId`, informacja czy FEFO może działać po kategorii.
9. Bezpieczeństwo: blokada usunięcia, jeśli składnik jest używany w opublikowanej wersji przepisu.

### Lista składników

Lista musi mieć:

- server-side search;
- paginację;
- filtry kategorii spożywczej, magazynowej, alergenu, typu zasobu i aktywności;
- sortowanie po nazwie, kategorii, statusie mapowania i aktualizacji;
- akcje: dodaj, edytuj, dezaktywuj, usuń jeśli bezpieczne.

### Kryteria akceptacji

- Składnik może mieć nutrition, alergeny, skład opisowy, zdjęcie i mapowanie magazynowe.
- Składnik bez `WarehouseCategoryId` blokuje publikację przepisu, jeśli jest użyty w składowej.
- `StockItemId` może być pusty tylko wtedy, gdy kategoria magazynowa jest zatwierdzona jako równoważna dla FEFO.
- Usunięcie składnika używanego w opublikowanym przepisie jest blokowane.
- Opakowania/pudełka da się filtrować oddzielnie od składników spożywczych.

## 4. Edytor planu diet M2

### Obecny stan

Plan dzienny i tygodniowy istnieje. Jest draft/publish, kopiowanie dnia, walidacja i blokada edycji po D-3. To dobry backbone, ale widok planowania nie pokazuje jeszcze pełnego obrazu dietetyka.

### Braki

- Wygodny widok 7+ dni z rozbiciem na daty, diety, warianty i sloty posiłków.
- Widoczne sumy gramatury dla diety/wariantu/dnia.
- Widoczna różnica między zwykłym mnożnikiem porcji a specjalnym wariantem dania.
- Walidacja kompletności całego dnia przed publikacją.
- Czytelne komunikaty braków: brak wariantu, brak opakowania, brak nutrition, brak alergenu, brak mapowania magazynowego.
- Search posiłków w planie po stronie serwera.
- Filtry posiłków po kategorii, alergenach, statusie i wariantach.
- Widok zmian po publikacji i alertów dla M3.

### Wymagany widok planu 7+ dni

Widok planu powinien pokazywać:

1. Zakres dat 7+ dni z możliwością przesunięcia tygodnia.
2. Każdy dzień jako osobny blok.
3. W każdym dniu: dieta, wariant diety, slot posiłku, danie/wariant dania, mnożnik, gramatura, status kompletności.
4. Walidację dnia i walidację całego zakresu.
5. Akcje: utwórz dzień, kopiuj dzień, edytuj pozycję, usuń pozycję, publikuj.
6. Blokadę publikacji, jeśli którakolwiek pozycja jest niekompletna.
7. Informację, czy plan był już odebrany przez M3 albo czy istnieje alert po zmianie.

### Kryteria akceptacji

- Dietetyk może ułożyć plan minimum 7 dni do przodu.
- Plan pokazuje daty, diety, warianty, sloty, dania i warianty dań.
- Publikacja jest możliwa tylko, gdy cały dzień jest kompletny.
- Plan odróżnia mnożnik porcji od specjalnego wariantu dania.
- Po zmianie opublikowanego planu powstaje alert dla M3.

## 5. Kuchnia M3: karta gotowania i etykieta

### Obecny stan

M3 zapisuje snapshot M2 w pozycji produkcyjnej i karta gotowania potrafi czytać komponenty, składniki oraz opakowania ze snapshotu. To daje bazę do widoku kuchni, ale jeszcze nie pełny proces operacyjny.

### Braki karty gotowania

- Pełna sesja gotowania `RecipeComponentVersion + ProductionDate`.
- Checkboxy/progres kroków przepisu-składowej.
- Progres składowych, np. ziemniaki gotowe, sos gotowy, mięso gotowe.
- Zapotrzebowanie jednostkowe i łączne dla każdej składowej.
- Możliwość zapisania faktycznego zużycia zasobów.
- Manager approval dla odchyleń, niedoborów i zmiany liczby pojemników/etykiet.
- Drugi krok potwierdzenia przy zmianie liczby pudełek/etykiet.
- Podgląd etykiety przed drukiem.

### Wymagany widok karty gotowania M3

Karta gotowania powinna mieć:

1. Nagłówek: data produkcji, danie, wariant, liczba sztuk do przygotowania, status.
2. Składowe: każda składowa z nazwą, rolą, wersją, instrukcją i statusem.
3. Checklisty: kroki przepisu-składowej do odhaczania w trybie M3.
4. Zapotrzebowanie: tabela per składowa z ilością jednostkową i łączną.
5. Opakowania: wymagane pojemniki i liczba pojemników.
6. Kontrole: temperatura rdzenia, braki magazynowe, odchylenia.
7. Progres: procent/stan przygotowania składowych i całego dania.
8. Podgląd etykiety: skład z M2, alergeny, nutrition per 100 g i porcja, gramatura, data ważności, numer partii, QR.

### Kryteria akceptacji

- Karta gotowania używa snapshotu, a nie live danych M2.
- Kucharz widzi liczbę dań do przygotowania.
- Kucharz widzi zapotrzebowanie jednostkowe i łączne per przepis-składowa.
- Kucharz może oznaczać składowe/kroki jako wykonane.
- Etykieta nie drukuje się przed akceptacją gotowania.
- Zmiana ilości pojemników wymaga drugiego potwierdzenia i wizualizacji skutku.

## 6. Wydajność i bezpieczeństwo danych

### Wymagania wspólne

- Listy przepisów, dań, składników i planów muszą używać server-side search i paginacji.
- Nie ładować dużych katalogów do pełnych selectów, jeśli liczba rekordów może rosnąć.
- Filtry muszą działać po stronie serwera.
- Widoki powinny używać stronicowania domyślnie, np. 25 lub 50 rekordów.
- Usuwanie powinno być bezpieczne: preferowana dezaktywacja, jeśli rekord jest używany historycznie.
- Publikowane wersje technologiczne nie mogą być edytowane w miejscu.
- Upload zdjęć musi mieć whitelist MIME/rozszerzeń, limit rozmiaru, losową nazwę pliku, web-relative URL i usuwanie pliku po usunięciu zdjęcia.
- Każda zmiana wpływająca na produkcję musi mieć autora, datę i powód.

### Kryteria akceptacji

- Search działa szybko dla dużych katalogów.
- Lista nie pobiera całej bazy do pamięci.
- Dezaktywacja nie psuje historycznych snapshotów.
- Brak wymaganych danych blokuje publikację, a nie dopiero produkcję.
- Komunikaty walidacyjne są zrozumiałe dla dietetyka i kuchni.

## 7. Proponowany podział pracy dla Gabriela

### Pakiet A: przepisy-składowe

Zakres: kategorie, zdjęcia, czas przygotowania, kroki instrukcji, alergeny ze składników, wersjonowanie i lista z filtrami.

Efekt: przepis-składowa jest pełnym, wersjonowanym obiektem technologicznym, gotowym do użycia w daniu i w M3.

### Pakiet B: składniki i opakowania

Zakres: nutrition składników, alergeny, skład opisowy, zdjęcie, kategorie, pełne mapowanie magazynowe i rozdzielenie składników od opakowań.

Efekt: składniki i pudełka są spójnymi zasobami do planowania, walidacji i FEFO.

### Pakiet C: dania i warianty dań

Zakres: widok dania jako agregatu składowych, warianty wagowe/specjalne, podsumowania składników, alergenów, nutrition, gramatury i opakowań.

Efekt: dietetyk widzi gotowy posiłek tak, jak będzie rozumiany przez kuchnię i etykietę.

### Pakiet D: plan 7+ dni

Zakres: widok tygodniowy/dzienny z dietami, wariantami, slotami, daniami, gramaturą, walidacją kompletności i alertami.

Efekt: M2 publikuje plan gotowy do snapshotu M3.

### Pakiet E: kontrakt dla M3

Zakres: snapshot zawiera wersje składowych, wariant dania, składniki, alergeny, nutrition, gramaturę, opakowania, shelf-life, alerty i identyfikatory do traceability.

Efekt: M3 może budować kartę gotowania, zapotrzebowanie magazynowe, FEFO i etykietę bez ponownego pytania M2 o live dane.

## 8. Minimalny Definition of Done

Moduł M2 można uznać za kompatybilny z założeniami M3 dopiero wtedy, gdy:

- przepis-składowa ma komplet danych technologicznych i wersjonowanie;
- danie jest agregatem przepisów-składowych;
- wariant dania wybiera konkretne wersje składowych, mnożniki, gramaturę i opakowania;
- składniki mają nutrition, alergeny i mapowanie magazynowe;
- plan 7+ dni publikuje tylko kompletne pozycje;
- snapshot M2 zawiera dane potrzebne do gotowania, FEFO, etykiety i pakowania;
- M3 może wyświetlić kartę gotowania bez czytania live danych M2;
- etykieta korzysta z danych M2, a traceability realnych stock itemów zostaje po stronie M3.
