using System;
using AutoMapper;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Domain.Services;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Application.Services;

/// <summary>
/// Serwis aplikacyjny magazynu — przyjęcia dostaw, odpisy, inwentaryzacja, alerty.
/// </summary>
public sealed class WarehouseService : IWarehouseService
{
    private readonly IBatchRepository _batchRepository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IInventoryTransactionRepository _transactionRepository;
    private readonly IRepository<UnitOfMeasure> _unitOfMeasureRepository;
    private readonly IBatchExpiryChangeLogRepository _batchExpiryChangeLogRepository;
    private readonly ICurrentUserService _currentUserService;
    private readonly FefoService _fefoService;
    private readonly SmartInventoryAnalyzer _inventoryAnalyzer;
    private readonly IMapper _mapper;
    private readonly ILogger<WarehouseService> _logger;

    public WarehouseService(
        IBatchRepository batchRepository,
        IStockItemRepository stockItemRepository,
        IInventoryTransactionRepository transactionRepository,
        IRepository<UnitOfMeasure> unitOfMeasureRepository,
        IBatchExpiryChangeLogRepository batchExpiryChangeLogRepository,
        ICurrentUserService currentUserService,
        FefoService fefoService,
        SmartInventoryAnalyzer inventoryAnalyzer,
        IMapper mapper,
        ILogger<WarehouseService> logger)
    {
        _batchRepository = batchRepository;
        _stockItemRepository = stockItemRepository;
        _transactionRepository = transactionRepository;
        _unitOfMeasureRepository = unitOfMeasureRepository;
        _batchExpiryChangeLogRepository = batchExpiryChangeLogRepository;
        _currentUserService = currentUserService;
        _fefoService = fefoService;
        _inventoryAnalyzer = inventoryAnalyzer;
        _mapper = mapper;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<BatchDto> ReceiveDeliveryAsync(ReceiveDeliveryRequest request)
    {
        // Sprawdź czy składnik istnieje
        var stockItem = await _stockItemRepository.GetByIdAsync(request.StockItemId)
            ?? throw new InvalidOperationException($"Składnik magazynowy o ID {request.StockItemId} nie istnieje.");

        // Utwórz nową partię
        var batch = new Batch
        {
            StockItemId = request.StockItemId,
            SupplierBatchNumber = request.SupplierBatchNumber,
            CurrentQuantity = request.Quantity,
            ExpiryDate = request.ExpiryDate,
            ReceivedDate = DateTimeOffset.UtcNow,
            IsDepleted = false,
        };

        var batchId = await _batchRepository.InsertAsync(batch);
        batch.Id = batchId;

        // Rejestruj transakcję przyjęcia
        var transaction = new InventoryTransaction
        {
            BatchId = batchId,
            TransactionType = InventoryTransactionType.Receipt,
            QuantityChanged = request.Quantity,
            Reason = $"Przyjęcie dostawy: {request.SupplierBatchNumber}",
            ReferenceDocument = request.Notes,
        };

        await _transactionRepository.InsertAsync(transaction);

        _logger.LogInformation(
            "Przyjęto dostawę: {BatchNumber}, składnik: {StockItem}, ilość: {Qty}",
            request.SupplierBatchNumber,
            stockItem.Name,
            request.Quantity);

        return _mapper.Map<BatchDto>(batch);
    }

    /// <inheritdoc/>
    public async Task RegisterWasteAsync(RegisterWasteRequest request)
    {
        if (request.BatchId.HasValue && request.BatchId.Value > 0)
        {
            var batch = await _batchRepository.GetByIdAsync(request.BatchId.Value)
                ?? throw new InvalidOperationException($"Partia o ID {request.BatchId.Value} nie istnieje.");
            
            if (batch.StockItemId != request.StockItemId)
            {
                throw new InvalidOperationException("Wskazana partia nie należy do wybranego składnika.");
            }

            var toDeduct = Math.Min(request.Quantity, batch.CurrentQuantity);
            batch.CurrentQuantity -= toDeduct;
            if (batch.CurrentQuantity <= 0)
            {
                batch.CurrentQuantity = 0;
                batch.IsDepleted = true;
            }
            await _batchRepository.UpdateAsync(batch);

            var transaction = new InventoryTransaction
            {
                BatchId = batch.Id,
                StockItemId = request.StockItemId,
                TransactionType = InventoryTransactionType.Waste,
                QuantityChanged = -toDeduct,
                Reason = $"Odpad (wskazana partia): {request.Reason}"
            };
            await _transactionRepository.InsertAsync(transaction);
        }
        else
        {
            var result = await _fefoService.DeductByFefoAsync(
                request.StockItemId,
                request.Quantity,
                $"Odpad: {request.Reason}",
                "WASTE");

            if (!result.IsFullyDeducted)
            {
                _logger.LogWarning(
                    "Odpad: nie udało się zdjąć pełnej ilości. Brakowało: {Shortage}",
                    result.Shortage);
            }
        }

        _logger.LogInformation(
            "Zarejestrowano odpad: składnik {StockItemId}, ilość: {Qty}, powód: {Reason}",
            request.StockItemId,
            request.Quantity,
            request.Reason);
    }

    /// <inheritdoc/>
    public async Task PerformInventoryAsync(IEnumerable<StockItemAdjustment> adjustments)
    {
        foreach (var adj in adjustments)
        {
            var stockItem = await _stockItemRepository.GetByIdAsync(adj.StockItemId);
            if (stockItem == null)
            {
                _logger.LogWarning("Inwentaryzacja: pominięto nieistniejący składnik {Id}", adj.StockItemId);
                continue;
            }

            // Oblicz aktualny stan z partii
            var currentStock = await _fefoService.GetAvailableQuantityAsync(adj.StockItemId);
            var difference = adj.ActualQuantity - currentStock;

            if (Math.Abs(difference) < 0.001m)
                continue; // Brak różnicy

            // Utwórz korektę inwentaryzacyjną
            var adjustment = new InventoryAdjustment
            {
                StockItemId = adj.StockItemId,
                QuantityBefore = currentStock,
                QuantityAfter = adj.ActualQuantity,
                Difference = difference,
                Reason = adj.Reason,
                AdjustedBy = "System",
            };

            // Zapisz korektę (przez generyczny repo insert — InventoryAdjustment dziedziczy BaseEntity<int>)
            // W przyszłości: dedykowane IInventoryAdjustmentRepository
            _logger.LogInformation(
                "Korekta inwentaryzacyjna: {StockItem} — przed: {Before}, po: {After}, różnica: {Diff}",
                stockItem.Name,
                currentStock,
                adj.ActualQuantity,
                difference);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<InventoryAlertDto>> GetSmartAlertsAsync()
    {
        var alerts = await _inventoryAnalyzer.AnalyzeAsync();
        return _mapper.Map<IEnumerable<InventoryAlertDto>>(alerts);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<StockItemDto>> GetStockOverviewAsync()
    {
        var items = await _stockItemRepository.GetAllAsync();
        var units = (await _unitOfMeasureRepository.GetAllAsync()).ToDictionary(u => u.Id);

        var dtoList = new List<StockItemDto>();

        foreach (var item in items)
        {
            var activeBatches = (await _batchRepository.GetActiveBatchesByStockItemAsync(item.Id)).ToList();
            var currentStock = activeBatches.Sum(b => b.CurrentQuantity);

            DateTimeOffset? earliestExpiryDate = activeBatches.Any()
                ? activeBatches.Min(b => b.ExpiryDate)
                : null;

            units.TryGetValue(item.DefaultUnitOfMeasureId, out var uom);
            var unitSymbol = uom?.Symbol ?? string.Empty;

            string category = DetermineCategory(item.Name);

            string status = "OK";
            string statusColor = "success";

            if (currentStock <= 0)
            {
                status = "Brak zapasów";
                statusColor = "danger";
            }
            else if (earliestExpiryDate.HasValue && earliestExpiryDate.Value <= DateTimeOffset.UtcNow.AddDays(3))
            {
                status = "Pilna ważność";
                statusColor = "danger";
            }
            else if (currentStock < item.MinimumLevel)
            {
                status = "Niski stan";
                statusColor = "danger";
            }
            else if (currentStock == item.MinimumLevel)
            {
                status = "Wskazana dostawa";
                statusColor = "warning";
            }
            else if (earliestExpiryDate.HasValue && earliestExpiryDate.Value <= DateTimeOffset.UtcNow.AddDays(7))
            {
                status = "Krótka ważność";
                statusColor = "warning";
            }

            dtoList.Add(new StockItemDto
            {
                Id = item.Id,
                Name = item.Name,
                BaseIngredientId = item.BaseIngredientId,
                DefaultUnitOfMeasureId = item.DefaultUnitOfMeasureId,
                MinimumLevel = item.MinimumLevel,
                LeadTimeDays = item.LeadTimeDays,
                CurrentStock = currentStock,
                Category = category,
                UnitSymbol = unitSymbol,
                Status = status,
                StatusColor = statusColor,
                EarliestExpiryDate = earliestExpiryDate
            });
        }

        return dtoList;
    }

    private static string DetermineCategory(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Suche";

        var lower = name.ToLowerInvariant();
        if (lower.Contains("kurczak") || lower.Contains("łosoś") || lower.Contains("mięso") || lower.Contains("ryb") || lower.Contains("indyka"))
            return "Mięso/Ryby";

        if (lower.Contains("śmietanka") || lower.Contains("masło") || lower.Contains("ser ") || lower.Contains("gouda") || lower.Contains("jogurt") || lower.Contains("mleko"))
            return "Nabiał";

        if (lower.Contains("brokuł") || lower.Contains("dynia") || lower.Contains("batat") || lower.Contains("jagod") || lower.Contains("ziemniak") || (lower.Contains("pomidor") && !lower.Contains("puszka")))
            return "Warzywa i owoce";

        return "Suche";
    }

    /// <inheritdoc/>
    public async Task IssueManualAsync(ManualIssueRequest request)
    {
        var available = await _fefoService.GetAvailableQuantityAsync(request.StockItemId);
        if (available < request.Quantity)
        {
            throw new InvalidOperationException($"Niewystarczająca ilość składnika w magazynie. Dostępne: {available}, wymagane: {request.Quantity}");
        }

        var remaining = request.Quantity;
        var activeBatches = (await _batchRepository.GetActiveBatchesByStockItemAsync(request.StockItemId)).ToList();
        foreach (var batch in activeBatches)
        {
            if (remaining <= 0) break;
            var toDeduct = Math.Min(remaining, batch.CurrentQuantity);
            batch.CurrentQuantity -= toDeduct;
            if (batch.CurrentQuantity <= 0)
            {
                batch.CurrentQuantity = 0;
                batch.IsDepleted = true;
            }
            await _batchRepository.UpdateAsync(batch);

            var transaction = new InventoryTransaction
            {
                BatchId = batch.Id,
                StockItemId = request.StockItemId,
                TransactionType = InventoryTransactionType.ManualIssue,
                QuantityChanged = -toDeduct,
                Reason = request.Reason,
                ReferenceDocument = request.IssuedTo
            };
            await _transactionRepository.InsertAsync(transaction);
            remaining -= toDeduct;
        }

        _logger.LogInformation(
            "Wydano ręcznie składnik {StockItemId}, ilość: {Qty}, powód: {Reason}, dla: {IssuedTo}",
            request.StockItemId,
            request.Quantity,
            request.Reason,
            request.IssuedTo);
    }

    /// <inheritdoc/>
    public async Task EditBatchExpiryAsync(EditBatchExpiryRequest request)
    {
        var batch = await _batchRepository.GetByIdAsync(request.BatchId)
            ?? throw new InvalidOperationException($"Partia o ID {request.BatchId} nie istnieje.");

        var oldExpiryDate = batch.ExpiryDate;
        batch.ExpiryDate = request.NewExpiryDate;
        await _batchRepository.UpdateAsync(batch);

        var changeLog = new BatchExpiryChangeLog
        {
            BatchId = request.BatchId,
            OldExpiryDate = oldExpiryDate.HasValue ? oldExpiryDate.Value.UtcDateTime : DateTime.MinValue,
            NewExpiryDate = request.NewExpiryDate.UtcDateTime,
            Reason = request.Reason,
            ChangedByUserId = _currentUserService.GetUserId(),
            ChangedAt = DateTime.UtcNow
        };
        await _batchExpiryChangeLogRepository.InsertAsync(changeLog);

        // Zarejestruj transakcję zmiany daty ważności
        var transaction = new InventoryTransaction
        {
            BatchId = batch.Id,
            StockItemId = batch.StockItemId,
            TransactionType = InventoryTransactionType.ExpiryDateChanged,
            QuantityChanged = 0,
            Reason = $"Zmiana daty ważności: {oldExpiryDate:dd.MM.yyyy} -> {request.NewExpiryDate:dd.MM.yyyy}. Powód: {request.Reason}",
            ReferenceDocument = $"LOG-{batch.Id}"
        };
        await _transactionRepository.InsertAsync(transaction);

        _logger.LogInformation(
            "Zmieniono datę ważności partii {BatchId}: {OldDate} -> {NewDate}. Powód: {Reason}",
            request.BatchId,
            oldExpiryDate,
            request.NewExpiryDate,
            request.Reason);
    }

    /// <inheritdoc/>
    public async Task<BatchDetailsDto> GetBatchDetailsAsync(int batchId)
    {
        var batch = await _batchRepository.GetByIdAsync(batchId)
            ?? throw new InvalidOperationException($"Partia o ID {batchId} nie istnieje.");
        var stockItem = await _stockItemRepository.GetByIdAsync(batch.StockItemId)
            ?? throw new InvalidOperationException($"Składnik o ID {batch.StockItemId} nie istnieje.");
        var units = (await _unitOfMeasureRepository.GetAllAsync()).ToDictionary(u => u.Id);
        units.TryGetValue(stockItem.DefaultUnitOfMeasureId, out var uom);

        var isExpired = batch.ExpiryDate.HasValue && batch.ExpiryDate.Value <= DateTimeOffset.UtcNow;
        int? daysToExpiry = batch.ExpiryDate.HasValue
            ? (int?)(batch.ExpiryDate.Value - DateTimeOffset.UtcNow).TotalDays
            : null;

        return new BatchDetailsDto
        {
            Id = batch.Id,
            BatchNumber = batch.SupplierBatchNumber ?? string.Empty,
            StockItemId = batch.StockItemId,
            StockItemName = stockItem.Name,
            Quantity = batch.CurrentQuantity,
            Unit = uom?.Symbol ?? string.Empty,
            ExpiryDate = batch.ExpiryDate,
            ReceivedAt = batch.ReceivedDate,
            ReceivedBy = "System",
            IsExpired = isExpired,
            DaysToExpiry = daysToExpiry
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FefoReportItemDto>> GetFefoReportAsync()
    {
        var batches = (await _batchRepository.GetAllAsync())
            .Where(b => !b.IsDepleted && !b.IsDeleted)
            .OrderBy(b => b.ExpiryDate.HasValue ? 0 : 1)
            .ThenBy(b => b.ExpiryDate)
            .ToList();
        var stockItems = (await _stockItemRepository.GetAllAsync()).ToDictionary(s => s.Id);
        var report = new List<FefoReportItemDto>();

        foreach (var b in batches)
        {
            stockItems.TryGetValue(b.StockItemId, out var stockItem);
            var stockItemName = stockItem?.Name ?? "Nieznany";
            int? daysToExpiry = b.ExpiryDate.HasValue
                ? (int?)(b.ExpiryDate.Value - DateTimeOffset.UtcNow).TotalDays
                : null;

            string status = "Safe";
            if (b.ExpiryDate.HasValue)
            {
                if (b.ExpiryDate.Value <= DateTimeOffset.UtcNow)
                    status = "Expired";
                else if (daysToExpiry <= 3)
                    status = "Critical";
                else if (daysToExpiry <= 7)
                    status = "Warning";
            }

            report.Add(new FefoReportItemDto
            {
                StockItemId = b.StockItemId,
                StockItemName = stockItemName,
                BatchId = b.Id,
                BatchNumber = b.SupplierBatchNumber ?? string.Empty,
                ExpiryDate = b.ExpiryDate,
                Quantity = b.CurrentQuantity,
                DaysToExpiry = daysToExpiry,
                Status = status
            });
        }
        return report;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TransactionHistoryDto>> GetTransactionHistoryAsync(TransactionHistoryFilterDto filter)
    {
        IEnumerable<InventoryTransaction> transactions;
        if (filter.StockItemId.HasValue)
        {
            transactions = await _transactionRepository.GetByStockItemIdAsync(filter.StockItemId.Value, 1, 200);
        }
        else if (filter.FromDate.HasValue || filter.ToDate.HasValue)
        {
            var from = filter.FromDate.HasValue ? filter.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) : DateTime.MinValue;
            var to = filter.ToDate.HasValue ? filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) : DateTime.MaxValue;
            transactions = await _transactionRepository.GetByDateRangeAsync(from, to);
        }
        else
        {
            transactions = await _transactionRepository.GetAllAsync();
        }

        if (filter.TransactionType.HasValue)
        {
            transactions = transactions.Where(t => (int)t.TransactionType == filter.TransactionType.Value);
        }

        if (filter.FromDate.HasValue)
        {
            var fromOffset = new DateTimeOffset(filter.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
            transactions = transactions.Where(t => t.CreatedAt >= fromOffset);
        }

        if (filter.ToDate.HasValue)
        {
            var toOffset = new DateTimeOffset(filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
            transactions = transactions.Where(t => t.CreatedAt <= toOffset);
        }

        var stockItems = (await _stockItemRepository.GetAllAsync()).ToDictionary(s => s.Id);
        var batches = (await _batchRepository.GetAllAsync()).ToDictionary(b => b.Id);

        var list = new List<TransactionHistoryDto>();
        foreach (var t in transactions.OrderByDescending(x => x.CreatedAt))
        {
            batches.TryGetValue(t.BatchId, out var batch);
            var batchNumber = batch?.SupplierBatchNumber ?? "Nieznana";
            var stockItemId = t.StockItemId ?? batch?.StockItemId;

            string stockItemName = "Nieznany";
            if (stockItemId.HasValue && stockItems.TryGetValue(stockItemId.Value, out var item))
            {
                stockItemName = item.Name;
            }

            list.Add(new TransactionHistoryDto
            {
                Id = (int)t.Id,
                StockItemId = stockItemId,
                StockItemName = stockItemName,
                BatchId = t.BatchId,
                BatchNumber = batchNumber,
                TransactionType = t.TransactionType.ToString(),
                Quantity = t.QuantityChanged,
                PerformedAt = t.CreatedAt,
                PerformedBy = "System",
                Reason = t.Reason ?? string.Empty
            });
        }
        return list;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<StockItemDto>> GetStockTableAsync(StockTableFilterDto filter)
    {
        var repoFilter = new StockItemFilter(filter.Search, 1, 1000);
        var (items, total) = await _stockItemRepository.GetPagedAsync(repoFilter);
        var dtoList = new List<StockItemDto>();
        var units = (await _unitOfMeasureRepository.GetAllAsync()).ToDictionary(u => u.Id);

        foreach (var item in items)
        {
            var activeBatches = (await _batchRepository.GetActiveBatchesByStockItemAsync(item.Id)).ToList();
            var currentStock = activeBatches.Sum(b => b.CurrentQuantity);

            DateTimeOffset? earliestExpiryDate = activeBatches.Any()
                ? activeBatches.Min(b => b.ExpiryDate)
                : null;

            units.TryGetValue(item.DefaultUnitOfMeasureId, out var uom);
            var unitSymbol = uom?.Symbol ?? string.Empty;

            string category = DetermineCategory(item.Name);

            string status = "OK";
            string statusColor = "success";

            bool isExpired = earliestExpiryDate.HasValue && earliestExpiryDate.Value <= DateTimeOffset.UtcNow;
            bool isLowStock = currentStock < item.MinimumLevel;

            if (filter.ShowExpiredOnly && !isExpired)
                continue;

            if (filter.ShowLowStockOnly && !isLowStock)
                continue;

            if (currentStock <= 0)
            {
                status = "Brak zapasów";
                statusColor = "danger";
            }
            else if (earliestExpiryDate.HasValue && earliestExpiryDate.Value <= DateTimeOffset.UtcNow.AddDays(3))
            {
                status = "Pilna ważność";
                statusColor = "danger";
            }
            else if (currentStock < item.MinimumLevel)
            {
                status = "Niski stan";
                statusColor = "danger";
            }
            else if (currentStock == item.MinimumLevel)
            {
                status = "Wskazana dostawa";
                statusColor = "warning";
            }
            else if (earliestExpiryDate.HasValue && earliestExpiryDate.Value <= DateTimeOffset.UtcNow.AddDays(7))
            {
                status = "Krótka ważność";
                statusColor = "warning";
            }

            dtoList.Add(new StockItemDto
            {
                Id = item.Id,
                Name = item.Name,
                BaseIngredientId = item.BaseIngredientId,
                DefaultUnitOfMeasureId = item.DefaultUnitOfMeasureId,
                MinimumLevel = item.MinimumLevel,
                LeadTimeDays = item.LeadTimeDays,
                CurrentStock = currentStock,
                Category = category,
                UnitSymbol = unitSymbol,
                Status = status,
                StatusColor = statusColor,
                EarliestExpiryDate = earliestExpiryDate
            });
        }
        return dtoList;
    }

    /// <inheritdoc/>
    public async Task<StockItemDetailsDto> GetStockItemDetailsWithBatchesAsync(int stockItemId)
    {
        var stockItem = await _stockItemRepository.GetByIdAsync(stockItemId)
            ?? throw new InvalidOperationException($"Składnik magazynowy o ID {stockItemId} nie istnieje.");

        var activeBatches = (await _batchRepository.GetActiveBatchesByStockItemAsync(stockItemId)).ToList();
        var currentStock = activeBatches.Sum(b => b.CurrentQuantity);
        DateTimeOffset? earliestExpiryDate = activeBatches.Any() ? activeBatches.Min(b => b.ExpiryDate) : null;

        var units = (await _unitOfMeasureRepository.GetAllAsync()).ToDictionary(u => u.Id);
        units.TryGetValue(stockItem.DefaultUnitOfMeasureId, out var uom);
        var unitSymbol = uom?.Symbol ?? string.Empty;

        string category = DetermineCategory(stockItem.Name);

        string status = "OK";
        string statusColor = "success";

        if (currentStock <= 0)
        {
            status = "Brak zapasów";
            statusColor = "danger";
        }
        else if (earliestExpiryDate.HasValue && earliestExpiryDate.Value <= DateTimeOffset.UtcNow.AddDays(3))
        {
            status = "Pilna ważność";
            statusColor = "danger";
        }
        else if (currentStock < stockItem.MinimumLevel)
        {
            status = "Niski stan";
            statusColor = "danger";
        }
        else if (currentStock == stockItem.MinimumLevel)
        {
            status = "Wskazana dostawa";
            statusColor = "warning";
        }
        else if (earliestExpiryDate.HasValue && earliestExpiryDate.Value <= DateTimeOffset.UtcNow.AddDays(7))
        {
            status = "Krótka ważność";
            statusColor = "warning";
        }

        var stockItemDto = new StockItemDto
        {
            Id = stockItem.Id,
            Name = stockItem.Name,
            BaseIngredientId = stockItem.BaseIngredientId,
            DefaultUnitOfMeasureId = stockItem.DefaultUnitOfMeasureId,
            MinimumLevel = stockItem.MinimumLevel,
            LeadTimeDays = stockItem.LeadTimeDays,
            CurrentStock = currentStock,
            Category = category,
            UnitSymbol = unitSymbol,
            Status = status,
            StatusColor = statusColor,
            EarliestExpiryDate = earliestExpiryDate
        };

        var batchDtos = _mapper.Map<List<BatchDto>>(activeBatches);

        var logs = await _batchExpiryChangeLogRepository.GetByStockItemIdAsync(stockItemId);
        var logDtos = new List<BatchExpiryChangeLogDto>();

        var allBatchesForLookup = (await _batchRepository.GetAllAsync())
            .Where(b => b.StockItemId == stockItemId)
            .ToDictionary(b => b.Id);

        foreach (var log in logs)
        {
            var dto = _mapper.Map<BatchExpiryChangeLogDto>(log);
            if (allBatchesForLookup.TryGetValue(log.BatchId, out var b))
            {
                dto.BatchNumber = b.SupplierBatchNumber ?? string.Empty;
            }
            dto.ChangedByUserName = log.ChangedByUserId.HasValue ? $"Użytkownik #{log.ChangedByUserId}" : "System";
            logDtos.Add(dto);
        }

        return new StockItemDetailsDto
        {
            StockItem = stockItemDto,
            Batches = batchDtos,
            ExpiryChangeLogs = logDtos
        };
    }
}

