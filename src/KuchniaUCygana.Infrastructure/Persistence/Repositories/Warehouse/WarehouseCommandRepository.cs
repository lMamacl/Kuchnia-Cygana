using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Warehouse;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Warehouse;

public sealed class WarehouseCommandRepository : IWarehouseCommandRepository
{
    private readonly IDbConnectionFactory connectionFactory;

    public WarehouseCommandRepository(IDbConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public Task<Batch> ReceiveDeliveryAsync(Batch batch, InventoryTransaction transaction)
    {
        return ExecuteInTransactionAsync(async (db, tx) =>
        {
            await EnsureStockItemExistsAsync(db, tx, batch.StockItemId);

            var now = DateTimeOffset.UtcNow;
            batch.CreatedAt = now;
            batch.ReceivedDate = batch.ReceivedDate == default ? now : batch.ReceivedDate;
            batch.IsDeleted = false;
            batch.IsDepleted = batch.CurrentQuantity <= 0;

            batch.Id = await InsertBatchAsync(db, tx, batch);

            transaction.BatchId = batch.Id;
            transaction.StockItemId = batch.StockItemId;
            transaction.CreatedAt = now;
            await InsertInventoryTransactionAsync(db, tx, transaction);

            return batch;
        });
    }

    public Task<IReadOnlyList<InventoryTransaction>> DeductStockAsync(WarehouseDeductionCommand command)
    {
        if (command.Quantity <= 0)
        {
            throw new ArgumentException("Ilość musi być większa od 0.", nameof(command));
        }

        return ExecuteInTransactionAsync(async (db, tx) =>
        {
            await EnsureStockItemExistsAsync(db, tx, command.StockItemId);
            var now = DateTimeOffset.UtcNow;
            var batches = command.BatchId.HasValue
                ? await LoadSingleBatchForDeductionAsync(db, tx, command.StockItemId, command.BatchId.Value)
                : await LoadBatchesForDeductionAsync(db, tx, command.StockItemId, command.ExcludeExpired, now);

            var available = batches.Sum(b => b.CurrentQuantity);
            if (command.RequireFullQuantity && available < command.Quantity)
            {
                var scope = command.BatchId.HasValue ? "wybranej partii" : "magazynie";
                throw new InvalidOperationException(
                    $"Niewystarczająca ilość składnika w {scope}. Dostępne: {available:F2}, wymagane: {command.Quantity:F2}.");
            }

            var remaining = command.Quantity;
            var transactions = new List<InventoryTransaction>();

            foreach (var batch in batches)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var toDeduct = Math.Min(remaining, batch.CurrentQuantity);
                if (toDeduct <= 0)
                {
                    continue;
                }

                await UpdateBatchQuantityAsync(db, tx, batch, batch.CurrentQuantity - toDeduct, now);

                var transaction = new InventoryTransaction
                {
                    BatchId = batch.Id,
                    StockItemId = command.StockItemId,
                    TransactionType = command.TransactionType,
                    QuantityChanged = -toDeduct,
                    Reason = command.Reason,
                    ReferenceDocument = command.ReferenceDocument,
                    CreatedAt = now,
                };

                transaction.Id = await InsertInventoryTransactionAsync(db, tx, transaction);
                transactions.Add(transaction);
                remaining -= toDeduct;
            }

            if (command.RequireFullQuantity && remaining > 0)
            {
                throw new InvalidOperationException(
                    $"Nie udało się zdjąć pełnej ilości. Brakująca ilość: {remaining:F2}.");
            }

            return (IReadOnlyList<InventoryTransaction>)transactions;
        });
    }

