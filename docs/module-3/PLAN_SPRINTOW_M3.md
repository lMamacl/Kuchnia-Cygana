# Plan sprintów M3: kuchnia, magazyn i kompletacja

**Wersja:** 2.3, po integracji kontrolowanej `Gabriel-Nuevo`
**Data rewizji:** 2026-06-01
**Zakres:** M3 konsumuje M2, zapisuje snapshot produkcyjny i prowadzi kuchnię, magazyn, etykiety oraz pakowanie.

## Zasada modułów

- M2 jest właścicielem diet, posiłków, receptur-składowych, nutrition, opakowań posiłku i datowanego planu menu.
- M3 nie edytuje planu M2. M3 ma podgląd, walidację, snapshot, zapotrzebowanie, FEFO, gotowanie, etykiety i pakowanie.
- M3 generuje produkcję tylko z opłaconych zamówień.
- M4 jest właścicielem logistyki i widoku kierowcy; M3 dostarcza manifest i status przygotowania.
- Szczegółowy handoff M2 dla Gabriela: [GABRIEL_M2_PLAN_DIET_RECEPTUR.md](GABRIEL_M2_PLAN_DIET_RECEPTUR.md).

## Stan obecny

### Zrealizowane

- Magazyn ma obsługę stanów, partii, FEFO, korekt, wydań, raportów i indeksów pod duże zapytania.
- Packing/Loading zostały rozdzielone operacyjnie; pakowanie wymaga etykiety foliowej przed spakowaniem pudełka.
- M2 ma datowane `DietMenuPlans` i `DietMenuPlanItems`.
- `Gabriel-Nuevo` dostarczył częściowy backbone M2: wersjonowane składowe, opakowania, alerty, widoki planu i widoki składowych.
- M3 zapisuje snapshot JSON pozycji M2 w `ProductionPlanItems` oraz hash zawartości.
- Karta gotowania potrafi czytać składowe, składniki i opakowania ze snapshotu pozycji.
- FEFO składników jest idempotentne przez `FefoDeductedAt`.
- Opakowania/pojemniki są zdejmowane po akceptacji gotowania przez `PackagingDeductedAt`.

### Częściowe

- Snapshot produkcyjny jest zapisany na poziomie pozycji planu, ale nie ma jeszcze osobnej tabeli dla pełnego audytu snapshotów.
- Alerty M2 istnieją w modelu, ale wymagają pełnego generowania, potwierdzania odbioru i widoku “Plan z M2”.
- Sesje gotowania składowych są zaczęte, ale wymagają pełnego modelu `RecipeComponentVersion + ProductionDate`.
- FEFO po kategorii działa operacyjnie, ale wymaga doprecyzowania równoważności kategorii pod skład i alergeny.
- Widoki kuchni pokazują dane snapshotu, ale wymagają dopracowania UX dla sesji składowych, korekt i odchyleń.

## Sprint 8: Backbone M2 -> M3 i kuchnia

### Sprint 8.1: kontrakt i snapshot

- [x] Przyjąć backbone `Gabriel-Nuevo` na gałęzi integracyjnej.
- [x] Zapisywać snapshot M2 w pozycji produkcyjnej jako JSON + hash.
- [x] Nie używać live M2 do FEFO, jeśli pozycja ma zapisany snapshot.
- [ ] Dodać osobną tabelę audytu snapshotów, jeśli JSON w pozycji okaże się za mały dla raportowania.
- [ ] Dodać widok “Plan z M2” na 7+ dni: status publikacji, braki, alerty i potwierdzenie odbioru.

### Sprint 8.2: walidacja publikacji M2 konsumowana przez M3

- [x] Publikacja planu blokuje brak nutrition, alergenów, opakowań i kategorii magazynowej.
- [x] `StockItemId` pozostaje opcjonalny przy `WarehouseCategoryId`.
- [ ] Dodać flagę równoważności kategorii FEFO i blokować kategorie nierównoważne pod alergeny/skład.
- [ ] Dodać alert po zmianie opublikowanego planu, zmianie wersji składowej albo override D-3.

