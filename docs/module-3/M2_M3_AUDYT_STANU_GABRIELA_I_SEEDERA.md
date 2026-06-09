# Audyt M2/M3 po integracji Gabriela i plan naprawy seedera

## Cel

Ten dokument opisuje aktualny stan po integracji zmian Gabriela w kontekście planów:

- `PLAN_SPRINTOW_M3.md`,
- `GABRIEL_M2_PLAN_DIET_RECEPTUR.md`,
- `GABRIEL_M2_BRAKI_WIDOKI_I_KONTRAKTY.md`,
- `GABRIEL_M2_KRYTYCZNE_WIDOKI_PLAN_PRODUKCJI.md`.

Nie jest to kolejny ogólny plan M2/M3. To audyt operacyjny: co działa, co nie działa, skąd biorą się blokery publikacji i co trzeba poprawić, żeby demo dane oraz środowisko developerskie były zgodne z nowym kontraktem M2 -> M3.

## Co działa

### Migracje i schemat

W aktualnym repozytorium poprawny stan migracji dla tego zakresu to:

- `306_AddNutritionToRecipeComponentVersions` - dodaje nutrition do `RecipeComponentVersions`;
- `508_AddM2CatalogArchitecture` - dodaje architekturę katalogu M2: instrukcje, warianty posiłków, pola zatwierdzania alergenów, cooking sessions;
- `510_AddBaseColumnsToModule5Tables` - migracja M5, już zajęta;
- `511_AddMealVariantPackagingRequirements` - migracja Gabriela dla opakowań wariantu posiłku.

Migracja opakowań Gabriela jest celowo pod numerem `511`, ponieważ `508` i `510` były już zajęte.

### Backbone M2

Działa fundament zgodny z planem Gabriela:

- `MealVariantResultCalculator` liczy wynik posiłku/wariantu ze składowych;
- istnieją `RecipeComponent`, `RecipeComponentVersion`, `MealRecipeComponent`, `MealVariant`, `MealVariantComponent`;
- wariant posiłku może mieć własne składowe, nutrition, alergeny i opakowania;
- `DietMenuPlanItems` mają `MealVariantId`;
- snapshot M2 przekazuje do M3 finalną gramaturę, nutrition, składniki, opakowania i wersje składowych;
- listy składników i przepisów mają search/paginację po stronie serwera;
- karta gotowania M3 potrafi czytać dane ze snapshotu M2;
- produkcja M3 tworzy pozycje z opłaconych zamówień i opublikowanego planu M2.

### M3

Po stronie M3 działa wersja operacyjna backbone:

- `ProductionPlanItems` przechowują `M2SnapshotJson` i `M2SnapshotHash`;
- karta gotowania pokazuje komponenty, składniki, opakowania, alergeny i nutrition ze snapshotu;
- FEFO produkcji jest idempotentne;
- opakowania mogą być zdejmowane po akceptacji gotowania;
- foliowanie i pakowanie mają twarde blokady wokół etykiet.

## Co nie działa

### Seeder nadal produkuje dane legacy

Najważniejszy problem nie leży w założeniach Gabriela, tylko w danych startowych.

Obecny seeder tworzy:

- `Meals`,
- legacy `Recipes`,
- `NutritionFacts` dla posiłków,
- `MealAllergens`,
- `MealVariants`,
- `PackagingRequirements` na poziomie posiłku.

Ale w typowym demo stanie może zostawić:

- `RecipeComponents = 0`,
- `RecipeComponentVersions = 0`,
- `MealRecipeComponents = 0`,
- `MealVariantComponents = 0`.

Wtedy M2/M3 formalnie mają opublikowany plan i posiłki, ale nowy kontrakt nie ma realnej składowej technologicznej. System spada do legacy fallbacku `Recipes`, a walidacja publikacji pokazuje blokery.

### Plan wygląda na opublikowany, ale produkt nie jest kompletny

`DietMenuPlans` i `DietMenuPlanItems` mogą mieć status `Published`, lecz pozycje planu nie mają kompletnego wyniku technologicznego, jeśli nie ma opublikowanych `RecipeComponentVersion` oraz powiązań `MealVariantComponents`.

To tworzy fałszywe poczucie gotowości: dietetyk widzi plan, ale kuchnia nie ma pełnej karty technologicznej.

### Seeder potrafi wyjść za wcześnie

`SeedMenuAsync` wcześniej kończył działanie, gdy tabela `Categories` miała jakiekolwiek rekordy. To jest zbyt szeroki warunek. Baza może mieć kategorie, ale nie mieć danych M2 potrzebnych po integracji Gabriela.

Seeder powinien działać idempotentnie per rekord/tabela i uzupełniać brakujące części, zamiast zakładać, że obecność kategorii oznacza kompletny katalog M2.

## Błędy środowiskowe

### `Invalid column name CaloriesPer100g... AllergensApproved`

Błąd:

```text
Invalid column name 'CaloriesPer100g'.
Invalid column name 'ProteinPer100g'.
Invalid column name 'CarbohydratesPer100g'.
Invalid column name 'FatPer100g'.
Invalid column name 'FiberPer100g'.
Invalid column name 'AllergensApproved'.
```

oznacza, że aplikacja działa na bazie, która nie ma pełnych migracji M2.

Minimalny sanity check:

```sql
SELECT [Version]
FROM [VersionInfo]
WHERE [Version] IN (306, 508, 510, 511)
ORDER BY [Version];
```

oraz:

```sql
SELECT [name]
FROM sys.columns
WHERE object_id = OBJECT_ID('dbo.RecipeComponentVersions')
  AND [name] IN (
      'CaloriesPer100g',
      'ProteinPer100g',
      'CarbohydratesPer100g',
      'FatPer100g',
      'FiberPer100g',
      'NutritionSource',
      'AllergensApproved'
  );
```

Jeśli tych migracji albo kolumn nie ma, problemem jest rozjechana baza, pominięty startup migracji, stary wolumen Dockera albo uruchomienie aplikacji z konfiguracją pomijającą migracje.

## Blokery publikacji i ich źródło

Przykład z UI:

```text
Jajecznica z szczypiorkiem na masle (legacy Recipes): wersja skladowej nie jest opublikowana
Jajecznica z szczypiorkiem na masle (legacy Recipes): brak finalnej gramatury skladowej
Jajecznica z szczypiorkiem na masle (legacy Recipes): brak nutrition skladowej
Jajecznica z szczypiorkiem na masle (legacy Recipes): alergeny skladowej nie sa zatwierdzone
Jajecznica z szczypiorkiem na masle (legacy Recipes): brak opakowan skladowej
brak finalnej gramatury porcji
brak pelnego nutrition do agregacji
alergeny wyniku nie sa zatwierdzone
```

### Interpretacja

- `legacy Recipes` - posiłek nie ma nowej, opublikowanej `RecipeComponentVersion`; system używa starego fallbacku.
- `wersja skladowej nie jest opublikowana` - brak `RecipeComponentVersion.Status = Published`.
- `brak finalnej gramatury skladowej` - brak `RawWeightGrams` lub `CookedWeightGrams` w składowej.
- `brak nutrition skladowej` - brak nutrition na wersji składowej.
- `alergeny skladowej nie sa zatwierdzone` - `RecipeComponentVersions.AllergensApproved = 0`.
- `brak opakowan skladowej` - brak `PackagingRequirements` dla `RecipeComponentVersion`.
- `brak finalnej gramatury porcji` - wynik wariantu nie ma finalnego `CookedWeightGrams`.
- `brak pelnego nutrition do agregacji` - wariant nie może policzyć nutrition z kompletnych składowych.
- `alergeny wyniku nie sa zatwierdzone` - wynik posiłku/wariantu nie ma zatwierdzonych alergenów.

## Propozycje napraw

### 1. Migracje i start bazy

- Utrzymać migrację Gabriela jako `511`.
- Dodać sanity check w dokumentacji i testach integracyjnych: `VersionInfo` musi zawierać `306`, `508`, `510`, `511`.
- Przy błędzie `Invalid column name` zalecać pełny restart migracji lub odtworzenie wolumenu dev, jeśli baza była migrowana przed integracją Gabriela.

### 2. Seeder M2

- Zmienić `SeedMenuAsync`, żeby nie kończył działania tylko przez istniejące `Categories`.
- Dodać `EnsureDemoRecipeComponentsAsync`.
- Dla każdego demo posiłku utworzyć jedną opublikowaną składową technologiczną, która zastępuje legacy fallback.
- Składowa demo ma mieć:
  - `Status = Published`;
  - `RawWeightGrams` i `CookedWeightGrams`;
  - komplet nutrition na 100 g;
  - `NutritionSource = Manual`;
  - `AllergensApproved = 1`;
  - shelf-life;
  - sekcję instrukcji i kroki;
  - składniki z `WarehouseCategoryId`;
  - opakowanie w `PackagingRequirements`.
- Dodać `MealRecipeComponents` i `MealVariantComponents` dla domyślnych wariantów.
- Uzupełnić `DietMenuPlanItems.MealVariantId` dla wszystkich planów 7 dni, nie tylko dla planu dzisiejszego.

### 3. Testy

Dodać lub rozszerzyć test integracyjny seedera:

- `RecipeComponentVersions > 0`;
- `MealVariantComponents > 0`;
- każda demo wersja ma `Status = Published`;
- każda demo wersja ma nutrition, gramaturę, zatwierdzone alergeny, kroki instrukcji i opakowania;
- wszystkie aktywne demo pozycje planu mają `MealVariantId`;
- produkcja demo tworzy snapshot bez legacy fallbacku.

## Priorytet napraw

1. Naprawić seeder, żeby tworzył kompletny model `RecipeComponentVersion`.
2. Dodać powiązania `MealVariantComponents`.
3. Uzupełnić `MealVariantId` w planach 7 dni.
4. Dodać test integracyjny sanity dla migracji i danych M2.
5. Dopiero potem rozbudowywać kuchnię M3 o pełne sesje gotowania i widok "Plan z M2".

## Wniosek

Zmiany Gabriela są wystarczające, żeby rozpocząć kuchnię M3 na faktycznych planach M2, ale nasze demo dane nie są jeszcze z nimi kompatybilne. Główny problem to seeder i stan bazy, nie sam kierunek modelu M2.

Po naprawie seedera środowisko developerskie powinno pozwalać przejść ścieżkę:

```text
opublikowany plan M2 -> opłacone zamówienie -> snapshot produkcji M3 -> karta gotowania -> FEFO -> akceptacja gotowania -> foliowanie -> etykieta -> pakowanie
```
