-- =========================================================================================
-- SYSTEM ZARZĄDZANIA PLATFORMĄ CATERINGOWĄ „KUCHNIA U CYGANA”
-- SKRYPT BENCHMARKOWY DO PREZENTACJI OPTYMALIZACJI BAZODANOWYCH W SSMS
-- =========================================================================================
-- Instrukcja użycia:
-- 1. Otwórz ten plik w programie SQL Server Management Studio (SSMS).
-- 2. Połącz się z bazą danych 'KuchniaUCygana'.
-- 3. Upewnij się, że w oknie zapytań włączona jest opcja "Include Actual Execution Plan" (Ctrl + M).
-- 4. Uruchom cały skrypt (klawisz F5).
-- 5. Przejdź do zakładki "Messages", aby zobaczyć szczegółowe statystyki wejścia/wyjścia (I/O) i czasu.
-- 6. Przejdź do zakładki "Execution Plan", aby porównać koszt relatywny zapytań.
-- =========================================================================================

USE [KuchniaUCygana];
GO

-- Włączenie statystyk wydajnościowych w SSMS
SET STATISTICS IO ON;
SET STATISTICS TIME ON;
GO

PRINT '=========================================================================================';
PRINT 'ROZPOCZĘCIE TESTÓW WYDAJNOŚCIOWYCH - PROFIL DANYCH: VolumeDemo';
PRINT '=========================================================================================';
PRINT '';

-- =========================================================================================
-- SCENARIUSZ 1: TRASY LOGISTYCZNE - FILTROWANIE DATY (SARGable) ORAZ PROBLEM ZAPYTAŃ N+1
-- =========================================================================================
PRINT '-----------------------------------------------------------------------------------------';
PRINT 'SCENARIUSZ 1: OPTYMALIZACJA WYSZUKIWANIA TRAS I ZAPOBIEGANIE PROBLEMOWI N+1 ZAPYTAŃ';
PRINT '-----------------------------------------------------------------------------------------';
PRINT '';

-- Parametry testowe
DECLARE @TargetDate DATETIMEOFFSET = '2026-06-15 00:00:00 +00:00';
DECLARE @TargetDateStart DATETIMEOFFSET = '2026-06-15 00:00:00 +00:00';
DECLARE @TargetDateEnd DATETIMEOFFSET = '2026-06-16 00:00:00 +00:00';

PRINT '>>> 1A. STARA WERSJA (Non-SARGable CAST + N+1 Zapytania)';
-- 1. Pobranie tras za pomocą CAST (uniemożliwia użycie indeksu na RouteDate)
SELECT /* SCENARIUSZ 1 (A - Non-SARGable CAST) */ * 
FROM [DeliveryRoutes] 
WHERE CAST([RouteDate] AS DATE) = CAST(@TargetDate AS DATE) 
  AND [IsDeleted] = 0;

-- Symulacja pętli N+1 w aplikacji (dla każdej pobranej trasy wysyłane jest osobne zapytanie o przystanki)
-- Na dany dzień mamy 8-9 tras, co oznacza wykonanie 9 osobnych zapytań o przystanki.
SELECT /* SCENARIUSZ 1 (A - N+1) */ * FROM [DeliveryRouteStops] WHERE [RouteId] = 1001 AND [IsDeleted] = 0 ORDER BY [SequenceNumber];
SELECT /* SCENARIUSZ 1 (A - N+1) */ * FROM [DeliveryRouteStops] WHERE [RouteId] = 1002 AND [IsDeleted] = 0 ORDER BY [SequenceNumber];
SELECT /* SCENARIUSZ 1 (A - N+1) */ * FROM [DeliveryRouteStops] WHERE [RouteId] = 1003 AND [IsDeleted] = 0 ORDER BY [SequenceNumber];
SELECT /* SCENARIUSZ 1 (A - N+1) */ * FROM [DeliveryRouteStops] WHERE [RouteId] = 1004 AND [IsDeleted] = 0 ORDER BY [SequenceNumber];
SELECT /* SCENARIUSZ 1 (A - N+1) */ * FROM [DeliveryRouteStops] WHERE [RouteId] = 1005 AND [IsDeleted] = 0 ORDER BY [SequenceNumber];
SELECT /* SCENARIUSZ 1 (A - N+1) */ * FROM [DeliveryRouteStops] WHERE [RouteId] = 1006 AND [IsDeleted] = 0 ORDER BY [SequenceNumber];
SELECT /* SCENARIUSZ 1 (A - N+1) */ * FROM [DeliveryRouteStops] WHERE [RouteId] = 1007 AND [IsDeleted] = 0 ORDER BY [SequenceNumber];
SELECT /* SCENARIUSZ 1 (A - N+1) */ * FROM [DeliveryRouteStops] WHERE [RouteId] = 1008 AND [IsDeleted] = 0 ORDER BY [SequenceNumber];

