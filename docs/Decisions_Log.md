# Dziennik Decyzji i Nauka (Decisions Log)

W tym miejscu będą zapisywane kamienie milowe projektu wraz z szerszym wytłumaczeniem koncepcyjnym. Plik ten służy jako poradnik edukacyjny dla członków zespołu i stanowi odpowiedź na pytania: *Dlaczego? Jak używać? Co się pod tym kryje?*

---

## 1. Implementacja Obiektów Bazowych (BaseEntity & AuditableEntity)
**Data:** 2026-04-20
**Obszar:** `KuchniaUCygana.Domain.Common`

### 💡 Kontekst i dlaczego podjęto taką decyzję?
Podczas projektowania bazy dla Modułu 3 Magazynu (i docelowo całego ERP), musieliśmy ustanowić standard tworzenia Encji. Skonfrontowaliśmy proste podejście CRUD (gdzie encja to tylko wiersz z ID) za podejściem **Domain-Driven Design (DDD)**.
Ze względu na wymagania sanitarno-epidemiologiczne (HACCP) i potrzebę audytu zmian zadecydowano o przyjęciu ustrukturyzowanych wzorców DDD:
1. Skomponowaliśmy osobne interfejsy (tzw. _Composition over Inheritance_) dla tzw. logiki Soft Delete.
2. Zmieniliśmy tradycyjne `DateTime` na `DateTimeOffset` wspierające uwarunkowania stref czasowych po stronie chmury i serwera bazy.
3. Wdrożyliśmy bazowe porównywanie Encji stricte po ich `Id`, aby po dodaniu do np. słowników i potężnych zapytań system nie porównywał samej ścieżki pamięci lecz fizyczny byt w bazie.

### 🛠️ Jak to zaimplementowano i z czego się składa?

Zostały wdrożone 4 podstawowe filary w katalogu `Domain/Common`:

#### 1. `BaseEntity<TId>` oraz `BaseEntity`
*Rdzeń całego projektu.* Każda tabelka (Słownikowa, operacyjna) musi dziedziczyć z `BaseEntity<T>`. 
Klasa ta dostarcza:
* `Id` oraz daty powiązane `CreatedAt`, `UpdatedAt`.
* Wbudowane nadpisania operatorów (`==`, `!=`, `Equals()`), które sprawiają, że `Twaróg(Id:1) == Twaróg(Id:1)`, nawet jeśli dwa obiekty są pobrane we fragmentach innego pamięci.
* **DomainEvents** (`_domainEvents`) – wewnętrzna, ignorowana w bazie lista powiadomień rzucanych na zewnątrz.

#### 2. Interfejs `ISoftDeletable`
Definiuje on przymusowe atrybuty: `IsDeleted`, `DeletedAt` i `DeletedBy`. 
**Dlaczego interfejs?** Gdyż C# nie wspiera wielokrotnego dziedziczenia. Jeśli kiedykolwiek inna struktura wymagałaby tylko bycia SoftDeletable.

#### 3. `AuditableEntity<TId>`
Najważniejszy model dla ERP. Zamiast `BaseEntity` – tabele ruchome takie jak "Partia Towaru", "Dziennik Logowania Temperatury", "Zlecenie" będą po nim dziedziczyć.
Klasa powołuje do życia zasady śledzenia historii (Kto stworzył? Kto edytował? Zaimplementowano u niej automatycznie _Soft Delete_, co pozwala nam nigdy nie wymazywać z bazy (komenda DB `DELETE`) rekordów dla sanepidu, a zmieniać im flagę na `IsDeleted = true`).

#### 4. Użycie atrybutów `standardowych atrybutow .NET` w Domain
Aby nie ciągnąć zależności ORM-a takich jak sam `OrmLite`, ale posiadać nad klasami logikę dla silnika bazy np. `[PrimaryKey]` czy `[AutoIncrement]`, dograno cienką paczkę **`standardowe atrybuty .NET`**. Pozwala to utrzymać Czystą Architekturę, gdzie biblioteka domenowa jest maksymalnie bezinwazyjna, ale baza danych wie po czym ma utworzyć autoincrement.

