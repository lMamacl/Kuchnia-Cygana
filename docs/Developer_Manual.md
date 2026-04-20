# Developer Manual - Kuchnia U Cygana

Witaj w podręczniku technicznym dla Deweloperów systemu ERP Platformy Cateringowej.

## Nawigacja po Dokumentacji
Poniżej znajdziesz szybkie linki do innych kluczowych dokumentów określających pracę w środowisku projektu:
* [ARCHITEKTURA PROJEKTU](./ARCHITEKTURA_KuchniaUCygana.html) - Definicje bazy, Clean Architecture (Plik HTML).
* [DZIENNIK DECYZJI (Decisions Log)](./Decisions_Log.md) - Poradnik z wytłumaczeniem "Dlaczego używamy takiej technologii, a nie innej i jak ona pod spodem działa" (Szczególnie DDD, Soft Delete).
* [MODUŁ 3: Produkcja i Magazyn](./module%203/Modu%C5%823.pdf) - Założenia domenowe modułu nr 3.

---

## 🏗️ Status Prac i Najważniejsze Komponenty

Obecnie skupiamy się na **Module 3 (Magazyn, Produkcja)** i izolacji go, wplatając zasady bezpiecznych operacji sanitarno-epidemiologicznych i audytu danych (środowisko budowane na SQLite + ServiceStack.OrmLite, bezinwazyjne dla niezbudowanych jeszcze zewnętrznych modułów e-commerce za pomocą Mocków).

Poniżej przegląd stworzonych lub modyfikowanych do tej pory rozwiązań:

### Komponent 1: Obiekty Bazowe i DDD (`Domain/Common/`)
Skonfigurowano tzw. szkielet relacyjny bazy dla ServiceStack.OrmLite pod założenia Domain-Driven Design we wnętrzu *Clean Architecture*.

#### 🔸 `BaseEntity<T>` (Domain/Common/BaseEntity.cs)
**Dla kogo i Kiedy?** Stosuj, gdy tworzysz proste obiekty słownikowe w których człowiek ich nie aktualizuje (np. Tabela z nazwami Państw, Tabela Jednostek Miar - gramy/litry, Tagowanie).
**Co zawiera?** Primary Key (`Id`), nadpisane operatory do porównania by identyfikować w całości obiekt przy listach oraz pola tworzenia `CreatedAt` w bezpiecznym standardzie strefowym świata z `DateTimeOffset`.

#### 🔸 `AuditableEntity<T>` (Domain/Common/AuditableEntity.cs)
**Dla kogo i Kiedy?** Stosuj ZAWSZE dla ruchomych danych biznesowych tworzonych/edycje przez pracownika i klienta. Jeśli encją jest Transakcja, Zlecenie Produkcyjne, Karta Magazyniera lub Dodana Partia Kurczaka do chłodni... Używamy *AuditableEntity*!
**Co wpierają dodatkowo?**
- Znaczniki `CreatedBy` i `UpdatedBy` określające kto kliknął przycisk (śledzenie akcji w celach audytu/błędu ludzkiego).
- Implementacja **Soft Delete**! (dziedziczy interfejs z `Domain/Common/Interfaces/ISoftDeletable.cs`). To znaczy, że nie używasz hardkore'owego `DELETE FROM Tabelka` w OrmLite, lecz ustawiasz flagę `IsDeleted = true; DeletedBy = {pracownik_id}` zachowując ciągłość rekordów dla bazy historycznej Sanepidu.

#### 🔸 `IDomainEvent`
Sygnalizator (Marker w `Domain/Common/Events/`). Jeśli wystąpi skompresowana akcja, będziemy nim w łatwy sposób nasłuchiwali i odpalali handler, by np. notyfikować wszystkie systemy ("Uwaga, partia nr 00923 skończyła ważność!"). 

### Komponent 2: Modele Bazy Magazynu (Smart Inventory / FEFO) z uwzgl. Prawa Sanitarnego (`Domain/Entities/Warehouse/`)
Powołane nowe Encje definiują fizyczny i ewidencyjny obraz funkcjonowania Magazynu, odporny na braki zewnętrznych modułów:
- `UnitOfMeasure`: Prosty słownik (BaseEntity). Zawiera koncepcję logistyczną (symbol i nazwa jednostki np. *L*, *Litr*).
- `StockItem`: Składnik fizyczny z alertami do zamawiania (AuditableEntity). Określa limit (`MinimumLevel`) rzucający alert przy przekroczeniu, oraz `LeadTimeDays` (ile dni czekamy na hurtownika).
- `Batch`: Zastosowane tu prawo FEFO. Dostawy nie wchodzą fizycznie w surowiec sam w sobie, tylko tworzą obiekty "Partii" (Batches). Posiadają kluczową dla algorytmu `ExpiryDate` (Ważność) oraz `IsDepleted` (Zatwierdzono do wyczerpania).
- `InventoryTransaction`: Zabezpiecza proces audytorny przed modyfikacjami - to Immutable Transaction Log tworzony z BaseEntity w oparciu o typ (`InventoryTransactionType` w Enumach np. Receipt, Waste, ProductionIssue).
- `TemperatureLog`: Zabezpieczenie na wypadek audytu Sanepidu ze stałej kontroli temperatury w punktach logistycznych (AuditableEntity).

---

## 📝 Zasady Zgłaszania Commitów i Czystego Kodu
Poniżej znajdują się standardy dodawania zmian związanych z powyższymi elementami:

**Proponowana struktura flag commitów (np. zgodnie z Conventional Commits):**
* `feat:` (Nowa funkcja, np. Utworzono encję dla dostawców magazynowych która dziedziczy po AuditableEntity).
* `fix:` (Rozwiązanie błędu produkcyjnego).
* `refactor:` (Przepisanie części kodu zgodnie ze standardami architektonicznymi np. zmiana `DateTime` na `DateTimeOffset`).
* `docs:` (Wszelakie zmiany w dokumentacji np. Dzienniku decyzji, diagramach UML, wewnątrz /docs).
* `test:` (Pokrywanie unit testami koncepcji baz danych w xUnit).
* `chore:` (Aktualizacje np. pliki typu `.gitignore` i paczki NuGet - m.in. dostarczenie `ServiceStack.Interfaces`).

Przykład Commita na obecny stan dla gita po zrealizowaniu zadań:
`feat: Wdrożono zasady DDD oraz klasy BaseEntity i AuditableEntity`
`chore: Zaktualizowano reguły pliku .gitignore pod konwersje rozszerzeń (txt)`


## ??? Baza Danych: Migracje (FluentMigrator)

W utworzonej warstwie \Infrastructure/Persistence/Migrations/\ powo�ano migracje schemat�w, wykorzystuj�c klas� \CreateWarehouseTables\. Podczas tworzenia tabel opartych o \AuditableEntity\ zawsze pami�taj o r�cznym rzutowaniu kolumn: \CreatedBy, UpdatedBy, IsDeleted, DeletedAt, DeletedBy\, poniewa� FluentMigrator nie realizuje automatycznego sczytywania dziedziczenia w�asno�ci modeli jak EF Core!
