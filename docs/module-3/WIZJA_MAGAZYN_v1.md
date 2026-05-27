# WIZJA MODUŁU MAGAZYN — Specyfikacja Implementacyjna
**KuchniaUCygana · Sprint 3+4 · Wersja 1.1**
*Aktualizacja v1.1: decyzje na pytania otwarte, poprawki z code review (DeepSeek), endpointy HTMX, testy, migracje*

---

## 1. DECYZJE PROJEKTOWE — KOMPLETNE

| Temat | Decyzja |
|-------|---------| 
| Dostawa do magazynu | Jedna lub wiele dziennie — brak limitu. Każda dostawa = osobna partia (`Batch`). |
| FEFO (First Expiry First Out) | Automatyczne — najstarsza data ważności ma priorytet. Algorytm w `FefoService`. Partie bez `ExpiryDate` (np. przyprawy) sortowane na koniec: `ORDER BY CASE WHEN ExpiryDate IS NULL THEN 1 ELSE 0 END, ExpiryDate ASC, ReceivedDate ASC`. |
| Alerty magazynowe | SmartInventoryAnalyzer generuje: `BelowMinimum` (stan < minimum), `ExpiringSoon` (≤3 dni), `NoStock` (stan = 0), `Expired` (data ważności < dziś). Nazwy zgodne z istniejącym kodem. |
| Kolorowanie wierszy | ✅ TAK — trzy stany: `table-danger` (stan = 0 LUB partia expired), `table-warning` (stan < minimum LUB expiring ≤3 dni), domyślny (OK). Uproszczone z 4 do 3 stanów (wg rekomendacji). |
| Szczegóły partii | ✅ TAK — widok `BatchDetails` z pełną historią transakcji i traceability. |
| Ręczne wydanie (`Issue`) | ✅ TAK — przechodzi przez `FefoService.DeductByFefoAsync` (lub wskazaną partię). Rejestruje `InventoryTransaction` z `TransactionType = ManualIssue`. |
| Raport FEFO | ✅ TAK — zestawienie aktywnych partii. Partie bez `ExpiryDate` wyświetlane na końcu z ozn. "Brak daty". |
| Prognoza zużycia | ❌ NIE — plan produkcji i FEFO wystarczają. |
| Bulk receive | ❌ NIE — zbyt skomplikowane, jedna dostawa na raz. |
| Rezerwacje partii | ❌ NIE — zbędne przy automatycznym FEFO. |
| Edycja daty ważności | ✅ TAK — z obowiązkowym powodem i pełnym logiem zmiany. |
| Eksport HACCP do CSV/PDF | ✅ TAK — PDF przez QuestPDF, CSV przez `CsvHelper`. Format A4 z mniejszą czcionką (8pt tabela). |
| Wykresy w HACCP | ✅ TAK — Chart.js wykres temperatur, implementujemy teraz (nie później). |
| Powiadomienia o alertach | Przez wbudowany system powiadomień w panelu użytkownika. Brak emaili. |
| Jednostki miary | Konfigurowane per składnik. Tabela `UnitOfMeasure` z symbolem (kg, szt, l, op). |
| Lead Time | Per składnik — `StockItem.LeadTimeDays` informuje SmartAnalyzer kiedy zamawiać. |
| Kategorie składników | Pole `Category` (string) w `StockItem` — używane do filtrowania i grupowania. |
| Filtrowanie stanów | Wyszukiwarka + filtr kategorii (HTMX). Filtry i paginacja w querystring — serwer zwraca partial tabeli. Paginacja: **25 wierszy na stronę**. Sortowanie: klikalne nagłówki, parametry `sort`/`dir` w querystring. |
| HACCP — temperatura | Temperatura rdzenia per urządzenie/lokalizacja. Zakres walidacji: -50°C do +50°C. |
| Uprawnienia | Warehouse, WarehouseManager, Admin — pełny dostęp. Kitchen — read-only stany. |
| UserId w serwisach | Serwis otrzymuje `ICurrentUserService` (interfejs w Domain, impl. w Infrastructure z `IHttpContextAccessor`) z wyciągniętym `UserId`. |
| Historia transakcji | Osobna strona `/warehouse/transaction-history` oprócz widoku w `BatchDetails`. |
| Dokumenty drukowane | Format A4, mniejsza czcionka (8pt), specjalny CSS `@media print`. Bez logo/pieczątki (projekt akademicki). |

---

## 2. MODEL DANYCH — NOWE ENCJE I POLA

### 2.1 Istniejące encje — ZMIANA: dodanie `StockItemId` do `InventoryTransaction`

> **Uwaga z code review:** W obecnym kodzie `InventoryTransaction` nie ma pola `StockItemId` — tylko `BatchId`. Dla wydajności zapytań w `BatchDetails` i `TransactionHistory` dodajemy denormalizację.

