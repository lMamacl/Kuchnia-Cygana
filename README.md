# KuchniaUCygana

Repozytorium projektu zaliczeniowego zorganizowane w modelu Clean Architecture:

- `src/KuchniaUCygana.Web` - warstwa prezentacji ASP.NET MVC
- `src/KuchniaUCygana.Application` - logika aplikacyjna (DTO, uslugi, mapowania)
- `src/KuchniaUCygana.Domain` - model domenowy
- `src/KuchniaUCygana.Infrastructure` - dostep do danych, migracje, integracje
- `tests/KuchniaUCygana.Tests` - testy jednostkowe i integracyjne

## Start lokalny

1. `dotnet restore`
2. Utworz lokalny `src/KuchniaUCygana.Web/appsettings.Development.json` (nie commituj)
3. `dotnet run --project src/KuchniaUCygana.Web`

Migracje SQLite uruchamiaja sie przy starcie aplikacji.

## Docker

1. Skopiuj `.env.example` do `.env` i uzupelnij wartosci.
2. `docker compose up --build`

## Workflow zespolu

- Praca na branchach `feature/devX-*` od `develop`
- PR: `feature/* -> develop`
- `main` tylko przez merge z `develop`
- Pliki konfliktowe i DI przez uzgodnionego wlasciciela
