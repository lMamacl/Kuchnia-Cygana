using KuchniaUCygana.Application.DTOs.Packing;

namespace KuchniaUCygana.Application.Interfaces;

public interface IBoxLabelService
{
    Task<PackingLabelDto> PrintBoxLabelAsync(
        int packingItemId,
        string operatorName,
        string? reprintReason = null);

    Task<PackingLabelDto?> GetLatestBoxLabelAsync(int packingItemId);
}
