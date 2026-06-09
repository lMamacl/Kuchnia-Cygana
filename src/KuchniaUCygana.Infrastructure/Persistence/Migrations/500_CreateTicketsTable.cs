using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(500)]
public sealed class CreateTicketsTable : Migration
{
    public override void Up()
    {
        Create.Table("Tickets")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Title").AsString(200).NotNullable()
            .WithColumn("Description").AsString(int.MaxValue).NotNullable()
            .WithColumn("ClientUserId").AsInt32().NotNullable()
                .ForeignKey("FK_Tickets_Users", "Users", "Id")
            .WithColumn("AssignedToUserId").AsInt32().Nullable()
                .ForeignKey("FK_Tickets_AssignedUser", "Users", "Id")
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("Priority").AsInt32().NotNullable().WithDefaultValue(1)
            .WithColumn("ClosedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.Index("IX_Tickets_ClientUserId").OnTable("Tickets").OnColumn("ClientUserId");
        Create.Index("IX_Tickets_Status").OnTable("Tickets").OnColumn("Status");
        Create.Index("IX_Tickets_IsDeleted").OnTable("Tickets").OnColumn("IsDeleted");
    }

    public override void Down() => Delete.Table("Tickets");
}