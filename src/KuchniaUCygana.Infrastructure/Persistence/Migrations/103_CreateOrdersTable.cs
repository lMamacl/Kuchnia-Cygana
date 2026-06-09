using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(103)]
public sealed class CreateOrdersTable : Migration
{
    public override void Up()
    {
        Create.Table("Orders")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("CustomerId").AsInt32().NotNullable().ForeignKey("Users", "Id")
            .WithColumn("OrderNumber").AsString(30).NotNullable().Unique()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("TotalPrice").AsDecimal(10, 2).NotNullable()
            .WithColumn("DiscountAmount").AsDecimal(10, 2).NotNullable().WithDefaultValue(0)
            .WithColumn("FinalPrice").AsDecimal(10, 2).NotNullable()
            .WithColumn("DiscountCodeId").AsInt32().Nullable()
                .ForeignKey("DiscountCodes", "Id")
            .WithColumn("Notes").AsString(1000).Nullable()
            .WithColumn("StartDate").AsDateTime().Nullable()
            .WithColumn("EndDate").AsDateTime().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();
    }

    public override void Down() => Delete.Table("Orders");
}
