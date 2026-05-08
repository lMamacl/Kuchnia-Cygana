# Kuchnia u Cygana

[![CI - Build, Test i Coverage](https://github.com/lMamacl/Kuchnia-Cygana/actions/workflows/ci.yml/badge.svg)](https://github.com/lMamacl/Kuchnia-Cygana/actions/workflows/ci.yml)

Platforma webowa dla firmy cateringowej laczaca portal klienta B2C z panelami ERP (produkcja, magazyn, logistyka, administracja).

## Architektura

Projekt jest podzielony warstwowo (Clean Architecture):

- `KuchniaUCygana.Web` - UI ASP.NET MVC
- `KuchniaUCygana.Application` - logika aplikacyjna, DTO, mapowania
- `KuchniaUCygana.Domain` - encje i kontrakty domenowe
- `KuchniaUCygana.Infrastructure` - dostep do danych, migracje, integracje zewnetrzne

Zaleznosci: `Web -> Application -> Domain <- Infrastructure`.

## Stos technologiczny

- .NET 8
- MS SQL Server (Docker)
- ServiceStack.OrmLite
- FluentMigrator
- xUnit + FluentAssertions + Moq + Bogus + Testcontainers

## Instalacja i pierwsze uruchomienie

### 1. Wymagania

- .NET 8 SDK
- Docker Desktop
- Git

### 2. Klonowanie repozytorium

```bash
git clone https://github.com/lMamacl/Kuchnia-Cygana.git
cd Kuchnia-Cygana
```

### 3. Przygotowanie zmiennych srodowiskowych

```bash
cp .env.example .env
```

Nastepnie uzupelnij w `.env` co najmniej:

- `MSSQL_SA_PASSWORD`
- `MSSQL_DB_NAME`
- opcjonalnie `MSSQL_PORT`

> Uwaga: `.env` zawiera sekrety i nie moze byc commitowany do repozytorium.

### 4. Przywrocenie zaleznosci

```bash
dotnet restore KuchniaUCygana.sln
```

### 5. Uruchomienie w Dockerze (zalecane)

```bash
docker compose up --build
```

Po starcie aplikacja jest dostepna pod `http://localhost:8080`.

## Uruchomienie lokalne bez Dockera

Mozesz uruchomic aplikacje lokalnie, ale baza dalej powinna wskazywac na SQL Server.

1. Przygotuj `src/KuchniaUCygana.Web/appsettings.Development.json` (na bazie `appsettings.Development.json.example`).
2. Ustaw poprawny `ConnectionStrings:DefaultConnection` do SQL Server.
3. Uruchom:

```bash
dotnet run --project src/KuchniaUCygana.Web
```

## Baza danych, migracje i seeding

- Strategia danych: **greenfield-only** (bez migracji danych ze SQLite).
- Kolejnosc startu runtime: `MigrateUp -> Seed (warunkowo) -> Start aplikacji`.
- Migracje sa uruchamiane automatycznie przy starcie aplikacji.
- Seeding w startupie dziala tylko dla `Development` i `Test`.
- W `Production` startup seeding jest wylaczony.

Reczne uruchomienie seedingu:

```bash
dotnet run --project src/KuchniaUCygana.Web -- seed
```

## Docker - zatrzymanie i bezpieczny reset bazy

### Zwykle zatrzymanie kontenerow

```bash
docker compose down
```

### Pelny reset danych (greenfield rebuild)

```bash
docker compose down -v --remove-orphans
docker compose up --build
```

Co robi reset:

- usuwa kontenery i sieci projektu,
- usuwa volume `mssql_data` (tracisz wszystkie dane bazy),
- usuwa osierocone kontenery compose,
- po ponownym starcie tworzy czysta baze od zera przez migracje.

### Sygnały poprawnego startu

W logach powinny pojawic sie miedzy innymi:

- `Container kuchnia_sqlserver Healthy`
- `Starting up database 'KuchniaUCygana'`

## Diagnostyka startupu (runbook)

- `18456` przed stanem `Healthy` - zwykle transient przy zimnym starcie SQL Server.
- `4060 Cannot open database ...` - zla nazwa bazy lub brak utworzenia DB.
- `17836 Length specified in network packet payload ...` - najczesciej probe/klient wysyla niepoprawny pakiet.

Regula alarmowa: powtarzalne `4060` lub `18456` po uzyskaniu `Healthy` dla SQL Server oznaczaja blad konfiguracji.

## Testy

Wszystkie testy:

```bash
dotnet test KuchniaUCygana.sln
```

Tylko testy integracyjne:

```bash
dotnet test KuchniaUCygana.sln --filter "Category=Integration"
```

Tylko testy poza integracyjnymi:

```bash
dotnet test KuchniaUCygana.sln --filter "Category!=Integration"
```

## Dokumentacja deweloperska

Szczegolowe wytyczne implementacyjne (encje, migracje, repozytoria, testy, workflow) sa w:

- `docs/guides/Developer_Manual.md`

## Workflow zespolu

- Praca na branchach `feature/*` od `develop`.
- Merge do `develop` przez PR z zielonym CI.
- `main` aktualizowany tylko przez kontrolowany merge z `develop`.
