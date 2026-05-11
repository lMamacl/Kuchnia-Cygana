using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(100)]
public sealed class CreateAddressesTable : Migration
{
    public override void Up()
    {
        Create.Table("Addresses")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable().ForeignKey("Users", "Id")
            .WithColumn("Label").AsString(50).NotNullable()
            .WithColumn("Street").AsString(200).NotNullable()
            .WithColumn("BuildingNumber").AsString(20).NotNullable()
            .WithColumn("ApartmentNumber").AsString(20).Nullable()
            .WithColumn("City").AsString(100).NotNullable()
            .WithColumn("PostalCode").AsString(10).NotNullable()
            .WithColumn("IsDefault").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeliveryNotes").AsString(500).Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();
    }

    public override void Down() => Delete.Table("Addresses");
}
