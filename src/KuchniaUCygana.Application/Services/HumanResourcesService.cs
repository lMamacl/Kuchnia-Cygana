using AutoMapper;
using KuchniaUCygana.Application.DTOs.HR;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Admin;
using KuchniaUCygana.Domain.Entities.Auth;
using KuchniaUCygana.Domain.Entities.HR;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Application.Services;

public sealed class HumanResourcesService : IHumanResourcesService
{
    private readonly IRepository<Department> departmentRepository;
    private readonly IRepository<Employee> employeeRepository;
    private readonly IRepository<LeaveRequest> leaveRequestRepository;
    private readonly IWorkScheduleRepository workScheduleRepository;
    private readonly IUserRepository userRepository;
    private readonly IHumanResourcesReadRepository readRepository;
    private readonly IMapper mapper;

    public HumanResourcesService(
        IRepository<Department> departmentRepository,
        IRepository<Employee> employeeRepository,
        IRepository<LeaveRequest> leaveRequestRepository,
        IWorkScheduleRepository workScheduleRepository,
        IUserRepository userRepository,
        IHumanResourcesReadRepository readRepository,
        IMapper mapper)
    {
        this.departmentRepository = departmentRepository;
        this.employeeRepository = employeeRepository;
        this.leaveRequestRepository = leaveRequestRepository;
        this.workScheduleRepository = workScheduleRepository;
        this.userRepository = userRepository;
        this.readRepository = readRepository;
        this.mapper = mapper;
    }

    public async Task<HumanResourcesSummaryDto> GetSummaryAsync(DateOnly today)
    {
        var summary = await readRepository.GetSummaryAsync(today);
        return new HumanResourcesSummaryDto
        {
            PendingLeaveCount = summary.PendingLeaveCount,
            ActiveEmployeeCount = summary.ActiveEmployeeCount,
            InactiveEmployeeCount = summary.InactiveEmployeeCount,
            TodayScheduleCount = summary.TodayScheduleCount,
        };
    }

    public async Task<IReadOnlyList<DepartmentStaffSummaryDto>> GetDepartmentStaffSummariesAsync(string? search = null)
    {
        var rows = await readRepository.GetDepartmentStaffSummariesAsync(search);
        return rows.Select(row => new DepartmentStaffSummaryDto
        {
            DepartmentId = row.DepartmentId,
            DepartmentName = row.DepartmentName,
            ActiveEmployeeCount = row.ActiveEmployeeCount,
            TotalEmployeeCount = row.TotalEmployeeCount,
            HeadEmployeeFullName = row.HeadEmployeeFullName,
            SampleEmployeeNames = row.SampleEmployeeNames,
        }).ToArray();
    }