- **StockItem** — `AuditableEntity<int>`: Name, Category, DefaultUnitOfMeasureId, MinimumLevel, LeadTimeDays
- **Batch** — `AuditableEntity<int>`: StockItemId FK, SupplierBatchNumber, CurrentQuantity, ExpiryDate, ReceivedDate, IsDepleted
- **InventoryTransaction** — `AuditableEntity<long>`: BatchId FK, **StockItemId FK (NOWE — denormalizacja)**, TransactionType, Quantity, Notes, PerformedByUserId
- **TemperatureLog** — `AuditableEntity<long>`: DeviceNameOrLocation, RecordedTemperatureCelsius, Remarks
- **UnitOfMeasure** — `BaseEntity<int>`: Name, Symbol
- **InventoryAdjustment** — `AuditableEntity<int>`: StockItemId FK, QuantityBefore, QuantityAfter, Reason, AdjustedBy

### 2.2 Nowe wartości w enum `InventoryTransactionType`

```csharp
public enum InventoryTransactionType
{
    // Istniejące (nazwy z kodu):
    Receipt,              // Przyjęcie dostawy (w kodzie: Receipt, nie Received)
    ProductionIssue,      // Auto-dedukcja FEFO po zatwierdzeniu gotowania
    Waste,                // Zgłoszenie odpadu (w kodzie: Waste, nie WasteDisposal)
    Adjustment,           // Korekta inwentaryzacyjna
    
    // NOWE:
    ManualIssue,          // Ręczne wydanie (test kuchni, awaria)
    ExpiryDateChanged     // Zmiana daty ważności partii (audit log)
}
```

### 2.3 BatchExpiryChangeLog — Log zmian daty ważności

```csharp
public class BatchExpiryChangeLog : BaseEntity<long>
{
    public int BatchId { get; set; }
    public DateTimeOffset? OldExpiryDate { get; set; }
    public DateTimeOffset? NewExpiryDate { get; set; }
    public string Reason { get; set; }          // obowiązkowy
    public int ChangedByUserId { get; set; }    // z ICurrentUserService
    public DateTimeOffset ChangedAt { get; set; }
}
```

### 2.4 ManualIssueRequest — DTO wydania ręcznego

```csharp
public class ManualIssueRequest
{
    public int StockItemId { get; set; }
    public decimal Quantity { get; set; }
    public int? BatchId { get; set; }           // opcjonalny — jeśli pusty, FEFO
    public string Reason { get; set; }          // obowiązkowy, dropdown + "Inny"
    public string IssuedTo { get; set; }        // dropdown: "Kuchnia główna", "Kuchnia testowa", "Awaria lodówki"... + "Inny"
}
```

### 2.5 EditBatchExpiryRequest — DTO edycji daty ważności

```csharp
public class EditBatchExpiryRequest
{
    public int BatchId { get; set; }
    public DateTimeOffset NewExpiryDate { get; set; }
    public string Reason { get; set; }          // obowiązkowy
}
```

### 2.6 Migracje FluentMigrator

| Nr migracji | Opis | Zakres |
|---|---|---|
| `307` | Dodanie kolumny `StockItemId` do `InventoryTransactions` + backfill z `Batches` | M3 (300-399) |
| `308` | Utworzenie tabeli `BatchExpiryChangeLogs` | M3 |
| `309` | Dodanie wartości `ManualIssue`, `ExpiryDateChanged` do komentarza enum (enum in-code, nie w DB) | — (brak DDL) |

---

## 3. WIDOKI — SZCZEGÓŁOWE OPISY

---

### 3.1 `Warehouse/Index` — Pulpit Magazynu (modyfikacja istniejącego)

**Role:** Warehouse, WarehouseManager, Admin
**URL:** `/warehouse`

**Endpointy HTMX:**

| Akcja | Endpoint | Trigger | Swap |
|---|---|---|---|
| Filtrowanie tabeli | `hx-get="/warehouse/stock-table?search={q}&category={cat}&page={p}&sort={col}&dir={asc|desc}"` | `input[name=search]` → `hx-trigger="input changed delay:400ms"`, `select[name=category]` → `hx-trigger="change"` | `#stock-table-container` |
| Paginacja | `hx-get="/warehouse/stock-table?page=2&..."` | klik na numer strony | `#stock-table-container` |
| Sortowanie | `hx-get="/warehouse/stock-table?sort=name&dir=asc&..."` | klik na nagłówek kolumny | `#stock-table-container` |

