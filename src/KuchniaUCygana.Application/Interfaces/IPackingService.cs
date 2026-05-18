using KuchniaUCygana.Application.DTOs.Production;

namespace KuchniaUCygana.Application.Interfaces;

/// <summary>
/// Serwis aplikacyjny kompletacji — sesje pakowania, etykiety.
/// </summary>
public interface IPackingService
{
    Task<PackingSessionDto> StartPackingSessionAsync(DateOnly date, string packedBy);

    Task<PackingItemDto> PackClientDietAsync(int sessionId, int orderId);

    Task<IEnumerable<PackingLabelDto>> GenerateLabelsAsync(int sessionId);

    Task ApproveDispatchAsync(int sessionId);
}
