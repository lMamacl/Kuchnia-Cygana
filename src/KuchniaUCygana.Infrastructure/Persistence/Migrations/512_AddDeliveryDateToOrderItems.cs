using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(512)]
public sealed class AddDeliveryDateToOrderItems : Migration
{
    public override void Up()
    {
        if (!Schema.Table("OrderItems").Column("DeliveryDate").Exists())
        {
            Alter.Table("OrderItems").AddColumn("DeliveryDate").AsDateTime().Nullable();
            Create.Index("IX_OrderItems_Order_DeliveryDate_IsDeleted")
                .OnTable("OrderItems")
                .OnColumn("OrderId").Ascending()
                .OnColumn("DeliveryDate").Ascending()
                .OnColumn("IsDeleted").Ascending();
        }
    }

    public override void Down()
    {
        if (Schema.Table("OrderItems").Column("DeliveryDate").Exists())
        {
            Delete.Index("IX_OrderItems_Order_DeliveryDate_IsDeleted").OnTable("OrderItems");
            Delete.Column("DeliveryDate").FromTable("OrderItems");
        }
    }
}
