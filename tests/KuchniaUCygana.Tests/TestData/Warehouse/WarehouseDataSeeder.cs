using Bogus;
using KuchniaUCygana.Domain.Entities.Warehouse;
using KuchniaUCygana.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KuchniaUCygana.Tests.TestData.Warehouse;

public class WarehouseDataSeeder
{
    public static WarehouseMockContext GenerateMockData(int seed = 12345)
    {
        Randomizer.Seed = new Random(seed);
        
        var dateRef = DateTimeOffset.UtcNow;
        var auditableUser = "Bogus_System_User";

        // 1. Units of Measure
        var uomIdCounter = 1;
        var unitFaker = new Faker<UnitOfMeasure>("pl")
            .RuleFor(u => u.Id, f => uomIdCounter++)
            .RuleFor(u => u.Symbol, f => f.PickRandom("kg", "L", "szt", "g", "ml"))
            .RuleFor(u => u.Name, (f, u) => u.Symbol switch {
                "kg" => "Kilogram",
                "L" => "Litr",
                "szt" => "Sztuka",
                "g" => "Gram",
                "ml" => "Mililitr",
                _ => "Inne"
            })
            .RuleFor(u => u.CreatedAt, f => dateRef.AddDays(-f.Random.Int(100, 200)));

        var units = unitFaker.Generate(5).DistinctBy(u => u.Symbol).ToList();

        // 2. Stock Items
        var stockItemIdCounter = 1;
        var stockItemFaker = new Faker<StockItem>("pl")
            .RuleFor(si => si.Id, f => stockItemIdCounter++)
            .RuleFor(si => si.Name, f => f.Commerce.ProductName() + " (Składnik)")
            .RuleFor(si => si.BaseIngredientId, f => f.Random.Int(1, 100))
            .RuleFor(si => si.DefaultUnitOfMeasureId, f => f.PickRandom(units).Id)
            .RuleFor(si => si.MinimumLevel, f => f.Random.Decimal(10, 100))
            .RuleFor(si => si.LeadTimeDays, f => f.Random.Int(1, 14))
            .RuleFor(si => si.CreatedBy, f => auditableUser)
            .RuleFor(si => si.CreatedAt, f => dateRef.AddDays(-f.Random.Int(50, 100)));

        var stockItems = stockItemFaker.Generate(20);

        // 3. Batches (FEFO Core)
        var batchIdCounter = 1;
        var batchFaker = new Faker<Batch>("pl")
            .RuleFor(b => b.Id, f => batchIdCounter++)
            .RuleFor(b => b.StockItemId, f => f.PickRandom(stockItems).Id)
            .RuleFor(b => b.SupplierBatchNumber, f => "Dost-" + f.Random.Replace("###-???"))
            .RuleFor(b => b.CurrentQuantity, f => f.Random.Decimal(1, 500))
            .RuleFor(b => b.ReceivedDate, f => f.Date.PastOffset(1, dateRef))
            .RuleFor(b => b.ExpiryDate, (f, b) => b.ReceivedDate.AddDays(f.Random.Int(-5, 90))) // Some expired, some fresh
            .RuleFor(b => b.IsDepleted, (f, b) => b.CurrentQuantity <= 0 || f.Random.Bool(0.2f)) // 20% randomly marked deleted
            .RuleFor(b => b.CreatedBy, f => auditableUser)
            .RuleFor(b => b.CreatedAt, (f, b) => b.ReceivedDate);

        var batches = batchFaker.Generate(100);

        // 4. Inventory Transactions (Log for Sanepid)
        var transactionIdCounter = 1L;
        var transactionFaker = new Faker<InventoryTransaction>("pl")
            .RuleFor(t => t.Id, f => transactionIdCounter++)
            .RuleFor(t => t.BatchId, f => f.PickRandom(batches).Id)
            .RuleFor(t => t.TransactionType, f => f.PickRandom<InventoryTransactionType>())
            .RuleFor(t => t.QuantityChanged, (f, t) => t.TransactionType == InventoryTransactionType.Receipt ? f.Random.Decimal(10, 100) : -f.Random.Decimal(1, 20))
            .RuleFor(t => t.Reason, f => "Operacja symulowana")
            .RuleFor(t => t.ReferenceDocument, f => f.Random.Replace("DOC-#####"))
            .RuleFor(t => t.CreatedAt, f => f.Date.RecentOffset(30));

        var transactions = transactionFaker.Generate(300);

        // 5. Temperature Logs (Audit log for cold rooms)
        var tempLogIdCounter = 1L;
        var tempLogFaker = new Faker<TemperatureLog>("pl")
            .RuleFor(tl => tl.Id, f => tempLogIdCounter++)
            .RuleFor(tl => tl.DeviceNameOrLocation, f => f.PickRandom("Chłodnia A", "Chłodnia B", "Zamrażarka 1", "Magazyn Suchy"))
            .RuleFor(tl => tl.RecordedTemperatureCelsius, (f, tl) => 
                tl.DeviceNameOrLocation.Contains("Zamrażarka") 
                ? f.Random.Decimal(-22, -18) 
                : f.Random.Decimal(2, 8))
            .RuleFor(tl => tl.RecordedAt, f => f.Date.RecentOffset(30))
            .RuleFor(tl => tl.CreatedBy, f => "SensorSystem");

        var temperatureLogs = tempLogFaker.Generate(150);

        return new WarehouseMockContext
        {
            UnitsOfMeasure = units,
            StockItems = stockItems,
            Batches = batches,
            InventoryTransactions = transactions,
            TemperatureLogs = temperatureLogs
        };
    }
}

public class WarehouseMockContext
{
    public List<UnitOfMeasure> UnitsOfMeasure { get; set; } = new();
    public List<StockItem> StockItems { get; set; } = new();
    public List<Batch> Batches { get; set; } = new();
    public List<InventoryTransaction> InventoryTransactions { get; set; } = new();
    public List<TemperatureLog> TemperatureLogs { get; set; } = new();
}
