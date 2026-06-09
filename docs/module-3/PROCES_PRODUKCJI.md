# Proces Produkcji, Kompletacji i Załadunku — Opis Biznesowy

> **Wersja:** 1.0 | **Data:** 2026-05-11 | **Moduł:** 3

---

## 1. Przegląd Procesu — Od Zamówienia do Dostawy

```
DZIEŃ D-1 (wieczór):
  22:00 → System auto-generuje Plan Produkcji na dzień D
       → System oblicza Food Cost (zapotrzebowanie materiałowe)
       → System sprawdza stany magazynowe (FEFO)
       → Jeśli braki → alert do zaopatrzeniowca

DZIEŃ D (rano, ~4:00-6:00):
  Szef Kuchni → otwiera Plan Produkcji w web (dashboard)
             → drukuje Zbiorczą Kartę Gotowania (QuestPDF)
             → gotuje wg karty
             → po ugotowaniu: zatwierdza pozycje w systemie
             → System automatycznie zdejmuje składniki FEFO
             → System porcjuje i drukuje Etykiety Produktowe (folia)

DZIEŃ D (późne rano, ~8:00-10:00):
  Magazynier → kompletuje paczki (torby) per klient
            → skanuje pudełka do torby
            → System drukuje Etykietę Wysyłkową (QR + trasa + auto)
            → ładuje torby do aut wg LIFO (ostatni stop = pierwszy załadowany)
            → zatwierdza wydanie → Manifest Załadunkowy (PDF dla kierowcy)

DZIEŃ D (dostawa):
  Kierowca → skanuje QR przy rozładunku → potwierdza dostawę
```

---

## 2. Aktorzy i Ich Akcje

### 2.1 System (Zadanie cykliczne)

| Akcja | Kiedy | Dane wejściowe | Dane wyjściowe |
|-------|-------|-----------------|-----------------|
| Generuj Plan Produkcji | D-1 o 22:00 | Zamówienia (M1) + Receptury (M2) | `ProductionPlan` ze statusem Draft |
| Oblicz Food Cost | Razem z planem | Receptury × ilości | Lista składników + braki |
| Sprawdź stany FEFO | Razem z planem | Stan magazynu (Batches) | Alerty o brakach |
| Wyślij alert braków | Gdy braki | Raport braków | Powiadomienie email/web |
| Audyt dat ważności | Codziennie 6:00 | Batches.ExpiryDate | Alerty przeterminowania |

### 2.2 Szef Kuchni

| Akcja | Gdzie | Co robi | Efekt w systemie |
|-------|-------|---------|------------------|
| Przegląd planu | Web: `/Production` | Widzi tabelę posiłków do ugotowania | — |
| Druk karty gotowania | Web → PDF | Klika "Drukuj kartę" → QuestPDF | Pobranie PDF |
| Rozpocznij gotowanie | Web: checkbox/przycisk | Zmienia status pozycji na "Cooking" | `ProductionItemStatus.Cooking` |
| Zatwierdź ugotowanie | Web: formularz | Wpisuje ugotowaną ilość | System zdejmuje składniki FEFO |
| Zgłoś problem | Web: formularz | Wpisuje powód (np. "spalony sos") | `ProductionItemStatus.Failed` |
| Druk etykiet produktowych | Web → PDF (folia) | Klika "Drukuj etykiety" dla pozycji | Etykiety na folię A4/termiczna |

### 2.3 Magazynier

| Akcja | Gdzie | Co robi | Efekt w systemie |
|-------|-------|---------|------------------|
| Rozpocznij sesję pakowania | Web: `/Packing` | Widzi listę zamówień do skompletowania | `PackingSession` created |
| Kompletuj torbę klienta | Web: skanowanie | Skanuje/wybiera pudełka pasujące do diety | `PackingItem` + weryfikacja |
| Druk etykiety wysyłkowej | Web → PDF (folia) | System generuje etykietę z trasą | QR + auto + kolejność |
| Załadunek do auta | Web: skan QR torby | Skanuje torby wg kolejności LIFO | Status → Loaded |
| Zatwierdź wydanie | Web: przycisk | Manifest załadunkowy gotowy | PDF dla kierowcy |
| Przyjmij dostawę | Web: `/Warehouse/Delivery` | Wpisuje partie, ilości, daty ważności | Nowe Batches w FEFO |

