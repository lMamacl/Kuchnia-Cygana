namespace KuchniaUCygana.Domain.Constants;

public static class AppRoles
{
    public const string Client = "Client";
    public const string Kitchen = "Kitchen";
    public const string KitchenManager = "KitchenManager";
    public const string Warehouse = "Warehouse";
    public const string WarehouseManager = "WarehouseManager";
    public const string Packing = "Packing";
    public const string PackingManager = "PackingManager";
    public const string Dietitian = "Dietitian";
    public const string Logistics = "Logistics";
    public const string LogisticsManager = "LogisticsManager";
    public const string Driver = "Driver";
    public const string DriverManager = "DriverManager";
    public const string Admin = "Admin";
    public const string HR = "HR";
    public const string HRManager = "HRManager";

    public const string StaffAuthorizationRoles =
        Kitchen + "," +
        KitchenManager + "," +
        Warehouse + "," +
        WarehouseManager + "," +
        Packing + "," +
        PackingManager + "," +
        Dietitian + "," +
        Logistics + "," +
        LogisticsManager + "," +
        Driver + "," +
        DriverManager + "," +
        Admin + "," +
        HR + "," +
        HRManager;

    public static readonly string[] StaffRoleNames =
    [
        Kitchen,
        KitchenManager,
        Warehouse,
        WarehouseManager,
        Packing,
        PackingManager,
        Dietitian,
        Logistics,
        LogisticsManager,
        Driver,
        DriverManager,
        Admin,
        HR,
        HRManager,
    ];

    public static bool IsStaffRole(string role)
        => StaffRoleNames.Contains(role, StringComparer.Ordinal);
}
