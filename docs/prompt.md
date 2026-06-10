# Instrukcja i Specyfikacja Implementacji Seedera `VolumeDemo`
**Projekt:** System Zarządzania Platformą Cateringową „Kuchnia u Cygana”  
**Wersja:** 2.1 (Zrewidowany prompt na podstawie rzeczywistych testów w Dockerze)  
**Status:** Gotowy do przekazania deweloperowi / agentowi kodującemu

---

## 1. Cel i Kontekst Audytowy

Analiza schematu bazy danych i testów wydajnościowych w Dockerze (MS SQL Server 2022) wykazała następujące parametry bazy produkcyjnej przy seedowaniu podstawowym (`DemoData`):
*   **Rozmiar początkowy bazy:** **272.00 MB** (plik `.mdf` 72 MB, `.ldf` 200 MB, w tym rzeczywiste dane tabel: **19.35 MB**, indeksy: **5.90 MB**).
*   **Krytyczne hotspoty rozmiaru:**
    1.  `SystemLogs` (Moduł 5) – zawiera logi audytowe (wartości JSON). Średni wiersz zajmuje aż **~6.5 KB**. Roczny przyrost bez archiwizacji to ok. **4.68 GB**.
    2.  `DietMenuPlanItems` (Moduł 2) – przechowuje snapshoty JSON dań. Średni wiersz zajmuje **~20 KB**.
    3.  `BoxLabels` & `PackingLabels` (Moduł 3) – przechowują JSON-y etykiet. Średni wiersz zajmuje **~1.5 KB** (roczny przyrost to **1.35 GB**).
*   **Szacowany roczny przyrost danych (dla 500 klientów):** **~7.30 GB** (przed archiwizacją) lub **~1.15 GB** (po wdrożeniu archiwizacji i 30-dniowej retencji etykiet).

Zadaniem dewelopera/agenta jest implementacja ręcznego profilu seedera o nazwie **`VolumeDemo`**, który pozwoli wygenerować wolumen danych odpowiadający rocznemu obciążeniu średniej wielkości cateringu dietetycznego, w celu przeprowadzenia testów wydajnościowych i weryfikacji procedur archiwizacyjnych.

---

## 2. Parametry i Założenia Seedera `VolumeDemo`

Seeder musi być uruchamiany jako osobny profil (np. poprzez CLI lub flagę w konfiguracji `DatabaseSeedingProfile.VolumeDemo`) i nie może uruchamiać się automatycznie przy standardowym startowaniu aplikacji w trybie deweloperskim.

### 2.1. Wolumen generowanych danych
*   **Konta użytkowników:** `1200` użytkowników (w tym `1150` klientów oraz `50` pracowników różnych szczebli i działów).
*   **Aktywni klienci:** `200-300` aktywnych klientów na typowy dzień roboczy (generujących realne dostawy), z pikami zamówień dochodzącymi do `500` na dobę.
*   **Katalog Diet:** `5` głównych diet z różnymi wariantami kalorycznymi (np. 1500, 1800, 2000, 2200, 2500 kcal) oraz zdefiniowanym planem menu (`DietMenuPlans` i `DietMenuPlanItems`) na `14` dni do przodu.
*   **Dania i Receptury:** `40-60` dań, `100-150` wariantów dań, `120-200` składowych przepisów.
*   **Składniki magazynowe:** `1000` składników (`StockItems`), z czego `150-250` aktywnie używanych w recepturach.
*   **Magazyn (Batches):** `3000-7000` aktywnych i historycznych partii surowców, w tym opakowania jako osobna kategoria strefy przechowywania (`WarehouseCategory`).
*   **Transakcje i Telemetria:** `10 000 - 30 000` logów transakcji magazynowych (FEFO), telemetrycznych (`TemperatureLogs` HACCP) oraz logów zmian statusów kompletacji i dostaw.
*   **Kompletacja:** Kilka tysięcy spakowanych pudełek (`PackingItems`) oraz toreb przypisanych do manifestów.

