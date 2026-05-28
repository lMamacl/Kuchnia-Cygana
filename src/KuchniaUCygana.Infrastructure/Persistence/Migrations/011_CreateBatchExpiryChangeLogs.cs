using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(11)]
public sealed class CreateBatchExpiryChangeLogs : Migration
{
    public override void Up()
    {
        Create.Table("BatchExpiryChangeLogs")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("BatchId").AsInt32().NotNullable()
                .ForeignKey("FK_BatchExpiryChangeLogs_Batches", "Batches", "Id")
            .WithColumn("OldExpiryDate").AsDateTime().NotNullable()
            .WithColumn("NewExpiryDate").AsDateTime().NotNullable()
            .WithColumn("Reason").AsString(500).NotNullable()
            .WithColumn("ChangedByUserId").AsInt32().Nullable()
            .WithColumn("ChangedAt").AsDateTime().NotNullable();

        Create.Index("IX_BatchExpiryChangeLogs_BatchId")
            .OnTable("BatchExpiryChangeLogs")
            .OnColumn("BatchId").Ascending();

        Create.Index("IX_BatchExpiryChangeLogs_ChangedAt")
            .OnTable("BatchExpiryChangeLogs")
            .OnColumn("ChangedAt").Descending();
    }

    public override void Down()
    {
        Delete.Index("IX_BatchExpiryChangeLogs_ChangedAt").OnTable("BatchExpiryChangeLogs");
        Delete.Index("IX_BatchExpiryChangeLogs_BatchId").OnTable("BatchExpiryChangeLogs");
        Delete.ForeignKey("FK_BatchExpiryChangeLogs_Batches").OnTable("BatchExpiryChangeLogs");
        Delete.Table("BatchExpiryChangeLogs");
    }
}
