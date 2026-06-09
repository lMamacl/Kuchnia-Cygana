using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class StaffShiftAccessService : IStaffShiftAccessService
{
    private readonly ICurrentUserService currentUserService;
    private readonly IWorkScheduleRepository workScheduleRepository;

    public StaffShiftAccessService(
        ICurrentUserService currentUserService,
        IWorkScheduleRepository workScheduleRepository)
    {
        this.currentUserService = currentUserService;
        this.workScheduleRepository = workScheduleRepository;
    }

    public async Task<bool> IsCurrentUserInsideActiveShiftAsync(DateTimeOffset now)
    {
        var userId = currentUserService.GetUserId();
        if (userId is null or <= 0)
        {
            return false;
        }

        var localNow = now.LocalDateTime;
        var today = DateOnly.FromDateTime(localNow);
        var yesterday = today.AddDays(-1);
        var schedules = await workScheduleRepository.GetByUserIdAndDateRangeAsync(
            userId.Value,
            yesterday,
            today);

        return schedules.Any(schedule => IsInsideShift(schedule, localNow));
    }

    private static bool IsInsideShift(WorkSchedule schedule, DateTime localNow)
    {
        var shiftStart = schedule.Shift switch
        {
            WorkShift.Morning => schedule.ShiftDate.ToDateTime(new TimeOnly(6, 0)),
            WorkShift.Evening => schedule.ShiftDate.ToDateTime(new TimeOnly(14, 0)),
            WorkShift.Night => schedule.ShiftDate.ToDateTime(new TimeOnly(22, 0)),
            _ => schedule.ShiftDate.ToDateTime(TimeOnly.MinValue),
        };

        var shiftEnd = schedule.Shift switch
        {
            WorkShift.Morning => shiftStart.AddHours(8),
            WorkShift.Evening => shiftStart.AddHours(8),
            WorkShift.Night => shiftStart.AddHours(8),
            _ => shiftStart,
        };

        return localNow >= shiftStart && localNow < shiftEnd;
    }
}
