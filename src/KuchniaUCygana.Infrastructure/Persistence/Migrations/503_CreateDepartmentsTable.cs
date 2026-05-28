using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(503)]
public sealed class CreateDepartmentsTable : Migration
{
    public override void Up()
    {
        Create.Table("Departments")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Name").AsString(50).NotNullable()
            .WithColumn("Description").AsString(200).Nullable()
            .WithColumn("HeadEmployeeId").AsInt32().Nullable(); 
    }

    public override void Down() => Delete.Table("Departments");
}