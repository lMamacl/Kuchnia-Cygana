# WIZJA MODUŁU KOMPLETACJA — Specyfikacja Implementacyjna
**KuchniaUCygana · Sprint 3+4 · Wersja 1.1**
*Aktualizacja v1.1: skanowanie QR z weryfikacją, manifest cyfrowy, decyzje, poprawki z code review (DeepSeek), log statusów, endpointy HTMX*

---

## 1. DECYZJE PROJEKTOWE — KOMPLETNE

| Temat | Decyzja |
|-------|---------|
| Podział procesów | **Pakowanie toreb** i **Załadunek aut** to DWA ODRĘBNE procesy z wydzielonymi widokami, kontrolerami i flow. |
| Punkt wejścia | Osobne pozycje w menu sidebar: "Pakowanie toreb" i "Załadunek aut" |
| Grupowanie w pakowaniu | Per zamówienie → torba. Jedna torba = jedno zamówienie klienta na dany dzień. |
| Grupowanie w załadunku | Per trasa/auto. Manifest generowany per auto. |
| **Skanowanie QR — pakowanie pudełek** | ✅ TAK — skaner (symulator input) odczytuje QR pudełka, system **automatycznie weryfikuje** czy pudełko pasuje do zamówienia (typ dania, dieta) i dodaje do torby. Walidacja: `BoxCode` → sprawdzenie `PackingItem.OrderId == Session.OrderId`. |
| **Skanowanie QR — załadunek toreb** | ✅ TAK — skaner odczytuje QR etykiety transportowej torby, system **automatycznie weryfikuje** zgodność torby z manifestem/trasą i oznacza jako załadowaną. |
| **Symulator skanera — obsługa Enter** | Input z `autofocus` + `hx-trigger="keydown[key=='Enter'] from:closest form"` + `preventDefault` na submit. Fizyczny skaner wysyła Enter po odczycie. |
| **Manifest — cyfrowy (nie papierowy)** | ✅ TAK — po zweryfikowaniu manifestu automatycznie pojawia się w widoku przypisanego kierowcy. Przesyłanie wewnętrzne (JSON w DB, nie specjalny przycisk). Podpis = profil użytkownika (PackingManager). Drukowanie opcjonalne na wypadek awarii. |
| Etykieta transportowa | Generowana per torba (zamówienie). QR z pełnym URL: `{AppSettings.BaseUrl}/delivery/verify/{sessionId}`. PublicId obok QR (8 znaków). |
| Kolejność posiłków | `DietVariantMeal.SortOrder`: 1=śniadanie, 2=II śniadanie, 3=obiad, 4=podwieczorek, 5=kolacja. |
| Multi-diet per klient | Klient może mieć wiele zamówień/diet pod jeden adres. Każde zamówienie = osobna torba. |
| **Hurtowy druk etykiet** | ✅ TAK — skoro skanowanie weryfikuje zgodność, ryzyko pomyłek niskie. |
| **Widok załadunku** | Karty tras (nie tabela). |
| **Alergeny na etykiecie transportowej** | ❌ NIE — alergeny są na etykiecie foliowej pudełka. |
| **Manifest PDF** | ❌ NIE — zbędne. Manifest cyfrowy (JSON w DB) + opcjonalny druk HTML. |
| **Kierowca — widok mobilny** | Mini podgląd zapełnienia przypisanego manifestu (read-only). Nie pełny widok załadunku. |
| **Historia zmian statusu torby** | ✅ TAK — log per status transition. |
| **Potwierdzenie dostawy (verify)** | ❌ Osobny moduł — nie w zakresie kompletacji. |
| **Redruk etykiet transportowych** | Każdy druk (oryginał + redruk) = nowy rekord `PackingLabel`. `PrintCount` obliczany dynamicznie (COUNT per session). Powód wymagany przy redruku. Spójne z podejściem `BoxLabel` w produkcji. |
| **Status `Packing` w `PackingStatus`** | ❌ USUNIĘTY — zbędny. `Pending` = torba nie gotowa. `Packed` = wszystkie pudełka spakowane. |
| **Kitchen — read-only** | ✅ TAK — `[Authorize(Roles = "Packing,PackingManager,Kitchen,Admin")]` na Index, ale Kitchen nie może pakować. |
| **Uprawnienia załadunek** | PackingManager, Admin — pełny dostęp. Packing — read-only lista. Driver — mini widok. |
| **UserId w serwisach** | Przez `ICurrentUserService` (interfejs w Domain, impl. w Infrastructure). |
| **Dokumenty drukowane** | Format A4, czcionka 8pt, CSS `@media print`. Bez logo (projekt akademicki). |

---

## 2. MODEL DANYCH — KONTEKST KOMPLETACJI

### 2.1 Istniejące encje (z modyfikacjami)

- **PackingSession** — `AuditableEntity<int>`: PackingDate, OrderId, PackedBy, Status (PackingStatus)
- **PackingItem** — `AuditableEntity<int>`: SessionId FK, OrderId, MealId, DietVariantId, BatchId FK, Status (PackingItemStatus), BoxCode, PackedBy, PackedAt
- **PackingLabel** — `BaseEntity<int>`: ItemId FK, QrCode, ClientName, DietName, Allergens, RouteInfo, LabelType, DeliveryWindow
- **PackingManifest** — `AuditableEntity<int>`: ManifestNumber (UUID), RouteId, VehicleRegistration, GeneratedAt, IsVerified, VerifiedAt, PayloadJson, BagCount

