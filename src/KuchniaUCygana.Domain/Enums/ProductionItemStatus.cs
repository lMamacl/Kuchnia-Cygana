namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status realizacji pojedynczej pozycji planu produkcji.
/// </summary>
public enum ProductionItemStatus
{
    Planned = 0,   // Zaplanowana — oczekuje na realizację
    Cooking = 1,   // W trakcie gotowania
    Cooked = 2,    // Ugotowana — gotowa do porcjowania
    Failed = 3     // Nieudana — problem podczas gotowania
}
