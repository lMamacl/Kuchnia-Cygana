# Instrukcje integracji Modułu 4 z Modułem 3

## Cel

Moduł 4 odpowiada za logistykę: plan tras, pojazdy, kierowców, kolejność stopów i widok kierowcy. Moduł 3 odpowiada za kuchnię, magazyn, kompletację, etykiety transportowe, manifest i fizyczny załadunek toreb.

Granica integracji jest oparta o `DeliveryCalendarId`. M3 nie powinien przypisywać toreb do tras po pozycji na liście, `OrderId` ani nazwie klienta.

## Kontrakt M4 -> M3

M4 utrzymuje:

- `DeliveryRoutes` - trasy na konkretny dzień,
- `DeliveryRouteStops` - przystanki tras,
- `DeliveryRouteStops.DeliveryCalendarId` - wymagany klucz łączący przystanek M4 z dostawą M1/M3,
- `DeliveryRouteStops.SequenceNumber` - kolejność stopów,
- pojazd i docelowo kierowcę trasy.

M3 czyta plan tras przez `IDeliveryManifestProvider` i oczekuje DTO:

- `RouteId`,
- `RouteName`,
- `VehicleId`,
- `VehicleRegistration`,
- lista stopów z `StopId`, `SequenceNumber`, `DeliveryCalendarId`, oknem dostawy.

Każdy stop musi mieć stabilny `DeliveryCalendarId`. Jeżeli `DeliveryCalendarId` jest pusty lub zmienia się bez wersjonowania, M3 nie może bezpiecznie powiązać torby, etykiety i manifestu.

## Skąd M4 pobiera dane

M4 powinien pobierać zapotrzebowanie dostaw przez `ILogisticsDeliveryDataProvider` albo docelowy provider M1/M3. Minimalny zestaw danych:

- `DeliveryCalendarId`,
- adres dostawy,
- okno dostawy,
- współrzędne po geokodowaniu albo dane wystarczające do geokodowania,
- `ClientPublicId`,
- liczba pudełek/toreb, masa albo wolumen, jeśli algorytm trasowania ma tego używać.

M4 nie potrzebuje imienia i nazwiska klienta do wygenerowania trasy. Dane osobowe pełne powinny zostać w M1 albo w widokach dostawy, gdzie są rzeczywiście potrzebne operacyjnie.

## Jak M3 używa danych M4

M3 synchronizuje sesje kompletacji z zamówień M1, a potem łączy sesję z trasą wyłącznie przez:

`PackingSession.DeliveryCalendarId -> DeliveryRouteStops.DeliveryCalendarId -> DeliveryRouteStops.RouteId`

Na tej podstawie M3:

- pokazuje dostawy w `/packing`,
- pozwala drukować etykiety transportowe w `/packing/labels`,
- generuje manifest w `/loading/{routeId}/manifest`,
- blokuje załadunek, jeżeli manifest nie jest finalnie zatwierdzony,
- waliduje skan torby w `/loading/{routeId}`.

Brak trasy M4 nie blokuje pakowania pudełek, ale blokuje:

- etykietę transportową,
- manifest,
- załadunek,
- dispatch.

## Zmiany trasy i regeneracja manifestu

M4 musi docelowo wystawiać wersję trasy albo znacznik zmiany. Następujące operacje powinny wymuszać regenerację manifestu w M3:

- zmiana kolejności stopów,
- przeniesienie dostawy do innej trasy,
- usunięcie stopu,
- usunięcie trasy,
- zmiana pojazdu albo przypisania logistycznego istotnego dla manifestu.

Po wykryciu zmiany M3 oznacza aktualny manifest jako `RequiresRegeneration`. Jeżeli manifest był już finalnie zatwierdzony, nowa zmiana powinna tworzyć kolejną wersję dokumentu zgodnie z workflow M3.

## Czego M4 nie powinien robić

M4 nie powinien:

- generować etykiet transportowych toreb,
- oznaczać toreb jako załadowane,
- zatwierdzać manifestu M3,
- modyfikować `PackingSessions`, `PackingBags`, `PackingLabels` ani `PackingManifests`,
- polegać na imieniu i nazwisku klienta jako identyfikatorze operacyjnym.

## Merge gałęzi `origin/Mod4`

Nie należy mergować `origin/Mod4` w całości do aktualnej gałęzi M3. Pełny diff tej gałęzi usuwa dużą część obecnego M1/M2/M3/M5 oraz dokumentację i widoki, więc zwykły merge grozi utratą funkcjonalności kompletacji, magazynu, produkcji i powiadomień.

Zalecany sposób integracji:

1. Utworzyć osobny branch integracyjny z aktualnej gałęzi bazowej.
2. Z `origin/Mod4` cherry-pickować albo ręcznie przenieść tylko pliki związane z kontraktem i logistyką:
   - `ILogisticsDeliveryDataProvider`,
   - DTO tras i dostaw z `DeliveryCalendarId`,
   - `RoutingService` i generator tras, jeśli nie usuwają obecnego kodu M3,
   - kontrolery/widoki logistyczne, które nie nadpisują kompletacji,
   - zgodne migracje `DeliveryRoutes` i `DeliveryRouteStops`.
3. Po każdym większym przeniesieniu sprawdzić `git diff --stat` oraz listę usuniętych plików.
4. Uruchomić:
   - `dotnet build --no-restore`,
   - `dotnet test --no-build --filter "FullyQualifiedName!~Integration"`.
5. Dopiero po przejściu testów podłączyć M3 do prawdziwego provider-a M4 przez konfigurację `DeliveryManifestProvider = M4`.

## Kryteria gotowości M4 dla domknięcia M3

M4 jest gotowy do pełnego domknięcia procesu M3, gdy:

- każdy zaplanowany stop ma `DeliveryCalendarId`,
- M3 widzi trasy w `IDeliveryManifestProvider`,
- zmiana trasy ma wersję albo znacznik zmiany,
- usunięcie/przeniesienie stopu jest wykrywalne przez M3,
- M4 nie wymaga pełnych danych osobowych klienta do planowania,
- build i testy przechodzą po integracji bez usuwania funkcji M3.
