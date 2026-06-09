# WIZJA MODUŁU PRODUKCJA — Specyfikacja Implementacyjna
**KuchniaUCygana · Sprint 3+4 · Wersja 3.0**
*Aktualizacja: jedna dostawa dziennie, uproszczone grupowanie, etykiety z public ID klienta*

---

## 1. DECYZJE PROJEKTOWE — KOMPLETNE (zaktualizowane)

| Temat | Decyzja |
|-------|---------|
| Liczba posiłków w diecie | Konfigurowalne: 3, 5 lub wariant (np. sam obiad) |
| Struktura posiłku | `Meal` → `MealComponent[]` — każdy komponent = osobny przepis |
| Wiele diet per klient | ✅ Jeden klient może mieć wiele zamówień/diet pod ten sam adres |
| Foliarka | PDF-only (QuestPDF), bez integracji sprzętowej |
| Dostawa dziennie | **Jedna** – wszystkie posiłki gotowe do wspólnego okna (np. do 10:00). Brak podziału na sloty. |
| Grupy gotowania | Usunięte. Pozostaje wewnętrzne pole `ProductionGroup` (1=zimne, 2=zupy, 3=dania główne, 4=desery) dla potrzeb HACCP i kategoryzacji, bez wpływu na logistykę. |
| Zgłoszenia problemów | Tylko kuchnia i logistyka (wewnętrzne). Szef kuchni może powiadomić logistykę o opóźnieniu. |
| Yield Factor | Dietetyk podaje `RawWeightGrams` + `CookedWeightGrams` per składnik |
| Shelf life | Dietetyk podaje `ShelfLifeHours` per `MealComponent` |
| Data spożycia | `ExpiryDate = CookingApprovedAt + ShelfLifeHours` |
| Waga pudełek | Symulowana – suma `CookedWeightGrams` składników + `BoxType.EmptyWeightGrams` |
| Alergeny cross-contamination | Ostrzeżenie na karcie gotowania gdy tego samego dnia są dania z alergenem i bez |
| HACCP checkpoint | Pole temperatura rdzenia (°C) wymagane przy zatwierdzeniu "ugotowane" dla mięs/ryb |
| Reprint etykiet | Tak – każdy druk/redruk tworzy nowy wpis w `BoxLabel` z polem `ReprintCount` (numer wydruku dla danego `PackingItemId`) i `ReprintReason`. |
| Notatki kucharza | Per-dzień: notatki/warianty bez zmiany oryginalnego przepisu, archiwowane z datą |
| BoxType – kto przypisuje | Dietetyk przy tworzeniu MealComponent |
| Wzór folii | Dietetyk definiuje parametry (gramatura, instrukcje), implementacja szablonu QuestPDF |
| Auto-dedukcja FEFO | Automatyczna po zatwierdzeniu "ugotowane" – bez osobnego przycisku |
| Kod QR na torbie | URL do potwierdzenia dostawy: `https://domena/delivery/verify/{sessionId}`. **Numer klienta czytelny** (plain text) obok QR. |
| Numer klienta | Wewnętrzny `Id` nie jest ujawniany. Stosujemy publiczny identyfikator `PublicId` (Guid) generowany przy tworzeniu profilu klienta. Na etykiecie torby wyświetlamy `PublicId` (skrócony, np. pierwsze 8 znaków). |
| Kolejność posiłków na etykiecie | `DietVariantMeal.SortOrder` – śniadanie = 1, drugie śniadanie = 2, obiad = 3, podwieczorek = 4, kolacja = 5 (zależnie od diety). |

---

## 2. MODEL DANYCH — NOWE ENCJE I POLA (zaktualizowane)

### 2.1 Typy dostaw – usunięte (jedna dostawa dziennie)

`DeliverySlotType` nie jest potrzebny. `Order` nie ma tego pola.

### 2.2 CookingGroup – USUNIĘTE