PRINT '>>> 1B. NOWA WERSJA (SARGable Zakres Dat + Set-Based Fetch)';
-- 1. Pobranie tras za pomocą SARGable zakresu (RouteDate >= Start AND RouteDate < End)
SELECT /* SCENARIUSZ 1 (B - SARGable range) */ * 
FROM [DeliveryRoutes] 
WHERE [RouteDate] >= @TargetDateStart 
  AND [RouteDate] < @TargetDateEnd 
  AND [IsDeleted] = 0;

-- 2. Pobranie wszystkich przystanków dla wszystkich tras na raz (pojedyncze zapytanie set-based)
SELECT /* SCENARIUSZ 1 (B - Set-Based Fetch) */ * 
FROM [DeliveryRouteStops] 
WHERE [RouteId] IN (1001, 1002, 1003, 1004, 1005, 1006, 1007, 1008) 
  AND [IsDeleted] = 0 
ORDER BY [RouteId], [SequenceNumber];
GO


-- =========================================================================================
-- SCENARIUSZ 2: PAGINACJA LOGÓW AUDYTOWYCH (Bez indeksu pokrywającego vs Indeks okna czasu)
-- =========================================================================================
PRINT '';
PRINT '-----------------------------------------------------------------------------------------';
PRINT 'SCENARIUSZ 2: PAGINACJA LOGÓW AUDYTOWYCH (OFFSET/FETCH) I INDEKS OKNA CZASU (WINDOW INDEX)';
PRINT '-----------------------------------------------------------------------------------------';
PRINT '';

-- Parametry testowe paginacji
DECLARE @Offset INT = 5000;
DECLARE @PageSize INT = 25;

-- 2A. Tymczasowe usunięcie optymalnych indeksów
PRINT '>>> Przygotowanie bazy: Usuwanie indeksów optymalizacyjnych dla SystemLogs...';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_SystemLogs_Window' AND [object_id] = OBJECT_ID(N'[dbo].[SystemLogs]'))
BEGIN
    DROP INDEX [IX_SystemLogs_Window] ON [dbo].[SystemLogs];
END;
GO

PRINT '>>> 2A. STARA WERSJA (Brak dedykowanego indeksu okna czasu - Clustered Index Scan + Sort)';
-- Zapytanie o stronę logów zmuszone do pełnego skanowania tabeli i ciężkiego sortowania w pamięci
SELECT /* SCENARIUSZ 2 (A - Paginacja bez indeksu Scan + Sort) */ [Id], [UserId], [Action], [TargetEntity], [TargetId], [Timestamp], [IPAddress]
FROM [dbo].[SystemLogs]
ORDER BY [Timestamp] DESC, [Id] DESC
OFFSET 5000 ROWS FETCH NEXT 25 ROWS ONLY;
GO

-- 2B. Przywrócenie indeksu pokrywającego (okna czasu)
PRINT '>>> Przygotowanie bazy: Tworzenie indeksu pokrywającego (Timestamp DESC, Id DESC) z klauzulą INCLUDE...';
CREATE INDEX [IX_SystemLogs_Window]
ON [dbo].[SystemLogs] ([Timestamp] DESC, [Id] DESC)
INCLUDE ([UserId], [Action], [TargetEntity], [TargetId], [IPAddress]);
GO

