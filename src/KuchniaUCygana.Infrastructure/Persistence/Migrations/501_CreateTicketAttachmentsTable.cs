using FluentMigrator;
using System.Data;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(401)]
public sealed class CreateTicketAttachmentsTable : Migration
{
    public override void Up()
    {
        Create.Table("TicketAttachments")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("TicketId").AsInt32().NotNullable()
                .ForeignKey("FK_TicketAttachments_Tickets", "Tickets", "Id").OnDelete(Rule.Cascade)
            .WithColumn("FileName").AsString(255).NotNullable()
            .WithColumn("FilePath").AsString(500).NotNullable()
            .WithColumn("UploadedByUserId").AsInt32().NotNullable()
                .ForeignKey("FK_TicketAttachments_Users", "Users", "Id")
            .WithColumn("UploadedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.Index("IX_TicketAttachments_TicketId").OnTable("TicketAttachments").OnColumn("TicketId");
    }

    public override void Down() => Delete.Table("TicketAttachments");
}