---

## 3. Plan Produkcji — Dokument i Workflow

### 3.1 Czym jest Plan Produkcji?

Plan Produkcji to **dokument roboczy** istniejący w dwóch formach:

| Forma | Cel | Interaktywność |
|-------|-----|----------------|
| **Web (dashboard)** | Praca operacyjna kucharza — zatwierdzanie, statusy | ✅ Pełna — checkboxy, formularze, statusy real-time |
| **PDF (QuestPDF)** | Druk na kuchnię — odporny na wilgoć, tłuszcz | ❌ Brak — wypełniany ręcznie długopisem |

### 3.2 Struktura Planu w Web

```
┌─────────────────────────────────────────────────────────┐
│  PLAN PRODUKCJI — 2026-05-12 (Poniedziałek)            │
│  Status: Active │ Wygenerowany: 2026-05-11 22:00       │
├─────────────────────────────────────────────────────────┤
│ # │ Posiłek           │ Wariant │ Plan │ Ugot │ Status  │
│───┼───────────────────┼─────────┼──────┼──────┼─────────│
│ 1 │ Zupa pomidorowa   │ 1500cal │  45  │  45  │ ✅ Done │
│ 2 │ Kurczak grillowany│ 2000cal │  32  │  30  │ ⚠️ -2   │
│ 3 │ Sałatka grecka    │ 1200cal │  28  │   0  │ 🔲 Plan │
│ 4 │ Risotto grzybowe  │ 1800cal │  18  │   0  │ 🔲 Plan │
├─────────────────────────────────────────────────────────┤
│ [Drukuj Kartę Gotowania]  [Drukuj Etykiety]  [Zatwierdź]│
└─────────────────────────────────────────────────────────┘
```

### 3.3 PDF — Karta Gotowania (do druku)

QuestPDF generuje kartę A4 z:
- Nagłówek: data, numer planu, Szef Kuchni
- Tabela posiłków z kolumnami: Danie | Ilość | Składniki (z gramatury) | Uwagi
- Puste pole "Ugotowano: ___" do ręcznego wypełnienia
- Puste pole "Podpis: ___"
- Stopka: czas wydruku, numer strony

**Dlaczego druk?** W kuchni cateringowej tablet/komputer narażony jest na wilgoć i tłuszcz. Karta papierowa to standard branżowy. Kucharz wypełnia ręcznie, a potem aktualizuje system w web.

### 3.4 Jak wypełniany jest plan?

```
1. Kucharz otwiera web → widzi plan z ilościami
2. Drukuje kartę gotowania (PDF) → laminuje lub wkłada do koszulki
3. Gotuje wg karty — zapisuje ręcznie ile ugotował
4. Po skończeniu → wraca do web → wpisuje ugotowane ilości
5. Klika "Zatwierdź" → System:
   a) zmienia status na Cooked
   b) uruchamia FEFO — zdejmuje składniki z magazynu
   c) tworzy PackingItems (pudełka) gotowe do kompletacji
   d) generuje etykiety produktowe do druku na folię
```

---

## 4. Kierunek Przepływu: M3 → M4 (Model B-lite — etapowy rozwóz)

### 4.1 Strategia: Plan z ETA → Trasy → Stopniowy Wyjazd

System udostępnia plan produkcji do M4 **tuż po wygenerowaniu** (D-1, 22:05)
lub **po zatwierdzeniu przez Szefa Kuchni** (D, rano).
Plan zawiera **grupy produkcyjne z szacowanym czasem gotowości (ETA)**,
co pozwala M4 zaplanować trasy uwzględniające czas produkcji.

```
22:00  System → generuje Plan z grupami + ETA per grupa
22:05  M3 → M4: Plan na jutro (4 grupy: ETA 06:30, 07:30, 08:30, 09:00)
       M4 planuje trasy z uwzględnieniem ETA + bufor 30 min

05:00  Szef Kuchni zatwierdza plan (lub auto-aktywacja)
       Korekty → event "plan zaktualizowany" → M4

07:00  Grupa 1 (zimne/śniadania) gotowa → pakowanie → Auto 1 wyjeżdża
08:00  Grupa 2 (zupy) gotowa → pakowanie → Auto 2 wyjeżdża
09:00  Grupa 3+4 (główne, sałatki) gotowe → Auto 3-4 wyjeżdżają
```