### 2.2 Nowe pola w PackingSession

```csharp
public class PackingSession : AuditableEntity<int>
{
    // istniejące...
    public string? ClientPublicId { get; set; }    // NOWE: skrócony PublicId (8 znaków) — kopiowane z CustomerProfile przy tworzeniu sesji
    public string? DietName { get; set; }          // NOWE: "Dieta Standard 1800 kcal"
    public int? RouteId { get; set; }              // NOWE: FK do trasy (przypisywane przy załadunku)
    public int? StopNumber { get; set; }           // NOWE: numer przystanku na trasie
}
```
> Uwaga (z code review): `ClientPublicId` to denormalizacja — kopiowane jednorazowo przy tworzeniu sesji z `CustomerProfile.PublicId`. PublicId jest niezmienne (Guid), więc ryzyko niespójności minimalne.

### 2.3 PackingLabel — rozszerzenie (nowe rekordy per druk)

```csharp
public class PackingLabel : BaseEntity<int>
{
    // istniejące...
    public string? MealsList { get; set; }         // NOWE: "1. Śniadanie: Owsianka\n2. II śniadanie: Kanapka\n..."
    public int? Kcal { get; set; }                 // NOWE: kaloryczność diety
    public string? ReprintReason { get; set; }     // NOWE: powód redruku (null = oryginał)
    // PrintCount NIE jest polem — obliczany dynamicznie: SELECT COUNT(*) FROM PackingLabels WHERE PackingSessionId = @id
}
```
> Każdy druk (oryginał i redruk) tworzy nowy rekord `PackingLabel`. Spójne z `BoxLabel` w produkcji.

> **Skąd dane do `MealsList`:** Serwis `PackingService.GenerateTransportLabelAsync` buduje listę posiłków z: `Order` → `DietVariant` → `DietVariantMeal` (z `Meal`), posortowanych wg `SortOrder`.

### 2.4 PackingStatusLog — Log zmian statusu torby (NOWY)

```csharp
public class PackingStatusLog : BaseEntity<long>
{
    public int PackingSessionId { get; set; }
    public PackingStatus OldStatus { get; set; }
    public PackingStatus NewStatus { get; set; }
    public int ChangedByUserId { get; set; }       // z ICurrentUserService
    public DateTimeOffset ChangedAt { get; set; }
    public string? Notes { get; set; }             // opcjonalne
}
```

### 2.5 Enum PackingStatus — UPROSZCZONY (bez Packing)

```csharp
public enum PackingStatus
{
    Pending,     // Torba utworzona, pudełka nie gotowe lub częściowo spakowane
    Packed,      // Wszystkie pudełka w torbie spakowane
    Labeled,     // Etykieta transportowa wydrukowana
    Loaded,      // Załadowana do auta
    Dispatched   // Wysłana w trasę
}
```
> Status `Packing` usunięty (rekomendacja z code review). Postęp widoczny w UI jako `3/5 pudełek` bez dodatkowego statusu.

### 2.6 Enum PackingItemStatus — bez zmian

```csharp
public enum PackingItemStatus
{
    Pending,       // Czeka na zafoliowanie w kuchni
    FoilPrinted,   // Etykieta foliowa gotowa (z produkcji)
    Packed,        // Spakowane do torby
    Damaged,       // Uszkodzone
    Missing        // Brak
}
```

### 2.7 PackingManifest — rozszerzenie o podpis cyfrowy

```csharp
public class PackingManifest : AuditableEntity<int>
{
    // istniejące...
    public int? VerifiedByUserId { get; set; }     // NOWE: kto zweryfikował (PackingManager) — profil = podpis
    public int? DriverUserId { get; set; }         // NOWE: przypisany kierowca (User) — manifest automatycznie widoczny
    // PayloadJson — NIE zawiera pól podpisu (to dane elektroniczne, nie wydruk)
}
```

### 2.8 Migracje FluentMigrator

| Nr migracji | Opis | Zakres |
|---|---|---|
| `310` | Dodanie kolumn do `PackingSessions`: `ClientPublicId`, `DietName`, `RouteId`, `StopNumber` | M3 |
| `311` | Dodanie kolumn do `PackingLabels`: `MealsList`, `Kcal`, `ReprintReason` | M3 |
| `312` | Utworzenie tabeli `PackingStatusLogs` | M3 |
| `313` | Dodanie kolumn do `PackingManifests`: `VerifiedByUserId`, `DriverUserId` | M3 |

---

## 3. WIDOKI — MODUŁ A: PAKOWANIE TOREB

> **Kontekst:** Kompletacja pudełek do toreb klientów. Niezależne od załadunku aut.

---

### 3.A.1 `Packing/Index` — Pulpit Pakowania Toreb

**Role:** Packing, PackingManager, Kitchen (read-only), Admin
**URL:** `/packing?date=2026-05-15`
**Menu sidebar:** "Pakowanie toreb"

**Endpointy HTMX:**

| Akcja | Endpoint | Trigger | Swap |
|---|---|---|---|
| Filtrowanie | `hx-get="/packing/bag-table?date={d}&status={s}&route={r}&search={q}"` | `change` / `input changed delay:400ms` | `#bag-table-container` |
| Zmiana daty | `hx-get="/packing?date={d}"` | klik nawigacji dat | pełny reload |

