namespace KuchniaUCygana.Domain.Enums;

/// <summary>
/// Status cyklu kompletacji i załadunku paczki/torby.
/// </summary>
public enum PackingStatus
{
    Pending = 0,      // Oczekuje na kompletację
    Packed = 1,       // Skompletowana — pudełka w torbie
    Labeled = 2,      // Oetykietowana — etykieta wysyłkowa naklejona
    Loaded = 3,       // Załadowana — torba w aucie
    Dispatched = 4    // Wydana — kurier przejął auto
}
