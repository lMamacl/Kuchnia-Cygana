namespace KuchniaUCygana.Application.DTOs.Packing;

public sealed class PackingSynchronizationResultDto
{
    public DateOnly Date { get; set; }

    public int CreatedSessions { get; set; }

    public int UpdatedSessions { get; set; }

    public int CreatedBags { get; set; }

    public int TotalSessions { get; set; }
}