Nie tworzymy tabeli `CookingGroup`. Plan produkcji generowany jest jako jedna lista na dzień. Zachowujemy jedynie pole `ProductionGroup` (int) w `ProductionPlanItem` dla celów wewnętrznych (kategoryzacja dań). Wartości: 1 – dania zimne/śniadania, 2 – zupy, 3 – dania główne (mięsa/ryby), 4 – desery/sałatki.

### 2.3 MealComponent – bez zmian

```csharp
public class MealComponent : AuditableEntity<int>
{
    public int MealId { get; set; }
    public string Name { get; set; }
    public MealComponentCategory Category { get; set; }
    public int SortOrder { get; set; }               // kolejność w przepisie i na etykiecie
    public int PrepTimeMinutes { get; set; }
    public int ShelfLifeHours { get; set; }
    public decimal StorageTemperatureMinCelsius { get; set; }
    public decimal StorageTemperatureMaxCelsius { get; set; }
    public string? ReheatingInstructions { get; set; }
    public int BoxTypeId { get; set; }
    public int RecipeId { get; set; }
    public bool RequiresCoreTemperatureCheck { get; set; }
    public decimal MinCoreTemperatureCelsius { get; set; } // domyślnie 75.0
}

public enum MealComponentCategory
{
    Protein, Carb, Vegetable, Soup, Dessert, Dairy, Bread, Sauce, Other
}
```

### 2.4 Recipe — Przepis dietetyka

```csharp
public class Recipe : AuditableEntity<int>
{
    public int MealComponentId { get; set; }
    public string Instructions { get; set; }        // Markdown — kroki gotowania
    public string? PrepNotes { get; set; }          // uwagi techniczne dla kucharza
    public decimal EnergyKcalPer100g { get; set; }
    public decimal ProteinGramsPer100g { get; set; }
    public decimal FatGramsPer100g { get; set; }
    public decimal CarbsGramsPer100g { get; set; }
    public decimal FiberGramsPer100g { get; set; }
    public int CreatedByDietitianId { get; set; }
    public DateTimeOffset LastModifiedAt { get; set; }
    public IReadOnlyList<RecipeIngredient> Ingredients { get; set; }
}
```

### 2.5 RecipeIngredient — Składnik przepisu

```csharp
public class RecipeIngredient : AuditableEntity<int>
{
    public int RecipeId { get; set; }
    public int StockItemId { get; set; }            // FK do magazynu
    public decimal RawWeightGrams { get; set; }     // ile zużyjesz z magazynu (FEFO)
    public decimal CookedWeightGrams { get; set; }  // ile będzie w porcji gotowej (etykieta)
    public string? AllergenNames { get; set; }      // "Gluten, Soja" lub null
    public bool IsMajorAllergen { get; set; }       // flaga dla ostrzeżenia cross-contamination
    public int SortOrder { get; set; }
}
```

### 2.6 ChefDailyNote — Notatka kucharza per dzień

```csharp
public class ChefDailyNote : AuditableEntity<int>
{
    public int RecipeId { get; set; }
    public DateOnly ProductionDate { get; set; }    // dotyczy konkretnego dnia
    public int AuthorUserId { get; set; }
    public string Note { get; set; }                // korekta/wariant na ten dzień
    // Archiwowana — nigdy nie modyfikuje oryginalnego Recipe
}
```

### 2.7 BoxType — Typ pudełka

```csharp
public class BoxLabel : AuditableEntity<int>
{
    public int PackingItemId { get; set; }
    public DateTimeOffset PrintedAt { get; set; }
    public int PrintedByUserId { get; set; }
    public int ReprintCount { get; set; }        // 0 = oryginał, 1 = pierwszy redruk, ...
    public string? ReprintReason { get; set; }   // wymagane gdy ReprintCount > 0
    public DateOnly ExpiryDate { get; set; }
    public decimal SimulatedWeightGrams { get; set; }
    public string LabelDataJson { get; set; }    // pełny snapshot etykiety
}
```