    public async Task<DepartmentPageDto> SearchDepartmentsAsync(DepartmentSearchRequest request)
    {
        var result = await readRepository.SearchDepartmentsAsync(new DepartmentSearchQuery(
            request.Search,
            request.Page,
            request.PageSize));

        return new DepartmentPageDto
        {
            Items = result.Items.Select(MapDepartmentSearchRow).ToArray(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<DepartmentDto?> GetDepartmentByIdAsync(int id)
    {
        var department = await departmentRepository.GetByIdAsync(id);
        if (department is null)
        {
            return null;
        }

        return (await MapDepartmentsAsync(new[] { department })).Single();
    }

    public async Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentRequest request)
    {
        if (request.HeadEmployeeId.HasValue)
        {
            await EnsureEmployeeExistsAsync(request.HeadEmployeeId.Value);
        }

        var department = mapper.Map<Department>(request);
        var id = await departmentRepository.InsertAsync(department);
        department.Id = id;

        return (await MapDepartmentsAsync(new[] { department })).Single();
    }

    public async Task<DepartmentDto?> UpdateDepartmentAsync(int id, UpdateDepartmentRequest request)
    {
        var existing = await departmentRepository.GetByIdAsync(id);
        if (existing is null)
        {
            return null;
        }

        if (request.HeadEmployeeId.HasValue)
        {
            await EnsureEmployeeExistsAsync(request.HeadEmployeeId.Value);
        }

        request.Id = id;
        mapper.Map(request, existing);
        await departmentRepository.UpdateAsync(existing);

        return (await MapDepartmentsAsync(new[] { existing })).Single();
    }

    public async Task<bool> DeleteDepartmentAsync(int id)
    {
        var employees = await readRepository.SearchEmployeesAsync(new EmployeeSearchQuery(
            Search: null,
            DepartmentId: id,
            Status: null,
            Role: null,
            Page: 1,
            PageSize: 1));
        if (employees.TotalCount > 0)
        {
            throw new InvalidOperationException("Nie mozna usunac dzialu, do ktorego przypisani sa pracownicy.");
        }

        return await departmentRepository.DeleteAsync(id);
    }

    public async Task<EmployeePageDto> SearchEmployeesAsync(EmployeeSearchRequest request)
    {
        var result = await readRepository.SearchEmployeesAsync(new EmployeeSearchQuery(
            request.Search,
            request.DepartmentId,
            request.Status,
            request.Role,
            request.Page,
            request.PageSize));

        return new EmployeePageDto
        {
            Items = result.Items.Select(MapEmployeeSearchRow).ToArray(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<IReadOnlyList<EmployeeDto>> GetEmployeeOptionsAsync(bool activeOnly, int limit = 500)
    {
        var rows = await readRepository.GetEmployeeOptionsAsync(activeOnly, limit);
        return rows.Select(row => new EmployeeDto
        {
            Id = row.Id,
            UserId = row.UserId,
            FirstName = row.FullName,
            FullName = row.FullName,
            IsActive = row.IsActive,
        }).ToArray();
    }

    public async Task<EmployeeDto?> GetEmployeeByUserIdAsync(int userId)
    {
        var row = await readRepository.GetEmployeeByUserIdAsync(userId);
        return row is null ? null : MapEmployeeSearchRow(row);
    }

    public async Task<EmployeeDto?> GetEmployeeByIdAsync(int id)
    {
        var employee = await employeeRepository.GetByIdAsync(id);
        if (employee is null)
        {
            return null;
        }

        return (await MapEmployeesAsync(new[] { employee })).Single();
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request)
    {
        await EnsureUserExistsAsync(request.UserId);
        await EnsureDepartmentExistsAsync(request.DepartmentId);

        var employee = mapper.Map<Employee>(request);
        var id = await employeeRepository.InsertAsync(employee);
        employee.Id = id;

        return (await MapEmployeesAsync(new[] { employee })).Single();
    }

    public async Task<EmployeeDto?> UpdateEmployeeAsync(int id, UpdateEmployeeRequest request)
    {
        var existing = await employeeRepository.GetByIdAsync(id);
        if (existing is null)
        {
            return null;
        }

        await EnsureDepartmentExistsAsync(request.DepartmentId);

        request.Id = id;
        mapper.Map(request, existing);
        await employeeRepository.UpdateAsync(existing);

        return (await MapEmployeesAsync(new[] { existing })).Single();
    }

    public async Task<EmployeeDto?> ChangeEmployeeAssignmentAsync(int id, int departmentId, string position)
    {
        var existing = await employeeRepository.GetByIdAsync(id);
        if (existing is null)
        {
            return null;
        }

        var normalizedPosition = position?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedPosition))
        {
            throw new InvalidOperationException("Stanowisko jest wymagane.");
        }

        if (normalizedPosition.Length > 100)
        {
            throw new InvalidOperationException("Stanowisko moze miec maksymalnie 100 znakow.");
        }

        await EnsureDepartmentExistsAsync(departmentId);

        existing.DepartmentId = departmentId;
        existing.Position = normalizedPosition;
        await employeeRepository.UpdateAsync(existing);

        return (await MapEmployeesAsync(new[] { existing })).Single();
    }

    public async Task<bool> DeactivateEmployeeAsync(int id, DateOnly? terminationDate = null)
    {
        var employee = await employeeRepository.GetByIdAsync(id);
        if (employee is null)
        {
            return false;
        }

        employee.IsActive = false;
        employee.TerminationDate = terminationDate ?? DateOnly.FromDateTime(DateTime.Today);
        return await employeeRepository.UpdateAsync(employee);
    }

    public async Task<bool> DeleteEmployeeAsync(int id)
    {
        return await employeeRepository.DeleteAsync(id);
    }

    public async Task<LeaveRequestPageDto> SearchLeaveRequestsAsync(LeaveRequestSearchRequest request)
    {
        var result = await readRepository.SearchLeaveRequestsAsync(new LeaveRequestSearchQuery(
            request.Status,
            request.LeaveType,
            request.Search,
            request.Page,
            request.PageSize));

        return new LeaveRequestPageDto
        {
            Items = result.Items.Select(MapLeaveRequestSearchRow).ToArray(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<IEnumerable<LeaveRequestDto>> GetLeaveRequestsByEmployeeAsync(int employeeId)
    {
        var rows = await readRepository.GetLeaveRequestsByEmployeeAsync(employeeId);
        return rows.Select(MapLeaveRequestSearchRow).ToArray();
    }

    public async Task<LeaveRequestDto?> GetLeaveRequestByIdAsync(int id)
    {
        var leaveRequest = await leaveRequestRepository.GetByIdAsync(id);
        if (leaveRequest is null)
        {
            return null;
        }

        return (await MapLeaveRequestsAsync(new[] { leaveRequest })).Single();
    }

    public async Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestRequest request)
    {
        await EnsureEmployeeExistsAsync(request.EmployeeId);

        var leaveRequest = mapper.Map<LeaveRequest>(request);
        var id = await leaveRequestRepository.InsertAsync(leaveRequest);
        leaveRequest.Id = id;

        return (await MapLeaveRequestsAsync(new[] { leaveRequest })).Single();
    }

    public async Task<LeaveRequestDto?> UpdateLeaveRequestAsync(int id, UpdateLeaveRequestRequest request)
    {
        var existing = await leaveRequestRepository.GetByIdAsync(id);
        if (existing is null)
        {
            return null;
        }

        request.Id = id;
        mapper.Map(request, existing);
        await leaveRequestRepository.UpdateAsync(existing);

        return (await MapLeaveRequestsAsync(new[] { existing })).Single();
    }

    public async Task<LeaveRequestDto?> ReviewLeaveRequestAsync(ReviewLeaveRequestRequest request)
    {
        var existing = await leaveRequestRepository.GetByIdAsync(request.Id);
        if (existing is null)
        {
            return null;
        }

        if (request.ApprovedByEmployeeId.HasValue)
        {
            await EnsureEmployeeExistsAsync(request.ApprovedByEmployeeId.Value);
        }

        mapper.Map(request, existing);
        await leaveRequestRepository.UpdateAsync(existing);

        return (await MapLeaveRequestsAsync(new[] { existing })).Single();
    }

    public async Task<bool> DeleteLeaveRequestAsync(int id)
    {
        return await leaveRequestRepository.DeleteAsync(id);
    }

    public async Task<WorkSchedulePageDto> SearchWorkSchedulesAsync(WorkScheduleSearchRequest request)
    {
        var result = await readRepository.SearchWorkSchedulesAsync(new WorkScheduleSearchQuery(
            request.From,
            request.To,
            request.Shift,
            request.Role,
            request.Search,
            request.Page,
            request.PageSize));

        return new WorkSchedulePageDto
        {
            Items = result.Items.Select(MapWorkScheduleSearchRow).ToArray(),
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<IEnumerable<WorkScheduleDto>> GetWorkSchedulesByUserAsync(int userId)
    {
        var schedules = await workScheduleRepository.GetByUserIdAsync(userId);
        return await MapWorkSchedulesAsync(schedules);
    }

    public async Task<IEnumerable<WorkScheduleDto>> GetWorkSchedulesByDateRangeAsync(DateOnly start, DateOnly end)
    {
        if (end < start)
        {
            throw new InvalidOperationException("Data konca zakresu nie moze byc wczesniejsza niz data poczatku.");
        }

        var schedules = await workScheduleRepository.GetByDateRangeAsync(start, end);
        return await MapWorkSchedulesAsync(schedules);
    }

    public async Task<WorkScheduleDto?> GetWorkScheduleByIdAsync(int id)
    {
        var schedule = await workScheduleRepository.GetByIdAsync(id);
        if (schedule is null)
        {
            return null;
        }

        return (await MapWorkSchedulesAsync(new[] { schedule })).Single();
    }

    public async Task<WorkScheduleDto> CreateWorkScheduleAsync(CreateWorkScheduleRequest request)
    {
        await EnsureUserExistsAsync(request.UserId);
        await EnsureScheduleIsUniqueAsync(request.UserId, request.ShiftDate, request.Shift);

        var schedule = mapper.Map<WorkSchedule>(request);
        var id = await workScheduleRepository.InsertAsync(schedule);
        schedule.Id = id;

        return (await MapWorkSchedulesAsync(new[] { schedule })).Single();
    }

    public async Task<WorkScheduleDto?> UpdateWorkScheduleAsync(int id, UpdateWorkScheduleRequest request)
    {
        var existing = await workScheduleRepository.GetByIdAsync(id);
        if (existing is null)
        {
            return null;
        }

        await EnsureUserExistsAsync(request.UserId);
        await EnsureScheduleIsUniqueAsync(request.UserId, request.ShiftDate, request.Shift, id);

        request.Id = id;
        mapper.Map(request, existing);
        await workScheduleRepository.UpdateAsync(existing);

        return (await MapWorkSchedulesAsync(new[] { existing })).Single();
    }

    public async Task<bool> DeleteWorkScheduleAsync(int id)
    {
        return await workScheduleRepository.DeleteAsync(id);
    }

    private static DepartmentDto MapDepartmentSearchRow(DepartmentSearchRow row)
        => new()
        {
            Id = row.Id,
            Name = row.Name,
            Description = row.Description,
            HeadEmployeeId = row.HeadEmployeeId,
            HeadEmployeeFullName = row.HeadEmployeeFullName,
        };

    private static EmployeeDto MapEmployeeSearchRow(EmployeeSearchRow row)
        => new()
        {
            Id = row.Id,
            UserId = row.UserId,
            FirstName = row.FirstName,
            LastName = row.LastName,
            FullName = $"{row.FirstName} {row.LastName}".Trim(),
            Email = row.Email,
            PhoneNumber = row.PhoneNumber,
            HireDate = row.HireDate,
            TerminationDate = row.TerminationDate,
            DepartmentId = row.DepartmentId,
            DepartmentName = row.DepartmentName,
            Position = row.Position,
            IsActive = row.IsActive,
            UserRole = row.UserRole,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };

    private static LeaveRequestDto MapLeaveRequestSearchRow(LeaveRequestSearchRow row)
        => new()
        {
            Id = row.Id,
            EmployeeId = row.EmployeeId,
            EmployeeFullName = row.EmployeeFullName,
            LeaveType = row.LeaveType,
            StartDate = row.StartDate,
            EndDate = row.EndDate,
            Status = row.Status,
            ApprovedByEmployeeId = row.ApprovedByEmployeeId,
            ApprovedByEmployeeFullName = row.ApprovedByEmployeeFullName,
            RejectionReason = row.RejectionReason,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };

    private static WorkScheduleDto MapWorkScheduleSearchRow(WorkScheduleSearchRow row)
        => new()
        {
            Id = row.Id,
            UserId = row.UserId,
            EmployeeFullName = row.EmployeeFullName,
            UserRole = row.UserRole,
            ShiftDate = row.ShiftDate,
            Shift = row.Shift.ToString(),
            RoleAtShift = row.RoleAtShift,
            CreatedAt = row.CreatedAt,
            UpdatedAt = row.UpdatedAt,
        };

    private async Task<List<DepartmentDto>> MapDepartmentsAsync(IEnumerable<Department> departments)
    {
        var list = mapper.Map<List<DepartmentDto>>(departments);
        var headEmployeeIds = list
            .Where(department => department.HeadEmployeeId.HasValue)
            .Select(department => department.HeadEmployeeId!.Value)
            .Distinct()
            .ToArray();
        var employees = (await readRepository.GetEmployeeOptionsByIdsAsync(headEmployeeIds))
            .ToDictionary(employee => employee.Id);

        foreach (var department in list)
        {
            if (department.HeadEmployeeId.HasValue &&
                employees.TryGetValue(department.HeadEmployeeId.Value, out var head))
            {
                department.HeadEmployeeFullName = head.FullName;
            }
        }

        return list;
    }

    private async Task<List<EmployeeDto>> MapEmployeesAsync(IEnumerable<Employee> employees)
    {
        var list = mapper.Map<List<EmployeeDto>>(employees);
        var departments = new Dictionary<int, Department>();
        foreach (var departmentId in list.Select(employee => employee.DepartmentId).Distinct())
        {
            var department = await departmentRepository.GetByIdAsync(departmentId);
            if (department is not null)
            {
                departments[department.Id] = department;
            }
        }

        foreach (var employee in list)
        {
            if (departments.TryGetValue(employee.DepartmentId, out var department))
            {
                employee.DepartmentName = department.Name;
            }
        }

        return list;
    }

    private async Task<List<LeaveRequestDto>> MapLeaveRequestsAsync(IEnumerable<LeaveRequest> leaveRequests)
    {
        var list = mapper.Map<List<LeaveRequestDto>>(leaveRequests);
        var employeeIds = list
            .Select(request => request.EmployeeId)
            .Concat(list.Where(request => request.ApprovedByEmployeeId.HasValue).Select(request => request.ApprovedByEmployeeId!.Value))
            .Distinct()
            .ToArray();
        var employees = (await readRepository.GetEmployeeOptionsByIdsAsync(employeeIds))
            .ToDictionary(employee => employee.Id);

        foreach (var leaveRequest in list)
        {
            if (employees.TryGetValue(leaveRequest.EmployeeId, out var employee))
            {
                leaveRequest.EmployeeFullName = employee.FullName;
            }

            if (leaveRequest.ApprovedByEmployeeId.HasValue &&
                employees.TryGetValue(leaveRequest.ApprovedByEmployeeId.Value, out var approver))
            {
                leaveRequest.ApprovedByEmployeeFullName = approver.FullName;
            }
        }

        return list;
    }

    private async Task<List<WorkScheduleDto>> MapWorkSchedulesAsync(IEnumerable<WorkSchedule> schedules)
    {
        var list = mapper.Map<List<WorkScheduleDto>>(schedules);
        var userIds = list.Select(schedule => schedule.UserId).Distinct().ToArray();
        var employeesByUserId = (await readRepository.GetEmployeeOptionsByUserIdsAsync(userIds))
            .GroupBy(employee => employee.UserId)
            .ToDictionary(group => group.Key, group => group.First());
        var users = (await userRepository.GetByIdsAsync(userIds)).ToDictionary(user => user.Id);

        foreach (var schedule in list)
        {
            if (employeesByUserId.TryGetValue(schedule.UserId, out var employee))
            {
                schedule.EmployeeFullName = employee.FullName;
            }
            else if (users.TryGetValue(schedule.UserId, out var user))
            {
                schedule.EmployeeFullName = BuildUserFullName(user);
            }

            if (users.TryGetValue(schedule.UserId, out var account))
            {
                schedule.UserRole = account.Role;
            }
        }

        return list;
    }

    private async Task EnsureDepartmentExistsAsync(int departmentId)
    {
        if (await departmentRepository.GetByIdAsync(departmentId) is null)
        {
            throw new InvalidOperationException($"Dzial o ID {departmentId} nie istnieje.");
        }
    }

    private async Task EnsureEmployeeExistsAsync(int employeeId)
    {
        if (await employeeRepository.GetByIdAsync(employeeId) is null)
        {
            throw new InvalidOperationException($"Pracownik o ID {employeeId} nie istnieje.");
        }
    }

    private async Task EnsureUserExistsAsync(int userId)
    {
        if (await userRepository.GetByIdAsync(userId) is null)
        {
            throw new InvalidOperationException($"Uzytkownik o ID {userId} nie istnieje.");
        }
    }

    private async Task EnsureScheduleIsUniqueAsync(
        int userId,
        DateOnly shiftDate,
        WorkShift shift,
        int? excludedScheduleId = null)
    {
        var schedules = await workScheduleRepository.GetByDateRangeAsync(shiftDate, shiftDate);
        var duplicateExists = schedules.Any(schedule =>
            schedule.UserId == userId &&
            schedule.Shift == shift &&
            (!excludedScheduleId.HasValue || schedule.Id != excludedScheduleId.Value));

        if (duplicateExists)
        {
            throw new InvalidOperationException("Pracownik ma juz przypisana taka zmiane w wybranym dniu.");
        }
    }

    private static string BuildEmployeeFullName(Employee employee)
    {
        return $"{employee.FirstName} {employee.LastName}".Trim();
    }

    private static string BuildUserFullName(User user)
    {
        var fullName = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.Email : fullName;
    }
}
