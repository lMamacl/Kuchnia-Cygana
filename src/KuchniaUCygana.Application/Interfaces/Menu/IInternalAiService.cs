namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IInternalAiService
{
    Task<string> GenerateDescriptionAsync(string prompt);
}
