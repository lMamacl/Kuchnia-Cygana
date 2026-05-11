using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(101)]
public sealed class CreateDiscountCodesTable : Migration
{
    public override void Up()
    {
        Create.Table("DiscountCodes")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Code").AsString(50).NotNullable().Unique()
            .WithColumn("DiscountType").AsInt32().NotNullable()
            .WithColumn("DiscountValue").AsDecimal(10, 2).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("ValidFrom").AsDateTimeOffset().Nullable()
            .WithColumn("ValidTo").AsDateTimeOffset().Nullable()
            .WithColumn("MaxUsageCount").AsInt32().Nullable()
            .WithColumn("UsedCount").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("MinimumOrderValue").AsDecimal(10, 2).Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();
    }

    public override void Down() => Delete.Table("DiscountCodes");
}
