namespace KuchniaUCygana.Application.Interfaces;

public interface IStaffShiftAccessService
{
    Task<bool> IsCurrentUserInsideActiveShiftAsync(DateTimeOffset now);
}