### 📚 Jak to rozumieć i zapamiętać?
* Pomyśl o **`BaseEntity`** jak o pustym pojemniku, który dostaję metkę na taśmie produkcyjnej (ID + Data utworzenia). Idealny na encje słownikowe, stałe wartości (Typy Jednostek: *Kilogramy, Litry*).
* Pomyśl o **`AuditableEntity`** jak o pojemniku klasy VIP – posiada wbudowany nadajnik GPS i zawsze wiesz kto zamknął jego wieko (CreatedBy), kto je otwierał (UpdatedBy) i kto wrzucił do rzeki udając że go nie ma (mechanizm Soft Delete, DeletedBy). Użyj na wszystkim co jest modyfikowane przez człowieka w systemie operacyjnym (Transakcje, Statusy Diety, Dokumenty Wydania).

---

## 2. Architektura Magazynu wg Prawa Sanitarnego (Traceability)
**Data:** 2026-04-20
**Obszar:** `KuchniaUCygana.Domain.Entities.Warehouse`

### 💡 Kontekst i dlaczego podjęto taką decyzję?
Tworzenie Modułu 3 nakazało nam oparcie działania o ścisłe rygory polskiego Sanepidu oraz zaleceń procedur HACCP w cateringu. Gdy gotujesz tysiące posiłków, ser "po prostu jako X gram" nie jest legalnie identyfikowalny w przypadku wypuszczenia na rynek skażonego mleka przez hurtownię. 
Stwierdziliśmy użycie podejścia **Smart Inventory (FEFO)** z podziałem "pojęcia surowca" na "wzorzec" i jego "fizyczne dostawy". Pozwala to identyfikować kod dostawcy.

### 🛠️ Jak to zaimplementowano i z czego się składa?
Wdrożyliśmy bezinwazyjny dla nieistniejących jeszcze modułów układ 4 podstawowych struktur.

1. **`UnitOfMeasure`**: Najprostszy słownik (Litry, Kg, Sztuki). Dziedziczy po podstawowym `BaseEntity<int>`, ponieważ użutkownik po dodaniu np. ujęcia "Litr" nigdy nie będzie ani potrzebował Soft Delacji, ani skrupulatnej wiedzy, kto dodał słowo "Litr" (unikanie inwazyjności).
2. **`StockItem`**: Matryca/wzorzec surowca (np. "Udko z Kurczaka", "Sól"). Powiązany docelowy będzie Mockiem do `IngredientId` z Modułu 2. Posiada kluczowe parametry magazynowe jak `LeadTimeDays` (ile dni na dostawę) czy minimalny rygor dostępności. Dziedziczy z `AuditableEntity<int>`, bo edycja tych parametrów pociąga za sobą duże koszty operacyjne (śledzimy kto edytował parametry zamawiania). 
3. **`Batch` (Partia z FEFO)**: Tu dzieje się magia! Reprezentuje ona przyjęcie od konkretnego dostawcy 50kg Udka. Zawiera numer dostawcy i co najważniejsze datę przydatności. Ścieżki produkcyjne będą podpowiadały zużycie partii w której `IsDepleted` = false zaczynając od tyłu (FEFO). W razie skażenia wiemy do kogo trafił `BatchId = 14`.
4. **`InventoryTransaction`**: To nienaruszalny strażnik Sanepidu! Rejestruje logi z transakcji w konkretnej partii np. `Type = Waste (Strata)` i ilość w koszu, albo ile wydano na produkcję obiadu. Transakcje same sobie nie podlegają modyfikacjom historycznym z punktu widzenia Czystego Kodu i bazy (Immutability), dlatego nie są `AuditableEntity` a surowym `BaseEntity` generującym unikalny ślad audytowy.
5. **`TemperatureLog`**: Realizuje założenie stałej, żądanej przez organy nadzoru formy testów chłodni. Posiada podpis logującego i miarę w celcjuszach. Model `AuditableEntity`.

---

