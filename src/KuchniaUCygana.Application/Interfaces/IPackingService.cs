using KuchniaUCygana.Application.DTOs.Packing;

namespace KuchniaUCygana.Application.Interfaces;

/// <summary>
/// Serwis aplikacyjny kompletacji — sesje pakowania, etykiety.
/// </summary>
public interface IPackingService
{
    Task<PackingBoardDto> GetPackingBoardAsync(DateOnly date);

    Task<PackingSessionDto> StartPackingSessionAsync(DateOnly date, string packedBy);

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

    Task<IEnumerable<PackingLabelDto>> GenerateTransportLabelsForBagAsync(
        int packingBagId,
        string? reprintReason = null,
        bool forceNewPrint = false);

    Task<IEnumerable<PackingLabelDto>> GenerateLabelsAsync(int sessionId);

    Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForSessionAsync(int sessionId);

    Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForRouteAsync(DateOnly date, int routeId);

    Task<IEnumerable<PackingLabelDto>> GetTransportLabelsForDeliveryAsync(
        DateOnly date,
        int routeId,
        string? reprintReason = null,
        bool forceNewPrint = false);

    Task<IReadOnlyList<PackingLabelDto>> GenerateMissingTransportLabelsForRouteAsync(DateOnly date, int routeId);

    Task<PackingLabelDto> ConfirmTransportLabelAttachedAsync(int labelId);

    Task<int> ConfirmTransportLabelsAttachedAsync(IReadOnlyCollection<int> labelIds);

    Task<IEnumerable<PackingSessionDto>> GetSessionsByDateAsync(DateOnly date);

    Task<PackingSessionDto?> GetSessionByIdAsync(int sessionId);

    Task<FoilLabelPreparationResultDto> EnsureFoilBoxesForDateAsync(DateOnly date);

    Task<FoilLabelDashboardDto> GetFoilLabelDashboardAsync(FoilLabelFilterDto filter);

    Task PackBoxByCodeAsync(int sessionId, string barcode, string packedBy);

    Task<PackingLabelDto> PrintFoilLabelAsync(
        int packingItemId,
        string operatorName,
        string? reprintReason = null);

    Task<PackingLabelDto?> GetLatestFoilLabelAsync(int packingItemId);
}
