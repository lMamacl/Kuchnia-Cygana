using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(106)]
public sealed class CreatePaymentsTable : Migration
{
    public override void Up()
    {
        Create.Table("Payments")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("OrderId").AsInt32().NotNullable().ForeignKey("Orders", "Id")
            .WithColumn("StripePaymentIntentId").AsString(200).NotNullable()
            .WithColumn("StripeClientSecret").AsString(500).Nullable()
            .WithColumn("Amount").AsDecimal(10, 2).NotNullable()
            .WithColumn("Currency").AsString(3).NotNullable().WithDefaultValue("PLN")
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("AttemptCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("LastAttemptAt").AsDateTimeOffset().Nullable()
            .WithColumn("ErrorMessage").AsString(1000).Nullable()
            .WithColumn("PaidAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();
    }

    public override void Down() => Delete.Table("Payments");
}
