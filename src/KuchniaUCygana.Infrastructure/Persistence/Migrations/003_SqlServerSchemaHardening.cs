using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(3)]
public sealed class SqlServerSchemaHardening : Migration
{
    public override void Up()
    {
        IfDatabase("SqlServer").Execute.WithConnection((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                ALTER TABLE [StockItems] ALTER COLUMN [MinimumLevel] DECIMAL(18,4) NOT NULL;
                ALTER TABLE [Batches] ALTER COLUMN [CurrentQuantity] DECIMAL(18,4) NOT NULL;
                ALTER TABLE [InventoryTransactions] ALTER COLUMN [QuantityChanged] DECIMAL(18,4) NOT NULL;
                ALTER TABLE [TemperatureLogs] ALTER COLUMN [RecordedTemperatureCelsius] DECIMAL(9,4) NOT NULL;

                ALTER TABLE [Users] ALTER COLUMN [CreatedAt] DATETIMEOFFSET(0) NOT NULL;
                ALTER TABLE [Users] ALTER COLUMN [UpdatedAt] DATETIMEOFFSET(0) NULL;
                ALTER TABLE [StockItems] ALTER COLUMN [UpdatedAt] DATETIMEOFFSET(0) NULL;
                ALTER TABLE [StockItems] ALTER COLUMN [DeletedAt] DATETIMEOFFSET(0) NULL;

                ALTER TABLE [Batches] ALTER COLUMN [ExpiryDate] DATETIMEOFFSET(0) NULL;
                ALTER TABLE [Batches] ALTER COLUMN [ReceivedDate] DATETIMEOFFSET(0) NOT NULL;
                ALTER TABLE [Batches] ALTER COLUMN [UpdatedAt] DATETIMEOFFSET(0) NULL;
                ALTER TABLE [Batches] ALTER COLUMN [DeletedAt] DATETIMEOFFSET(0) NULL;

                ALTER TABLE [InventoryTransactions] ALTER COLUMN [UpdatedAt] DATETIMEOFFSET(0) NULL;

                ALTER TABLE [TemperatureLogs] ALTER COLUMN [RecordedAt] DATETIMEOFFSET(0) NOT NULL;
                ALTER TABLE [TemperatureLogs] ALTER COLUMN [UpdatedAt] DATETIMEOFFSET(0) NULL;
                ALTER TABLE [TemperatureLogs] ALTER COLUMN [DeletedAt] DATETIMEOFFSET(0) NULL;
                """;
            command.ExecuteNonQuery();
        });
    }

    public override void Down()
    {
        IfDatabase("SqlServer").Execute.WithConnection((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                ALTER TABLE [StockItems] ALTER COLUMN [MinimumLevel] DECIMAL(18,2) NOT NULL;
                ALTER TABLE [Batches] ALTER COLUMN [CurrentQuantity] DECIMAL(18,2) NOT NULL;
                ALTER TABLE [InventoryTransactions] ALTER COLUMN [QuantityChanged] DECIMAL(18,2) NOT NULL;
                ALTER TABLE [TemperatureLogs] ALTER COLUMN [RecordedTemperatureCelsius] DECIMAL(18,2) NOT NULL;

                ALTER TABLE [Users] ALTER COLUMN [CreatedAt] DATETIME2 NOT NULL;
                ALTER TABLE [Users] ALTER COLUMN [UpdatedAt] DATETIME2 NULL;
                ALTER TABLE [StockItems] ALTER COLUMN [UpdatedAt] DATETIME2 NULL;
                ALTER TABLE [StockItems] ALTER COLUMN [DeletedAt] DATETIME2 NULL;
                ALTER TABLE [Batches] ALTER COLUMN [ExpiryDate] DATETIME2 NULL;
                ALTER TABLE [Batches] ALTER COLUMN [ReceivedDate] DATETIME2 NOT NULL;
                ALTER TABLE [Batches] ALTER COLUMN [UpdatedAt] DATETIME2 NULL;
                ALTER TABLE [Batches] ALTER COLUMN [DeletedAt] DATETIME2 NULL;
                ALTER TABLE [InventoryTransactions] ALTER COLUMN [UpdatedAt] DATETIME2 NULL;
                ALTER TABLE [TemperatureLogs] ALTER COLUMN [RecordedAt] DATETIME2 NOT NULL;
                ALTER TABLE [TemperatureLogs] ALTER COLUMN [UpdatedAt] DATETIME2 NULL;
                ALTER TABLE [TemperatureLogs] ALTER COLUMN [DeletedAt] DATETIME2 NULL;
                """;
            command.ExecuteNonQuery();
        });
    }
}