**Karta nagłówkowa z KPI:**
```
╔══════════════════════════════════════════════════════════════════════╗
║  Stan ogólny    │  Pozycji     │  Krytyczne alerty │  Wydajność    ║
║  ✅ Aktywny     │  24          │  🔴 3             │  FEFO 98.4%   ║
║  Lodówki OK     │  w bazie     │  Wymaga reakcji   │  Brak strat   ║
╚══════════════════════════════════════════════════════════════════════╝
```

**Filtry:**
```
[🔍 Szukaj składnika...] [Kategoria: Wszystkie ▼]
```
Przy zmianie filtrów: paginacja resetowana do strony 1, sortowanie zachowane.

**Przyciski w nagłówku:**
```
[📥 Przyjmij dostawę]  [📤 Wydanie ręczne]  [📊 Raport FEFO]  [📋 Historia transakcji]
```

**Tabela — kolorowanie 3-stanowe:**
```
| ID | Składnik          | Kat.     | Stan       | Próg Min. | Termin ważn. | Status       | Operacje                          |
|----|-------------------|----------|------------|-----------|--------------|--------------|-----------------------------------|
| 1  | Pierś z kurczaka  | Białko   | 5.20 kg    | 10.00 kg  | 28.05.2026   | ⚠️ Niski stan | [Przyjmij][Odpad][Wydaj][Szczeg.] |  ← table-warning
| 2  | Mąka pszenna      | Węglowod.| 0.00 kg    | 5.00 kg   | —            | 🔴 Brak      | [Przyjmij][Odpad][Wydaj][Szczeg.] |  ← table-danger
| 3  | Masło             | Nabiał   | 12.50 kg   | 3.00 kg   | 15.06.2026   | ✅ OK        | [Przyjmij][Odpad][Wydaj][Szczeg.] |  ← domyślny
| 4  | Mleko             | Nabiał   | 8.00 l     | 5.00 l    | 27.05.2026   | 🔴 Wygasłe   | [Przyjmij][Odpad][Wydaj][Szczeg.] |  ← table-danger (expired!)
```

**Logika kolorowania (3 stany):**
```
if (stock == 0 || earliestExpiryDate < DateTime.Today)
    → table-danger
else if (stock < minimumLevel || earliestExpiryDate <= DateTime.Today.AddDays(3))
    → table-warning
else
    → (domyślny)
```

**Paginacja pod tabelą:**
```
Pokazano 1–25 z 48 składników   [« Poprz.] [1] [2] [Nast. »]
```

---

### 3.2 `Warehouse/Receive` — Przyjęcie Dostawy (modyfikacja)

**Role:** Warehouse, WarehouseManager, Admin
**URL:** `/warehouse/receive`

**Endpointy HTMX:**

| Akcja | Endpoint | Trigger | Swap |
|---|---|---|---|
| Auto-uzupełnianie po wyborze składnika | `hx-get="/warehouse/stock-info/{stockItemId}"` | `select[name=StockItemId]` → `hx-trigger="change"` | `#stock-info-panel` |

**Zmiany:**

1. **Przycisk "Przyjmij i dodaj kolejną"** — obok "Zapisz Przyjęcie":
   ```
   [Anuluj]                    [Przyjmij i dodaj kolejną] [Zapisz Przyjęcie]
   ```
   POST z parametrem `returnToForm=true` → redirect na pusty formularz z toastem.

2. **Panel info po wyborze składnika (HTMX):**
   ```
   ℹ️ Aktualny stan: 5.20 kg | Ostatnia dostawa: 10.05.2026 | Sugerowany nr: B-2026-05-27-001
   ```

3. **Opcjonalne pole temperatury transportu:**
   ```
   Temperatura transportu (°C)  [___]  ← opcjonalne, logowane do audytu
   ```

---

### 3.3 `Warehouse/Waste` — Rejestracja Odpadu (modyfikacja)

**Role:** Warehouse, WarehouseManager, Admin
**URL:** `/warehouse/waste`

**Endpointy HTMX:**

| Akcja | Endpoint | Trigger | Swap |
|---|---|---|---|
| Załadowanie partii po wyborze składnika | `hx-get="/warehouse/batches-for-stock/{stockItemId}"` | `select[name=StockItemId]` → `hx-trigger="change"` | `#batch-selector` |
| Preview odpisu | `hx-get="/warehouse/waste-preview?stockItemId={id}&quantity={q}&batchId={b}"` | `input[name=Quantity]` → `hx-trigger="input changed delay:500ms"` | `#waste-preview` |

**Dropdown partii:**
```
Partia (opcjonalnie):
[── Automatyczny FEFO ──]
[B-2026-05-08 | 2.10 kg | wygasa 26.05]   ← FEFO wybierze tę
[B-2026-05-10 | 3.10 kg | wygasa 28.05]
```

**Preview rozliczenia:**
```
⚠️ Odpis 3.00 kg z partii B-2026-05-08 (FEFO) — po odpisie zostanie: 0.00 kg
Partia zostanie oznaczona jako wyczerpana (IsDepleted = true).
```

