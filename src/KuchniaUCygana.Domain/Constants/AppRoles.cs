namespace KuchniaUCygana.Domain.Constants;

public static class AppRoles
{
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
    public const string BOK = "BOK";
    public const string BOKManager = "BOKManager";

    public static readonly string[] StaffRoleNames =
    [
        Admin,
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
        HR,
        HRManager,
        BOK,
        BOKManager,
    ];

    public static bool IsStaffRole(string? role)
    {
        return !string.IsNullOrWhiteSpace(role) &&
            StaffRoleNames.Contains(role, StringComparer.Ordinal);
    }
}
