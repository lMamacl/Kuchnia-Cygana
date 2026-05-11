namespace KuchniaUCygana.Domain.Enums;

public enum InventoryTransactionType
{
    Receipt = 1,          // Przyjęcie
    ProductionIssue = 2,  // Wydanie na produkcję
    Adjustment = 3,       // Korekta inwentaryzacyjna
    Waste = 4             // Strata / Przeterminowanie
}