**Nagłówek z KPI:**
```
╔══════════════════════════════════════════════════════════════════════════╗
║ 📦 KOMPLETACJA TOREB                        15.05.2026                 ║
║                                                                        ║
║ Torby łącznie: 47  │  Skompletowane: 23  │  Z etykietą: 18  │  █████░ 49%  ║
║                                                                        ║
║ [← Poprzedni]  [📅 Picker daty]  [Następny →]                         ║
╚══════════════════════════════════════════════════════════════════════════╝
```

**Filtry:**
```
[Status: Wszystkie ▼]  [Trasa: Wszystkie ▼]  [🔍 Szukaj klienta...]
```

**Tabela toreb:**
```
| #  | Zamówienie | Klient               | Public ID | Dieta               | Pudełka   | Status torby    | Akcja            |
|----|------------|----------------------|-----------|---------------------|-----------|-----------------|------------------|
| 1  | #247       | Jan Kowalski         | a3f5c9e1  | Standard 1800 kcal  | 3/5       | ⏳ Oczekuje     | [Kompletuj]      |
| 2  | #251       | Jan Kowalski         | a3f5c9e1  | Junior 1200 kcal    | 2/2       | ✅ Gotowa       | [Etykieta]       |
| 3  | #248       | Anna Nowak           | b7d2e4f8  | Vege 1500 kcal      | 0/4       | ⏳ Oczekuje     | [Kompletuj]      |
| 4  | #252       | Piotr Zieliński      | c1a8f3d9  | Standard 2000 kcal  | 5/5       | 🏷️ Z etykietą  | [Podgląd]        |
```

**Przycisk globalny:**
```
[🏷️ Hurtowy druk etykiet]
```

---

### 3.A.2 `Packing/Session/{sessionId}` — Kompletacja Torby z Weryfikacją QR

**Role:** Packing, PackingManager, Admin
**URL:** `/packing/session/42`

**Endpointy HTMX:**

| Akcja | Endpoint | Trigger | Swap |
|---|---|---|---|
| Skan pudełka (weryfikacja QR) | `hx-post="/packing/scan-box/{sessionId}"` z body `{ boxCode: "..." }` | `keydown[key=='Enter']` na input skanera | `#scan-result` + `#box-table` |
| Oznacz spakowane | `hx-post="/packing/mark-packed/{itemId}"` | klik `[Spakuj]` | wiersz tabeli |
| Spakuj torbę | `hx-post="/packing/pack-bag/{sessionId}"` | klik `[Spakuj torbę]` | nagłówek + toolbar |

**Nagłówek:**
```
╔══════════════════════════════════════════════════════════════╗
║ 📦 Torba #42 — Zamówienie #247                              ║
║ Klient: Jan Kowalski (a3f5c9e1)                             ║
║ Dieta: Standard 1800 kcal                                   ║
║ Data: 15.05.2026                                            ║
║                                                              ║
║ Postęp: ██████░░░░ 3/5 pudełek           Status: ⏳ Oczekuje║
╚══════════════════════════════════════════════════════════════╝
```

**🔍 Symulator skanera pudełek (KLUCZOWA FUNKCJA):**
```
╔════════════════════════════════════════════════════════════╗
║ 📷 Zeskanuj kod QR pudełka:                               ║
║ [🔍 Kod pudełka (np. BOX-000015)...       ] [Weryfikuj]   ║
║                                                            ║
║ ─── Wynik skanowania ─────────────────────────────────── ║
║ ✅ Pudełko BOX-000015 zweryfikowane!                      ║
║    Posiłek: 4. Podwieczorek — Jogurt naturalny            ║
║    Status: Zafoliowane → [Spakuj do torby]                ║
║                                                            ║
║ ⚠️ PRZYKŁAD ODRZUCENIA:                                   ║
║ ❌ Pudełko BOX-000099 NIE pasuje do tego zamówienia!      ║
║    Oczekiwane zamówienie: #247 (Standard 1800)            ║
║    Pudełko z zamówienia: #251 (Junior 1200)               ║
╚════════════════════════════════════════════════════════════╝
```

**Logika weryfikacji skanowania:**
1. Pakowacz skanuje QR pudełka (lub wpisuje kod ręcznie)
2. System wyszukuje `PackingItem` po `BoxCode`
3. **Weryfikacja:** Sprawdza czy `PackingItem.OrderId == PackingSession.OrderId`
   - ✅ TAK: wyświetla info o posiłku, aktywuje przycisk "Spakuj do torby"
   - ❌ NIE: wyświetla alert z informacją o niezgodności, nie pozwala spakować
4. Sprawdza czy pudełko ma status `FoilPrinted` (zafoliowane):
   - ✅ TAK: można pakować
   - ❌ NIE (`Pending`): wyświetla ostrzeżenie "Pudełko jeszcze nie zafoliowane!"

**Tabela pudełek — z kolumną "Posiłek #" (SortOrder):**
```
| Kod        | # | Posiłek                    | Status          | Operator        | Akcja         |
|------------|---|----------------------------|-----------------|-----------------|---------------|
| BOX-000012 | 1 | Śniadanie — Owsianka       | ✅ Spakowane    | Pakowacz A 07:12| [Spakowane]   |
| BOX-000013 | 2 | II śniadanie — Kanapka     | ✅ Spakowane    | Pakowacz A 07:15| [Spakowane]   |
| BOX-000014 | 3 | Obiad — Kurczak teriyaki   | ✅ Spakowane    | Pakowacz A 07:20| [Spakowane]   |
| BOX-000015 | 4 | Podwieczorek — Jogurt      | 🔵 Zafoliowane | —               | [Spakuj]      |
| BOX-000016 | 5 | Kolacja — Sałatka          | ⏳ Czeka        | —               | [Brak folii]  |
```