---

### 3.4 `Warehouse/Inventory` — Inwentaryzacja (modyfikacja)

**Role:** WarehouseManager, Admin
**URL:** `/warehouse/inventory`

**Zmiany:**

1. **Filtrowanie po kategorii (HTMX):**
   `hx-get="/warehouse/inventory-table?category={cat}&search={q}"` → `#inventory-table-container`

2. **Wyszukiwarka** — Alpine.js client-side filtr (do 100 wierszy), server-side jeśli więcej.

3. **Kolorowanie różnic (JavaScript, on-input):**
   - Wartość < system: wiersz `bg-warning-lt`
   - Wartość > system: wiersz `bg-info-lt`
   - Wartość = system: brak

4. **Podsumowanie przed zatwierdzeniem (Alpine.js computed):**
   ```
   📊 Podsumowanie: Zgodne: 18 | Niedobory: 3 | Nadwyżki: 2
   Największe odchylenie: Pierś z kurczaka — system: 5.20 kg, fizyczny: 3.80 kg (−1.40 kg)
   ```

---

### 3.5 `Warehouse/BatchDetails/{stockItemId}` — Szczegóły Partii (NOWY)

**Role:** Warehouse, WarehouseManager, Admin
**URL:** `/warehouse/batch-details/1`

**Endpointy HTMX:**

| Akcja | Endpoint | Trigger | Swap |
|---|---|---|---|
| Lazy-load historii transakcji | `hx-get="/warehouse/transactions/{stockItemId}?from={date}&to={date}&page={p}"` | `revealed` (scroll) lub klik "Pokaż więcej" | `#transaction-history` |
| Modal edycji daty ważności | `hx-get="/warehouse/edit-batch-expiry/{batchId}"` | klik `[Edytuj datę]` | `#modal-container` (modal) |
| Zapis edycji daty | `hx-post="/warehouse/edit-batch-expiry/{batchId}"` | submit formularza w modalu | `#batch-table` + zamknięcie modalu |

**Nagłówek:**
```
[← Wróć do Pulpitu]
Pierś z kurczaka     [badge: Białko]
Aktualny stan: 5.20 kg | Minimum: 10.00 kg | Jednostka: kg
Status: ⚠️ Niski stan
```

**Sekcja 1 — Aktywne partie (tabela):**
```
| # | Nr partii dostawcy    | Przyjęto    | Data ważności | Ilość poz.  | Status     | Akcje              |
|---|-----------------------|-------------|---------------|-------------|------------|--------------------| 
| 1 | B-2026-05-08          | 08.05.2026  | 26.05.2026    | 2.10 kg     | ⏰ Wygasa  | [Edytuj datę]      |
| 2 | B-2026-05-10          | 10.05.2026  | 28.05.2026    | 3.10 kg     | ✅ Aktywna | [Edytuj datę]      |
```

**Sekcja 2 — Historia transakcji (z filtrem dat):**
```
📋 Historia transakcji:
[Od: __/__/____]  [Do: __/__/____]  [Zastosuj filtr]

| Data       | Typ                | Ilość     | Partia       | Użytkownik  | Notatki               |
|------------|--------------------|-----------|--------------|-------------|----------------------|
| 15.05 09:12| ✅ Przyjęcie       | +5.00 kg  | B-2026-05-10 | Magazynier  | Stan opakowań OK     |
| 15.05 07:30| 🔴 Dedukcja FEFO   | −3.20 kg  | B-2026-05-08 | System      | Plan prod. #42       |
```
> Uwaga: zapytanie po `StockItemId` (nowa kolumna) zamiast JOIN przez `Batch`.

**Sekcja 3 — Log zmian dat ważności:**
```
📅 Zmiany dat ważności:
| Data zmiany | Partia       | Stara data  | Nowa data   | Powód              | Kto          |
|-------------|--------------|-------------|-------------|--------------------|--------------| 
| 13.05 14:00 | B-2026-05-06 | 20.05.2026  | 18.05.2026  | Kontrola jakości   | Kier. magaz. |
```

---

### 3.6 `Warehouse/Issue` — Ręczne Wydanie Składnika (NOWY)

**Role:** Warehouse, WarehouseManager, Admin
**URL:** `/warehouse/issue`

**Endpointy HTMX:**

| Akcja | Endpoint | Trigger | Swap |
|---|---|---|---|
| Info o składniku | `hx-get="/warehouse/stock-info/{stockItemId}"` | `change` na select | `#stock-info-panel` |
| Lista partii | `hx-get="/warehouse/batches-for-stock/{stockItemId}"` | `change` na select | `#batch-selector` |

