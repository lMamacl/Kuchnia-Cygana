# Windows Docker SQL Server Setup Checklist

Ten guide jest dla osób, które uruchamiają projekt na innym komputerze z Windows + Docker Desktop. Celem jest uniknięcie problemów z portami, uprawnieniami, hasłami i starymi wolumenami SQL Server.

## Checklist przed uruchomieniem

Sprawdź na komputerze:

- Docker Desktop jest uruchomiony.
- Docker Desktop ma włączony backend WSL2 albo Hyper-V.
- Konto Windows ma dostęp do Docker engine.
- W PowerShell działają komendy:

```powershell
docker version
docker compose version
```

- Port `8080` jest wolny dla aplikacji web.
- Port `1433` jest wolny dla SQL Server.
- Komputer ma sensowny zapas RAM. Minimalnie przy lokalnym demo zostaw:
  - SQL container: około `1g`,
  - web container: około `512m`,
  - cały Docker Desktop: najlepiej 2 GB lub więcej.
- Repo jest w katalogu, do którego użytkownik ma normalne prawa zapisu, np. `C:\Users\<user>\source\repos\...`.

Sprawdzenie portów:

```powershell
netstat -ano | findstr ":8080"
netstat -ano | findstr ":1433"
```

Jeżeli port jest zajęty, zatrzymaj proces albo zmień mapowanie portu w `.env`.

## Konfiguracja `.env`

Z katalogu głównego repo:

```powershell
Copy-Item .env.example .env
```

W pliku `.env` ustaw przynajmniej:

```text
MSSQL_SA_PASSWORD=YourStrong!Passw0rd123
MSSQL_DB_NAME=KuchniaUCygana
MSSQL_ADMIN_PASSWORD=YourStrong!Admin123
MSSQL_PRACOWNIK_PASSWORD=YourStrong!Pracownik123
MSSQL_KLIENT_PASSWORD=YourStrong!Klient123
MSSQL_PORT=1433
MSSQL_MEMORY_LIMIT_MB=768
MSSQL_CONTAINER_MEMORY_LIMIT=1g
WEB_CONTAINER_MEMORY_LIMIT=512m

# Stripe (wymagane przez moduł płatności)
STRIPE_SECRET=sk_test_...
STRIPE_PUBLISHABLE=pk_test_...

# OpenAI (wymagane przez marketing AI w katalogu)
OPENAI_KEY=sk-...
```

*Uwaga: Port aplikacji webowej (`8080`) jest w `docker-compose.yml` zmapowany bezpośrednio jako port hosta. Zmienna `WEB_PORT` nie jest aktualnie używana w konfiguracji compose.*

Hasła SQL Server muszą być wystarczająco mocne. W praktyce użyj wielkiej litery, małej litery, cyfry i znaku specjalnego.

Nie commituj `.env`.

## Start projektu

Najpierw sprawdź konfigurację:

```powershell
docker compose --env-file .env config
```

Potem uruchom:

```powershell
docker compose --env-file .env up -d --build
```

Sprawdź status:

```powershell
docker compose --env-file .env ps --all
```

Poprawny stan:

```text
kuchnia_sqlserver       Up (healthy)
kuchnia_sqlserver_init  Exited (0)
kuchnia_web             Up
```

Ważne: `sqlserver-init Exited (0)` jest poprawny. To jednorazowy kontener bootstrapujący bazę, loginy i użytkowników. Nie ma działać stale.

Aplikacja:

```text
http://localhost:8080
```

## Co oznaczają statusy kontenerów

| Status | Znaczenie | Co zrobić |
|---|---|---|
| `sqlserver Up (healthy)` | SQL Server działa i odpowiada na healthcheck | OK |
| `sqlserver-init Exited (0)` | Init loginów zakończył się sukcesem | OK |
| `web Up` | Aplikacja ASP.NET działa | OK |
| `sqlserver-init Exited (1)` | Skrypt init nie przeszedł | Sprawdź logi init |
| `web Created` | Web czeka na SQL/init albo nie wystartował | Sprawdź `sqlserver-init` i logi |
| `web Exited` | Aplikacja wystartowała i padła | Sprawdź logi web |

Logi:

```powershell
docker compose --env-file .env logs --tail 120 sqlserver-init
docker compose --env-file .env logs --tail 120 web
docker compose --env-file .env logs --tail 120 sqlserver
```