**Nawigacja:**
```
[← Wróć do listy toreb]    [Spakuj torbę]    [Drukuj etykietę ↓]
```
- "Wróć" → `/packing` (nie do Loading!)
- "Spakuj torbę" — aktywny gdy wszystkie pudełka = `Packed`
- "Drukuj etykietę" — aktywny gdy torba = `Packed`

---

### 3.A.3 `Packing/TransportLabel/{sessionId}` — Etykieta Transportowa

**Role:** Packing, PackingManager, Admin
**URL:** `/packing/transport-label/42`

**Podgląd etykiety (rendered HTML, format A4-kompatybilny):**
```
┌──────────────────────────────────────────────────────────┐
│ KUCHNIA U CYGANA — Etykieta transportowa                 │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Klient: a3f5c9e1                [█████ KOD QR █████]   │
│                                  {BaseUrl}/delivery/     │
│                                  verify/{sessionId}      │
│  Dieta: STANDARD 1800 kcal                               │
│  Zamówienie: #247                                        │
│                                                          │
├──────────────────────────────────────────────────────────┤
│  POSIŁKI:                                                │
│  1. Śniadanie — Owsianka z jagodami                     │
│  2. II śniadanie — Kanapka z serem                      │
│  3. Obiad — Kurczak teriyaki z ryżem                    │
│  4. Podwieczorek — Jogurt naturalny                     │
│  5. Kolacja — Sałatka grecka                            │
├──────────────────────────────────────────────────────────┤
│  Data dostawy: 15.05.2026                                │
│  Pudełek w torbie: 5                                     │
│  Trasa: A3 | Stop: #05                                  │
└──────────────────────────────────────────────────────────┘
```
> URL w QR budowany z `AppSettings.BaseUrl` (konfigurowalny per środowisko).
> Bez alergenów — alergeny na etykiecie foliowej pudełka.

**Akcje:**
- `[🖨️ Drukuj etykietę]` → nowy rekord `PackingLabel` (ReprintReason = null), status torby → `Labeled`
- `[🔄 Drukuj ponownie]` → modal z powodem → nowy rekord `PackingLabel` (ReprintReason = powód)
- `[← Wróć do torby]`

**Format druku (CSS @media print):** A4, czcionka 10pt, QR min. 3cm×3cm.

---

### 3.A.4 `Packing/BulkLabels` — Druk Etykiet Hurtowy

**Role:** PackingManager, Admin
**URL:** `/packing/bulk-labels?date=2026-05-15`

```
╔══════════════════════════════════════════════════╗
║ 🏷️ Hurtowy druk etykiet transportowych          ║
║ Data: 15.05.2026                                ║
║ Torby gotowe do etykiety: 15                    ║
║                                                  ║
║ [✅ Zaznacz wszystkie]                           ║
║ ☑ #247 — Jan Kowalski — Standard 1800           ║
║ ☑ #251 — Jan Kowalski — Junior 1200             ║
║ ☑ #248 — Anna Nowak — Vege 1500                 ║
║ ☐ #249 — Piotr Zieliński — Standard 2000        ║
║                                                  ║
║ [🖨️ Drukuj zaznaczone (3)]                      ║
╚══════════════════════════════════════════════════╝
```

Endpoint: `POST /packing/bulk-print` z listą `sessionIds[]`.
Każda etykieta → nowy rekord `PackingLabel`. Status toreb → `Labeled`.

---

## 4. WIDOKI — MODUŁ B: ZAŁADUNEK AUT

> **Kontekst:** Ładowanie gotowych toreb do aut dostawczych. Torby muszą mieć status ≥ `Labeled`.
> **Całkowicie niezależny** od pakowania toreb.

---

### 4.B.1 `Loading/Index` — Pulpit Załadunku Aut (karty tras)

**Role:** PackingManager, Admin, (Packing — read-only)
**URL:** `/loading?date=2026-05-15`
**Menu sidebar:** "Załadunek aut"

**Nagłówek z KPI:**
```
╔══════════════════════════════════════════════════════════════════════════╗
║ 🚛 ZAŁADUNEK AUT                             15.05.2026               ║
║                                                                        ║
║ Trasy: 4  │  Torby łącznie: 47  │  Gotowe do załadunku: 35  │  Załadowane: 12  ║
║                                                                        ║
║ [← Poprzedni]  [📅 Picker daty]  [Następny →]                         ║
╚══════════════════════════════════════════════════════════════════════════╝
```

