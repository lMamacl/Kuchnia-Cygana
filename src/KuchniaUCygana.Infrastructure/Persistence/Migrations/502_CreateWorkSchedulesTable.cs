using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(502)]
public sealed class CreateWorkSchedulesTable : Migration
{
    public override void Up()
    {
        Create.Table("WorkSchedules")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable()
                .ForeignKey("FK_WorkSchedules_Users", "Users", "Id")
            .WithColumn("ShiftDate").AsDate().NotNullable()
            .WithColumn("Shift").AsInt32().NotNullable()
            .WithColumn("RoleAtShift").AsString(50).Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.UniqueConstraint("UQ_WorkSchedules_User_Date_Shift")
            .OnTable("WorkSchedules")
            .Columns("UserId", "ShiftDate", "Shift");

        Create.Index("IX_WorkSchedules_ShiftDate").OnTable("WorkSchedules").OnColumn("ShiftDate");
    }

    public override void Down() => Delete.Table("WorkSchedules");
}