PRINT '>>> 2B. NOWA WERSJA (Dedykowany indeks okna czasu - Index Scan/Seek bezpośrednio na uporządkowanej strukturze)';
-- Zapytanie o stronę logów wykonuje szybki skan indeksu pokrywającego, eliminując potrzebę sortowania (Sort Operator = 0%)
SELECT /* SCENARIUSZ 2 (B - Paginacja z indeksem TIMESTAMP DESC) */ [Id], [UserId], [Action], [TargetEntity], [TargetId], [Timestamp], [IPAddress]
FROM [dbo].[SystemLogs]
ORDER BY [Timestamp] DESC, [Id] DESC
OFFSET 5000 ROWS FETCH NEXT 25 ROWS ONLY;
GO


-- =========================================================================================
-- SCENARIUSZ 3: GOSPODARKA FEFO (Batch sorting - Indeks starej wersji vs nowej wersji)
-- =========================================================================================
PRINT '';
PRINT '-----------------------------------------------------------------------------------------';
PRINT 'SCENARIUSZ 3: GOSPODARKA MAGAZYNOWA FEFO (Wyszukiwanie aktywnych partii z Expiry Date)';
PRINT '-----------------------------------------------------------------------------------------';
PRINT '';

-- Parametry testowe
DECLARE @TestStockItemId INT = 120; -- Wybrany produkt testowy

-- 3A. Przygotowanie starego indeksu (CurrentQuantity w klauzuli INCLUDE)
PRINT '>>> Przygotowanie bazy: Zmiana indeksu na wersję starszą (CurrentQuantity w INCLUDE)...';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Batches_StockItem_Active_Expiry' AND [object_id] = OBJECT_ID(N'[dbo].[Batches]'))
BEGIN
    DROP INDEX [IX_Batches_StockItem_Active_Expiry] ON [dbo].[Batches];
END;
CREATE INDEX [IX_Batches_StockItem_Active_Expiry]
ON [dbo].[Batches] ([StockItemId], [IsDeleted], [IsDepleted], [ExpiryDate])
INCLUDE ([CurrentQuantity], [SupplierBatchNumber]);
GO

PRINT '>>> 3A. STARA WERSJA INDEKSU FEFO (CurrentQuantity jako kolumna INCLUDE)';
-- Zapytanie FEFO szuka aktywnych partii dla surowca
SELECT /* SCENARIUSZ 3 (A - FEFO - Indeks stary INCLUDE) */ [Id], [StockItemId], [SupplierBatchNumber], [CurrentQuantity], [ExpiryDate], [IsDepleted], [IsDeleted]
FROM [dbo].[Batches]
WHERE [StockItemId] = 120
  AND [IsDepleted] = 0
  AND [IsDeleted] = 0
ORDER BY
  CASE WHEN [ExpiryDate] IS NULL THEN 1 ELSE 0 END,
  [ExpiryDate],
  [Id];
GO

-- 3B. Przygotowanie nowego indeksu (CurrentQuantity jako kolumna klucza głównego indeksu)
PRINT '>>> Przygotowanie bazy: Tworzenie zoptymalizowanego indeksu (CurrentQuantity w kluczu indeksu)...';
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = N'IX_Batches_StockItem_Active_Expiry' AND [object_id] = OBJECT_ID(N'[dbo].[Batches]'))
BEGIN
    DROP INDEX [IX_Batches_StockItem_Active_Expiry] ON [dbo].[Batches];
END;
CREATE INDEX [IX_Batches_StockItem_Active_Expiry]
ON [dbo].[Batches] ([StockItemId], [IsDeleted], [IsDepleted], [CurrentQuantity], [ExpiryDate]);
GO

PRINT '>>> 3B. NOWA WERSJA INDEKSU FEFO (CurrentQuantity w strukturze klucza)';
-- Zapytanie FEFO z optymalnym indeksem
SELECT /* SCENARIUSZ 3 (B - FEFO - Indeks optymalny CurrentQuantity, ExpiryDate) */ [Id], [StockItemId], [SupplierBatchNumber], [CurrentQuantity], [ExpiryDate], [IsDepleted], [IsDeleted]
FROM [dbo].[Batches]
WHERE [StockItemId] = 120
  AND [IsDepleted] = 0
  AND [IsDeleted] = 0
ORDER BY
  CASE WHEN [ExpiryDate] IS NULL THEN 1 ELSE 0 END,
  [ExpiryDate],
  [Id];
GO