## Typowe problemy i naprawy

### Brak dostępu do Docker engine

Objaw:

```text
permission denied while trying to connect to the docker API at npipe:////./pipe/docker_engine
```

Co sprawdzić:

- Docker Desktop jest uruchomiony.
- Użytkownik jest w grupie `docker-users`.
- Po dodaniu do grupy zrób wylogowanie/logowanie albo restart Windows.
- Uruchom PowerShell ponownie.

### Ostrzeżenie o `C:\Users\<user>\.docker\config.json`

Objaw:

```text
WARNING: Error loading config file: open C:\Users\<user>\.docker\config.json: Odmowa dostępu.
```

Co sprawdzić:

- Uprawnienia do katalogu `C:\Users\<user>\.docker`.
- Czy plik nie jest zablokowany przez inny proces.
- Czy PowerShell nie jest uruchomiony jako inny użytkownik niż Docker Desktop.

Jeżeli compose mimo ostrzeżenia działa, jest to problem lokalnej konfiguracji Docker CLI, a nie aplikacji.

### `sqlserver-init Exited (1)`

Sprawdź:

```powershell
docker compose --env-file .env logs --tail 120 sqlserver-init
```

Najczęstsze przyczyny:

- Brakuje któregoś hasła w `.env`.
- Hasło SQL Server jest za słabe.
- Skrypt init nie widzi zmiennej `MSSQL_DB_NAME`.
- Stary volume ma stan niezgodny z nowymi loginami.
- SQL Server nie był jeszcze zdrowy, a init został ręcznie odpalony za wcześnie.

Po poprawce konfiguracji:

```powershell
docker compose --env-file .env up -d --force-recreate sqlserver-init web
```

### `web Created`

`web Created` zwykle oznacza, że aplikacja czeka na warunek `sqlserver-init service_completed_successfully`.

Sprawdź:

```powershell
docker compose --env-file .env ps --all
docker compose --env-file .env logs --tail 120 sqlserver-init
```

Jeżeli `sqlserver-init` ma `Exited (0)`, uruchom ponownie web:

```powershell
docker compose --env-file .env up -d --force-recreate web
```

### Port `1433` albo `8080` jest zajęty

Sprawdź PID:

```powershell
netstat -ano | findstr ":1433"
netstat -ano | findstr ":8080"
```

Opcje:

- zatrzymaj proces zajmujący port,
- zmień `MSSQL_PORT` w `.env` (dla bazy) lub zmień mapowanie portu `8080:8080` bezpośrednio w `docker-compose.yml` (dla aplikacji web),
- uruchom ponownie compose.

### SQL Server zużywa dużo RAM

Na lokalnym demo to normalne, że SQL Server rezerwuje sporo pamięci.

Możesz obniżyć:

```text
MSSQL_MEMORY_LIMIT_MB=512
MSSQL_CONTAINER_MEMORY_LIMIT=768m
```

Po zmianie:

```powershell
docker compose --env-file .env up -d --force-recreate sqlserver web
```

## Kiedy używać `down -v`

Nie używaj `docker compose down -v` do zwykłych zmian Razor, CSS, JS ani kontrolerów. Ta komenda usuwa named volume `mssql_data`, czyli lokalną bazę.

Użyj `down -v` tylko gdy:

- chcesz czystą bazę od zera,
- testujesz migracje greenfield,
- stary volume ma błędne loginy/stan po zmianach infrastruktury,
- świadomie nie potrzebujesz lokalnych danych.

Pełny reset:

```powershell
docker compose --env-file .env down -v --remove-orphans
docker compose --env-file .env up -d --build
```

## Checklist przed pokazem lub pracą na nowym urządzeniu

- `.env` istnieje i ma wszystkie hasła.
- `docker compose --env-file .env config` przechodzi.
- `docker compose --env-file .env ps --all` pokazuje:
  - `sqlserver` jako `Up (healthy)`,
  - `sqlserver-init` jako `Exited (0)`,
  - `web` jako `Up`.
- `http://localhost:8080` zwraca stronę aplikacji.
- W trybie `Development` widać menu `DEV: Szybkie logowanie`.
- Kliknięcie `Admin` pokazuje link `Panel pracowniczy`.
- Test bez integracji przechodzi:

```powershell
dotnet test KuchniaUCygana.sln --no-restore --filter "FullyQualifiedName!~Integration"
```