**Formularz:**
```
╔════════════════════════════════════════════════════════════╗
║ 📤 Ręczne wydanie składnika                               ║
║ (Użyj tylko w sytuacjach awaryjnych lub do testów kuchni) ║
╠════════════════════════════════════════════════════════════╣
║                                                            ║
║ Składnik magazynowy*:  [Pierś z kurczaka ▼]               ║
║ Aktualny stan: 5.20 kg                    ← badge HTMX    ║
║                                                            ║
║ Ilość do wydania*:     [  1.00  ] kg                      ║
║ Partia (opcjonalnie):  [── Auto FEFO ──▼]                 ║
║                                                            ║
║ Powód wydania*:                                            ║
║ [── Wybierz powód ── ▼]                                   ║
║   • Test nowego przepisu                                   ║
║   • Awaria lodówki / utylizacja                           ║
║   • Wydanie na życzenie szefa kuchni                      ║
║   • Inny → [textbox pojawia się po wyborze "Inny"]        ║
║                                                            ║
║ Komu wydano*:                                              ║
║ [── Wybierz odbiorcę ── ▼]                                ║
║   • Kuchnia główna                                         ║
║   • Kuchnia testowa                                        ║
║   • Awaria lodówki #1-3                                    ║
║   • Inny → [textbox...]                                   ║
║                                                            ║
║ [Anuluj]                              [Wydaj składnik]    ║
╚════════════════════════════════════════════════════════════╝
```

**Walidacja FluentValidation:**
- `StockItemId` > 0 (required)
- `Quantity` > 0, ≤ aktualny stan (server-side check)
- `Reason` — not empty
- `IssuedTo` — not empty

Po wydaniu:
- `FefoService.DeductByFefoAsync` (jeśli brak `BatchId`) lub dedukcja z konkretnej partii
- `InventoryTransaction` z `TransactionType = ManualIssue`
- Toast: "✅ Wydano 1.00 kg piersi z kurczaka z partii B-2026-05-08"
- Redirect do `/warehouse`

---

### 3.7 `Warehouse/FefoReport` — Raport FEFO (NOWY)

**Role:** Warehouse, WarehouseManager, Admin
**URL:** `/warehouse/fefo-report`

**Nagłówek:**
```
📊 Raport FEFO — Aktywne Partie wg Daty Ważności
Data wygenerowania: 15.05.2026 12:00
[🖨️ Drukuj] [📥 Eksport CSV]
```

**Filtry (HTMX):**
```
[Kategoria: Wszystkie ▼]  [Status: Wszystkie ▼]  [Wygasa w ciągu: __ dni]
```
`hx-get="/warehouse/fefo-report-table?category={cat}&status={s}&expiresInDays={d}"` → `#fefo-table`

**Tabela — posortowana wg daty ważności ASC (NULL na końcu):**
```
| # | Składnik          | Kat.     | Nr partii    | Przyjęto    | Wygasa      | Dni do wyg. | Ilość     | Status      |
|---|-------------------|----------|--------------|-------------|-------------|-------------|-----------|-------------|
| 1 | Pierś z kurczaka  | Białko   | B-2026-05-06 | 06.05.2026  | 18.05.2026  | 3 dni       | 0.80 kg   | ⚠️ PILNE    |
| 2 | Mleko UHT         | Nabiał   | B-2026-05-01 | 01.05.2026  | 20.05.2026  | 5 dni       | 4.00 l    | ⏰ Wygasa   |
| 3 | Masło             | Nabiał   | B-2026-05-08 | 08.05.2026  | 15.06.2026  | 31 dni      | 12.50 kg  | ✅ OK       |
| 4 | Sól               | Przyprawy| B-2026-04-01 | 01.04.2026  | —           | —           | 2.00 kg   | ∞ Brak daty |
```

**Format druku (CSS @media print):**
- Rozmiar: A4 portret
- Czcionka tabeli: 8pt
- Marginesy: 15mm
- Nagłówek z datą generowania
- Brak kolorów tła (oszczędność tuszu) — statusy jako tekst

---

### 3.8 `Warehouse/TransactionHistory` — Historia Transakcji (NOWY)

**Role:** Warehouse, WarehouseManager, Admin
**URL:** `/warehouse/transaction-history`

**Filtry:**
```
[Składnik: Wszystkie ▼]  [Typ: Wszystkie ▼]  [Od: __/__/____]  [Do: __/__/____]  [Szukaj]
```
`hx-get="/warehouse/transaction-history-table?stockItemId={id}&type={t}&from={d}&to={d}&page={p}"` → `#history-table`