-- =========================================================================================
-- SCENARIUSZ 4: MENU I PLANY (M2) - DYNAMICZNE WYLICZENIA VS PRE-KALKULOWANY SNAPSHOT JSON
-- =========================================================================================
PRINT '';
PRINT '-----------------------------------------------------------------------------------------';
PRINT 'SCENARIUSZ 4: KATALOG I PLANY DIET (M2) - DYNAMICZNE WYLICZENIA VS SNAPSHOT JSON';
PRINT '-----------------------------------------------------------------------------------------';
PRINT '';

-- Parametry testowe
DECLARE @TestPlanId INT = 20; -- Wybrany dzień testowy (2026-06-15) dla seedu VolumeDemo

PRINT '>>> 4A. STARA WERSJA (Dynamiczne wyliczanie - N+1 oraz złączenia wielu tabel receptur/składników)';
-- W starej wersji system musiał pobrać pozycje menu, a następnie dla każdej z nich doładowywać komponenty i składniki w celu obliczenia kosztu/makro.
-- Zapytanie 1: Pozycje planu, warianty, posiłki i kategorie
SELECT /* SCENARIUSZ 4 (A - M2 - Dynamiczne wyliczanie) */ 
    p.[PlanDate],
    i.[Id] AS [DietMenuPlanItemId],
    i.[DietMenuPlanId],
    i.[DietVariantId],
    i.[MealId],
    i.[MealSlot],
    i.[ServingSizeMultiplier] AS [ServingMultiplier],
    d.[Name] AS [DietName],
    dv.[Name] AS [DietVariantName],
    m.[Name] AS [MealName],
    c.[Name] AS [CategoryName],
    mv.[Name] AS [MealVariantName]
FROM [DietMenuPlans] p
INNER JOIN [DietMenuPlanItems] i ON i.[DietMenuPlanId] = p.[Id]
INNER JOIN [DietVariants] dv ON dv.[Id] = i.[DietVariantId] AND dv.[IsDeleted] = 0
INNER JOIN [Diets] d ON d.[Id] = dv.[DietId] AND d.[IsDeleted] = 0
INNER JOIN [Meals] m ON m.[Id] = i.[MealId]
LEFT JOIN [MealVariants] mv ON mv.[Id] = i.[MealVariantId] AND mv.[IsDeleted] = 0
LEFT JOIN [Categories] c ON c.[Id] = m.[CategoryId]
WHERE p.[Id] = @TestPlanId 
  AND i.[IsDeleted] = 0 
  AND i.[IsActive] = 1 
  AND m.[IsDeleted] = 0 
  AND m.[IsActive] = 1;

-- Zapytanie 2: Pobranie komponentów receptur dla tych pozycji (skomplikowany UNION/JOIN)
SELECT /* SCENARIUSZ 4 (A - M2 - Dynamiczne wyliczanie) */ 
    i.[Id] AS [DietMenuPlanItemId],
    rc.[Id] AS [RecipeComponentId],
    rcv.[Id] AS [RecipeComponentVersionId],
    rc.[Name] AS [ComponentName],
    mrc.[Role],
    mrc.[QuantityPerServing],
    mrc.[Unit]
FROM [DietMenuPlanItems] i
INNER JOIN (
    SELECT i2.[Id] AS [DietMenuPlanItemId], mrc.[RecipeComponentVersionId], mrc.[Role], mrc.[QuantityPerServing], mrc.[Unit]
    FROM [DietMenuPlanItems] i2
    INNER JOIN [MealRecipeComponents] mrc ON mrc.[MealId] = i2.[MealId]
    WHERE i2.[MealVariantId] IS NULL AND i2.[DietMenuPlanId] = @TestPlanId
    UNION ALL
    SELECT i2.[Id] AS [DietMenuPlanItemId], mvc.[RecipeComponentVersionId], mvc.[Role], mvc.[QuantityPerServing], mvc.[Unit]
    FROM [DietMenuPlanItems] i2
    INNER JOIN [MealVariantComponents] mvc ON mvc.[MealVariantId] = i2.[MealVariantId]
    WHERE i2.[DietMenuPlanId] = @TestPlanId
) mrc ON mrc.[DietMenuPlanItemId] = i.[Id]
INNER JOIN [RecipeComponentVersions] rcv ON rcv.[Id] = mrc.[RecipeComponentVersionId]
INNER JOIN [RecipeComponents] rc ON rc.[Id] = rcv.[RecipeComponentId]
WHERE i.[DietMenuPlanId] = @TestPlanId;