### Sprint 8.3: zapotrzebowanie magazynowe

- [ ] Zbudować `WarehouseDemandService` liczący 7+ dni z opublikowanego planu M2 i opłaconych zamówień.
- [ ] Agregować składniki, przyprawy i opakowania według snapshotów.
- [ ] Pokazać FEFO preview, braki, partie ryzyka i eksport/druk zapotrzebowania.
- [ ] W traceability zapisywać realny stock item dobrany przez FEFO po kategorii.

### Sprint 8.4: sesje gotowania składowych

- [ ] Sesja gotowania = `RecipeComponentVersion + ProductionDate`.
- [ ] Wspólne składowe agregować między posiłkami i wariantami.
- [ ] FEFO składników i przypraw wykonywać raz przy starcie sesji składowej.
- [ ] Pozwalać na rezerwacje półproduktu w zatwierdzonym oknie 3 dni.
- [ ] Karta gotowania pokazuje składowe, statusy, ilości jednostkowe i grupowe, instrukcje, testy temperatury i opakowania.
- [ ] Wprowadzić miękką blokadę współpracy, żeby dwie osoby nie zatwierdzały tej samej sesji bez ostrzeżenia.

### Sprint 8.5: odchylenia, pojemniki i etykiety

- [x] Pudełka/pojemniki ze snapshotu schodzą po akceptacji gotowania.
- [ ] Odchylenia zużycia i niedobory wymagają akceptacji KitchenManager/Admin.
- [ ] Zmiana liczby pojemników i etykiet wymaga drugiego potwierdzenia z wizualizacją.
- [ ] Każdy fizyczny pojemnik ma własny QR.
- [ ] Posiłek wielopojemnikowy wymaga zeskanowania wszystkich pojemników przed spakowaniem torby.
- [ ] Redruk etykiety wymaga powodu i osobnego logu.
- [ ] Etykieta pokazuje skład z M2, nutrition 100 g + porcja, alergeny, datę ważności, QR i identyfikację partii.

## Sprint 9: stabilizacja i testy

- [ ] Test: snapshot M3 nie zmienia się po zmianie M2.
- [ ] Test: publikacja planu blokuje brak nutrition/alergenów/opakowań/mapowania.
- [ ] Test: FEFO po `StockItemId` i kategorii jest idempotentne.
- [ ] Test: opakowania są zdejmowane po akceptacji gotowania i nie schodzą drugi raz.
- [ ] Test: karta gotowania pokazuje składowe i ilości jednostkowe/grupowe.
- [ ] Docker smoke: M2 publikuje plan -> M3 generuje produkcję -> FEFO -> gotowanie -> etykieta -> pakowanie.

## Ryzyka

- `Gabriel-Nuevo` wszedł częściowo w M3. Każdą zmianę produkcji trzeba traktować jako integracyjną, nie jako finalną implementację kuchni.
- Snapshot JSON w pozycji planu jest dobrym hardeningiem teraz, ale może wymagać osobnych tabel przy audycie i raportach.
- FEFO po kategorii bez twardej równoważności może dobrać produkt niezgodny alergennie, jeśli dane magazynu będą zbyt ogólne.
- Bez pełnych alertów M2/M3 kuchnia i magazyn mogą nie zauważyć zmiany planu po publikacji.

## Kryteria gotowości Sprintu 8

- Produkcja powstaje tylko z opłaconych zamówień.
- Każda pozycja produkcyjna ma zamrożony snapshot M2 albo jawnie działa w trybie legacy.
- FEFO nie odejmuje dwa razy składników ani opakowań.
- Karta gotowania pokazuje dane potrzebne kuchni bez ponownego liczenia live M2.
- Etykieta i pakowanie są blokowane bez zatwierdzonego gotowania oraz etykiety foliowej.
- Docker smoke przechodzi bez regresji na widokach M2, produkcji, magazynu i pakowania.