### 2.2. Integralność danych między-modułowa
Dane generowane przez seeder muszą być w 100% spójne pod kątem relacji logicznych na poziomie aplikacji:
1.  **Moduł 1 (E-commerce) $\rightarrow$ Moduł 2 (Katalog):** Pozycje zamówień (`OrderItems`) muszą mieć poprawnie uzupełnione `DietMenuPlanItemId`, `MealId`, `MealVariantId` oraz `MealSlot` z planu opublikowanego w module katalogu na dany dzień.
2.  **Moduł 3 (Produkcja) $\rightarrow$ Moduł 2 (Katalog):** Plany produkcyjne i sesje gotowania (`CookingSessions`) muszą być trwale powiązane z konkretną wersją komponentu receptury (`RecipeComponentVersionId`) w celu zachowania spójności wyliczania food-costów.
3.  **Moduł 3 (WMS) $\rightarrow$ Moduł 1 (E-commerce):** Magazyn musi posiadać wystarczającą ilość partii surowców z datami ważności (FEFO) na pokrycie zamówień wygenerowanych z Modułu 1.
4.  **Moduł 4 (Logistyka) $\rightarrow$ Moduł 1 & 3:** Trasy dostaw (`DeliveryRoutes`) i przystanki (`DeliveryRouteStops`) muszą odpowiadać adresom aktywnych klientów z zamówień, a paczki i torby kompletacyjne muszą odwoływać się do wygenerowanych sesji pakowania.
5.  **Moduł 5 (HR) $\rightarrow$ Wszystkie:** Pracownicy przypisani do grafików (`WorkSchedules`) muszą fizycznie odpowiadać rolom na zmianach (kucharze w kuchni, pakowacze na kompletacji, kierowcy przypisani do pojazdów w Module 4).

6. Dane muszą być w pełni ze sobą kompatybilne, i zgodne logiczne, plan dań powinny w 80-90% pokrywać się z zamówieniami, reszta to losowe dania. Reszta zamówień powinna być losowa, tak aby można było testować inne funkcjonalności aplikacji. Niektóre zamówienia, powinny być anulowane, i odrzucone.  
7. Wszelkie nazwy, opisy i komentarze danych wprowadzanych przez seeder powinny być w języku polskim, z polskimi znakami diakrytycznymi.
8. Wszelkie dane wprowadzane przez seeder powinny być zgodne ze schematami bazy danych, z uwzględnieniem wszystkich relacji i ograniczeń.

---

## 3. Wymagania Techniczne i Optymalizacyjne

> [!CAUTION]
> Generowanie kilkudziesięciu tysięcy wierszy powiązanych relacyjnie przy użyciu pojedynczych insertów (Dapper / EF Core SaveChanges w pętli) doprowadzi do przekroczenia limitu czasu (timeout) i potencjalnego braku pamięci (Out of Memory).

Deweloper musi zastosować optymalne techniki zapisu masowego:
1.  **Zapis Batchowy:** Wymagane użycie `SqlBulkCopy` w .NET dla tabel o bardzo dużej liczbie rekordów (np. `SystemLogs`, `TemperatureLogs`, `PackingItems`, `BoxLabels`).
2.  **Table-Valued Parameters (TVP):** Wykorzystanie TVP lub procedur składowanych z logiką set-based do masowego generowania powiązań.
3.  **Optymalizacja Pamięciowa Serwera:** Batch size podczas ładowania partii danych powinien wynosić **1000 - 2000 wierszy** na transakcję. Należy unikać materializowania całego datasetu w pamięci aplikacji (stosować yield / proces strumieniowy).
4.  **Rekomendacje Zasobów Kontenera:**
    *   Profil podstawowy (Smoke Demo): min. **4-6 GB RAM** dla Dockera.
    *   Profil `VolumeDemo`: zalecane **10-12 GB RAM** przypisane do instancji Dockera (szczególnie ze względu na limit pamięci SQL Server i procesy analityczne aplikacji).

### 3.1. Interfejs CLI i Parametryzacja
Seeder musi obsługiwać parametry konfiguracyjne przekazywane z poziomu środowiska lub CLI:
*   `--dry-run` – wykonuje symulację generowania danych bez zatwierdzania transakcji w bazie.
*   `--reset-volume-demo` – usuwa poprzednio wygenerowane dane z prefiksem `VOL-` przed rozpoczęciem generowania.
*   `--days <int>` – liczba dni planu i zamówień wstecz/w przód (domyślnie `14`).
*   `--active-customers <int>` – liczba aktywnych klientów na dzień (domyślnie `300`).
*   `--peak-orders <int>` – maksymalna liczba zamówień w piku (domyślnie `500`).
*   `--seed <int>` – ziarno generatora losowości w celu zapewnienia powtarzalności zestawu danych testowych

