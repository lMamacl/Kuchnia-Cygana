using KuchniaUCygana.Application.DTOs.Packing;

namespace KuchniaUCygana.Application.Interfaces;

public interface IPackingSynchronizationService
{
    Task<PackingSynchronizationResultDto> EnsureSessionsForDateAsync(DateOnly date, string requestedBy);

    Task<PackingSynchronizationResultDto> RefreshFromRoutesAsync(DateOnly date, string requestedBy);
}