### 4.2 Grupy Produkcyjne

| Grupa | Typ dań | Szacowany ETA | Bufor |
|-------|---------|---------------|-------|
| 1 | Śniadania, dania zimne, przekąski | 06:30 | +30 min |
| 2 | Zupy, buliony | 07:30 | +30 min |
| 3 | Dania główne (ciepłe) | 08:30 | +30 min |
| 4 | Sałatki, desery (świeżość!) | 09:00 | +30 min |

Pola w encji `ProductionPlanItem`:
- `ProductionGroup` (int?) — numer grupy 1-4
- `EstimatedReadyTime` (TimeOnly?) — szacowany czas gotowości
- `ActualReadyTime` (TimeOnly?) — rzeczywisty czas (po zatwierdzeniu)

Pola w encji `ProductionPlan`:
- `IsSharedWithLogistics` (bool) — czy wysłano do M4
- `SharedAt` (DateTimeOffset?) — kiedy wysłano

### 4.3 Przepływ danych między modułami

| Krok | Kierunek | Co się dzieje |
|------|----------|---------------|
| 1 | M1 → M3 | Zamówienia na jutro → plan produkcji |
| 2 | M2 → M3 | Receptury → food cost + karty gotowania |
| 3 | **M3 → M4** | **Plan z ETA grup → M4 planuje trasy z buforami** |
| 4 | M4 → M3 | Trasy + auta + stopy → przypisanie do PackingSessions |
| 5 | M3 → M3 | Gotowanie → zatwierdza grupę → pakowanie etapowe |
| 6 | M3 → M4 | Event "Grupa X gotowa" → auto może wyruszać |
| 7 | M3 → M4 | Manifest załadunkowy → kierowca rusza |

### 4.4 Fallback

Jeśli produkcja się opóźni (danie Failed, opóźnienie > bufor):
- Auto czeka max 30 min, potem jedzie z tym co ma
- Brakujący posiłek → powiadomienie klienta
- System przełącza się na **Model A** (wszystko naraz) jako fallback

**Kluczowy punkt:** Kolejność gotowania wynika z grupy produkcyjnej, nie z tras.
M4 wpływa na **etap pakowania** — kolejność pakowania torb i załadunku do aut.

---

## 5. Etykiety — Dwa Typy

### 5.1 Etykieta Produktowa (na pudełko/folię)

Drukowana po ugotowaniu, naklejana na folię pudełka.

```
┌──────────────────────────────────┐
│  🍽️ Kurczak grillowany           │
│  Wariant: 2000 kcal              │
│  ──────────────────────────────  │
│  Alergeny: GLUTEN, SEZAM         │
│  Data ważności: 2026-05-13       │
│  Partia: BATCH-2026-05-12-007    │
│  ──────────────────────────────  │
│  [QR: M3-PI-00342]              │
│  Kuchnia u Cygana                │
└──────────────────────────────────┘
```

**Format:** 70×50mm, druk termiczny lub na folii A4 (wiele etykiet na arkuszu)
**Cel:** Informacja dla klienta + HACCP traceability (nr partii)

### 5.2 Etykieta Wysyłkowa (na torbę)

Drukowana podczas kompletacji, po przypisaniu do trasy.

```
┌──────────────────────────────────────┐
│  📦 TORBA #T-20260512-0089          │
│  ════════════════════════════════    │
│  Klient: Jan Kowalski               │
│  Dieta: 2000 kcal, 5 posiłków      │
│  ────────────────────────────────    │
│  🚗 Auto: WA 12345                  │
│  📍 Trasa: Mokotów-Południe         │
│  🔢 Stop: 7 / 15                    │
│  ⏰ Okno: 11:00 - 12:00             │
│  ────────────────────────────────    │
│  ████████████████  [duży QR code]    │
│  ████████████████                    │
│  ████████████████                    │
│  ────────────────────────────────    │
│  Załadunek: jako 9. (LIFO → stop 7) │
└──────────────────────────────────────┘
```

