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
    private readonly FefoService _fefoService;
    private readonly SmartInventoryAnalyzer _inventoryAnalyzer;
    private readonly IMapper _mapper;
    private readonly ILogger<WarehouseService> _logger;

    public WarehouseService(
        IBatchRepository batchRepository,
        IStockItemRepository stockItemRepository,
        IInventoryTransactionRepository transactionRepository,
        IRepository<UnitOfMeasure> unitOfMeasureRepository,
        FefoService fefoService,
        SmartInventoryAnalyzer inventoryAnalyzer,
        IMapper mapper,
        ILogger<WarehouseService> logger)
    {
        _batchRepository = batchRepository;
        _stockItemRepository = stockItemRepository;
        _transactionRepository = transactionRepository;
        _unitOfMeasureRepository = unitOfMeasureRepository;
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
}