### 2.8 BoxLabel — Wygenerowana etykieta

```csharp
public class BoxLabel : AuditableEntity<int>
{
    public int PackingItemId { get; set; }
    public DateTimeOffset PrintedAt { get; set; }
    public int PrintedByUserId { get; set; }
    public int ReprintCount { get; set; }           // 0 = oryginał
    public string? ReprintReason { get; set; }
    public DateOnly ExpiryDate { get; set; }        // CookingApprovedAt + ShelfLifeHours
    public decimal SimulatedWeightGrams { get; set; }  // mock waga
    public string LabelDataJson { get; set; }       // pełna treść etykiety (snapshot)
}
```
Każde wydrukowanie etykiety (również redruk) tworzy nowy rekord. ReprintCount to liczba porządkowa wydruku dla tego samego PackingItemId. Dzięki temu mamy pełny audyt.

### 2.9 ProductionIssue — Zgłoszenie problemu

```csharp
public class ProductionIssue : AuditableEntity<int>
{
    public int ProductionPlanId { get; set; }
    public int? CookingGroupId { get; set; }
    public int ReportedByUserId { get; set; }
    public DateTimeOffset ReportedAt { get; set; }
    public ProductionIssueType IssueType { get; set; }
    public string Description { get; set; }
    public int? EstimatedDelayMinutes { get; set; }
    public bool IsResolved { get; set; }
    public string? Resolution { get; set; }
    public int? ResolvedByUserId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}

public enum ProductionIssueType
{
    Delay, Equipment, IngredientsShortage, QualityIssue, Other
}
```

### 2.10 CookingLog — Logi analityczne

```csharp
public class CookingLog : BaseEntity<long>
{
    public int ProductionPlanItemId { get; set; }
    public CookingLogEventType EventType { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int UserId { get; set; }
    public decimal? CoreTemperatureCelsius { get; set; }  // przy Approved
    public int? ActualQuantity { get; set; }              // przy Approved
    public int? PlannedQuantity { get; set; }             // przy Approved
    public string? Notes { get; set; }
    public string? MetadataJson { get; set; }             // dodatkowe dane
}

public enum CookingLogEventType
{
    CookingStarted,
    CookingApproved,
    FefoDeducted,
    LabelPrinted,
    LabelReprinted,
    ChefNoteAdded,
    IssueReported
}
```
W CustomerProfile dodajemy pole:
csharp

public Guid PublicId { get; set; } = Guid.NewGuid();

Na etykiecie torby wyświetlamy PublicId (np. pierwsze 8 znaków: a3f5c9e1).

---

## 3. WIDOKI — SZCZEGÓŁOWE OPISY

---

### 3.1 `Production/Index` — Plan Dnia (uproszczony)

**Role:** KitchenManager, Admin
**URL:** `/production?date=2026-05-15`

**Co zawiera:**

Pasek nawigacji dat (← poprzedni | dziś | następny →) + picker daty.

Karta nagłówkowa z KPI dla całego dnia:
- Status planu (Draft / Active / InProgress / Completed)
- Liczniki: `Diety: 47 | Posiłki do ugotowania: 235 | Ugotowane: 120 | Pudełek do foliarki: 45`
- Przycisk "Generuj plan" (KitchenManager, gdy Draft)
- Przycisk "Zgłoś problem" → Issues

Alerty (jeśli są):
- Pasek ostrzeżenia z liczbą aktywnych zgłoszeń
- Alerty magazynowe (braki składników z SmartInventoryAnalyzer)

Sekcja grup gotowania — jedna karta per CookingGroup:

