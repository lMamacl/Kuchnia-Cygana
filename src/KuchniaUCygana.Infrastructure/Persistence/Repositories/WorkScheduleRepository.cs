using Dapper;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class WorkScheduleRepository : BaseRepository<WorkSchedule>, IWorkScheduleRepository
{
    public WorkScheduleRepository(IDbConnectionFactory factory) : base(factory) { }

    public async Task<IEnumerable<WorkSchedule>> GetByUserIdAsync(int userId)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<WorkSchedule>(
            "SELECT * FROM [WorkSchedules] WHERE [UserId] = @UserId AND [IsDeleted] = 0", 
            new { UserId = userId });
    }

    public async Task<IEnumerable<WorkSchedule>> GetByDateRangeAsync(DateOnly start, DateOnly end)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<WorkSchedule>(
            "SELECT * FROM [WorkSchedules] WHERE [ShiftDate] >= @Start AND [ShiftDate] <= @End AND [IsDeleted] = 0", 
            new { Start = start, End = end });
    }
}