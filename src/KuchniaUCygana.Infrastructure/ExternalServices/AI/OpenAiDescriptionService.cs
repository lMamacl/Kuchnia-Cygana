namespace KuchniaUCygana.Infrastructure.ExternalServices.AI;

public sealed class OpenAiDescriptionService : IAiDescriptionService
{
    public Task<string> GenerateDescriptionAsync(string prompt)
    {
        return Task.FromResult($"Generated: {prompt}");
    }
}
