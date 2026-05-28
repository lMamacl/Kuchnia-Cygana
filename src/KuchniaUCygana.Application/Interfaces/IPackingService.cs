using KuchniaUCygana.Application.DTOs.Production;

namespace KuchniaUCygana.Application.Interfaces;

/// <summary>
/// Serwis aplikacyjny kompletacji — sesje pakowania, etykiety.
/// </summary>
public interface IPackingService
{
    Task<PackingBoardDto> GetPackingBoardAsync(DateOnly date);

    Task<PackingSessionDto> StartPackingSessionAsync(DateOnly date, string packedBy);

    Task<PackingItemDto> PackClientDietAsync(int sessionId, int orderId);

    Task<IEnumerable<PackingItemDto>> PrepareOrderBoxesAsync(int packingSessionId);

    Task MarkBoxPackedAsync(int packingItemId, string packedBy);

    Task PackOrderBagAsync(int packingSessionId, string packedBy);

    Task LoadOrderBagAsync(int packingSessionId);

    Task<IEnumerable<PackingLabelDto>> GenerateTransportLabelsAsync(int sessionId);

    Task<IEnumerable<PackingLabelDto>> GenerateLabelsAsync(int sessionId);

    Task<PackingManifestDto> GeneratePackingManifestAsync(DateOnly date, int routeId, string generatedBy);

    Task<PackingManifestDto?> GetLatestPackingManifestAsync(DateOnly date, int routeId);

    Task<PackingManifestDto> VerifyPackingManifestAsync(DateOnly date, int routeId, string verifiedBy);

    Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForDeliveryAsync(DateOnly date, int routeId);

    Task DispatchDeliveryAsync(DateOnly date, int routeId);

    Task ApproveDispatchAsync(int sessionId);

    Task<IEnumerable<PackingSessionDto>> GetSessionsByDateAsync(DateOnly date);

    Task<PackingSessionDto?> GetSessionByIdAsync(int sessionId);

    Task PackBoxByCodeAsync(int sessionId, string barcode, string packedBy);

    Task<PackingBagDto> LoadBagByCodeAsync(int routeId, string transportCode);

    Task<PackingLabelDto> PrintFoilLabelAsync(int packingItemId, string operatorName);
}