    public Task<IReadOnlyList<InventoryAdjustmentResult>> ApplyInventoryAsync(
        IEnumerable<InventoryAdjustmentCommand> adjustments,
        string adjustedBy)
    {
        var commands = adjustments.ToList();
        if (commands.Any(a => a.ActualQuantity < 0))
        {
            throw new InvalidOperationException("Stan fizyczny nie może być ujemny.");
        }

        return ExecuteInTransactionAsync(async (db, tx) =>
        {
            var now = DateTimeOffset.UtcNow;
            var results = new List<InventoryAdjustmentResult>();

            foreach (var adjustment in commands)
            {
                await EnsureStockItemExistsAsync(db, tx, adjustment.StockItemId);

                var batches = await LoadBatchesForDeductionAsync(
                    db,
                    tx,
                    adjustment.StockItemId,
                    excludeExpired: false,
                    now);
                var currentStock = batches.Sum(b => b.CurrentQuantity);
                var difference = adjustment.ActualQuantity - currentStock;

                if (Math.Abs(difference) < 0.001m)
                {
                    continue;
                }

                await InsertInventoryAdjustmentAsync(
                    db,
                    tx,
                    adjustment.StockItemId,
                    currentStock,
                    adjustment.ActualQuantity,
                    difference,
                    adjustment.Reason,
                    adjustedBy,
                    now);

                if (difference < 0)
                {
                    await ApplyInventoryShortageAsync(db, tx, batches, adjustment, -difference, now);
                }
                else
                {
                    await ApplyInventorySurplusAsync(db, tx, adjustment, difference, now);
                }

                results.Add(new InventoryAdjustmentResult(
                    adjustment.StockItemId,
                    currentStock,
                    adjustment.ActualQuantity,
                    difference));
            }

            return (IReadOnlyList<InventoryAdjustmentResult>)results;
        });
    }

    public Task<IReadOnlyList<BatchInventoryAdjustmentResult>> ApplyBatchInventoryAsync(
        IEnumerable<BatchInventoryAdjustmentCommand> adjustments,
        string adjustedBy)
    {
        var commands = adjustments.ToList();
        if (commands.Any(a => a.ActualQuantity < 0))
        {
            throw new InvalidOperationException("Stan fizyczny partii nie może być ujemny.");
        }

        return ExecuteInTransactionAsync(async (db, tx) =>
        {
            var now = DateTimeOffset.UtcNow;
            var results = new List<BatchInventoryAdjustmentResult>();

            foreach (var adjustment in commands)
            {
                var batch = await LoadBatchForInventoryAsync(db, tx, adjustment.BatchId);
                var quantityBefore = batch.CurrentQuantity;
                var difference = adjustment.ActualQuantity - quantityBefore;

                if (Math.Abs(difference) < 0.001m)
                {
                    continue;
                }

                var reason = NormalizeReason(adjustment.Reason);
                await InsertInventoryAdjustmentAsync(
                    db,
                    tx,
                    batch.StockItemId,
                    quantityBefore,
                    adjustment.ActualQuantity,
                    difference,
                    $"Partia #{batch.Id} ({batch.SupplierBatchNumber}): {reason}",
                    adjustedBy,
                    now);

                await UpdateBatchQuantityAsync(db, tx, batch, adjustment.ActualQuantity, now);
                await InsertInventoryTransactionAsync(
                    db,
                    tx,
                    new InventoryTransaction
                    {
                        BatchId = batch.Id,
                        StockItemId = batch.StockItemId,
                        TransactionType = InventoryTransactionType.Adjustment,
                        QuantityChanged = difference,
                        Reason = $"Inwentaryzacja partii #{batch.Id}: {reason}",
                        ReferenceDocument = $"INV-BATCH-{batch.Id}",
                        CreatedAt = now,
                    });

                results.Add(new BatchInventoryAdjustmentResult(
                    batch.Id,
                    batch.StockItemId,
                    quantityBefore,
                    adjustment.ActualQuantity,
                    difference));
            }

            return (IReadOnlyList<BatchInventoryAdjustmentResult>)results;
        });
    }

