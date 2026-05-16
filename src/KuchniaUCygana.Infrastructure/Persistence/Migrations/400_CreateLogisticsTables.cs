using FluentMigrator;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Infrastructure.Persistence.Migrations;

[Migration(400, "Create logistics module tables")]
public class CreateLogisticsTables : Migration
{
    public override void Up()
    {
        // 1. Vehicles
        Create.Table("Vehicles")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("RegistrationNumber").AsString(50).NotNullable()
            .WithColumn("Model").AsString(100).NotNullable()
            .WithColumn("MaxLoadKg").AsDecimal(18, 2).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(VehicleStatus.Active)
            // Kolumny bazowe i audytowe (wymagane dla AuditableEntity)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();

        // 2. Drivers
        Create.Table("Drivers")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable().ForeignKey("Users", "Id")
            .WithColumn("LicenseNumber").AsString(50).NotNullable()
            .WithColumn("IsActive").AsBoolean().NotNullable().WithDefaultValue(true)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();

        // 3. Dispatchers
        Create.Table("Dispatchers")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("UserId").AsInt32().NotNullable().ForeignKey("Users", "Id")
            .WithColumn("DeskPhoneNumber").AsString(20).NotNullable()
            .WithColumn("IsOnDuty").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();

        // 4. DeliveryRoutes
        Create.Table("DeliveryRoutes")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("RouteDate").AsDateTimeOffset().NotNullable()
            .WithColumn("TotalDistanceKm").AsDouble().NotNullable().WithDefaultValue(0)
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("VehicleId").AsInt32().Nullable().ForeignKey("Vehicles", "Id")
            .WithColumn("DriverId").AsInt32().Nullable().ForeignKey("Drivers", "Id")
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();

        // 5. DeliveryRouteStops
        Create.Table("DeliveryRouteStops")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("RouteId").AsInt32().NotNullable().ForeignKey("DeliveryRoutes", "Id")
            .WithColumn("DeliveryCalendarId").AsInt32().NotNullable() // Prawdopodobnie ID z modułu E-commerce, więc nie robimy klucza obcego na sztywno, bo tej tabeli może tu nie być w prostym teście
            .WithColumn("SequenceNumber").AsInt32().NotNullable()
            .WithColumn("PlannedArrivalTime").AsDateTimeOffset().Nullable()
            .WithColumn("ActualArrivalTime").AsDateTimeOffset().Nullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();

        // 6. ThermalBags
        Create.Table("ThermalBags")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("SerialNumber").AsString(50).NotNullable()
            .WithColumn("Status").AsInt32().NotNullable().WithDefaultValue(0)
            .WithColumn("LastCustomerId").AsInt32().Nullable()
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();

        // 7. BagMovementLogs
        // Uwaga: To dziedziczy po BaseEntity, a nie AuditableEntity, więc nie ma Soft Delete!
        Create.Table("BagMovementLogs")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("ThermalBagId").AsInt32().NotNullable().ForeignKey("ThermalBags", "Id")
            .WithColumn("FromStatus").AsInt32().NotNullable()
            .WithColumn("ToStatus").AsInt32().NotNullable()
            .WithColumn("DriverId").AsInt32().Nullable().ForeignKey("Drivers", "Id")
            .WithColumn("RouteStopId").AsInt32().Nullable().ForeignKey("DeliveryRouteStops", "Id")
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

            //8. Address
            Create.Table("Addresses")
            .WithColumn("Id").AsInt32().PrimaryKey().Identity()
            .WithColumn("Street").AsString(255).NotNullable()
            .WithColumn("City").AsString(100).NotNullable()
            .WithColumn("PostalCode").AsString(10).NotNullable()
            .WithColumn("Latitude").AsDouble().Nullable()
            .WithColumn("Longitude").AsDouble().Nullable()
            .WithColumn("IsGeoCoded").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
            .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable()
            .WithColumn("CreatedBy").AsString(256).Nullable()
            .WithColumn("UpdatedBy").AsString(256).Nullable()
            .WithColumn("IsDeleted").AsBoolean().NotNullable().WithDefaultValue(false)
            .WithColumn("DeletedAt").AsDateTimeOffset().Nullable()
            .WithColumn("DeletedBy").AsString(256).Nullable();
    }

    public override void Down()
    {
        // Usuwanie w odwrotnej kolejności (żeby zachować integralność kluczy obcych)
        Delete.Table("Addresses");
        Delete.Table("BagMovementLogs");
        Delete.Table("ThermalBags");
        Delete.Table("DeliveryRouteStops");
        Delete.Table("DeliveryRoutes");
        Delete.Table("Dispatchers");
        Delete.Table("Drivers");
        Delete.Table("Vehicles");
    }
}