## 3. Kolejność Realizacji Modułów Projektu (Roadmap)
**Data:** 2026-04-27
**Obszar:** Cały projekt — `PLAN_PROJEKTU.md`

### Decyzja
Przyjęto 7-fazowy plan realizacji projektu:
1. Infrastruktura → 2. Domain Core → 3. Moduły 1, 2, 5 (równolegle) → 4. Moduł 3 → 5. Moduł 4 → 6. Integracja → 7. Frontend + Deploy

### Kontekst
Projekt składa się z 5 modułów domenowych z wyraźnymi zależnościami kierunkowymi. Niepoprawna kolejność realizacji prowadziłaby do blokad (np. próba wygenerowania planu produkcji bez istniejącego systemu zamówień).

### Alternatywy
- **Realizacja wszystkich modułów równolegle od dnia 1:** Odrzucono — powoduje duplikację interfejsów, niespójne encje bazowe, brak wspólnego fundamentu (BaseEntity, IRepository). Każdy deweloper tworzyłby własne rozwiązania tych samych problemów.
- **Realizacja modułów sekwencyjnie (1→2→3→4→5):** Odrzucono — wydłuża timeline x3, blokuje deweloperów M2/M5 na tygodnie. Moduły 1, 2, 5 są wystarczająco niezależne, by pracować równolegle.

### Konsekwencje
- **Pozytywne:** Fazy 0+1 tworzą wspólny fundament. Moduły 1/2/5 mogą pracować równolegle (3 deweloperów jednocześnie). Moduł 3 ma czas na dojrzenie interfejsów M1/M2. Minimalizacja re-worku.
- **Negatywne:** Moduł 3 musi pracować z mockami M1/M2 przez pierwsze tygodnie. Moduł 4 zaczyna najpóźniej. Wymaga dyscypliny w definiowaniu interfejsów (kontrakty między modułami).

---

## 4. Strategia Mocków Międzymodułowych
**Data:** 2026-04-27
**Obszar:** `KuchniaUCygana.Domain.Interfaces.External`

### Decyzja
Interfejsy kontraktów między modułami (`IOrderDataProvider`, `IDietDataProvider`, `IDeliveryManifestProvider`) definiowane w warstwie Domain. Implementacje mock w Domain/Mocks (tymczasowo), docelowe adaptery w Infrastructure (po integracji FAZY 5).

### Kontekst
Moduł 3 (Produkcja) potrzebuje danych z Modułu 1 (zamówienia) i Modułu 2 (receptury), ale te moduły mogą nie być gotowe w momencie rozpoczęcia pracy nad M3. Potrzebna strategia umożliwiająca niezależny rozwój.

### Alternatywy
- **Twarde zależności na gotowe repozytoria M1/M2:** Odrzucono — blokuje M3 do ukończenia M1/M2.
- **Duplikacja encji M1/M2 wewnątrz M3:** Odrzucono — naruszenie DRY, problemy przy integracji.
- **Interfejsy + Mocki + późniejsza podmiana (wybrane ✅):** Czyste podejście DIP/Clean Architecture. Zamiana implementacji w DI bez zmian w logice biznesowej.

### Konsekwencje
- **Pozytywne:** M3 może pracować natychmiast. Interfejsy wymuszają precyzyjne kontrakty. Podmiana mock→adapter to 1-liniowa zmiana w DI.
- **Negatywne:** Dane z mocków mogą nie pokrywać edge cases (np. puste zamówienia, diety z alergonami). Wymaga synchronizacji kontraktów z deweloperami M1/M2.

---

## 5. Podział Modułu 3 na Podsystemy (Produkcja / Kompletacja / Smart Inventory / HACCP)
**Data:** 2026-04-27
**Obszar:** `PLAN_MODUL_3.md`

### Decyzja
Moduł 3 został podzielony na 4 logiczne podsystemy z osobnymi namespace'ami, serwisami i kontrolerami: Produkcja (Kuchnia), Kompletacja (Załadunek), Smart Inventory (Magazyn FEFO), Nadzór HACCP.

