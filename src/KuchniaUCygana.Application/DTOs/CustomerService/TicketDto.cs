namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class TicketDto
{
    public int Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int ClientUserId { get; set; }

    public string? ClientFullName { get; set; }

    public int? AssignedToUserId { get; set; }

    public string? AssignedToFullName { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Priority { get; set; } = string.Empty;

    public DateTimeOffset? ClosedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