**Karty tras (format grid 2 kolumny):**
```
╔══════════════════════════════════════════════════════════════╗
║ 🚛 TRASA A3 — Auto: WA 12345                               ║
║ Kierowca: Marek Kowalczyk                                   ║
║                                                              ║
║ Torby: 12  │  Załadowane: 5                                  ║
║ Postęp: ████████░░░░ 42%                                    ║
║                                                              ║
║ Manifest: ⚠️ Do wygenerowania                               ║
║                                                              ║
║ [Otwórz załadunek trasy]                                    ║
╚══════════════════════════════════════════════════════════════╝

╔══════════════════════════════════════════════════════════════╗
║ 🚛 TRASA B1 — Auto: WA 54321                               ║
║ Kierowca: Anna Wiśniewska                                   ║
║                                                              ║
║ Torby: 8  │  Załadowane: 8                                  ║
║ Postęp: ████████████ 100%                                   ║
║                                                              ║
║ Manifest: ✅ Zweryfikowany → wysłany do kierowcy            ║
║                                                              ║
║ [Wyślij dostawę]  [Otwórz załadunek trasy]                  ║
╚══════════════════════════════════════════════════════════════╝
```

---

### 4.B.2 `Loading/Route/{routeId}` — Załadunek Trasy z Weryfikacją QR

**Role:** PackingManager, Admin
**URL:** `/loading/route/3?date=2026-05-15`

**Endpointy HTMX:**

| Akcja | Endpoint | Trigger | Swap |
|---|---|---|---|
| Skan torby (weryfikacja QR) | `hx-post="/loading/scan-bag/{routeId}"` z body `{ bagCode: "..." }` | `keydown[key=='Enter']` na input | `#scan-result` + `#bag-table` |
| Załaduj torbę | `hx-post="/loading/load-bag/{routeId}"` z `{ sessionId }` | klik `[Załaduj]` | wiersz + KPI |
| Generuj manifest | `hx-post="/loading/generate-manifest/{routeId}"` | klik | `#manifest-status` |
| Zweryfikuj manifest | `hx-post="/loading/verify-manifest/{routeId}"` | klik | `#manifest-status` + auto-wysłanie do kierowcy |
| Wyślij dostawę | `hx-post="/loading/dispatch/{routeId}"` | klik | redirect `/loading` |

**Nagłówek:**
```
╔══════════════════════════════════════════════════════════════╗
║ 🚛 Załadunek trasy A3                                      ║
║ Auto: WA 12345 | Kierowca: Marek Kowalczyk                 ║
║ Data: 15.05.2026                                            ║
║                                                              ║
║ Torby: 12  │  Załadowane: 5                                  ║
║ Manifest: ⚠️ Do weryfikacji (wygenerowany 15.05 08:30)      ║
╚══════════════════════════════════════════════════════════════╝
```

**🔍 Symulator skanera toreb (KLUCZOWA FUNKCJA):**
```
╔════════════════════════════════════════════════════════════╗
║ 📷 Zeskanuj etykietę transportową torby:                   ║
║ [🔍 Kod torby (np. BAG-42)...               ] [Weryfikuj] ║
║                                                            ║
║ ─── Wynik skanowania ─────────────────────────────────── ║
║ ✅ Torba #42 zweryfikowana dla trasy A3!                  ║
║    Klient: Jan Kowalski (a3f5c9e1)                        ║
║    Dieta: Standard 1800 kcal | 5 pudełek                  ║
║    Stop: #02 → automatycznie załadowana ✅                 ║
║                                                            ║
║ ⚠️ PRZYKŁAD ODRZUCENIA:                                   ║
║ ❌ Torba #48 NIE należy do trasy A3!                      ║
║    Przypisana trasa: B1 (Auto: WA 54321)                   ║
║    Przenoszenie niedozwolone — skontaktuj się z kierown.   ║
╚════════════════════════════════════════════════════════════╝
```

**Logika weryfikacji skanowania torby:**
1. Skan QR etykiety transportowej (lub wpisanie kodu ręcznie)
2. System wyszukuje `PackingSession` po kodzie
3. **Weryfikacja:** Sprawdza czy `PackingSession.RouteId == routeId`
   - ✅ TAK: automatycznie oznacza jako `Loaded`, wyświetla potwierdzenie
   - ❌ NIE: wyświetla alert, podaje prawidłową trasę
4. Sprawdza status torby:
   - ≥ `Labeled`: można załadować
   - < `Labeled`: "Torba nie ma jeszcze etykiety!"

**Tabela toreb na trasie:**
```
| Stop | Zamówienie | Odbiorca        | Public ID | Adres                   | Pudełka | Status torby   | Akcja         |
|------|------------|-----------------|-----------|-------------------------|---------|----------------|---------------|
| #01  | #252       | Piotr Zieliński | c1a8f3d9  | ul. Długa 12, Warszawa  | 5/5     | 🏷️ Z etykietą | [Załaduj]     |
| #02  | #247       | Jan Kowalski    | a3f5c9e1  | ul. Zielona 5/2, W-wa   | 5/5     | ✅ Załadowana  | [Załadowana]  |
| #02  | #251       | Jan Kowalski    | a3f5c9e1  | ul. Zielona 5/2, W-wa   | 2/2     | ✅ Załadowana  | [Załadowana]  |
| #03  | #248       | Anna Nowak      | b7d2e4f8  | ul. Krótka 8, Warszawa  | 4/4     | 🏷️ Z etykietą | [Załaduj]     |
```

**Panel operacji:**
```
[Generuj manifest]  [Zweryfikuj manifest]  [🖨️ Drukuj manifest (backup)]  [Wyślij dostawę]
```

Aktywność przycisków wg stanu:
| Przycisk | Warunek aktywności |
|---|---|
| Generuj manifest | Wszystkie torby mają status ≥ `Labeled` |
| Zweryfikuj manifest | Manifest istnieje i `IsVerified = false` |
| Drukuj manifest | Manifest istnieje (backup na wypadek awarii) |
| Wyślij dostawę | Manifest zweryfikowany AND wszystkie torby `Loaded` |

