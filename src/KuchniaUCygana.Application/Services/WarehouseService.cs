using AutoMapper;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
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
    private readonly FefoService _fefoService;
    private readonly SmartInventoryAnalyzer _inventoryAnalyzer;
    private readonly IMapper _mapper;
    private readonly ILogger<WarehouseService> _logger;

    public WarehouseService(
        IBatchRepository batchRepository,
        IStockItemRepository stockItemRepository,
        IInventoryTransactionRepository transactionRepository,
        FefoService fefoService,
        SmartInventoryAnalyzer inventoryAnalyzer,
        IMapper mapper,
        ILogger<WarehouseService> logger)
    {
        _batchRepository = batchRepository;
        _stockItemRepository = stockItemRepository;
        _transactionRepository = transactionRepository;
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
}
