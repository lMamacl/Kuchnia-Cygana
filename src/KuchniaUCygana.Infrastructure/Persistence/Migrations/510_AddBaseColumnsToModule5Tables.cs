using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(510)]
public sealed class AddBaseColumnsToModule5Tables : Migration
{
    public override void Up()
    {
        if (!Schema.Table("Departments").Column("CreatedAt").Exists())
        {
            Alter.Table("Departments")
                .AddColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
        }

        if (!Schema.Table("Departments").Column("UpdatedAt").Exists())
        {
            Alter.Table("Departments")
                .AddColumn("UpdatedAt").AsDateTimeOffset().Nullable();
        }

        if (!Schema.Table("SystemLogs").Column("CreatedAt").Exists())
        {
            Alter.Table("SystemLogs")
                .AddColumn("CreatedAt").AsDateTimeOffset().NotNullable().WithDefault(SystemMethods.CurrentUTCDateTime);
        }

        if (!Schema.Table("SystemLogs").Column("UpdatedAt").Exists())
        {
            Alter.Table("SystemLogs")
                .AddColumn("UpdatedAt").AsDateTimeOffset().Nullable();
        }
    }

    public override void Down()
    {
        if (Schema.Table("SystemLogs").Column("UpdatedAt").Exists())
        {
            Delete.Column("UpdatedAt").FromTable("SystemLogs");
        }

        if (Schema.Table("SystemLogs").Column("CreatedAt").Exists())
        {
            Delete.Column("CreatedAt").FromTable("SystemLogs");
        }

        if (Schema.Table("Departments").Column("UpdatedAt").Exists())
        {
            Delete.Column("UpdatedAt").FromTable("Departments");
        }

        if (Schema.Table("Departments").Column("CreatedAt").Exists())
        {
            Delete.Column("CreatedAt").FromTable("Departments");
        }
    }
}