```
╔══════════════════════════════════════════════════════╗
║ 🌅 PRODUKCJA                                         ║
║    Gotowanie: 04:00 – 06:30 | Deadline: 07:00        ║
║    Postęp: ██████████░░ 10/12 komponentów            ║
║                                                       ║
║  ⚠️ Ostrzeżenie: Gluten + bezglutenowe w tej turze   ║
║                                                       ║
║  [Otwórz Kartę Gotowania]                            ║
╚══════════════════════════════════════════════════════╝

| Grupa | Komponent                 | Kategoria | Porcji | Status   | Akcje                |
|-------|---------------------------|-----------|--------|----------|----------------------|
| 1     | Owsianka z jagodami       | Śniadanie | 42     | ⏳ Oczek. | [Przepis][Zatwierdź] |
| 1     | Kanapka z serem           | Śniadanie | 38     | ⏳ Oczek. | ...                  |
| 2     | Zupa pomidorowa           | Zupa      | 56     | ⏳ Oczek. | ...                  |
| 3     | Kurczak teriyaki          | Danie gł. | 71     | ⏳ Oczek. | ...                  |
```

Tabela diet klientów (pod grupami, rozwijana):
- Grupowana po typie slotu
- Pokazuje: Klient | Diety (ile i jakie) | Adres | Trasa | Status kompletacji

**Uwaga multi-diet:** Klient "Jan Kowalski" może pojawić się dwa razy:
→ "Dieta Standard 1800" + "Dieta Junior 1200" — oba pod tym samym adresem.
Widok powinnien to sygnalizować.

---

### 3.2 `Production/CookingCard/{groupId}` — Karta Gotowania

**Role:** Kitchen, KitchenManager, Admin
**URL:** `/production/cooking-card/3`

**Nagłówek:**
```
Produkcja Office | 2026-05-15 | Start: 08:00 | Deadline: 11:00
Postęp: ████░░░░ 3/8 pozycji   [badge: W toku]
[Zgłoś problem z tą turą]
```

**Baner cross-contamination (jeśli dotyczy):**
```
⚠️ UWAGA ALERGENOWA — ta tura zawiera jednocześnie:
  • Dania Z glutenem: Owsianka klasyczna (×12)
  • Dania BEZ glutenu: Owsianka bezglutenowa (×5)
  Użyj oddzielnych naczyń, utensyliów i stanowisk!
```

**Tabela komponentów do ugotowania:**

Każdy wiersz = jeden MealComponent z sumą porcji ze wszystkich diet w tej turze.

Kolumny: Komponent | Kategoria | Porcji łącznie | Czas prep. | Status | Akcje

Akcje: `[Przepis]` `[Zatwierdź ↓]`

Rozwinięcie wiersza (klik na wiersz) — pokazuje tabelę składników:
```
Składnik           | Na 1 porcję (surowiec) | Łącznie (surowiec) | Stan FEFO
Pierś z kurczaka   |        215g            |       3.655 kg      | ✅ OK (2 partie)
Sos teriyaki       |         30g            |         510 g       | ⚠️ Ostatnia partia wygasa 17.05
Sezam              |          5g            |          85 g       | ✅ OK
```

**Formularz zatwierdzenia (modal inline, otwierany przyciskiem "Zatwierdź"):**

```
Komponent: Pierś z kurczaka teriyaki
Zaplanowane porcje: 17

Ugotowana ilość (szt.)*    [  17  ]
Temperatura rdzenia (°C)*  [  78  ]   ← wymagane bo RequiresCoreTemperatureCheck = true
                                          walidacja: min 75°C, max 100°C
Notatka (opcjonalna)       [_____________]

Faktyczny czas zakończenia [  06:25  ]   ← domyślnie "teraz", edytowalne

[✅ Zatwierdź ugotowanie]    [Anuluj]
```

Po zatwierdzeniu (bez przeładowania strony — HTMX):
1. Wiersz zmienia status na "Cooked" (zielony)
2. Automatyczna dedukcja FEFO (toast: "✅ Składniki zdjęte — 3.655kg kurczaka z partii B-2026-05-10")
3. Komponent trafia do kolejki Boxing
4. `CookingLog` zapisuje zdarzenie z temperaturą i ilością

