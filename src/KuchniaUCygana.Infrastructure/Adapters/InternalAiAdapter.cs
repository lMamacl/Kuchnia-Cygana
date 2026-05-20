using KuchniaUCygana.Infrastructure.ExternalServices.AI;

namespace KuchniaUCygana.Infrastructure.Adapters;

public sealed class InternalAiAdapter : KuchniaUCygana.Application.Interfaces.Menu.IInternalAiService
{
    private readonly IAiDescriptionService aiDescriptionService;

    public InternalAiAdapter(IAiDescriptionService aiDescriptionService)
    {
        this.aiDescriptionService = aiDescriptionService;
    }

    public Task<string> GenerateDescriptionAsync(string prompt)
    {
        return this.aiDescriptionService.GenerateDescriptionAsync(prompt);
    }
}