**Tabela:**
```
| Data       | Składnik          | Typ              | Ilość     | Partia       | Użytkownik  | Notatki           |
|------------|-------------------|------------------|-----------|--------------|-------------|-------------------|
| 15.05 09:12| Pierś z kurczaka  | ✅ Przyjęcie     | +5.00 kg  | B-2026-05-10 | Magazynier  | Stan opakowań OK  |
| 15.05 07:30| Pierś z kurczaka  | 🔴 Produkcja     | −3.20 kg  | B-2026-05-08 | System      | Plan #42          |
| 14.05 16:00| Mąka pszenna      | ⚠️ Odpad         | −0.50 kg  | B-2026-05-08 | Magazynier  | Przeterminowana   |
```

Paginacja: 25 wierszy na stronę.

---

### 3.9 `Warehouse/Temperatures` — Logowanie Temperatur (modyfikacja)

**Role:** Warehouse, WarehouseManager, Admin
**URL:** `/warehouse/temperatures`

**Zmiany:**

1. **Wykres ostatnich 7 dni (Chart.js):**
   ```
   [Urządzenie: Lodówka #1 ▼]    ← HTMX: hx-get="/warehouse/temperature-chart-data/{device}?days=7"
   
   °C
   8 ┤                    ╱╲
   6 ┤               ╱───╱  ╲───╲
   4 ┤──────────────╱          ╲──────  ← norma 2-4°C (zielone pasmo)
   2 ┤─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─
   0 ┤
     └──┬──┬──┬──┬──┬──┬──┬──
       Pn Wt Śr Cz Pt So Nd
   ```
   - Pasmo normy: zielone tło
   - Odczyty poza normą: czerwone punkty
   - Endpoint JSON: `GET /warehouse/temperature-chart-data/{device}?days=7`

2. **Przycisk "Zapisz i dodaj kolejny"** — analogicznie do `Receive`

---

### 3.10 `Warehouse/HaccpReport` — Raport HACCP (modyfikacja)

**Role:** WarehouseManager, Admin
**URL:** `/warehouse/haccp-report`

**Zmiany:**

1. **Wykres Chart.js** — temperatura w czasie (jw. jak w Temperatures).

2. **Przyciski eksportu:**
   ```
   [🖨️ Drukuj] [📥 Eksport CSV] [📄 Eksport PDF]
   ```

3. **Eksport PDF (QuestPDF, format A4):**
   - Nagłówek: "Raport HACCP — KuchniaUCygana" (bez logo/pieczątki)
   - Okres: data od — do
   - Tabela: Urządzenie, Data/Czas, Temperatura, Status (norma/poza normą), Uwagi
   - Czcionka tabeli: **8pt** (aby zmieścić na A4)
   - Marginesy: 15mm
   - Podsumowanie: Łączne odczyty, Odczyty w normie %, Przekroczenia

4. **Eksport CSV (CsvHelper):**
   - Kolumny: Urządzenie, DataCzas, Temperatura, CzyWNormie, Uwagi
   - Endpoint: `GET /warehouse/haccp-report/export-csv?from={d}&to={d}`

5. **Format druku (CSS @media print):**
   - A4 portret, czcionka 8pt
   - Brak kolorów tła — statusy jako tekst [OK] / [PRZEKR.]
   - Nagłówek i stopka z datą i numerem strony

---

### 3.11 `Warehouse/EditBatchExpiry/{batchId}` — Edycja Daty Ważności (modal HTMX)

**Realizacja:** Modal inline w widoku `BatchDetails` — partial renderowany przez HTMX.

**Wywołanie:** Kliknięcie `[Edytuj datę]` → `hx-get="/warehouse/edit-batch-expiry/{batchId}"` → modal partial.
**Zapis:** `hx-post="/warehouse/edit-batch-expiry/{batchId}"` → swap `#batch-table` + zamknięcie modalu.

**Formularz:**
```
╔══════════════════════════════════════════════════╗
║ ✏️ Zmień datę ważności partii B-2026-05-06       ║
╠══════════════════════════════════════════════════╣
║                                                  ║
║ Składnik: Pierś z kurczaka                       ║
║ Aktualna data ważności: 20.05.2026               ║
║                                                  ║
║ Nowa data ważności*:  [  18.05.2026  ]           ║
║                                                  ║
║ Powód zmiany*:                                   ║
║ [── Wybierz powód ──▼]                           ║
║   • Kontrola jakości (skrócenie)                 ║
║   • Informacja od dostawcy                       ║
║   • Ponowna ocena organoleptyczna                ║
║   • Inny → [textbox...]                          ║
║                                                  ║
║ ⚠️ Zmiana zostanie zarejestrowana w dzienniku.   ║
║                                                  ║
║ [Anuluj]                    [Zapisz zmianę]      ║
╚══════════════════════════════════════════════════╝
```

