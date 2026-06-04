using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Domain.Interfaces.Warehouse;

public interface IWarehouseCommandRepository
{
    Task<Batch> ReceiveDeliveryAsync(Batch batch, InventoryTransaction transaction);

    Task<IReadOnlyList<InventoryTransaction>> DeductStockAsync(WarehouseDeductionCommand command);

    Task<IReadOnlyList<InventoryAdjustmentResult>> ApplyInventoryAsync(
        IEnumerable<InventoryAdjustmentCommand> adjustments,
        string adjustedBy);

    Task<IReadOnlyList<BatchInventoryAdjustmentResult>> ApplyBatchInventoryAsync(
        IEnumerable<BatchInventoryAdjustmentCommand> adjustments,
        string adjustedBy);

    Task<BatchExpiryEditResult> EditBatchExpiryAsync(BatchExpiryEditCommand command);
}

public sealed record WarehouseDeductionCommand(
    int StockItemId,
    decimal Quantity,
    InventoryTransactionType TransactionType,
    string Reason,
    string? ReferenceDocument,
    int? BatchId,
    bool ExcludeExpired,
    bool RequireFullQuantity);

public sealed record InventoryAdjustmentCommand(
    int StockItemId,
    decimal ActualQuantity,
    string Reason);

public sealed record InventoryAdjustmentResult(
    int StockItemId,
    decimal QuantityBefore,
    decimal QuantityAfter,
    decimal Difference);

public sealed record BatchInventoryAdjustmentCommand(
    int BatchId,
    decimal ActualQuantity,
    string Reason);

public sealed record BatchInventoryAdjustmentResult(
    int BatchId,
    int StockItemId,
    decimal QuantityBefore,
    decimal QuantityAfter,
    decimal Difference);

public sealed record BatchExpiryEditCommand(
    int BatchId,
    DateTimeOffset NewExpiryDate,
    string Reason,
    int? ChangedByUserId);

public sealed record BatchExpiryEditResult(
    int BatchId,
    int StockItemId,
    DateTimeOffset? OldExpiryDate,
    DateTimeOffset NewExpiryDate);
