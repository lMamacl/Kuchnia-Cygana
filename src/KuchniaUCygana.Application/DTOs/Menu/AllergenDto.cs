namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class AllergenDto
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? IconUrl { get; set; }
}