**Po weryfikacji manifestu:**
1. `PackingManifest.IsVerified = true`, `VerifiedAt = now`, `VerifiedByUserId = currentUser`
2. Manifest **automatycznie widoczny** w widoku kierowcy (`DriverUserId`)
3. Brak specjalnego przycisku "Wyślij do kierowcy" — automatyczne

---

### 4.B.3 `Loading/ManifestPreview/{routeId}` — Podgląd Manifestu

**Role:** PackingManager, Admin, Driver (read-only)
**URL:** `/loading/manifest-preview/3?date=2026-05-15`

**Widok manifestu (do druku backup A4):**
```
╔══════════════════════════════════════════════════════════════╗
║ MANIFEST ZAŁADUNKOWY                                        ║
║ Nr: 7a4c3e89-1f2b-4d5a-8c7e-9b0f1d2e3a4b                 ║
╠══════════════════════════════════════════════════════════════╣
║ Trasa: A3                                                   ║
║ Auto: WA 12345 | Kierowca: Marek Kowalczyk                 ║
║ Data dostawy: 15.05.2026                                    ║
║ Zweryfikowano: 15.05.2026 08:35 przez: Anna Kier. (profil) ║
║ Status: ✅ Zweryfikowany                                    ║
╠══════════════════════════════════════════════════════════════╣
║ TORBY DO DOSTARCZENIA:                                      ║
║                                                              ║
║ Stop #01 — Piotr Zieliński (c1a8f3d9)                      ║
║   ul. Długa 12, Warszawa                                    ║
║   • Torba #252: Standard 2000 kcal (5 pudełek)             ║
║                                                              ║
║ Stop #02 — Jan Kowalski (a3f5c9e1)                         ║
║   ul. Zielona 5/2, Warszawa                                 ║
║   • Torba #247: Standard 1800 kcal (5 pudełek)             ║
║   • Torba #251: Junior 1200 kcal (2 pudełka)               ║
║                                                              ║
║ Stop #03 — Anna Nowak (b7d2e4f8)                           ║
║   ul. Krótka 8, Warszawa                                    ║
║   • Torba #248: Vege 1500 kcal (4 pudełka)                 ║
╠══════════════════════════════════════════════════════════════╣
║ PODSUMOWANIE: Przystanki: 3 | Torby: 4 | Pudełek: 16       ║
╚══════════════════════════════════════════════════════════════╝
```

> `PayloadJson` nie zawiera podpisów — podpis = `VerifiedByUserId` (profil użytkownika).

**Format druku (backup, CSS @media print):** A4, czcionka 9pt, bez kolorów.

---

### 4.B.4 `Driver/Manifest` — Mini Widok Kierowcy (NOWY)

**Role:** Driver
**URL:** `/driver/manifest?date=2026-05-15`

**Uproszczony widok (mobilny):**
```
╔══════════════════════════════════════════╗
║ 🚛 Twoja trasa na dziś                  ║
║ Trasa: A3 | Auto: WA 12345              ║
║ Data: 15.05.2026                         ║
║                                          ║
║ Torby: 4/4 załadowane ✅                ║
║ Status: Gotowy do wyjazdu               ║
╠══════════════════════════════════════════╣
║ PRZYSTANKI:                              ║
║ #01 Piotr Zieliński — ul. Długa 12      ║
║     1 torba (5 pudełek)                  ║
║ #02 Jan Kowalski — ul. Zielona 5/2      ║
║     2 torby (7 pudełek)                  ║
║ #03 Anna Nowak — ul. Krótka 8           ║
║     1 torba (4 pudełka)                  ║
╠══════════════════════════════════════════╣
║ [📋 Szczegóły manifestu]                ║
╚══════════════════════════════════════════╝
```

Widoczny automatycznie po weryfikacji manifestu przez PackingManager.
Read-only — kierowca nie może modyfikować.

---

## 5. PRZEPŁYW DANYCH — PEŁNY CYKL KOMPLETACJI

```
=== MODUŁ A: PAKOWANIE TOREB ===

[Produkcja zatwierdza ugotowanie]
     │  → pudełka: status FoilPrinted, etykieta foliowa wydrukowana
     ▼
[Packing/Index] — pakowacz widzi listę toreb
     │
     ▼
[Packing/Session/{id}] — pakowacz kompletuje torbę
     │  ├─ 🔍 SKAN QR pudełka → system weryfikuje:
     │  │   • Czy BoxCode pasuje do zamówienia torby?
     │  │   • Czy pudełko zafoliowane (FoilPrinted)?
     │  │   → ✅ Dopuszczone → MarkBoxPacked
     │  │   → ❌ Odrzucone → alert z informacją
     │  ├─ Wszystkie pudełka Packed → PackBag (status → Packed)
     │  └─ PackingStatusLog zapis (Pending → Packed)
     │
     ▼
[Packing/TransportLabel/{id}] — druk etykiety transportowej
     │  → Nowy rekord PackingLabel (lista posiłków z DietVariantMeal)
     │  → QR z URL: {BaseUrl}/delivery/verify/{sessionId}
     │  → Status torby: Labeled
     │  → PackingStatusLog zapis (Packed → Labeled)
     │
     ▼
[Torba gotowa w strefie wydań]


=== MODUŁ B: ZAŁADUNEK AUT (ODRĘBNY PROCES) ===

[Loading/Index] — PackingManager widzi trasy/auta (karty)
     │
     ▼
[Loading/Route/{routeId}] — załadunek konkretnej trasy
     │  ├─ 🔍 SKAN QR etykiety torby → system weryfikuje:
     │  │   • Czy torba przypisana do tej trasy?
     │  │   • Czy torba ma status ≥ Labeled?
     │  │   → ✅ Dopuszczone → auto LoadBag (status → Loaded)
     │  │   → ❌ Odrzucone → alert z prawidłową trasą
     │  ├─ GenerateManifest → PackingManifest z UUID
     │  ├─ VerifyManifest → PackingManager potwierdza (profil = podpis)
     │  │   → Manifest AUTOMATYCZNIE widoczny w Driver/Manifest
     │  └─ DispatchDelivery → wszystkie torby: Dispatched
     │
     ▼
[Dostawa rusza w trasę — kierowca widzi manifest w Driver/Manifest]
```

