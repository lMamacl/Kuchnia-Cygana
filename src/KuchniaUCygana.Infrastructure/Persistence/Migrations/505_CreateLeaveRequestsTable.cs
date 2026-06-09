using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(505)]
public sealed class CreateLeaveRequestsTable : Migration
{
    public override void Up()
    {
        Create.Table("LeaveRequests")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("EmployeeId").AsInt32().NotNullable()
                .ForeignKey("FK_LeaveRequests_Employees", "Employees", "Id")
            .WithColumn("LeaveType").AsInt32().NotNullable()
            .WithColumn("StartDate").AsDate().NotNullable()
            .WithColumn("EndDate").AsDate().NotNullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("ApprovedByEmployeeId").AsInt32().Nullable()
                .ForeignKey("FK_LeaveRequests_Approver", "Employees", "Id")
            .WithColumn("RejectionReason").AsString(500).Nullable()
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();
    }

    public override void Down() => Delete.Table("LeaveRequests");
}