    public Task<BatchExpiryEditResult> EditBatchExpiryAsync(BatchExpiryEditCommand command)
    {
        return ExecuteInTransactionAsync(async (db, tx) =>
        {
            var now = DateTimeOffset.UtcNow;
            var batch = await db.QuerySingleOrDefaultAsync<Batch>(
                """
                SELECT TOP 1 *
                FROM [Batches] WITH (UPDLOCK, ROWLOCK)
                WHERE [Id] = @batchId
                  AND [IsDeleted] = 0;
                """,
                new { batchId = command.BatchId },
                tx);

            if (batch is null)
            {
                throw new InvalidOperationException($"Partia o ID {command.BatchId} nie istnieje.");
            }

            var oldExpiryDate = batch.ExpiryDate;
            await db.ExecuteAsync(
                """
                UPDATE [Batches]
                SET [ExpiryDate] = @newExpiryDate,
                    [UpdatedAt] = @now
                WHERE [Id] = @batchId;
                """,
                new
                {
                    batchId = batch.Id,
                    newExpiryDate = command.NewExpiryDate,
                    now,
                },
                tx);

            await db.ExecuteAsync(
                """
                INSERT INTO [BatchExpiryChangeLogs]
                    ([BatchId], [OldExpiryDate], [NewExpiryDate], [Reason], [ChangedByUserId], [ChangedAt], [CreatedAt], [UpdatedAt])
                VALUES
                    (@BatchId, @OldExpiryDate, @NewExpiryDate, @Reason, @ChangedByUserId, @ChangedAt, @CreatedAt, NULL);
                """,
                new
                {
                    BatchId = batch.Id,
                    OldExpiryDate = oldExpiryDate?.UtcDateTime ?? DateTime.MinValue,
                    NewExpiryDate = command.NewExpiryDate.UtcDateTime,
                    Reason = Truncate(command.Reason, 500),
                    command.ChangedByUserId,
                    ChangedAt = now.UtcDateTime,
                    CreatedAt = now,
                },
                tx);

            await InsertInventoryTransactionAsync(
                db,
                tx,
                new InventoryTransaction
                {
                    BatchId = batch.Id,
                    StockItemId = batch.StockItemId,
                    TransactionType = InventoryTransactionType.ExpiryDateChanged,
                    QuantityChanged = 0,
                    Reason = $"Zmiana daty ważności: {oldExpiryDate:dd.MM.yyyy} -> {command.NewExpiryDate:dd.MM.yyyy}. Powód: {command.Reason}",
                    ReferenceDocument = $"LOG-{batch.Id}",
                    CreatedAt = now,
                });

            return new BatchExpiryEditResult(batch.Id, batch.StockItemId, oldExpiryDate, command.NewExpiryDate);
        });
    }

