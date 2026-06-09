using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(104)]
public sealed class CreateOrderItemsTable : Migration
{
    public override void Up()
    {
        Create.Table("OrderItems")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OrderId").AsInt32().NotNullable().ForeignKey("Orders", "Id")
            .WithColumn("DietId").AsInt32().NotNullable()
            .WithColumn("DietVariantId").AsInt32().NotNullable()
            .WithColumn("DietName").AsString(200).NotNullable()
            .WithColumn("VariantName").AsString(100).NotNullable()
            .WithColumn("CaloriesPerDay").AsInt32().NotNullable()
            .WithColumn("PricePerDay").AsDecimal(10, 2).NotNullable()
            .WithColumn("TotalDays").AsInt32().NotNullable()
            .WithColumn("TotalPrice").AsDecimal(10, 2).NotNullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();
    }

    public override void Down() => Delete.Table("OrderItems");
}
