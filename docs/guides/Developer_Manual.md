# Podręcznik Dewelopera - Kuchnia U Cygana

Witaj w podręczniku technicznym dla Deweloperów systemu ERP Platformy Cateringowej. Ten dokument opisuje bazową architekturę znajdującą się na gałęzi `develop` i zasady, którymi należy się kierować przy budowie nowych modułów.

## Nawigacja po Dokumentacji
Poniżej znajdziesz szybkie linki do innych kluczowych dokumentów:
* [ARCHITEKTURA PROJEKTU](../architecture/ARCHITEKTURA_KuchniaUCygana.html) - Definicje bazy, Clean Architecture.
* [DZIENNIK DECYZJI (Decisions Log)](../Decisions_Log.md) - Poradnik z wytłumaczeniem "dlaczego" używamy takich a nie innych technologii i rozwiązań architektonicznych (szczególnie przydatne dla zrozumienia koncepcji DDD oraz Soft Delete).
* [PLAN PROJEKTU](../PLAN_PROJEKTU.md) - Etapy i status realizacji całego przedsięwzięcia.

---

## 🛠️ Stan Gałęzi `develop` (Czysta Baza)

Obecnie na gałęzi `develop` znajduje się **Czysty Szablon Architektoniczny**. Został on zoptymalizowany i oczyszczony z próbnych komponentów i logiki testowej, aby każdy deweloper, niezależnie od przypisanego modułu, mógł od niego bezpiecznie wystartować.

Czego możesz oczekiwać pobierając najnowszego `develop'a`:
1. Skonfigurowane i gotowe środowisko deweloperskie: pliki Docker, konfiguracja `user-secrets`, Pipeline CI w GitHub Actions (z wbudowanymi testami i weryfikacją pokrycia kodu).
2. Przygotowany i przetestowany układ katalogów (Clean Architecture): `Web`, `Application`, `Domain`, `Infrastructure`.
3. Gotowe fundamenty DDD i wzorce bazodanowe (Bazowe Encje i Generyczne Repozytoria).

Na gałęzi `develop` nie uświadczysz na ten moment **żadnej ścisłej logiki biznesowej** dla któregokolwiek z planowanych 5 modułów — jest to celowy zabieg. Startujesz na czysto i budujesz swój kod na udostępnionym niżej fundamencie.

---

## 🏗️ Komponenty Bazowe (Szkielet DDD w `Domain/Common/`)

Kluczem do pracy z bazą w naszym systemie (pod spodem korzystamy z ORM `ServiceStack.OrmLite`) są dostarczone encje bazowe, po których **musisz** dziedziczyć, budując modele danych dla swoich modułów.

### 🔸 `BaseEntity<T>` (`Domain/Common/BaseEntity.cs`)
**Dla kogo i kiedy?** Stosuj, gdy tworzysz proste słowniki, tagi czy tabele konfiguracyjne, które praktycznie nigdy nie będą edytowane ręcznie przez pracownika w trakcie działania systemu (np. Tabele jednostek miar `[L, kg, g]`, predefiniowane statusy, typy operacji, kody błędów).
**Co ułatwia?** 
- Dodaje klucz główny `Id` (typu generycznego, zazwyczaj `int`). Zauważysz tam **publiczny setter** — to zabieg celowy; ułatwia hydrację danych obiektowych przez `ServiceStack.OrmLite` z bazy, a także pozwala zdefiniować testowe ID w xUnit i klasach generujących mockowane dane.
- Automatycznie dokłada `CreatedAt` ustawiane jako `DateTimeOffset` według czasu UTC.
- Posiada nadpisane metody `Equals` dla standardowej identyfikacji tożsamości poprzez pole Id.
- Wprowadza obsługę `IDomainEvent`.

### 🔸 `AuditableEntity<T>` (`Domain/Common/AuditableEntity.cs`)
**Dla kogo i kiedy?** Stosuj ZAWSZE dla ruchomych danych biznesowych, których cykl życia polega na edycjach ze strony pracowników czy klientów! Jeśli encją jest Zamówienie, Receptura, Partia kurczaka włożona do chłodni czy Zgłoszenie Pracownika — zawsze dziedziczymy po `AuditableEntity`.
**Co wspiera dodatkowo?**
- Posiada sygnatury audytowe: `CreatedBy` i `UpdatedBy`. Śledzi kto wykonał akcję — zapisując ID użytkownika uwierzytelnionego z JWT.
- Posiada wbudowany i przygotowany pod repozytoria **Soft Delete**! (poprzez implementację interfejsu z `Domain/Common/Interfaces/ISoftDeletable.cs`). To znaczy, że nie używasz w swoim kodzie niebezpiecznego `DELETE FROM Tabelka`. Usuwanie odbywa się poprzez nałożenie wierszowej flagi `IsDeleted = true` oraz `DeletedBy = {pracownik_id}`. Utrzymujemy historię każdego usunięcia w bazie w celach dowodowych dla Sanepidu czy księgowości.

