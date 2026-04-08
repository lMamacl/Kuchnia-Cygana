namespace KuchniaUCygana.Infrastructure.ExternalServices.AI;

public interface IAiDescriptionService
{
    Task<string> GenerateDescriptionAsync(string prompt);
}