    private async Task<T> ExecuteInTransactionAsync<T>(Func<IDbConnection, IDbTransaction, Task<T>> action)
    {
        using var db = connectionFactory.CreateConnection();
        db.Open();
        using var tx = db.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            var result = await action(db, tx);
            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private static async Task EnsureStockItemExistsAsync(IDbConnection db, IDbTransaction tx, int stockItemId)
    {
        var exists = await db.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(1)
            FROM [StockItems]
            WHERE [Id] = @stockItemId
              AND [IsDeleted] = 0;
            """,
            new { stockItemId },
            tx);

        if (exists == 0)
        {
            throw new InvalidOperationException($"Składnik magazynowy o ID {stockItemId} nie istnieje.");
        }
    }

    private static async Task<int> InsertBatchAsync(IDbConnection db, IDbTransaction tx, Batch batch)
    {
        return await db.QuerySingleAsync<int>(
            """
            INSERT INTO [Batches]
                ([StockItemId], [SupplierBatchNumber], [CurrentQuantity], [ExpiryDate], [ReceivedDate], [IsDepleted],
                 [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            VALUES
                (@StockItemId, @SupplierBatchNumber, @CurrentQuantity, @ExpiryDate, @ReceivedDate, @IsDepleted,
                 @CreatedBy, @UpdatedBy, @IsDeleted, @DeletedAt, @DeletedBy, @CreatedAt, @UpdatedAt);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """,
            batch,
            tx);
    }

    private static async Task<long> InsertInventoryTransactionAsync(
        IDbConnection db,
        IDbTransaction tx,
        InventoryTransaction transaction)
    {
        return await db.QuerySingleAsync<long>(
            """
            INSERT INTO [InventoryTransactions]
                ([BatchId], [StockItemId], [TransactionType], [QuantityChanged], [Reason], [ReferenceDocument],
                 [CreatedAt], [UpdatedAt])
            OUTPUT INSERTED.[Id]
            VALUES
                (@BatchId, @StockItemId, @TransactionType, @QuantityChanged, @Reason, @ReferenceDocument,
                 @CreatedAt, @UpdatedAt);
            """,
            new
            {
                transaction.BatchId,
                transaction.StockItemId,
                TransactionType = (int)transaction.TransactionType,
                transaction.QuantityChanged,
                Reason = Truncate(transaction.Reason, 250),
                ReferenceDocument = Truncate(transaction.ReferenceDocument, 50),
                transaction.CreatedAt,
                transaction.UpdatedAt,
            },
            tx);
    }

    private static async Task InsertInventoryAdjustmentAsync(
        IDbConnection db,
        IDbTransaction tx,
        int stockItemId,
        decimal quantityBefore,
        decimal quantityAfter,
        decimal difference,
        string reason,
        string adjustedBy,
        DateTimeOffset now)
    {
        await db.ExecuteAsync(
            """
            INSERT INTO [InventoryAdjustments]
                ([StockItemId], [QuantityBefore], [QuantityAfter], [Difference], [Reason], [AdjustedBy],
                 [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy], [CreatedAt], [UpdatedAt])
            VALUES
                (@StockItemId, @QuantityBefore, @QuantityAfter, @Difference, @Reason, @AdjustedBy,
                 @AdjustedBy, NULL, 0, NULL, NULL, @CreatedAt, NULL);
            """,
            new
            {
                StockItemId = stockItemId,
                QuantityBefore = quantityBefore,
                QuantityAfter = quantityAfter,
                Difference = difference,
                Reason = Truncate(NormalizeReason(reason), 500),
                AdjustedBy = Truncate(string.IsNullOrWhiteSpace(adjustedBy) ? "System" : adjustedBy.Trim(), 50),
                CreatedAt = now,
            },
            tx);
    }

    private static async Task<IReadOnlyList<Batch>> LoadSingleBatchForDeductionAsync(
        IDbConnection db,
        IDbTransaction tx,
        int stockItemId,
        int batchId)
    {
        var batch = await db.QuerySingleOrDefaultAsync<Batch>(
            """
            SELECT TOP 1 *
            FROM [Batches] WITH (UPDLOCK, ROWLOCK)
            WHERE [Id] = @batchId
              AND [IsDeleted] = 0
              AND [IsDepleted] = 0;
            """,
            new { batchId },
            tx);

        if (batch is null)
        {
            throw new InvalidOperationException($"Partia o ID {batchId} nie istnieje albo jest już wyczerpana.");
        }

        if (batch.StockItemId != stockItemId)
        {
            throw new InvalidOperationException("Wskazana partia nie należy do wybranego składnika.");
        }

        return new[] { batch };
    }

    private static async Task<Batch> LoadBatchForInventoryAsync(
        IDbConnection db,
        IDbTransaction tx,
        int batchId)
    {
        var batch = await db.QuerySingleOrDefaultAsync<Batch>(
            """
            SELECT TOP 1 *
            FROM [Batches] WITH (UPDLOCK, ROWLOCK)
            WHERE [Id] = @batchId
              AND [IsDeleted] = 0;
            """,
            new { batchId },
            tx);

        if (batch is null)
        {
            throw new InvalidOperationException($"Partia o ID {batchId} nie istnieje.");
        }

        return batch;
    }

    private static async Task<List<Batch>> LoadBatchesForDeductionAsync(
        IDbConnection db,
        IDbTransaction tx,
        int stockItemId,
        bool excludeExpired,
        DateTimeOffset now)
    {
        var expiryFilter = excludeExpired
            ? "AND ([ExpiryDate] IS NULL OR [ExpiryDate] >= @now)"
            : string.Empty;

        var batches = await db.QueryAsync<Batch>(
            $"""
            SELECT *
            FROM [Batches] WITH (UPDLOCK, ROWLOCK)
            WHERE [StockItemId] = @stockItemId
              AND [IsDeleted] = 0
              AND [IsDepleted] = 0
              AND [CurrentQuantity] > 0
              {expiryFilter}
            ORDER BY
              CASE WHEN [ExpiryDate] IS NULL THEN 1 ELSE 0 END,
              [ExpiryDate] ASC,
              [Id] ASC;
            """,
            new { stockItemId, now },
            tx);

        return batches.ToList();
    }

    private static async Task UpdateBatchQuantityAsync(
        IDbConnection db,
        IDbTransaction tx,
        Batch batch,
        decimal newQuantity,
        DateTimeOffset now)
    {
        var safeQuantity = Math.Max(0, newQuantity);
        var affected = await db.ExecuteAsync(
            """
            UPDATE [Batches]
            SET [CurrentQuantity] = @newQuantity,
                [IsDepleted] = @isDepleted,
                [UpdatedAt] = @now
            WHERE [Id] = @batchId
              AND [CurrentQuantity] = @oldQuantity
              AND [IsDeleted] = 0;
            """,
            new
            {
                batchId = batch.Id,
                oldQuantity = batch.CurrentQuantity,
                newQuantity = safeQuantity,
                isDepleted = safeQuantity <= 0,
                now,
            },
            tx);

        if (affected != 1)
        {
            throw new InvalidOperationException("Stan partii zmienił się w trakcie operacji. Odśwież dane i spróbuj ponownie.");
        }

        batch.CurrentQuantity = safeQuantity;
        batch.IsDepleted = safeQuantity <= 0;
    }

    private static async Task ApplyInventoryShortageAsync(
        IDbConnection db,
        IDbTransaction tx,
        IReadOnlyList<Batch> batches,
        InventoryAdjustmentCommand adjustment,
        decimal shortage,
        DateTimeOffset now)
    {
        var remaining = shortage;
        foreach (var batch in batches)
        {
            if (remaining <= 0)
            {
                break;
            }

            var toDeduct = Math.Min(remaining, batch.CurrentQuantity);
            if (toDeduct <= 0)
            {
                continue;
            }

            await UpdateBatchQuantityAsync(db, tx, batch, batch.CurrentQuantity - toDeduct, now);
            await InsertInventoryTransactionAsync(
                db,
                tx,
                new InventoryTransaction
                {
                    BatchId = batch.Id,
                    StockItemId = adjustment.StockItemId,
                    TransactionType = InventoryTransactionType.Adjustment,
                    QuantityChanged = -toDeduct,
                    Reason = $"Inwentaryzacja: {NormalizeReason(adjustment.Reason)}",
                    ReferenceDocument = $"INV-{adjustment.StockItemId}",
                    CreatedAt = now,
                });

            remaining -= toDeduct;
        }

        if (remaining > 0.001m)
        {
            throw new InvalidOperationException("Nie udało się rozliczyć niedoboru inwentaryzacyjnego.");
        }
    }

    private static async Task ApplyInventorySurplusAsync(
        IDbConnection db,
        IDbTransaction tx,
        InventoryAdjustmentCommand adjustment,
        decimal surplus,
        DateTimeOffset now)
    {
        var batch = new Batch
        {
            StockItemId = adjustment.StockItemId,
            SupplierBatchNumber = $"INV-{now:yyyyMMddHHmmss}-{adjustment.StockItemId}",
            CurrentQuantity = surplus,
            ExpiryDate = null,
            ReceivedDate = now,
            IsDepleted = false,
            IsDeleted = false,
            CreatedBy = "Inventory",
            CreatedAt = now,
        };

        batch.Id = await InsertBatchAsync(db, tx, batch);
        await InsertInventoryTransactionAsync(
            db,
            tx,
            new InventoryTransaction
            {
                BatchId = batch.Id,
                StockItemId = adjustment.StockItemId,
                TransactionType = InventoryTransactionType.Adjustment,
                QuantityChanged = surplus,
                Reason = $"Inwentaryzacja: {NormalizeReason(adjustment.Reason)}",
                ReferenceDocument = batch.SupplierBatchNumber,
                CreatedAt = now,
            });
    }

    private static string NormalizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason) ? "Spis z natury" : reason.Trim();
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