**Komponent który NIE wymaga temperatury** (np. surówka):
Pole temperatury nie wyświetla się — tylko ilość.

---

### 3.3 `Production/Recipe/{componentId}` — Przepis

**Role:** Kitchen (read + notatki), KitchenManager, Dietitian (read + edycja), Admin
**URL:** `/production/recipe/42?date=2026-05-15&returnGroup=3`

**Nagłówek:**
```
[← Wróć do Karty Gotowania]
Pierś z kurczaka teriyaki     [badge: Danie Główne — Białko]
Ostatnia edycja przepisu: dietetyk Anna K., 12.05.2026
```

**Lewa kolumna — Składniki (tabela):**
```
Składnik          | Surowiec/porcję | Na talerzu (gotowe) | Alergen
Pierś z kurczaka  |   215g          |   150g               | —
Sos teriyaki      |    30g          |    30g               | ⚠️ Gluten, Soja
Sezam             |     5g          |     5g               | ⚠️ Sezam
Oliwa z oliwek    |    15g          |    10g               | —
```

Legenda pod tabelą:
> "Surowiec = ilość pobierana z magazynu przez FEFO.
>  Na talerzu = gramatura na etykiecie klienta."

Wartości odżywcze (per porcja gotowa):
```
Energia: 342 kcal | Białko: 36g | Tłuszcze: 12g | Węglowodany: 18g | Błonnik: 2g
```

Temperatura przechowywania: `2–4°C`
Instrukcja podgrzewania: `Podgrzać 3 min w mikrofali 800W lub 10 min w piekarniku 160°C`
Czas świeżości po ugotowaniu: `48 godzin`

**Prawa kolumna — Instrukcja gotowania (Markdown):**
```
## Przygotowanie

**Marynata (2h wcześniej):**
1. Pierś z kurczaka pokroić w plastry 1cm
2. Zamarynować w sosie teriyaki przez min. 2 godziny

**Gotowanie:**
3. Rozgrzać patelnię na dużym ogniu
4. Smażyć plastry 3–4 min z każdej strony
5. ⚠️ Temperatura rdzenia min. 75°C!
6. Posypać sezamem przed podaniem

**Uwaga:** Nie mieszać z naczyniami używanymi do dań bezglutenowych!
```

**Sekcja "Notatki kucharza":**
```
📋 Notatki archiwalne:
  • 12.05.2026 (M. Nowak): "Zwiększyć czas marynowania do 3h — lepszy smak"
  • 10.05.2026 (M. Nowak): "Użyć patelni grillowej zamiast płaskiej"

✏️ Twoja notatka na dziś (15.05.2026):
[Textarea: Dodaj wariant lub uwagę na dzisiaj...]
[Zapisz notatkę]
```

**Widok dla Dietitian — dodatkowe:**
Przycisk "Edytuj przepis" → osobna strona `/diet-editor/recipe/{id}/edit`
(to jest moduł DietEditor — osobna dyskusja)

---

### 3.4 `Production/Boxing` — Pakowanie do Pudełek

**Role:** Packing, PackingManager, KitchenManager, Admin
**URL:** `/production/boxing?date=2026-05-15&slot=WholeDay`

**Koncepcja kluczowa:**
Widok grupuje po **adresie dostawy** (przystanku), nie po zamówieniu.
Jeden przystanek może mieć pudełka z wielu diet tego samego klienta.

**Nagłówek z filtrami:**
```
[Trasa: Wszystkie ▼]  [Status: Wszystkie ▼]
Pudełek do spakowania: 47 | Gotowych do foliarki: 23 | Zafoliowanych: 156
```

**Widok per przystanek (accordion):**

