using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using ServiceStack.OrmLite;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class WorkScheduleRepository : BaseRepository<WorkSchedule>, IWorkScheduleRepository
{
    public WorkScheduleRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<WorkSchedule>> GetByUserIdAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        return await db.SelectAsync<WorkSchedule>(w => w.UserId == userId && !w.IsDeleted);
    }

    public async Task<IEnumerable<WorkSchedule>> GetByDateRangeAsync(DateOnly start, DateOnly end)
    {
        using var db = Factory.CreateConnection();
        return await db.SelectAsync<WorkSchedule>(w => w.ShiftDate >= start && w.ShiftDate <= end && !w.IsDeleted);
    }
}