### 🔸 Generyczne Repozytorium (`BaseRepository<T, TId>`)
Gotowe do użycia generyczne operacje asynchroniczne typu Create/Update/Delete/GetAll. Zostało odpowiednio oskryptowane — jeśli wskażesz mu by zwrócił dane encji typu `AuditableEntity`, repozytorium **automatycznie** zignoruje "usunięte" rekordy (Soft Delete) wywołując zwykłe zapytanie. Wywołanie usunięcia nadpisze status, zamiast usuwać wiersz z bazy.

### 🔸 `IDomainEvent` (`Domain/Common/Events/`)
Sygnalizator zdarzeń, który rozdziela odpowiedzialności. Jeśli proces produkcyjny (Moduł 3) zmieni datę ważności jogurtu na przedawniony, wypuści `BatchExpiredEvent`. Wtedy dowolny inny moduł (np. Komunikacja - Moduł 5) może w odpowiedzi bezproblemowo stworzyć subskrybenta i wysłać automatycznego e-maila menedżerowi do spraw bezpieczeństwa. Moduły nie "gadają" ze sobą bezpośrednio w kodzie.

---

## 💾 Baza Danych: Migracje (FluentMigrator)

Aplikacja wykorzystuje `FluentMigrator` do wersjonowania schematu bazy danych. Wszelkie migracje muszą znajdować się w projekcie infrastruktury w odpowiednim katalogu: `Infrastructure/Persistence/Migrations/`.

**🔴 Bardzo Ważne zasady tworzenia tabel dla `AuditableEntity`:**
Gdy przygotowujesz migracje tworzące w bazie nowe tabele obsługujące Soft Delete i Audyt, pamiętaj by dodać ręcznie definicję tych kolumn do definicji tabeli. `FluentMigrator` nie zagląda automatycznie do kodu w klasie `AuditableEntity` (jak robiłoby to EF Core).
Zawsze dodawaj odpowiedniki kolumn dziedziczonych: `CreatedBy`, `UpdatedBy`, `IsDeleted`, `DeletedAt`, `DeletedBy`. 

**Pamiętaj o przedziałach numeracji migracji:** Aby zapobiec konfliktom z innymi deweloperami, korzystaj wyłącznie z przedziału przydzielonego dla przypisanego do ciebie Modułu (Sprawdź zakresy przydziału w głównym `README.md`).

---

## 📝 Zasady Zgłaszania Commitów i Czystego Kodu

Utrzymujemy czysty i uporządkowany rejestr zmian korzystając z konwencji **Conventional Commits**. Każdą wykonaną u siebie w branchu `feature/*` zmianę flaguj w gicie następująco:

* `feat:` (Nowa funkcjonalność, kod dla encji, nowe polecenie w systemie, wdrożenie)
* `fix:` (Załatanie błędu zgłoszonego na `develop` lub produkcyjnego, poprawki łamiących się testów)
* `refactor:` (Optymalizacje kodu bez zmian w funkcjonalności aplikacji)
* `docs:` (Wszelkie aktualizacje w plikach `.md`, dziennikach ustaleń, logach)
* `test:` (Samo testowanie — np. wprowadzanie Mocków czy konfiguracja Bogusa)
* `chore:` (Obsługa techniczna np. aktualizacje NuGet, GitHub Actions, edycja .gitignore)

**Przed dodaniem Commita koniecznie sprawdź, czy:**
1. Masz schowane swoje dane produkcyjne API lub wygenerowane hasła w usłudze narzędziowej: `dotnet user-secrets` a w `.env` i `appsettings.json` nie ma twardo zapisanych kluczy!
2. Twoje zmiany na branchu skompilują się na serwerze i nie zerwą głównego strumienia. Zrób na konsoli kontrolny test: `dotnet test`.