```
📦 PRZYSTANEK #A3-05   Jan Kowalski — ul. Zielona 5/2, Warszawa
   Trasa A3 | Slot: 08:00–08:30 | 2 diety | 8 pudełek łącznie

   ZAMÓWIENIE #247 — Dieta Standard 1800 kcal
   ┌─────────────────────────────┬──────────┬───────┬───────────┬──────────┐
   │ Komponent                   │ Na tal.  │ Box   │ Status    │ Akcja    │
   ├─────────────────────────────┼──────────┼───────┼───────────┼──────────┤
   │ 🍗 Pierś z kurczaka teryaki │ 150g     │ STD   │ ✅ Gotowe │          │
   │ 🍚 Ryż jaśminowy            │  80g     │ STD   │ ✅ Gotowe │          │
   │ 🥗 Surówka z marchewki      │  50g     │ STD   │ ⏳ Czeka  │          │
   └─────────────────────────────┴──────────┴───────┴───────────┴──────────┘
   Waga symulowana: 280g + 45g (pudełko) = 325g
   [Wszystko gotowe → Wyślij do foliarki]  ← aktywny gdy wszystko = ✅

   ZAMÓWIENIE #251 — Dieta Junior 1200 kcal
   ┌─────────────────────────────┬──────────┬───────┬───────────┬──────────┐
   │ 🍗 Kurczak w sosie łagodnym │ 100g     │ JR    │ ✅ Gotowe │          │
   │ 🍚 Kasza manna              │  60g     │ JR    │ ✅ Gotowe │          │
   └─────────────────────────────┴──────────┴───────┴───────────┴──────────┘
   Waga symulowana: 160g + 35g (pudełko Junior) = 195g
   [Wyślij do foliarki]
```

Przycisk "Wyślij do foliarki" per zamówienie (nie per przystanek) — bo każde zamówienie ma własne etykiety.

---

### 3.5 `Production/FoilPrint/{packingItemId}` — Etykieta

**Role:** Packing, PackingManager, Admin
**URL:** `/production/foil-print/1547`

**Podgląd etykiety (rendered HTML 1:1 proporcje):**

```
┌──────────────────────────────────────────────────────────┐
│ DIETA STANDARD 1800 kcal    Posiłek 3 z 5 — Obiad       │
│ Partia: #2026-05-15-STD-247  [█████ KOD QR █████]        │
│ Zamówienie: #247                                          │
├──────────────────────────────────────────────────────────┤
│ KURCZAK TERIYAKI Z RYŻEM I SURÓWKĄ                       │
├──────────────────────────────────────────────────────────┤
│ SKŁAD: Pierś z kurczaka (150g), Ryż jaśminowy (80g),     │
│ Marchewka (50g), Sos teriyaki [GLUTEN, SOJA], Sezam      │
│ [SEZAM], Oliwa z oliwek                                  │
├──────────────────────────────────────────────────────────┤
│ Wartości odżywcze na 100g:                               │
│ Energia: 142 kcal | Białko: 18g | Tłuszcze: 4g           │
│ Węglowodany: 9g   | Błonnik: 1g                          │
│ Masa netto: 280g                                          │
├──────────────────────────────────────────────────────────┤
│ ⚠️ ALERGENY: GLUTEN, SOJA, SEZAM                         │
├──────────────────────────────────────────────────────────┤
│ Spożyć do: 16.05.2026     Przechowywać: 2–4°C           │
├──────────────────────────────────────────────────────────┤
│ 🔥 Podgrzać 3 min mikrofala 800W / 10 min piekarnik 160°C│
│    Nie zamrażać ponownie                                 │
├──────────────────────────────────────────────────────────┤
│ Wyprodukowano: KuchniaUCygana Sp. z o.o.                 │
│ kuchniacygana.pl  ·  kontakt@kuchniacygana.pl            │
└──────────────────────────────────────────────────────────┘
```

