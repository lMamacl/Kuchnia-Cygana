using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(102)]
public sealed class CreateDeliveryWindowsTable : Migration
{
    public override void Up()
    {
        Create.Table("DeliveryWindows")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(50).NotNullable()
            .WithColumn("StartTime").AsString(5).NotNullable()
            .WithColumn("EndTime").AsString(5).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("SortOrder").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsDateTime().NotNullable()
            .WithColumn("UpdatedAt").AsDateTime().Nullable();

        Insert.IntoTable("DeliveryWindows").Row(new
        {
            Name = "6:00-10:00",
            StartTime = "06:00",
            EndTime = "10:00",
            IsActive = true,
            SortOrder = 1,
            CreatedAt = DateTime.UtcNow
        });
        Insert.IntoTable("DeliveryWindows").Row(new
        {
            Name = "10:00-14:00",
            StartTime = "10:00",
            EndTime = "14:00",
            IsActive = true,
            SortOrder = 2,
            CreatedAt = DateTime.UtcNow
        });
        Insert.IntoTable("DeliveryWindows").Row(new
        {
            Name = "14:00-18:00",
            StartTime = "14:00",
            EndTime = "18:00",
            IsActive = true,
            SortOrder = 3,
            CreatedAt = DateTime.UtcNow
        });
    }

    public override void Down() => Delete.Table("DeliveryWindows");
}