### Kontekst
Use case diagram definiuje ~20 przypadków użycia dla M3, obsługiwanych przez 3 aktorów (Szef Kuchni, Magazynier, System). Umieszczenie całości w jednym serwisie/kontrolerze stworzyłoby monolityczną klasę >1000 linii.

### Alternatywy
- **Jeden `Module3Service` z wszystkim:** Odrzucono — God Object, nietestowalna, niezrozumiała.
- **Podział na 2 (Kuchnia, Magazyn):** Rozważano — za gruby podział, kompletacja i HACCP to odrębne konteksty od surowego magazynu.
- **Podział na 4 podsystemy (wybrane ✅):** Granulacja odpowiada podziałowi aktorów i odpowiedzialności z diagramu use case.

### Konsekwencje
- **Pozytywne:** Czytelna struktura. Każdy serwis <200 linii. Role `[Authorize]` można przypisać precyzyjnie (np. Kitchen vs Admin). Testy jednostkowe skupione.
- **Negatywne:** Więcej plików i rejestracji DI. Potencjalne współdzielenie stanu (np. stan partii wpływa na produkcję i kompletację) — rozwiązane przez wspólne repozytoria.

---

## 6. Algorytm FEFO w Repozytorium vs. Serwis Aplikacyjny
**Data:** 2026-04-27
**Obszar:** `KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse`

### Decyzja
Zapytania FEFO (sortowanie partii wg daty ważności) wykonywane w repozytorium (`BatchRepository.GetAvailableFefoAsync()`). Logika decyzyjna (ile zdjąć z którego batcha) w serwisie domenowym (`FefoService`).

### Kontekst
FEFO (First Expired, First Out) to kluczowy algorytm magazynowy wymagany przez regulacje sanitarne. Wymaga pobrania dostępnych partii posortowanych wg `ExpiryDate ASC`, a następnie zdejmowania z nich ilości do wyczerpania zapotrzebowania.

### Alternatywy
- **Cały FEFO w serwisie (pobranie wszystkich partii → sortowanie w pamięci):** Odrzucono — przy dużych wolumenach nieefektywne, ryzyko race condition na stanie magazynowym.
- **Cały FEFO w SQL (stored procedure):** Odrzucono — SQLite nie wspiera stored procedures, migracja utrudniona, logika ukryta.
- **Hybrid: SQL sortowanie + C# logika (wybrane ✅):** Baza robi to w czym jest dobra (sortowanie, filtrowanie). Serwis wykonuje logikę biznesową (ile zdjąć, co zrobić przy braku).

### Konsekwencje
- **Pozytywne:** Wydajne zapytania (SQLite robi ORDER BY). Logika biznesowa testowalna unitowo. Czytelny podział odpowiedzialności.
- **Negatywne:** Wymaga SQLite in-memory w testach integracyjnych repozytorium. Dwa miejsca do zrozumienia algorytmu.

---

## 7. Osobny Namespace dla Encji Kompletacji (Packing)
**Data:** 2026-04-27
**Obszar:** `KuchniaUCygana.Domain.Entities`

### Decyzja
Encje kompletacji (`PackingSession`, `PackingItem`, `PackingLabel`) umieszczone w osobnym namespace `Entities/Packing/`, a nie w istniejącym `Entities/Warehouse/`.

### Kontekst
Kompletacja (pakowanie diet klientów, etykietowanie, załadunek) jest logicznie powiązana z magazynem, ale operacyjnie obsługiwana przez innego aktora (Magazynier przy załadunku vs Szef Kuchni przy produkcji) i dotyczy innego poziomu abstrakcji (paczki klienckie vs surowce magazynowe).

### Alternatywy
- **Wszystko w `Entities/Warehouse/`:** Odrzucono — miesza surowce (Batch, StockItem) z produktami końcowymi (paczki dla klientów). Namespace rośnie do >10 encji z różnymi odpowiedzialnościami.
- **Encje w `Entities/Production/`:** Odrzucono — kompletacja następuje PO produkcji, to nie to samo.
- **Osobny `Entities/Packing/` (wybrane ✅):** Czytelna granica. Repozytorium `IPackingSessionRepository` operuje na innym zbiorze tabel niż `IBatchRepository`.