**Akcje:**
- `[🖨️ Drukuj etykietę]` → QuestPDF → druk lub pobierz PDF
- `[🔄 Drukuj ponownie]` (pojawia się po pierwszym druku) → modal z obowiązkowym powodem:
  ```
  Powód redruku:
  ○ Etykieta uszkodzona/nieczytelna
  ○ Błąd danych (wymaga korekty ← disabled, dane z bazy)
  ○ Zgubiona
  ○ Inny: [_______________]
  [Potwierdź redruk]
  ```
  → `BoxLabel.ReprintCount++`, zapis do `CookingLog` z `EventType.LabelReprinted`

- `[← Wróć do Boxing]`

**Kalkulacja `ExpiryDate` — widoczna dla kucharza:**
```
Ugotowano: 15.05.2026 06:25 | Ważność: 48h | Spożyć do: 17.05.2026 06:25
→ Na etykiecie: "Spożyć do: 17.05.2026"
```
Dodatkowo po pierwszym wydruku pojawia się przycisk "Drukuj ponownie". Po kliknięciu modal z obowiązkowym powodem i potwierdzeniem – tworzy nowy rekord BoxLabel z ReprintCount++.
---

### 3.6 `Production/Issues` — Zgłoszenia Problemów

**Role:** Kitchen, KitchenManager (zapis + podgląd), Admin, Logistics (read-only)
**URL:** `/production/issues?date=2026-05-15`

**Pasek filtrów:** Data | Typ | Status (Aktywne/Rozwiązane/Wszystkie)

**Tabela zgłoszeń:**

```
# | Tura          | Typ           | Opis                    | Opóźn. | Status     | Zgłosił      | Kiedy
--|---------------|---------------|-------------------------|--------|------------|--------------|--------
1 | Całodniowa    | ⚠️ Opóźnienie | Piekarnik się nie grzeje| +30min | 🔴 Aktywne | M. Nowak     | 05:12
2 | Office        | 🔧 Sprzęt     | Foliarka zacięta         | +15min | 🔴 Aktywne | K. Wiśniew.  | 09:45
3 | —             | ✅ Jakość     | Surówka za kwaśna        | —      | ✅ Rozwiąz.| M. Nowak     | 06:30
```

**Formularz nowego zgłoszenia** (`/production/issue-new`):
- Tura gotowania (dropdown z CookingGroup dla dnia)
- Typ problemu (dropdown)
- Opis (textarea, wymagany)
- Szacowane opóźnienie w minutach (opcjonalne)
- `[Zgłoś problem]` → toast "Logistyka powiadomiona"

**Logistyka widzi ten widok w read-only** → Tylko dla kuchni i logistyki (autoryzacja). Formularz zgłoszenia zawiera pole "Szacowane opóźnienie (minuty)" – po zapisie system może wysłać powiadomienie do logistyki (np. przez webhook lub email).

---

## 4. PRZEPŁYW DANYCH — PEŁNY CYKL

```
[GenerateDailyPlan] – tworzy ProductionPlan z wszystkimi pozycjami (bez podziału na grupy gotowania)
     │
     ▼
[Production/Index] — KitchenManager widzi grupy i postęp
     │
     ├─ [Issues] — zgłaszanie problemów (kuchnia ↔ logistyka)
     │
     ▼
[CookingCard/{groupId}] — kucharz gotuje per tura
     │  ├─ [Recipe/{id}] — otwiera przepis + dodaje notatkę
     │  └─ zatwierdza: ilość + temp. rdzenia (HACCP) + czas
     │      → auto FEFO deduction
     │      → ExpiryDate = now + ShelfLifeHours
     │      → CookingLog zapis
     ▼
[Boxing] — pracownik składa zamówienia do pudełek
     │ 
     │  Waga symulowana
     │  → status pudełka: ReadyForFoiling
     ▼
[FoilPrint/{id}] — podgląd etykiety + druk
     │  QuestPDF snapshot zapisany do BoxLabel.LabelDataJson
     │  ExpiryDate z CookingLog
     │  → status pudełka: Labeled
     ▼
[Packing/Session] — pudełko trafia do sesji pakowania logistycznego
     (Moduł Packing — osobna sesja projektowa)
```

