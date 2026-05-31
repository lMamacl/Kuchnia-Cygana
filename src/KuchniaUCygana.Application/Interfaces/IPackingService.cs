using KuchniaUCygana.Application.DTOs.Packing;

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

    Task<ScanBoxResponse> ScanBoxAsync(int sessionId, string barcode, string packedBy);

    Task<PackingItemDto> ReportPackingItemIssueAsync(ReportPackingItemIssueRequest request);

    Task<PackingBagDto> ReportPackingBagDamageAsync(ReportPackingBagDamageRequest request);

    Task<IEnumerable<PackingLabelDto>> GenerateTransportLabelsAsync(
        int sessionId,
        string? reprintReason = null,
        bool forceNewPrint = false);

    Task<IEnumerable<PackingLabelDto>> GenerateLabelsAsync(int sessionId);

    Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForDeliveryAsync(
        DateOnly date,
        int routeId,
        string? reprintReason = null,
        bool forceNewPrint = false);

    Task<IEnumerable<PackingSessionDto>> GetSessionsByDateAsync(DateOnly date);

    Task<PackingSessionDto?> GetSessionByIdAsync(int sessionId);

    Task PackBoxByCodeAsync(int sessionId, string barcode, string packedBy);

    Task<PackingLabelDto> PrintFoilLabelAsync(
        int packingItemId,
        string operatorName,
        string? reprintReason = null);
}
