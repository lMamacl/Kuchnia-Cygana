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
    private readonly IWarehouseCategoryRepository _warehouseCategoryRepository;
    private readonly IInventoryTransactionRepository _transactionRepository;
    private readonly IWarehouseCommandRepository _warehouseCommandRepository;
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
        IWarehouseCategoryRepository warehouseCategoryRepository,
        IInventoryTransactionRepository transactionRepository,
        IWarehouseCommandRepository warehouseCommandRepository,
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
        _warehouseCategoryRepository = warehouseCategoryRepository;
        _transactionRepository = transactionRepository;
        _warehouseCommandRepository = warehouseCommandRepository;
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
        var stockItem = await _stockItemRepository.GetByIdAsync(request.StockItemId)
            ?? throw new InvalidOperationException($"Składnik magazynowy o ID {request.StockItemId} nie istnieje.");

        var batch = new Batch
        {
            StockItemId = request.StockItemId,
            SupplierBatchNumber = request.SupplierBatchNumber,
            CurrentQuantity = request.Quantity,
            ExpiryDate = request.ExpiryDate,
            ReceivedDate = DateTimeOffset.UtcNow,
            IsDepleted = false,
        };

        var transaction = new InventoryTransaction
        {
            StockItemId = request.StockItemId,
            TransactionType = InventoryTransactionType.Receipt,
            QuantityChanged = request.Quantity,
            Reason = $"Przyjęcie dostawy: {request.SupplierBatchNumber}. Uwagi: {request.Notes}",
            ReferenceDocument = request.InvoiceNumber,
        };

        batch = await _warehouseCommandRepository.ReceiveDeliveryAsync(batch, transaction);

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
        string wasteReason = request.Reason == "Inny"
            ? $"Odpad: Inny - {request.Notes}"
            : $"Odpad: {request.Reason}";

        await _warehouseCommandRepository.DeductStockAsync(new WarehouseDeductionCommand(
            request.StockItemId,
            request.Quantity,
            InventoryTransactionType.Waste,
            request.BatchId.HasValue && request.BatchId.Value > 0
                ? $"Odpad (wskazana partia): {wasteReason}"
                : wasteReason,
            request.Reason == "Inny" ? request.Notes : "WASTE",
            request.BatchId is > 0 ? request.BatchId : null,
            ExcludeExpired: false,
            RequireFullQuantity: true));

        _logger.LogInformation(
            "Zarejestrowano odpad: składnik {StockItemId}, ilość: {Qty}, powód: {Reason}",
            request.StockItemId,
            request.Quantity,
            wasteReason);
    }

    /// <inheritdoc/>
    public async Task PerformInventoryAsync(IEnumerable<StockItemAdjustment> adjustments)
    {
        var commands = adjustments
            .Where(a => a.StockItemId > 0)
            .Select(a => new InventoryAdjustmentCommand(
                a.StockItemId,
                a.ActualQuantity,
                a.Reason))
            .ToList();

        var adjustedBy = _currentUserService.GetUserName() ?? "System";
        var results = await _warehouseCommandRepository.ApplyInventoryAsync(commands, adjustedBy);

        foreach (var result in results)
        {
            _logger.LogInformation(
                "Korekta inwentaryzacyjna: składnik {StockItemId}, przed: {Before}, po: {After}, różnica: {Diff}",
                result.StockItemId,
                result.QuantityBefore,
                result.QuantityAfter,
            result.Difference);
        }
    }

    /// <inheritdoc/>
    public async Task PerformBatchInventoryAsync(IEnumerable<BatchInventoryAdjustment> adjustments)
    {
        var commands = adjustments
            .Where(a => a.BatchId > 0)
            .Select(a => new BatchInventoryAdjustmentCommand(
                a.BatchId,
                a.ActualQuantity,
                a.Reason))
            .ToList();

        var adjustedBy = _currentUserService.GetUserName() ?? "System";
        var results = await _warehouseCommandRepository.ApplyBatchInventoryAsync(commands, adjustedBy);

        foreach (var result in results)
        {
            _logger.LogInformation(
                "Korekta inwentaryzacyjna partii: partia {BatchId}, składnik {StockItemId}, przed: {Before}, po: {After}, różnica: {Diff}",
                result.BatchId,
                result.StockItemId,
                result.QuantityBefore,
                result.QuantityAfter,
                result.Difference);
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
        var filter = new StockTableFilterDto
        {
            Page = 1,
            PageSize = 200,
        };
        var result = new List<StockItemDto>();
        PagedResultDto<StockItemDto> page;

        do
        {
            page = await GetStockTablePageAsync(filter);
            result.AddRange(page.Items);
            filter.Page++;
        }
        while (filter.Page <= page.TotalPages);

        return result;
    }

    public async Task<IReadOnlyList<StockItemDto>> SearchStockLookupAsync(StockLookupFilterDto filter)
    {
        var query = filter.Query?.Trim() ?? string.Empty;
        if (query.Length < 2)
        {
            return Array.Empty<StockItemDto>();
        }

        var rows = await _stockItemRepository.SearchStockLookupAsync(
            query,
            filter.Limit <= 0 ? 20 : filter.Limit,
            filter.OnlyAvailable);

        return rows.Select(MapStockRow).ToList();
    }

    public async Task<StockItemDto?> GetStockLookupByIdAsync(int stockItemId)
    {
        var row = await _stockItemRepository.GetStockLookupByIdAsync(stockItemId);
        return row is null ? null : MapStockRow(row);
    }

    /// <inheritdoc/>
    public async Task IssueManualAsync(ManualIssueRequest request)
    {
        await _warehouseCommandRepository.DeductStockAsync(new WarehouseDeductionCommand(
            request.StockItemId,
            request.Quantity,
            InventoryTransactionType.ManualIssue,
            request.Reason,
            request.IssuedTo,
            BatchId: null,
            ExcludeExpired: true,
            RequireFullQuantity: true));

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
        var result = await _warehouseCommandRepository.EditBatchExpiryAsync(new BatchExpiryEditCommand(
            request.BatchId,
            request.NewExpiryDate,
            request.Reason,
            _currentUserService.GetUserId()));

        _logger.LogInformation(
            "Zmieniono datę ważności partii {BatchId}: {OldDate} -> {NewDate}. Powód: {Reason}",
            result.BatchId,
            result.OldExpiryDate,
            result.NewExpiryDate,
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
        return await GetFefoReportAsync(new FefoReportFilterDto());
    }

    public async Task<IEnumerable<FefoReportItemDto>> GetFefoReportAsync(FefoReportFilterDto filter)
    {
        var query = new FefoReportQuery(
            filter.Search,
            filter.Status,
            Page: 1,
            PageSize: int.MaxValue,
            UsePaging: false);

        var rows = await _batchRepository.GetFefoReportAsync(query);
        return rows.Select(MapFefoRow).ToList();
    }

    public async Task<PagedResultDto<FefoReportItemDto>> GetFefoReportPageAsync(FefoReportFilterDto filter)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 1, 200);
        var query = new FefoReportQuery(filter.Search, filter.Status, page, pageSize, UsePaging: true);

        var (rows, totalCount) = await _batchRepository.GetFefoReportPageAsync(query);

        return new PagedResultDto<FefoReportItemDto>
        {
            Items = rows.Select(MapFefoRow).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<TransactionHistoryDto>> GetTransactionHistoryAsync(TransactionHistoryFilterDto filter)
    {
        var page = await GetTransactionHistoryPageAsync(filter);
        return page.Items;
    }

    public async Task<PagedResultDto<TransactionHistoryDto>> GetTransactionHistoryPageAsync(
        TransactionHistoryFilterDto filter)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 1, 200);

        var from = filter.FromDate.HasValue
            ? new DateTimeOffset(filter.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc))
            : (DateTimeOffset?)null;
        var to = filter.ToDate.HasValue
            ? new DateTimeOffset(filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc))
            : (DateTimeOffset?)null;

        var query = new TransactionHistoryQuery(
            filter.StockItemId,
            from,
            to,
            filter.TransactionType,
            page,
            pageSize);

        var (rows, totalCount) = await _transactionRepository.GetTransactionHistoryPageAsync(query);

        filter.Page = page;
        filter.PageSize = pageSize;
        filter.TotalCount = totalCount;

        return new PagedResultDto<TransactionHistoryDto>
        {
            Items = rows.Select(MapTransactionRow).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<StockItemDto>> GetStockTableAsync(StockTableFilterDto filter)
    {
        var page = await GetStockTablePageAsync(filter);
        return page.Items;
    }

    public async Task<PagedResultDto<StockItemDto>> GetStockTablePageAsync(StockTableFilterDto filter)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize <= 0 ? 15 : filter.PageSize, 1, 200);
        var categoryId = await ResolveCategoryIdAsync(filter);
        var query = new StockItemTableQuery(
            filter.Search,
            categoryId,
            filter.Category,
            filter.ShowExpiredOnly,
            filter.ShowLowStockOnly,
            filter.ShowExpiringSoonOnly,
            page,
            pageSize);

        var (rows, totalCount) = await _stockItemRepository.GetStockTablePageAsync(query);
        var items = rows.Select(MapStockRow).ToList();

        filter.Page = page;
        filter.PageSize = pageSize;
        filter.CategoryId = categoryId;
        filter.TotalCount = totalCount;

        return new PagedResultDto<StockItemDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<PagedResultDto<BatchInventoryItemDto>> GetBatchInventoryPageAsync(
        StockTableFilterDto filter)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize <= 0 ? 50 : filter.PageSize, 1, 200);
        var categoryId = await ResolveCategoryIdAsync(filter);
        var query = new BatchInventoryQuery(filter.Search, categoryId, filter.Category, page, pageSize);

        var (rows, totalCount) = await _batchRepository.GetBatchInventoryPageAsync(query);

        filter.Page = page;
        filter.PageSize = pageSize;
        filter.CategoryId = categoryId;
        filter.TotalCount = totalCount;

        return new PagedResultDto<BatchInventoryItemDto>
        {
            Items = rows.Select(MapBatchInventoryRow).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    private async Task<int?> ResolveCategoryIdAsync(StockTableFilterDto filter)
    {
        var categoryId = await _warehouseCategoryRepository.ResolveActiveCategoryIdAsync(
            filter.CategoryId,
            filter.Category);

        return filter.CategoryId.HasValue && !categoryId.HasValue
            ? -1
            : categoryId;
    }

    private static StockItemDto MapStockRow(StockItemStockRow row)
    {
        var (status, statusColor) = GetStockStatus(row.CurrentStock, row.MinimumLevel, row.EarliestExpiryDate);

        return new StockItemDto
        {
            Id = row.Id,
            Name = row.Name,
            BaseIngredientId = row.BaseIngredientId,
            DefaultUnitOfMeasureId = row.DefaultUnitOfMeasureId,
            MinimumLevel = row.MinimumLevel,
            LeadTimeDays = row.LeadTimeDays,
            CurrentStock = row.CurrentStock,
            CategoryId = row.CategoryId,
            Category = row.Category,
            UnitSymbol = row.UnitSymbol,
            Status = status,
            StatusColor = statusColor,
            EarliestExpiryDate = row.EarliestExpiryDate,
        };
    }

    private static (string Status, string Color) GetStockStatus(
        decimal currentStock,
        decimal minimumLevel,
        DateTimeOffset? earliestExpiryDate)
    {
        if (currentStock <= 0)
        {
            return ("Brak zapasów", "danger");
        }

        if (earliestExpiryDate.HasValue && earliestExpiryDate.Value <= DateTimeOffset.UtcNow)
        {
            return ("Przeterminowane", "danger");
        }

        if (earliestExpiryDate.HasValue && earliestExpiryDate.Value <= DateTimeOffset.UtcNow.AddDays(3))
        {
            return ("Pilna ważność", "danger");
        }

        if (currentStock < minimumLevel)
        {
            return ("Niski stan", "danger");
        }

        if (currentStock == minimumLevel)
        {
            return ("Wskazana dostawa", "warning");
        }

        if (earliestExpiryDate.HasValue && earliestExpiryDate.Value <= DateTimeOffset.UtcNow.AddDays(7))
        {
            return ("Krótka ważność", "warning");
        }

        return ("OK", "success");
    }

    private static FefoReportItemDto MapFefoRow(FefoReportRow row)
    {
        return new FefoReportItemDto
        {
            StockItemId = row.StockItemId,
            StockItemName = row.StockItemName,
            BatchId = row.BatchId,
            BatchNumber = row.BatchNumber,
            ExpiryDate = row.ExpiryDate,
            Quantity = row.Quantity,
            DaysToExpiry = row.DaysToExpiry,
            Status = row.Status,
        };
    }

    private static TransactionHistoryDto MapTransactionRow(TransactionHistoryRow row)
    {
        var transactionType = Enum.IsDefined(typeof(InventoryTransactionType), row.TransactionType)
            ? ((InventoryTransactionType)row.TransactionType).ToString()
            : row.TransactionType.ToString();

        return new TransactionHistoryDto
        {
            Id = (int)row.Id,
            StockItemId = row.StockItemId,
            StockItemName = row.StockItemName,
            BatchId = row.BatchId,
            BatchNumber = row.BatchNumber,
            TransactionType = transactionType,
            Quantity = row.Quantity,
            PerformedAt = row.PerformedAt,
            PerformedBy = "System",
            Reason = row.Reason,
            TransactionNumber = $"TXN-{row.PerformedAt:yyyyMMdd}-{row.Id:D4}",
            ReferenceDocument = row.ReferenceDocument,
        };
    }

    private static BatchInventoryItemDto MapBatchInventoryRow(BatchInventoryRow row)
    {
        return new BatchInventoryItemDto
        {
            BatchId = row.BatchId,
            StockItemId = row.StockItemId,
            StockItemName = row.StockItemName,
            BatchNumber = row.BatchNumber,
            CategoryId = row.CategoryId,
            Category = row.Category,
            CurrentQuantity = row.CurrentQuantity,
            UnitSymbol = row.UnitSymbol,
            ExpiryDate = row.ExpiryDate,
            ReceivedDate = row.ReceivedDate,
        };
    }

    /// <inheritdoc/>
    public async Task<StockItemDetailsDto> GetStockItemDetailsWithBatchesAsync(int stockItemId)
    {
        var stockItemDto = await GetStockLookupByIdAsync(stockItemId)
            ?? throw new InvalidOperationException($"Składnik magazynowy o ID {stockItemId} nie istnieje.");
        var activeBatches = (await _batchRepository.GetActiveBatchesByStockItemAsync(stockItemId)).ToList();
        var batchDtos = _mapper.Map<List<BatchDto>>(activeBatches);

        var logs = await _batchExpiryChangeLogRepository.GetByStockItemIdAsync(stockItemId);
        var logDtos = new List<BatchExpiryChangeLogDto>();

        var allBatchesForLookup = (await _batchRepository.GetBatchesByStockItemAsync(stockItemId))
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

    public async Task<IReadOnlyList<BatchDto>> GetActiveBatchesForStockItemAsync(int stockItemId)
    {
        var batches = await _batchRepository.GetActiveBatchesByStockItemAsync(stockItemId);
        return _mapper.Map<List<BatchDto>>(batches);
    }
}

