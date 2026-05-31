using KuchniaUCygana.Application.DTOs.Packing;

namespace KuchniaUCygana.Application.Interfaces;

public interface IManifestService
{
    Task<ManifestControlDto> GetManifestControlAsync(DateOnly date, int routeId);

    Task<PackingManifestDto> GenerateManifestAsync(
        DateOnly date,
        int routeId,
        string generatedBy,
        string? changeReason = null);

    Task<PackingManifestDto?> GetManifestAsync(DateOnly date, int routeId);

    Task<PackingManifestDto> ApproveManifestByWorkerAsync(DateOnly date, int routeId, string approvedBy);

    Task<PackingManifestDto> ApproveManifestBySupervisorAsync(DateOnly date, int routeId, string approvedBy);
}
