using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(12)]
public sealed class CreatePackingStatusLogs : Migration
{
    public override void Up()
    {
        Create.Table("PackingStatusLogs")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("PackingSessionId").AsInt32().NotNullable()
                .ForeignKey("FK_PackingStatusLogs_PackingSessions", "PackingSessions", "Id")
            .WithColumn("OldStatus").AsInt32().NotNullable()
            .WithColumn("NewStatus").AsInt32().NotNullable()
            .WithColumn("ChangedByUserId").AsInt32().Nullable()
            .WithColumn("ChangedAt").AsDateTime().NotNullable()
            .WithColumn("Notes").AsString(500).Nullable();

        Create.Index("IX_PackingStatusLogs_PackingSessionId")
            .OnTable("PackingStatusLogs")
            .OnColumn("PackingSessionId").Ascending();

        Create.Index("IX_PackingStatusLogs_ChangedAt")
            .OnTable("PackingStatusLogs")
            .OnColumn("ChangedAt").Descending();
    }

    public override void Down()
    {
        Delete.Index("IX_PackingStatusLogs_ChangedAt").OnTable("PackingStatusLogs");
        Delete.Index("IX_PackingStatusLogs_PackingSessionId").OnTable("PackingStatusLogs");
        Delete.ForeignKey("FK_PackingStatusLogs_PackingSessions").OnTable("PackingStatusLogs");
        Delete.Table("PackingStatusLogs");
    }
}