---

## 5. LOGI ANALITYCZNE — Co zbieramy i do czego

| Zdarzenie | Dane | Zastosowanie |
|-----------|------|--------------|
| `CookingStarted` | UserId, czas startu | Czas przygotowania per komponent |
| `CookingApproved` | Ilość plan/actual, temp. rdzenia, czas | HACCP, analiza wydajności, food waste |
| `FefoDeducted` | Jakie partie, ile zdjęto | Traceability, zgodność z FEFO |
| `LabelPrinted` | BoxId, czas, UserId | Audit trail, produkcja pudełek |
| `LabelReprinted` | Powód, kto, kiedy | Audit HACCP, jakość druku |
| `ChefNoteAdded` | RecipeId, data, treść | Optymalizacja przepisów |
| `IssueReported` | Typ, opis, opóźnienie | KPI operacyjne, trend usterek |

**Przyszłe dashboardy (nie teraz — zostawiamy miejsce w danych):**
- Średni czas gotowania per komponent (plan vs actual)
- Waste ratio (plan – actual) per danie per miesiąc
- Temperatura rdzenia trend (czy kuchnia zawsze trafia w normę)
- Najczęstsze typy usterek per dzień tygodnia

---

## 6. ZMIANY W ISTNIEJĄCYM KODZIE

| Plik/Komponent | Zmiana | Priorytet |
|---|---|---|
| `ProductionPlanItem` | Dodać `CookingGroupId` FK | 🔴 Krytyczne |
| `CustomerProfile` | Dodać `PublicId` (Guid, not null, default NewGuid()) | 🔴 Krytyczne |
| `CookingCard.cshtml` | Dodać pole temperatura rdzenia do zatwierdzenia | 🔴 Krytyczne |
| `CookingCard.cshtml` | Dodać baner cross-contamination | 🔴 Krytyczne |
| `CookingCard.cshtml` | Usunąć ręczny przycisk FEFO — auto po zatwierdzeniu | 🔴 Krytyczne |
| `CookingCards.cshtml` | Refaktor: grupowanie po CookingGroup zamiast flat listy | 🟡 Wysokie |
| `CookingCard.cshtml` | Usunąć `GetMealImage()` / `GetMealCategory()` — zastąpić `MealComponent.Category` | 🟡 Wysokie |
| `FoodCostCalculator` | Przeliczać przez `RawWeightGrams` (nie `WeightPerServing`) | 🔴 Krytyczne |
| `Boxing.cshtml` | Dodać wyświetlanie PublicId obok zamówienia | 🟡 Średnie |
| `BoxLabel` | Zmiana modelu: każdy druk to nowy rekord | 🔴 Krytyczne |
| Migracje | Nowe tabele: `MealComponents`, `Recipes`, `RecipeIngredients`, `BoxTypes`, `ChefDailyNotes`, `BoxLabels`, `ProductionIssues`, `CookingLogs` | 🔴 Krytyczne |

---


## 7. PYTANIA OTWARTE – ROZSTRZYGNIĘTE

    Grupy gotowania – kto je tworzy? → Nie tworzymy. Jedna dostawa dziennie. Pole ProductionGroup tylko do wewnętrznej kategoryzacji.

    Nazwa klienta na etykiecie? → Nie. Używamy PublicId (czytelny, krótki identyfikator).

    Kolejność posiłków na etykiecie torby → DietVariantMeal.SortOrder.

    Redruk etykiet → Każdy druk to nowy BoxLabel, ReprintCount numeruje wydruki.

    Problemy w produkcji → Tylko kuchnia i logistyka. Szef kuchni może zgłosić opóźnienie – logistyka dostaje powiadomienie.

