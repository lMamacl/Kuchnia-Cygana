namespace KuchniaUCygana.Application.DTOs.CustomerService;

public sealed class TicketPageDto
{
    public IReadOnlyList<TicketDto> Items { get; init; } = [];

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }
}
