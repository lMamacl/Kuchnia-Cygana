📖 Podręcznik Dewelopera – Kuchnia U Cygana

Witaj w technicznym przewodniku dla deweloperów systemu ERP Platformy
Cateringowej. Niniejszy dokument opisuje architekturę bazową na gałęzi develop
oraz standardy budowy modułów po migracji na SQL Server.

🧭 Nawigacja po Dokumentacji

  - ARCHITEKTURA PROJEKTU – Definicje bazy, Clean Architecture.
  - DZIENNIK DECYZJI (Decisions Log) – Uzasadnienie technologii (DDD, Soft
    Delete).
  - PLAN PROJEKTU – Status realizacji etapów.

1. Kontekst Techniczny i Stack

System oparty jest na architekturze Clean Architecture i wzorcach DDD
(Domain-Driven Design).

  - Runtime Bazy: MS SQL Server (Docker).
  - ORM: ServiceStack.OrmLite (Micro-ORM).
  - Migracje: FluentMigrator.
  - Strategia: Greenfield (brak migracji ze starego SQLite).
  - Gałąź develop: Zawiera czysty szablon architektoniczny (Infrastruktura,
    Pipeline CI, Auth Cookie) bez logiki biznesowej modułów.

2. Szybki Start (Developer Onboarding)

1.  **Konfiguracja:** Skopiuj `.env.example` do `.env` i uzupełnij `MSSQL_SA_PASSWORD`
    oraz `MSSQL_DB_NAME`.
2.  **Uruchomienie:**
    ```bash
    docker compose up --build
    ```
3.  **Weryfikacja:** Sprawdź logi pod kątem komunikatu: `Container kuchnia_sqlserver Healthy`.
4.  **Seeding (opcjonalnie):**
    ```bash
    dotnet run --project src/KuchniaUCygana.Web -- seed
    ```

### 💡 Limity Pamięci (RAM)
W pliku `.env` możesz dostosować limity pamięci dla kontenerów (domyślne wartości zoptymalizowane pod lokalne dev):
- `MSSQL_MEMORY_LIMIT_MB=768` (limit silnika SQL)
- `MSSQL_CONTAINER_MEMORY_LIMIT=1g` (limit kontenera Docker)
- `WEB_CONTAINER_MEMORY_LIMIT=512m` (limit kontenera aplikacji)

3. Fundamenty DDD (Warstwa Domain)

Kluczem do pracy z danymi są encje bazowe w Domain/Common/. Każda nowa encja
musi dziedziczyć po jednej z poniższych klas.

🔸 BaseEntity<T>

  - Zastosowanie: Proste słowniki, tagi, jednostki miar (np. kg, g), kody
    błędów.
  - Funkcje: Posiada Id (klucz główny) oraz CreatedAt (DateTimeOffset UTC).
  - Zaleta: Ułatwia hydrację danych przez OrmLite i testowanie (publiczne
    settery ID).

🔸 AuditableEntity<T> (Zalecane dla biznesu)

  - Zastosowanie: Wszystkie kluczowe dane biznesowe: Zamówienia, Receptury,
    Partie towaru.
  - Funkcje:
      - Audyt: CreatedBy, UpdatedBy (automatyczne śledzenie użytkownika).
      - Soft Delete: Implementuje ISoftDeletable. Rekordy nie są usuwane
        fizycznie (DELETE), lecz oznaczane flagą IsDeleted = true.
  - Wymóg: Zawsze używaj dla danych wymagających historii dla Sanepidu lub
    księgowości.

🔸 Zdarzenia Domenowe (IDomainEvent)

Służą do komunikacji między modułami bez tworzenia sztywnych zależności.

  - Przykład: Moduł Magazynu rzuca BatchExpiredEvent, a Moduł Komunikacji wysyła
    e-mail do menedżera.

4. Praca z Bazą Danych (FluentMigrator & OrmLite)

W tym projekcie nie używamy EF Core. Migracje i zapytania obsługujemy ręcznie.

📜 Zasady Tworzenia Migracji

1.  Lokalizacja: Infrastructure/Persistence/Migrations/.
2.  Numeracja: Trzymaj się przedziałów dla swojego modułu (np.
    Moduł 3: 300-399).
3.  Audyt (Ważne!): Dla encji AuditableEntity musisz ręcznie dodać kolumny w
    migracji: CreatedBy, UpdatedBy, IsDeleted, DeletedAt, DeletedBy.
4.  SQL: Używaj precyzyjnych typów (np. decimal(18,2) dla cen, datetimeoffset
    dla dat).

🗄️ Implementacja Repozytoriów

  - Dziedziczenie: Zawsze dziedzicz po BaseRepository<T, TId>. Otrzymasz gotowy
    CRUD z obsługą Soft Delete.
  - Relacje: Brak relacji nawigacyjnych (Lazy Loading nie istnieje). Używaj
    jawnych kluczy obcych (CategoryId) i złączeń JOIN w zapytaniach.
  - Zapytania: Wykorzystuj SqlExpression z OrmLite.

5. Standardy Kodowania Modułu (Krok po Kroku)

1.  Definicja Encji: Stwórz klasę w Domain/Entities/, dziedzicząc po
    AuditableEntity.
2.  Migracja: Stwórz plik migracji dodający tabelę w bazie danych.
3.  Interfejs: Zdefiniuj IMyRepository w Domain/Interfaces/.
4.  Implementacja: Stwórz MyRepository w
    Infrastructure/Persistence/Repositories/.
5.  Logika: Dodaj serwisy aplikacyjne w warstwie Application.

6. Testowanie i Jakość Kodu

Wymagamy wysokiej jakości kodu potwierdzonej testami:

  - Testy Jednostkowe: xUnit + Moq. Używaj biblioteki Bogus do generowania
    realistycznych danych testowych.
  - Testy Integracyjne: Obowiązkowe dla repozytoriów. Używamy Testcontainers +
    SQL Server, aby testować realne zapytania SQL.
  - Asercje: Stosuj FluentAssertions dla lepszej czytelności:
    result.Should().NotBeNull();.
  - Logika Magazynowa (FEFO): Testy muszą uwzględniać sortowanie po dacie
    ważności (rosnąco) z ExpiryDate == null na samym końcu.

7. Workflow i Commitowanie

Używamy Conventional Commits. Każdy commit musi być poprzedzony prefiksem:

  - feat: – nowa funkcjonalność.
  - fix: – poprawka błędu.
  - refactor: – zmiany w kodzie bez zmiany logiki.
  - docs: – zmiany w dokumentacji.
  - test: – dodanie lub poprawa testów.

✅ Lista kontrolna przed Pull Requestem (PR):

- [ ] dotnet build przechodzi bez błędów.
- [ ] Wszystkie testy jednostkowe i integracyjne przechodzą (dotnet test).
- [ ] Brak twardo zapisanych haseł/kluczy (używaj user-secrets).
- [ ] Dokumentacja (jeśli wymagana) jest zaktualizowana w języku polskim.
- [ ] Kod jest sformatowany zgodnie ze standardami projektu.

⚠️ Ważne Uwagi Techniczne

  - DateTime: Zawsze używaj DateTimeOffset, aby uniknąć problemów ze strefami
    czasowymi.
  - Usuwanie: Nigdy nie używaj db.Delete() na encjach biznesowych. Korzystaj z
    metod repozytorium wspierających Soft Delete.
  - Reset Środowiska: Aby całkowicie wyczyścić bazę i wolumeny Docker:
    ```bash
    docker compose down -v --remove-orphans
    docker compose up --build
    ```
