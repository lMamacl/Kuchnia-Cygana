using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(404)]
public sealed class CreateEmployeesTable : Migration
{
    public override void Up()
    {
        Create.Table("Employees")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable().Unique()
                .ForeignKey("FK_Employees_Users", "Users", "Id")
            .WithColumn("FirstName").AsString(50).NotNullable()
            .WithColumn("LastName").AsString(50).NotNullable()
            .WithColumn("Email").AsString(100).NotNullable()
            .WithColumn("PhoneNumber").AsString(15).Nullable()
            .WithColumn("HireDate").AsDate().NotNullable()
            .WithColumn("TerminationDate").AsDate().Nullable()
            .WithColumn("DepartmentId").AsInt32().NotNullable()
                .ForeignKey("FK_Employees_Departments", "Departments", "Id")
            .WithColumn("Position").AsString(100).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedBy").AsString(100).Nullable()
            .WithColumn("UpdatedBy").AsString(100).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(100).Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Create.ForeignKey("FK_Departments_HeadEmployee")
            .FromTable("Departments").ForeignColumn("HeadEmployeeId")
            .ToTable("Employees").PrimaryColumn("Id");
    }

    public override void Down()
    {
        Delete.ForeignKey("FK_Departments_HeadEmployee").OnTable("Departments");
        Delete.Table("Employees");
    }
}