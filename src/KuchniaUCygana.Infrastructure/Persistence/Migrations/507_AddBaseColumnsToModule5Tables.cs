using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(507)]
public sealed class AddBaseColumnsToModule5Tables : Migration
{
    public override void Up()
    {
        Alter.Table("Departments")
            .AddColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .AddColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Alter.Table("SystemLogs")
            .AddColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime)
            .AddColumn("UpdatedAt").AsDateTimeOffset().Nullable();
    }

    public override void Down()
    {
        Delete.Column("UpdatedAt").FromTable("SystemLogs");
        Delete.Column("CreatedAt").FromTable("SystemLogs");
        Delete.Column("UpdatedAt").FromTable("Departments");
        Delete.Column("CreatedAt").FromTable("Departments");
    }
}