### Podział Czasowy Seedowania (Zasady Biznesowe)

Aby umożliwić deweloperom i użytkownikom testowanie rzeczywistych procesów biznesowych (takich jak optymalizacja tras czy kompletowanie paczek) bezpośrednio w aplikacji, dane są dzielone czasowo wg poniższej tabeli:

| Moduł / Obszar danych | Przeszłość (`date < today`) | Dzień dzisiejszy i Przyszłość (`date >= today`) | Rationale / Uzasadnienie |
|:---|:---|:---|:---|
| **M2 Dietetyk (Plany Menu)** | **TAK** (Historia) | **TAK (do 14 dni w przód)** | Jadłospis musi być zaplanowany i opublikowany w przód, aby klienci mogli kupować diety. |
| **M3 Kuchnia (Plany Produkcji & Gotowanie)** | **TAK** (Historia) | **TAK (do 14 dni w przód)** | Produkcja jest planowana na bazie zamówień i gotowana w przód. Kucharze chcą widzieć zaplanowane sesje. |
| **M1 E-commerce (Zamówienia & Płatności)** | **TAK** (Historia) | **TAK (aktywne subskrypcje)** | Klient kupuje dietę na np. 30 dni, więc zamówienia i ich kalendarz dostaw (`DeliveryCalendar`) trwają w przyszłość. |
| **M3 Kompletacja (PackingSessions, Items, Bags, Etykiety)** | **TAK** (Historia) | **NIE** | Kompletacja (pakowanie) toreb i pudełek odbywa się dynamicznie na stanowisku pakowania. Zaseedowanie przyszłości uniemożliwiłoby testowanie skanowania QR. |
| **M4 Logistyka (Trasy, Przystanki, Manifesty)** | **TAK** (Historia) | **NIE** | Trasy i manifesty kurierskie są generowane dynamicznie przez spedytora. Przyszłe trasy zablokowałyby możliwość przetestowania algorytmu routingu. |
| **M4 Logistyka (Ruchy toreb termicznych)** | **TAK** (Historia) | **NIE** | Ruchy toreb (BagMovementLogs) to fizyczne skany QR przy załadunku/odbiorze. Mogą istnieć tylko w przeszłości/czasie rzeczywistym. |
| **M3 Magazyn (InventoryTransactions - rozchody FEFO)** | **TAK** (Historia) | **NIE** | Surowce są ściągane z partii (`Batches`) w momencie gotowania. Dla przyszłych planów surowce są tylko zablokowane, a nie zużyte. |
| **M5 HR (Grafiki pracy WorkSchedules)** | **TAK** (Historia) | **TAK (do 14 dni w przód)** | Grafiki pracy pracowników są planowane z wyprzedzeniem. |
| **Logi i Reklamacje (SystemLogs, Telemetria, Tickets)** | **TAK** (Historia) | **NIE** | Logi audytowe, telemetria lodówek i skargi klientów to dane reaktywne, nie występują w przyszłości. |

---

## 4. Plan Testów Wydajnościowych (Test Plan)

Po zaimplementowaniu seedera należy przeprowadzić weryfikację wydajnościową bazy danych:
1.  **SQL Sanity Counts:** Porównanie liczby rekordów w krytycznych tabelach z założonym wolumenem testu.
2.  **Paginacja i Lazy Loading:** Weryfikacja, czy kluczowe widoki panelu administracyjnego (`/production`, `/packing`, `/loading`, `/meals`, `/ingredients`, `/diet-editor/recipes`) otwierają się w czasie poniżej **1.5 sekundy** przy pełnej bazie.
3.  **Traceability E2E (Smoke Test):** Uruchomienie pełnego przepływu:
    *   Publikacja menu (M2) $\rightarrow$ Wygenerowanie zamówień klientów (M1) $\rightarrow$ Obliczenie zapotrzebowania (M3) $\rightarrow$ Wydanie FEFO (M3) $\rightarrow$ Rejestracja CCP gotowania (M3) $\rightarrow$ Generowanie i druk etykiet (M3) $\rightarrow$ Kompletacja do toreb (M3) $\rightarrow$ Załadunek i routing OSM (M4).
    Każdy z tych etapów musi wykonać się bez błędów blokowania (deadlocków) i w akceptowalnym czasie.
