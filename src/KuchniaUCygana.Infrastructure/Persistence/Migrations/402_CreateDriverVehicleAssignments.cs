using FluentMigrator;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(402, "Create driver vehicle assignments")]
public sealed class CreateDriverVehicleAssignments : Migration
{
    public override void Up()
    {
        if (Schema.Table("DriverVehicleAssignments").Exists())
        {
            return;
        }

        Create.Table("DriverVehicleAssignments")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("DriverId").AsInt32().NotNullable().ForeignKey("Drivers", "Id")
            .WithColumn("VehicleId").AsInt32().NotNullable().ForeignKey("Vehicles", "Id")
            .WithColumn("AssignedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UnassignedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

        Execute.Sql(
            """
            CREATE UNIQUE INDEX [UX_DriverVehicleAssignments_Active_Driver]
                ON [DriverVehicleAssignments] ([DriverId])
                WHERE [UnassignedAt] IS NULL;

            CREATE UNIQUE INDEX [UX_DriverVehicleAssignments_Active_Vehicle]
                ON [DriverVehicleAssignments] ([VehicleId])
                WHERE [UnassignedAt] IS NULL;
            """);
    }

    public override void Down()
    {
        if (Schema.Table("DriverVehicleAssignments").Exists())
        {
            Delete.Table("DriverVehicleAssignments");
        }
    }
}