---

## 6. STRUKTURA KONTROLERÓW I ROUTING

### 6.1 PackingController — Pakowanie toreb (REFAKTOR: usunąć Loading/Delivery)

```
GET  /packing                           → Index (lista toreb)
GET  /packing/bag-table                  → BagTable (partial HTMX)
GET  /packing/session/{sessionId}       → Session (kompletacja torby)
POST /packing/prepare-boxes/{sessionId} → PrepareBoxes
POST /packing/scan-box/{sessionId}      → ScanBox (skan + weryfikacja QR pudełka)
POST /packing/mark-packed/{itemId}      → MarkBoxPacked
POST /packing/pack-bag/{sessionId}      → PackBag
GET  /packing/transport-label/{sessId}  → TransportLabel (podgląd etykiety)
POST /packing/print-label/{sessionId}   → PrintLabel (druk / redruk → nowy PackingLabel)
GET  /packing/bulk-labels               → BulkLabels
POST /packing/bulk-print               → BulkPrint
```

### 6.2 LoadingController — Załadunek aut (NOWY)

> Uwaga z code review: istniejące akcje `Loading`, `Delivery`, `DispatchDelivery`, `LoadBag`, `ScanBag`, `GenerateManifest`, `VerifyManifest` z `PackingController` **zostaną przeniesione** do `LoadingController` i usunięte z `PackingController`.

```
GET  /loading                           → Index (karty tras)
GET  /loading/route/{routeId}           → Route (załadunek trasy)
POST /loading/scan-bag/{routeId}        → ScanBag (skan + weryfikacja QR torby)
POST /loading/load-bag/{routeId}        → LoadBag
POST /loading/generate-manifest/{rId}   → GenerateManifest
POST /loading/verify-manifest/{routeId} → VerifyManifest (+ auto do kierowcy)
GET  /loading/manifest-preview/{rId}    → ManifestPreview
GET  /loading/manifest-json/{routeId}   → ManifestJson (publiczny payload)
POST /loading/dispatch/{routeId}        → DispatchDelivery
```

### 6.3 DriverController — Widok kierowcy (NOWY)

```
GET  /driver/manifest                   → Manifest (mini widok, read-only)
GET  /driver/manifest-details/{rId}     → ManifestDetails (szczegóły)
```

---

## 7. WALIDACJA (FluentValidation)

| Request | Reguły |
|---|---|
| `ScanBoxRequest` | `BoxCode` not empty, format walidacja |
| `ScanBagRequest` | `BagCode` not empty, format walidacja |
| `PrintLabelRequest` | `SessionId > 0`, `ReprintReason` wymagany gdy `IsPrint > 0` |
| `BulkPrintRequest` | `SessionIds` not empty, max 50 na raz |
| `DispatchDeliveryRequest` | `RouteId > 0`, manifest zweryfikowany (server-side) |

---

## 8. TESTY

### 8.1 Testy jednostkowe

| Klasa testowa | Co testujemy |
|---|---|
| `PackingServiceTests` | `ScanBoxAsync` — weryfikacja zgodności pudełka z zamówieniem (dopuszczone vs odrzucone) |
| `LoadingServiceTests` | `ScanBagAsync` — weryfikacja zgodności torby z trasą |
| `PackingServiceTests` | `PackBagAsync` — zmiana statusu + log |
| `LoadingServiceTests` | `VerifyManifestAsync` — zapis VerifiedByUserId + auto widoczność dla kierowcy |
| `PackingLabelServiceTests` | `GenerateTransportLabelAsync` — budowanie MealsList z DietVariantMeal |

### 8.2 Testy integracyjne

| Test | Scenariusz |
|---|---|
| `ScanBox_WrongOrder_ReturnsError` | Skanowanie pudełka z innego zamówienia → odrzucenie |
| `ScanBag_WrongRoute_ReturnsError` | Skanowanie torby z innej trasy → odrzucenie |
| `VerifyManifest_DriverSeesIt` | Po weryfikacji — zapytanie kierowcy zwraca manifest |
| `Migration310-313` | Up + Down nowych migracji |

---

## 9. LOGI ANALITYCZNE

