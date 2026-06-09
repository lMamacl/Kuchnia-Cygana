namespace KuchniaUCygana.Domain.Enums;

public static class UserRoles
{
    // ── Klienckie ─────────────────────────────────
    public const string Client = "Client";

    // ── Kuchnia (M3 - Produkcja) ──────────────────
    public const string Kitchen = "Kitchen";
    public const string KitchenManager = "KitchenManager";

    // ── Magazyn (M3 - Warehouse) ──────────────────
    public const string Warehouse = "Warehouse";
    public const string WarehouseManager = "WarehouseManager";

    // ── Kompletacja (M3 - Packing) ────────────────
    public const string Packing = "Packing";
    public const string PackingManager = "PackingManager";

    // ── Dietetyk (M2) ─────────────────────────────
    public const string Dietitian = "Dietitian";

    // ── Logistyka (M4) ────────────────────────────
    public const string Logistics = "Logistics";
    public const string LogisticsManager = "LogisticsManager";
    public const string Driver = "Driver";
    public const string DriverManager = "DriverManager";

    // ── Administracja (M5) ────────────────────────
    public const string Admin = "Admin";
    public const string HR = "HR";
    public const string HRManager = "HRManager";
    public const string BOK = "BOK";
    public const string BOKManager = "BOKManager";
}
