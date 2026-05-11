namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status cyklu życia planu produkcji.
/// </summary>
public enum ProductionPlanStatus
{
    Draft = 0,        // Szkic — plan w trakcie generowania
    Active = 1,       // Aktywny — plan udostępniony Szefowi Kuchni
    InProgress = 2,   // W realizacji — trwa gotowanie
    Completed = 3,    // Zakończony — wszystkie pozycje ugotowane
    Cancelled = 4     // Anulowany
}