-- Zapytanie 3: Pobranie składników i kategorii magazynowych dla zidentyfikowanych wersji komponentów
SELECT /* SCENARIUSZ 4 (A - M2 - Dynamiczne wyliczanie) */ 
    rci.[RecipeComponentVersionId],
    rci.[IngredientId],
    i.[Name] AS [IngredientName],
    rci.[WeightInGrams],
    wc.[Name] AS [WarehouseCategoryName]
FROM [RecipeComponentIngredients] rci
INNER JOIN [Ingredients] i ON i.[Id] = rci.[IngredientId]
LEFT JOIN [WarehouseCategories] wc ON wc.[Id] = COALESCE(rci.[WarehouseCategoryId], i.[WarehouseCategoryId])
WHERE rci.[RecipeComponentVersionId] IN (
    SELECT DISTINCT mrc.RecipeComponentVersionId
    FROM [DietMenuPlanItems] i2
    INNER JOIN [MealRecipeComponents] mrc ON mrc.[MealId] = i2.[MealId]
    WHERE i2.[DietMenuPlanId] = @TestPlanId AND i2.[MealVariantId] IS NULL
    UNION
    SELECT DISTINCT mvc.RecipeComponentVersionId
    FROM [DietMenuPlanItems] i2
    INNER JOIN [MealVariantComponents] mvc ON mvc.[MealVariantId] = i2.[MealVariantId]
    WHERE i2.[DietMenuPlanId] = @TestPlanId
) AND rci.[IsDeleted] = 0 AND i.[IsActive] = 1 AND i.[IsDeleted] = 0;


PRINT '>>> 4B. NOWA WERSJA (Pre-kalkulowany snapshot JSON z bazy danych)';
-- W zoptymalizowanej wersji dane są zapisywane jako w pełni przeliczony JSON przy publikacji planu.
-- Aplikacja pobiera je jednym, prostym zapytaniem bez złączeń.
SELECT /* SCENARIUSZ 4 (B - M2 - Pre-calculated snapshot) */ 
    i.[Id] AS [DietMenuPlanItemId],
    i.[PublishedSnapshotJson]
FROM [DietMenuPlanItems] i
WHERE i.[DietMenuPlanId] = @TestPlanId 
  AND i.[IsDeleted] = 0 
  AND i.[IsActive] = 1 
  AND i.[PublishedSnapshotJson] IS NOT NULL 
  AND i.[PublishedSnapshotHash] IS NOT NULL
ORDER BY i.[DietVariantId], i.[SortOrder], i.[Id];
GO

-- Przywrócenie indeksu do stanu pierwotnego
PRINT '>>> Przywracanie bazy do stanu domyślnego...';
GO

-- Wyłączenie statystyk IO / TIME
SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
GO

-- =========================================================================================
-- DIAGNOSTYKA: ZBIORCZE PODSUMOWANIE STATYSTYK WYDAJNOŚCIOWYCH Z CACHE (TABELA)
-- =========================================================================================
PRINT '';
PRINT '-----------------------------------------------------------------------------------------';
PRINT 'ZBIORCZA TABELA WYNIKÓW BENCHMARKU (STATYSTYKI Z BUFFER CACHE)';
PRINT '-----------------------------------------------------------------------------------------';
PRINT '';

