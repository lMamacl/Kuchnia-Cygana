using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Tests.TestData.Warehouse;

public static class WarehouseDataSeeder
{
    public static WarehouseMockContext GenerateMockData(int seed = 12345)
    {
        Randomizer.Seed = new Random(seed);

        // Staly punkt odniesienia czasu zapewnia pelna deterministycznosc danych testowych.
        var dateRef = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var auditableUser = "Bogus_System_User";

        var unitIdCounter = 1;
        var unitFaker = new Faker<UnitOfMeasure>("pl")
            .RuleFor(u => u.Id, _ => unitIdCounter++)
            .RuleFor(u => u.Symbol, f => f.PickRandom("kg", "L", "szt", "g", "ml"))
            .RuleFor(u => u.Name, (_, u) => u.Symbol switch
            {
                "kg" => "Kilogram",
                "L" => "Litr",
                "szt" => "Sztuka",
                "g" => "Gram",
                "ml" => "Mililitr",
                _ => "Inne",
            })
            .RuleFor(u => u.CreatedAt, f => dateRef.AddDays(-f.Random.Int(100, 200)));

        var units = unitFaker.Generate(8)
            .DistinctBy(u => u.Symbol)
            .ToList();

        var stockItemIdCounter = 1;
        var stockItemFaker = new Faker<StockItem>("pl")
            .RuleFor(si => si.Id, _ => stockItemIdCounter++)
            .RuleFor(si => si.Name, f => $"{f.Commerce.ProductName()} (Skladnik)")
            .RuleFor(si => si.BaseIngredientId, f => f.Random.Int(1, 100))
            .RuleFor(si => si.DefaultUnitOfMeasureId, f => f.PickRandom(units).Id)
            .RuleFor(si => si.MinimumLevel, f => f.Random.Decimal(10, 100))
            .RuleFor(si => si.LeadTimeDays, f => f.Random.Int(1, 14))
            .RuleFor(si => si.CreatedBy, _ => auditableUser)
            .RuleFor(si => si.CreatedAt, f => dateRef.AddDays(-f.Random.Int(50, 100)));

        var stockItems = stockItemFaker.Generate(20);

        var batchIdCounter = 1;
        var batchFaker = new Faker<Batch>("pl")
            .RuleFor(b => b.Id, _ => batchIdCounter++)
            .RuleFor(b => b.StockItemId, f => f.PickRandom(stockItems).Id)
            .RuleFor(b => b.SupplierBatchNumber, f => "DOST-" + f.Random.Replace("###-???"))
            .RuleFor(b => b.CurrentQuantity, f => f.Random.Decimal(1, 500))
            .RuleFor(b => b.ReceivedDate, f => dateRef.AddDays(-f.Random.Int(1, 90)))
            .RuleFor(b => b.ExpiryDate, (f, b) =>
                f.Random.Bool(0.15f) ? null : b.ReceivedDate.AddDays(f.Random.Int(-5, 90)))
            .RuleFor(b => b.IsDepleted, f => f.Random.Bool(0.2f))
            .RuleFor(b => b.IsDeleted, f => f.Random.Bool(0.1f))
            .RuleFor(b => b.CreatedBy, _ => auditableUser)
            .RuleFor(b => b.CreatedAt, (_, b) => b.ReceivedDate);

        var batches = batchFaker.Generate(120);

        var transactionIdCounter = 1L;
        var transactionFaker = new Faker<InventoryTransaction>("pl")
            .RuleFor(t => t.Id, _ => transactionIdCounter++)
            .RuleFor(t => t.BatchId, f => f.PickRandom(batches).Id)
            .RuleFor(t => t.TransactionType, f => f.PickRandom<InventoryTransactionType>())
            .RuleFor(t => t.QuantityChanged, (f, t) =>
                t.TransactionType == InventoryTransactionType.Receipt
                    ? f.Random.Decimal(10, 100)
                    : -f.Random.Decimal(1, 20))
            .RuleFor(t => t.Reason, _ => "Operacja testowa")
            .RuleFor(t => t.ReferenceDocument, f => f.Random.Replace("DOC-#####"))
            .RuleFor(t => t.CreatedAt, f => dateRef.AddDays(-f.Random.Int(0, 30)));

        var transactions = transactionFaker.Generate(320);

        var temperatureLogIdCounter = 1L;
        var tempLogFaker = new Faker<TemperatureLog>("pl")
            .RuleFor(tl => tl.Id, _ => temperatureLogIdCounter++)
            .RuleFor(tl => tl.DeviceNameOrLocation, f => f.PickRandom("Chlodnia A", "Chlodnia B", "Zamrazarka 1", "Magazyn Suchy"))
            .RuleFor(tl => tl.RecordedTemperatureCelsius, (f, tl) =>
                tl.DeviceNameOrLocation.Contains("Zamrazarka", StringComparison.OrdinalIgnoreCase)
                    ? f.Random.Decimal(-22, -18)
                    : f.Random.Decimal(2, 8))
            .RuleFor(tl => tl.RecordedAt, f => dateRef.AddHours(-f.Random.Int(0, 24 * 30)))
            .RuleFor(tl => tl.CreatedBy, _ => "SensorSystem")
            .RuleFor(tl => tl.CreatedAt, f => dateRef.AddHours(-f.Random.Int(0, 24 * 30)));

        var temperatureLogs = tempLogFaker.Generate(160);

        return new WarehouseMockContext
        {
            UnitsOfMeasure = units,
            StockItems = stockItems,
            Batches = batches,
            InventoryTransactions = transactions,
            TemperatureLogs = temperatureLogs,
        };
    }
}

public sealed class WarehouseMockContext
{
    public List<UnitOfMeasure> UnitsOfMeasure { get; set; } = new();

    public List<StockItem> StockItems { get; set; } = new();

    public List<Batch> Batches { get; set; } = new();

    public List<InventoryTransaction> InventoryTransactions { get; set; } = new();

    public List<TemperatureLog> TemperatureLogs { get; set; } = new();
}
