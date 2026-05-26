using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(406)]
public sealed class CreateSystemLogsTable : Migration
{
    public override void Up()
    {
        Create.Table("SystemLogs")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable()
                .ForeignKey("FK_SystemLogs_Users", "Users", "Id")
            .WithColumn("Action").AsString(100).NotNullable()
            .WithColumn("TargetEntity").AsString(50).NotNullable()
            .WithColumn("TargetId").AsString(100).NotNullable()
            .WithColumn("OldValue").AsString(int.MaxValue).Nullable()
            .WithColumn("NewValue").AsString(int.MaxValue).Nullable()
            .WithColumn("Timestamp").AsDateTimeOffset().NotNullable()
            .WithColumn("IPAddress").AsString(45).Nullable();
    }

    public override void Down() => Delete.Table("SystemLogs");
}