**Format:** 100×70mm, druk termiczny
**Cel:**
- QR skanowany przez magazyniera przy załadunku
- QR skanowany przez kierowcę przy rozładunku
- Numer auta + kolejność ładowania widoczne na pierwszy rzut oka
- LIFO: stop 15 ładowany pierwszy (na dno), stop 1 ostatni (na wierzch)

### 5.3 Druk na Folię (QuestPDF)

QuestPDF generuje PDF z siatką etykiet:
- Etykiety produktowe: 3×5 = 15 etykiet na A4
- Etykiety wysyłkowe: 2×3 = 6 etykiet na A4
- Drukarka: standardowa laserowa + folia samoprzylepna A4
- Alternatywa: drukarka termiczna (Zebra/DYMO) na rolkę

---

## 6. Pakowanie — Szczegółowy Przepływ

### 6.1 Skąd wiemy co pakować?

```
System pobiera:
  1. Zamówienia na dziś (M1: IOrderDataProvider)
     → Klient X ma dietę 2000cal, 5 posiłków
  2. Plan diet na dziś (M2: IDietDataProvider)
     → Dieta 2000cal na dziś = [Śniadanie, II Śn., Obiad, Podwieczorek, Kolacja]
  3. Gotowe pudełka z produkcji (M3: PackingItems z BatchId)
     → 45× Zupa pomidorowa, 30× Kurczak, 28× Sałatka...
  4. Trasy na dziś (M4: IDeliveryManifestProvider)
     → Trasa "Mokotów-Południe", Auto WA 12345, Stop 7 = Klient X
```

### 6.2 Algorytm kompletacji

```
DLA KAŻDEGO zamówienia:
  1. Utwórz PackingSession (= Torba) dla klienta
  2. DLA KAŻDEGO posiłku w diecie klienta:
     a. Znajdź pasujące pudełko (MealId + DietVariantId)
     b. Weryfikuj zgodność (czy to właściwy posiłek?)
     c. Przypisz pudełko do torby (PackingItem → PackingSession)
  3. Pobierz dane trasy z M4 (RouteId, StopNumber, Vehicle)
  4. Wygeneruj Etykietę Wysyłkową z QR i danymi trasy
  5. Status torby → Packed → Labeled
```

### 6.3 Załadunek LIFO

```
Trasa "Mokotów-Południe" ma 15 stopów:
  Stop 1  → pierwszy rozładunek  → ładowany OSTATNI  (na wierzch)
  Stop 7  → Klient X             → ładowany jako 9.
  Stop 15 → ostatni rozładunek   → ładowany PIERWSZY (na dno)

Magazynier widzi na ekranie:
  "Załaduj torbę T-0089 jako 9. do auta WA 12345"

System weryfikuje kolejność:
  → sprawdzLIFO() → czy torba nr 9 jest ładowana po torbie nr 10?
```

---

## 7. QR Kody — Zastosowanie

| Etykieta | Zawartość QR | Kto skanuje | Kiedy |
|----------|-------------|-------------|-------|
| Produktowa | `M3-PI-{PackingItemId}` | Magazynier | Przy kompletacji → weryfikacja pudełka |
| Wysyłkowa | `M3-PS-{PackingSessionId}` | Magazynier | Przy załadunku → potwierdzenie na manifest |
| Wysyłkowa | `M3-PS-{PackingSessionId}` | Kierowca | Przy rozładunku → potwierdzenie dostawy |

**Skanowanie w web:** Kamera telefonu/tablet → przeglądarka czyta QR → redirect do `/Packing/Verify/{id}` lub `/Delivery/Confirm/{id}`

---

## 8. Podsumowanie Dokumentów PDF (QuestPDF)

| # | Dokument | Kiedy generowany | Kto używa | Format |
|---|----------|------------------|-----------|--------|
| 1 | Karta Gotowania | Po wygenerowaniu planu | Szef Kuchni | A4, druk na papier |
| 2 | Etykiety Produktowe | Po zatwierdzeniu ugotowania | Magazynier/Kucharz | Folia A4 lub termiczna |
| 3 | Etykiety Wysyłkowe | Podczas kompletacji | Magazynier | Folia A4 lub termiczna |
| 4 | Manifest Załadunkowy | Po zatwierdzeniu załadunku | Kierowca | A4, druk na papier |
| 5 | Raport Braków | Gdy brak składników | Zaopatrzeniowiec | A4 / email |