IF HAS_PERMS_BY_NAME(null, null, 'VIEW SERVER STATE') = 1 OR HAS_PERMS_BY_NAME(null, null, 'VIEW SERVER PERFORMANCE STATE') = 1
BEGIN
    WITH StatementStats AS (
        SELECT 
            SUBSTRING(st.text, (qs.statement_start_offset/2)+1, 
                ((CASE qs.statement_end_offset 
                  WHEN -1 THEN DATALENGTH(st.text) 
                 ELSE qs.statement_end_offset END - qs.statement_start_offset)/2) + 1) AS [StatementText],
            qs.execution_count AS [Wykonania],
            qs.total_logical_reads AS [TotalLogicalReads],
            qs.total_worker_time AS [TotalWorkerTime],
            qs.total_elapsed_time AS [TotalElapsedTime]
        FROM sys.dm_exec_query_stats qs
        CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st
    )
    SELECT 
        CASE 
            WHEN [StatementText] LIKE '%SCENARIUSZ 1 (A - Non-SARGable%' THEN 'Scenariusz 1 (A - Nielimitowane trasy CAST)'
            WHEN [StatementText] LIKE '%SCENARIUSZ 1 (A - N+1)%' THEN 'Scenariusz 1 (A - N+1 Zapytania o przystanki)'
            WHEN [StatementText] LIKE '%SCENARIUSZ 1 (B - SARGable%' THEN 'Scenariusz 1 (B - Optymalna trasa SARGable)'
            WHEN [StatementText] LIKE '%SCENARIUSZ 1 (B - Set-Based%' THEN 'Scenariusz 1 (B - Pobranie przystanków IN)'
            WHEN [StatementText] LIKE '%SCENARIUSZ 2 (A%' THEN 'Scenariusz 2 (A - Paginacja bez indeksu)'
            WHEN [StatementText] LIKE '%SCENARIUSZ 2 (B%' THEN 'Scenariusz 2 (B - Paginacja z indeksem okna)'
            WHEN [StatementText] LIKE '%SCENARIUSZ 3 (A%' THEN 'Scenariusz 3 (A - FEFO - Indeks stary INCLUDE)'
            WHEN [StatementText] LIKE '%SCENARIUSZ 3 (B%' THEN 'Scenariusz 3 (B - FEFO - Indeks optymalny klucza)'
            WHEN [StatementText] LIKE '%SCENARIUSZ 4 (A%' THEN 'Scenariusz 4 (A - M2 - Dynamiczne 11 tabel)'
            WHEN [StatementText] LIKE '%SCENARIUSZ 4 (B%' THEN 'Scenariusz 4 (B - M2 - Snapshot JSON)'
            ELSE 'Inne'
        END AS [Scenariusz],
        [Wykonania],
        [TotalLogicalReads] / [Wykonania] AS [OdczytyLogiczne],
        ([TotalWorkerTime] / [Wykonania]) / 1000.0 AS [CPUMs],
        ([TotalElapsedTime] / [Wykonania]) / 1000.0 AS [CzasMs]
    FROM StatementStats
    WHERE [StatementText] LIKE '%SCENARIUSZ%'
      AND [StatementText] NOT LIKE '%sys.dm_exec_query_stats%'
    ORDER BY [Scenariusz], [OdczytyLogiczne] DESC;
END
ELSE
BEGIN
    PRINT 'UWAGA: Brak uprawnień do odczytu sys.dm_exec_query_stats (VIEW SERVER STATE).';
    PRINT 'Aby wygenerować tabelę podsumowującą w SSMS, połącz się jako użytkownik "sa"';
    PRINT 'i uruchom poniższy fragment ręcznie:';
    PRINT '';
    PRINT 'WITH StatementStats AS (';
    PRINT '    SELECT ';
    PRINT '        SUBSTRING(st.text, (qs.statement_start_offset/2)+1, ';
    PRINT '            ((CASE qs.statement_end_offset ';
    PRINT '              WHEN -1 THEN DATALENGTH(st.text) ';
    PRINT '             ELSE qs.statement_end_offset END - qs.statement_start_offset)/2) + 1) AS [StatementText],';
    PRINT '        qs.execution_count AS [Wykonania],';
    PRINT '        qs.total_logical_reads AS [TotalLogicalReads],';
    PRINT '        (qs.total_worker_time / qs.execution_count) / 1000.0 AS [CPUMs]';
    PRINT '    FROM sys.dm_exec_query_stats qs';
    PRINT '    CROSS APPLY sys.dm_exec_sql_text(qs.sql_handle) st';
    PRINT ')';
    PRINT 'SELECT * FROM StatementStats WHERE [StatementText] LIKE ''%SCENARIUSZ%'';';
END
GO

PRINT '=========================================================================================';
PRINT 'TESTY ZAKOŃCZONE POMYŚLNIE';
PRINT '=========================================================================================';
GO


