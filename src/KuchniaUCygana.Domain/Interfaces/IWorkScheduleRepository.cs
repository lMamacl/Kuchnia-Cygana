using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KuchniaUCygana.Domain.Entities.Admin;

namespace KuchniaUCygana.Domain.Interfaces;

public interface IWorkScheduleRepository : IRepository<WorkSchedule>
{
    Task<IEnumerable<WorkSchedule>> GetByUserIdAsync(int userId);
    Task<IEnumerable<WorkSchedule>> GetByDateRangeAsync(DateOnly start, DateOnly end);
}
