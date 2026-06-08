using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Web.Helpers;

public static class EnumDisplayNames
{
    public static string DisplayName(TicketStatus status)
        => status switch
        {
            TicketStatus.New => "Nowe",
            TicketStatus.Open => "Otwarte",
            TicketStatus.Pending => "Oczekuje",
            TicketStatus.Resolved => "Rozwiazane",
            TicketStatus.Closed => "Zamkniete",
            _ => status.ToString(),
        };

    public static string DisplayName(TicketPriority priority)
        => priority switch
        {
            TicketPriority.Low => "Niski",
            TicketPriority.Medium => "Sredni",
            TicketPriority.High => "Wysoki",
            TicketPriority.Critical => "Krytyczny",
            _ => priority.ToString(),
        };

    public static string DisplayName(WorkShift shift)
        => shift switch
        {
            WorkShift.Morning => "Poranna",
            WorkShift.Evening => "Popoludniowa",
            WorkShift.Night => "Nocna",
            _ => shift.ToString(),
        };

    public static string TicketStatusName(string? status)
        => Enum.TryParse<TicketStatus>(status, ignoreCase: true, out var parsed)
            ? DisplayName(parsed)
            : TextOrDash(status);

    public static string TicketPriorityName(string? priority)
        => Enum.TryParse<TicketPriority>(priority, ignoreCase: true, out var parsed)
            ? DisplayName(parsed)
            : TextOrDash(priority);

    public static string WorkShiftName(string? shift)
        => Enum.TryParse<WorkShift>(shift, ignoreCase: true, out var parsed)
            ? DisplayName(parsed)
            : TextOrDash(shift);

    private static string TextOrDash(string? value)
        => string.IsNullOrWhiteSpace(value) ? "-" : value;
}
