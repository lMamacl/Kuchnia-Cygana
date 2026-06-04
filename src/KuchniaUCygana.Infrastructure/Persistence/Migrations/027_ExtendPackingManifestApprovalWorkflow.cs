using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(27)]
public sealed class ExtendPackingManifestApprovalWorkflow : Migration
{
    public override void Up()
    {
        if (!Schema.Table("PackingManifests").Column("WorkerApprovedAt").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("WorkerApprovedAt").AsDateTimeOffset().Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("WorkerApprovedBy").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("WorkerApprovedBy").AsString(100).Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("WorkerApprovedByUserId").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("WorkerApprovedByUserId").AsInt32().Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("SentToLogisticsAt").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("SentToLogisticsAt").AsDateTimeOffset().Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("SentToLogisticsByUserId").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("SentToLogisticsByUserId").AsInt32().Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("RequiresRegeneration").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("RequiresRegeneration").AsBoolean().NotNullable().WithDefaultValue(false);
        }

        if (!Schema.Table("PackingManifests").Column("RequiresRegenerationReason").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("RequiresRegenerationReason").AsString(500).Nullable();
        }

        if (!Schema.Table("PackingManifests").Column("SnapshotHash").Exists())
        {
            Alter.Table("PackingManifests")
                .AddColumn("SnapshotHash").AsString(128).Nullable();
        }

        if (!Schema.Table("PackingManifestIssues").Exists())
        {
            Create.Table("PackingManifestIssues")
                .WithColumn("Id").AsInt32().PrimaryKey().Identity()
                .WithColumn("PackingManifestId").AsInt32().Nullable()
                .WithColumn("PackingDate").AsDate().NotNullable()
                .WithColumn("RouteId").AsInt32().NotNullable()
                .WithColumn("IssueType").AsString(80).NotNullable()
                .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("IsBlocking").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("Title").AsString(200).NotNullable()
                .WithColumn("Details").AsString(1000).NotNullable()
                .WithColumn("SourceType").AsString(50).Nullable()
                .WithColumn("SourceId").AsInt32().Nullable()
                .WithColumn("ReportedAt").AsDateTimeOffset().NotNullable()
                .WithColumn("ResolvedAt").AsDateTimeOffset().Nullable()
                .WithColumn("ResolvedByUserId").AsInt32().Nullable()
                .WithColumn("ResolutionNotes").AsString(1000).Nullable()
                .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
                .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

            Create.Index("IX_PackingManifestIssues_DateRoute")
                .OnTable("PackingManifestIssues")
                .OnColumn("PackingDate").Ascending()
                .OnColumn("RouteId").Ascending()
                .OnColumn("Status").Ascending();
        }
    }

    public override void Down()
    {
        if (Schema.Table("PackingManifestIssues").Exists())
        {
            Delete.Table("PackingManifestIssues");
        }

        if (Schema.Table("PackingManifests").Column("SnapshotHash").Exists())
        {
            Delete.Column("SnapshotHash").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("RequiresRegenerationReason").Exists())
        {
            Delete.Column("RequiresRegenerationReason").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("RequiresRegeneration").Exists())
        {
            Delete.Column("RequiresRegeneration").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("SentToLogisticsByUserId").Exists())
        {
            Delete.Column("SentToLogisticsByUserId").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("SentToLogisticsAt").Exists())
        {
            Delete.Column("SentToLogisticsAt").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("WorkerApprovedByUserId").Exists())
        {
            Delete.Column("WorkerApprovedByUserId").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("WorkerApprovedBy").Exists())
        {
            Delete.Column("WorkerApprovedBy").FromTable("PackingManifests");
        }

        if (Schema.Table("PackingManifests").Column("WorkerApprovedAt").Exists())
        {
            Delete.Column("WorkerApprovedAt").FromTable("PackingManifests");
        }
    }
}
