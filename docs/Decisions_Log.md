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

#### 4. Użycie atrybutów `ServiceStack.DataAnnotations` w Domain
Aby nie ciągnąć zależności ORM-a takich jak sam `OrmLite`, ale posiadać nad klasami logikę dla silnika bazy np. `[PrimaryKey]` czy `[AutoIncrement]`, dograno cienką paczkę **`ServiceStack.Interfaces`**. Pozwala to utrzymać Czystą Architekturę, gdzie biblioteka domenowa jest maksymalnie bezinwazyjna, ale baza danych wie po czym ma utworzyć autoincrement.

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
