using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(107)]
public sealed class CreateCustomerProfilesTable : Migration
{
    public override void Up()
    {
        Create.Table("CustomerProfiles")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable().ForeignKey("Users", "Id").Unique()
            .WithColumn("Phone").AsString(20).Nullable()
            .WithColumn("DietaryNotes").AsString(1000).Nullable()
            .WithColumn("DefaultAddressId").AsInt32().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();
    }

    public override void Down() => Delete.Table("CustomerProfiles");
}