| Zdarzenie | Dane | Zastosowanie |
|-----------|------|--------------|
| `BoxScanned` | SessionId, BoxCode, wynik (OK/odrzucone), czas | Weryfikacja kompletności, statystyki błędów |
| `BoxPacked` | ItemId, PackedBy, czas | Wydajność pakowaczy |
| `BagPacked` | SessionId, OrderId, ile pudełek | KPI kompletacji |
| `LabelPrinted` | SessionId, PrintCount, QR, ReprintReason | Audit trail etykiet |
| `BagScanned` | RouteId, SessionId, wynik (OK/odrzucone), czas | Weryfikacja załadunku |
| `BagLoaded` | SessionId, RouteId, czas | Śledzenie załadunku |
| `ManifestGenerated` | RouteId, UUID, ile toreb | Audit załadunku |
| `ManifestVerified` | RouteId, VerifiedByUserId, kiedy | Odpowiedzialność |
| `DeliveryDispatched` | RouteId, ile toreb, DriverUserId | KPI logistyki |
| `StatusChanged` | SessionId, OldStatus, NewStatus, kto, kiedy | Pełna historia zmian |

---

## 10. ZMIANY W ISTNIEJĄCYM KODZIE

| Plik/Komponent | Zmiana | Priorytet |
|---|---|---|
| **Routing / Sidebar** | Rozdzielenie "Pakowanie" na: "Pakowanie toreb" (`/packing`) i "Załadunek aut" (`/loading`). Dodanie "Manifest" w sekcji kierowcy. | 🔴 Krytyczne |
| `PackingController.cs` | **REFAKTOR:** usunąć akcje Loading/Delivery/ScanBag/LoadBag/GenerateManifest/VerifyManifest/Dispatch | 🔴 Krytyczne |
| **NOWY:** `LoadingController.cs` | Przejęcie akcji załadunku z PackingController | 🔴 Krytyczne |
| **NOWY:** `DriverController.cs` | Mini widok manifestu dla kierowcy | 🟡 Średnie |
| `Packing/Index.cshtml` | Flat lista toreb, Public ID, filtry HTMX, Kitchen read-only | 🔴 Krytyczne |
| `Packing/Session.cshtml` | **Skanowanie QR pudełek z weryfikacją**, Public ID, SortOrder, nawigacja | 🔴 Krytyczne |
| `Packing/Labels.cshtml` | Refaktor → `TransportLabel.cshtml`, redruk → nowy rekord | 🔴 Krytyczne |
| **NOWY:** `Packing/BulkLabels.cshtml` | Hurtowy druk etykiet | 🟡 Średnie |
| **NOWY:** `Loading/Index.cshtml` | Pulpit załadunku — karty tras | 🔴 Krytyczne |
| **NOWY:** `Loading/Route.cshtml` | **Skanowanie QR toreb z weryfikacją**, manifest cyfrowy | 🔴 Krytyczne |
| **NOWY:** `Loading/ManifestPreview.cshtml` | Podgląd manifestu (backup druk A4) | 🟡 Wysokie |
| **NOWY:** `Driver/Manifest.cshtml` | Mini widok kierowcy | 🟡 Średnie |
| `_SidebarPartial.cshtml` | Nowe linki menu | 🔴 Krytyczne |
| Migracje 310-313 | Nowe kolumny i tabele | 🟡 Wysokie |
| `ICurrentUserService` | Interfejs (Domain) + impl (Infrastructure) — jeśli nie istnieje z Magazynu | 🟡 Wysokie |

---

## 11. PYTANIA OTWARTE — ROZSTRZYGNIĘTE

| # | Pytanie | Decyzja |
|---|---------|---------|
| 9.1 | Hurtowy druk etykiet (BulkLabels) | ✅ TAK — weryfikacja QR minimalizuje ryzyko pomyłek |
| 9.2 | Widok Loading/Index — karty czy tabela? | ✅ Karty tras |
| 9.3 | Alergeny na etykiecie transportowej? | ❌ NIE — alergeny na etykiecie foliowej pudełka |
| 9.4 | Manifest PDF? | ❌ NIE — manifest cyfrowy + opcjonalny druk HTML (backup) |
| 9.5 | Kierowca — pełny widok mobilny? | ❌ Mini podgląd zapełnienia manifestu (read-only) |
| 9.6 | Historia zmian statusu torby? | ✅ TAK — `PackingStatusLog` (log per status transition) |
| 9.7 | Potwierdzenie dostawy (verify)? | ❌ Osobny moduł — nie w zakresie kompletacji |
| DS-1 | Status `Packing` w enum? | ❌ Usunięty — zbędny |
| DS-2 | Redruk etykiet — podejście? | ✅ Nowy rekord `PackingLabel` per druk (spójne z `BoxLabel`) |
| DS-3 | Kitchen — read-only? | ✅ TAK — autoryzacja z rolą Kitchen |
| DS-4 | Skaner — obsługa Enter? | ✅ `hx-trigger="keydown[key=='Enter']"` + `preventDefault` |
| DS-5 | URL w QR — względny czy bezwzględny? | ✅ Bezwzględny: `{AppSettings.BaseUrl}/delivery/verify/{sessionId}` |
| DS-6 | `PayloadJson` — podpisy? | ❌ NIE — podpis = `VerifiedByUserId` (profil). PayloadJson = dane elektroniczne. |
| DS-7 | `MealsList` — skąd dane? | ✅ `PackingService` → `Order` → `DietVariant` → `DietVariantMeal` (SortOrder) |
| DS-8 | Refaktor PackingController | ✅ Przeniesienie akcji Loading/Delivery do nowego `LoadingController` |