### Konsekwencje
- **Pozytywne:** Separacja kontekstów. Łatwiejsze code review. Potencjalna możliwość wyodrębnienia jako osobny bounded context.
- **Negatywne:** Więcej folderów w projekcie. Potrzebne referencje między namespace'ami (PackingItem → Batch).

---

## 8. Serwisy Domenowe w Domain vs. cała Logika w Application
**Data:** 2026-04-27
**Obszar:** `KuchniaUCygana.Domain.Services`, `KuchniaUCygana.Application.Services`

### Decyzja
Kluczowe algorytmy biznesowe (`FefoService`, `FoodCostCalculator`, `SmartInventoryAnalyzer`, `ProductionPlanGenerator`) umieszczone jako serwisy domenowe w warstwie Domain. Orkiestracja (wywoływanie repozytoriów + serwisów + mapowanie) w serwisach Application.

### Kontekst
Clean Architecture mówi, że Domain nie powinien mieć zależności na Infrastructure. Jednocześnie algorytmy takie jak FEFO czy kalkulacja Food Cost to czysta logika domenowa, niezależna od bazy danych. Umieszczenie ich w Application mieszałoby orkiestrację z logiką biznesową.

### Alternatywy
- **Wszystko w Application Services:** Odrzucono — serwis aplikacyjny staje się „God Service" łączący logikę DB z logiką biznesową. Trudno testować bez mockowania repozytoriów.
- **Logika w encjach (Rich Domain Model):** Rozważano — zbyt inwazyjne dla obecnego projektu opartego na OrmLite (encje muszą być proste POCO). Encje nie mają dostępu do repozytoriów.
- **Serwisy domenowe + serwisy aplikacyjne (wybrane ✅):** Serwisy domenowe przyjmują czyste dane (listy, wartości), nie znają repozytoriów. Serwisy aplikacyjne pobierają dane, wywołują serwisy domenowe, zapisują wyniki.

### Konsekwencje
- **Pozytywne:** `FefoService` testowalne bez żadnego mocka (pure function-like). Application Services mają jasny wzorzec: pobierz → przetworz → zapisz. Zgodność z DDD tactical patterns.
- **Negatywne:** Więcej klas i interfejsów. Nowi deweloperzy muszą zrozumieć podział Domain Service vs Application Service.

---

## 9. Wybór QuestPDF do Generowania Dokumentów Modułu 3
**Data:** 2026-04-27
**Obszar:** `KuchniaUCygana.Infrastructure.Pdf`

### Decyzja
Użycie QuestPDF do generowania Kart Produkcyjnych, Kart Gotowania, Etykiet Paczkowych i Raportów Braków w Module 3.

### Kontekst
Moduł 3 wymaga generowania 4 typów dokumentów PDF: karty produkcyjnej (tabela posiłków na dzień), zbiorczej karty gotowania, etykiet na paczki klienckie (z QR kodem) i raportów braków magazynowych. Potrzebne narzędzie do programistycznego generowania PDF w .NET.

### Alternatywy
- **iTextSharp / iText7:** Odrzucono — licencja AGPL wymaga open-source'owania projektu lub zakupu licencji komercyjnej.
- **PdfSharpCore:** Odrzucono — niskopoziomowy API, brak wsparcia dla tabel i layoutu, dużo boilerplate.
- **QuestPDF (wybrane ✅):** Darmowy dla projektów community (akademicki). Fluent API, wbudowane tabele, layouty. Już skonfigurowany w projekcie (`LicenseType.Community`).

### Konsekwencje
- **Pozytywne:** Czytelny, deklaratywny API (`page.Content().Table(...)`) minimalizuje błędy. Hot-reload dokumentów w development. Duża społeczność i dokumentacja.
- **Negatywne:** Zależność od jednego vendora. Licencja Community wymaga publicznego repo lub <1M$ przychodu. QR Code wymaga dodatkowej paczki (np. `QRCoder`).
