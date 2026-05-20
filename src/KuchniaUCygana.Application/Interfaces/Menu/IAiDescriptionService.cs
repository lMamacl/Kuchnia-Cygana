using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IAiDescriptionService
{
    Task<AiDescriptionResponse> GenerateDescriptionAsync(AiGenerateDescriptionRequest request);
}