Po zapisie:
- Aktualizacja `Batch.ExpiryDate`
- Nowy rekord w `BatchExpiryChangeLog` (z `ChangedByUserId` z `ICurrentUserService`)
- `InventoryTransaction` z `TransactionType = ExpiryDateChanged`
- Toast: "✅ Data ważności partii B-2026-05-06 zmieniona na 18.05.2026"

---

## 4. PRZEPŁYW DANYCH — PEŁNY CYKL MAGAZYNOWY

```
[Dostawca przyjeżdża]
      │
      ▼
[Warehouse/Receive] — magazynier wprowadza dane dostawy
      │  → tworzy Batch + InventoryTransaction(Receipt)
      ▼
[Warehouse/Index] — stany aktualizowane na żywo
      │  ├─ SmartInventoryAnalyzer → alerty (BelowMinimum, ExpiringSoon, NoStock, Expired)
      │  ├─ Kolorowanie wierszy 3-stanowe
      │  ├─ [Szczegóły] → BatchDetails (traceability)
      │  ├─ [Raport FEFO] → FefoReport (aktywne partie wg FEFO)
      │  └─ [Historia] → TransactionHistory (pełna historia transakcji)
      │
      ├─ [Wydanie ręczne] → Issue
      │      → FefoService.DeductByFefoAsync
      │      → InventoryTransaction(ManualIssue)
      │
      ├─ [Odpad] → Waste
      │      → FefoService.DeductByFefoAsync
      │      → InventoryTransaction(Waste)
      │
      ├─ [Inwentaryzacja] → Inventory
      │      → InventoryAdjustment + InventoryTransaction(Adjustment)
      │
      └─ [Temperatury] → Temperatures → HaccpReport
             → TemperatureLog (odczyty)
             → Chart.js (wykres 7 dni)
             → Eksport CSV (CsvHelper) / PDF (QuestPDF)

Auto-dedukcja z produkcji:
[ProductionService.ApproveCookingAsync]
      │  → FefoService.DeductByFefoAsync
      │  → InventoryTransaction(ProductionIssue)
      ▼
[Stany magazynowe zaktualizowane]
```

---

## 5. WALIDACJA (FluentValidation)

| Request DTO | Reguły |
|---|---|
| `ReceiveDeliveryRequest` | `StockItemId > 0`, `SupplierBatchNumber` not empty (max 50), `Quantity > 0`, `ExpiryDate` opcjonalne (jeśli podane: ≥ dziś) |
| `RegisterWasteRequest` | `StockItemId > 0`, `Quantity > 0` (≤ aktualny stan, server-side), `Reason` not empty |
| `ManualIssueRequest` | `StockItemId > 0`, `Quantity > 0` (≤ aktualny stan), `Reason` not empty, `IssuedTo` not empty |
| `EditBatchExpiryRequest` | `BatchId > 0`, `NewExpiryDate` ≥ dziś, `Reason` not empty (min 5 znaków) |
| `TemperatureLogRequest` | `DeviceNameOrLocation` not empty, `RecordedTemperatureCelsius` ∈ [-50, +50] |
| `InventoryAdjustment` | `StockItemId > 0`, `ActualQuantity ≥ 0`, `Reason` not empty |

---

## 6. TESTY

### 6.1 Testy jednostkowe (xUnit + Moq + FluentAssertions)

| Klasa testowa | Co testujemy |
|---|---|
| `WarehouseServiceTests` | `IssueManualAsync` (FEFO + wskazana partia), `EditBatchExpiryAsync` (log + transakcja), `GetFefoReportAsync` (sortowanie NULL na końcu) |
| `FefoServiceTests` | `DeductByFefoAsync` z `ManualIssue` — weryfikacja poprawnej dedukcji |
| `ManualIssueValidatorTests` | Walidacja pól, ujemna ilość, przekroczenie stanu |
| `EditBatchExpiryValidatorTests` | Data w przeszłości, pusty powód |

### 6.2 Testy integracyjne (z SQL Server in-memory lub Testcontainers)

| Test | Scenariusz |
|---|---|
| `WarehouseRepository_GetBatchDetailsAsync` | Weryfikacja JOIN przez StockItemId (nowa kolumna) |
| `WarehouseRepository_GetTransactionHistoryAsync` | Filtrowanie po dacie, typie, składniku |
| `Migration307_AddStockItemIdToTransactions` | Up + Down migracji |

---

## 7. LOGI ANALITYCZNE — Co zbieramy

| Zdarzenie | Dane | Zastosowanie |
|-----------|------|--------------|
| `Receipt` | Partia, ilość, dostawca, data ważności | Traceability, kontrola dostaw |
| `Waste` | Ilość, powód, partia | Analiza strat, KPI waste ratio |
| `ManualIssue` | Ilość, komu, powód, partia | Audit trail, kontrola wydań |
| `ProductionIssue` | Ilość, partie, plan produkcji | FEFO compliance, food cost |
| `Adjustment` | Stan przed/po, powód | Inwentaryzacja, audyt |
| `ExpiryDateChanged` | Stara/nowa data, powód | Kontrola jakości, HACCP |
| `TemperatureLog` | Urządzenie, temperatura, czas | Raport HACCP, compliance |

---

## 8. ZMIANY W ISTNIEJĄCYM KODZIE

| Plik/Komponent | Zmiana | Priorytet |
|---|---|---|
| **Migracja 307** | Dodać `StockItemId` do `InventoryTransactions` + backfill | 🔴 Krytyczne |
| **Migracja 308** | Tabela `BatchExpiryChangeLogs` | 🟡 Wysokie |
| `InventoryTransactionType` enum | Dodać `ManualIssue`, `ExpiryDateChanged` | 🔴 Krytyczne |
| `Warehouse/Index.cshtml` | Kolorowanie 3-stanowe, nowe przyciski, filtry HTMX, paginacja 25/stronę | 🔴 Krytyczne |
| `Warehouse/Receive.cshtml` | "Przyjmij i dodaj kolejną", HTMX auto-info, temp. transportu | 🟡 Wysokie |
| `Warehouse/Waste.cshtml` | Dropdown partii HTMX, preview odpisu | 🟡 Wysokie |
| `Warehouse/Inventory.cshtml` | Filtr kategorii, kolorowanie różnic, podsumowanie Alpine.js | 🟡 Średnie |
| `Warehouse/Temperatures.cshtml` | Wykres 7 dni Chart.js | 🟡 Wysokie |
| `Warehouse/HaccpReport.cshtml` | Wykres, eksport CSV + PDF, format A4 8pt | 🟡 Wysokie |
| **NOWY:** `Warehouse/BatchDetails.cshtml` | Szczegóły partii, historia transakcji, log zmian dat | 🔴 Krytyczne |
| **NOWY:** `Warehouse/Issue.cshtml` | Formularz ręcznego wydania | 🟡 Wysokie |
| **NOWY:** `Warehouse/FefoReport.cshtml` | Raport aktywnych partii wg FEFO | 🟡 Wysokie |
| **NOWY:** `Warehouse/TransactionHistory.cshtml` | Osobna strona historii transakcji | 🟡 Średnie |
| `WarehouseService` | Nowe metody: `IssueManualAsync`, `EditBatchExpiryAsync`, `GetFefoReportAsync`, `GetBatchDetailsAsync`, `GetTransactionHistoryAsync` | 🔴 Krytyczne |
| `WarehouseController` | Nowe akcje + partial endpointy HTMX | 🔴 Krytyczne |
| `ICurrentUserService` | Nowy interfejs w Domain, implementacja w Infrastructure | 🟡 Wysokie |

---

## 9. PYTANIA OTWARTE — ROZSTRZYGNIĘTE

| # | Pytanie | Decyzja |
|---|---------|---------|
| 7.1 | Wykres temperatur (Chart.js) — kiedy? | ✅ Teraz (nie później) |
| 7.2 | Eksport PDF HACCP — logo/pieczątka? | ❌ Nie — projekt akademicki |
| 7.3 | Powiadomienia email o alertach? | ❌ Przez wbudowany system powiadomień w panelu |
| 7.4 | Historia transakcji jako osobna strona? | ✅ Tak — `/warehouse/transaction-history` |
| 7.5 | Paginacja — ile wierszy? | ✅ 25 wierszy na stronę |
| DS-1 | `StockItemId` w `InventoryTransaction` | ✅ Dodać (migracja 307, denormalizacja) |
| DS-2 | `UserId` w serwisach | ✅ `ICurrentUserService` |
| DS-3 | Kolorowanie — ile stanów? | ✅ 3 stany (danger, warning, domyślny) |
| DS-4 | Nazwy alertów (LOW_STOCK vs BelowMinimum) | ✅ Użyć nazw z kodu: `BelowMinimum`, `NoStock` itd. |
| DS-5 | Partie bez `ExpiryDate` w raporcie FEFO | ✅ Na końcu z oznaczeniem "Brak daty" |
| DS-6 | `Issue.IssuedTo` — tekst czy dropdown? | ✅ Dropdown z predefiniowan. wartościami + "Inny" |
| DS-7 | PDF/CSV — jakie biblioteki? | ✅ PDF: QuestPDF, CSV: CsvHelper |
| DS-8 | `EditBatchExpiry` — jak wywoływany? | ✅ Modal HTMX: `hx-get` partial → `hx-post` zapis |
| DS-9 | Zakres historii w `BatchDetails` | ✅ Filtr zakresu dat (zamiast stałe 30 dni) |
| DS-10 | Dokumenty drukowane — format | ✅ A4, 8pt czcionka, CSS @media print |
