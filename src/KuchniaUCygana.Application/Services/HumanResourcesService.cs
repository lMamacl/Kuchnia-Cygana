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
    private readonly IRepository<User> userRepository;
    private readonly IMapper mapper;

    public HumanResourcesService(
        IRepository<Department> departmentRepository,
        IRepository<Employee> employeeRepository,
        IRepository<LeaveRequest> leaveRequestRepository,
        IWorkScheduleRepository workScheduleRepository,
        IRepository<User> userRepository,
        IMapper mapper)
    {
        this.departmentRepository = departmentRepository;
        this.employeeRepository = employeeRepository;
        this.leaveRequestRepository = leaveRequestRepository;
        this.workScheduleRepository = workScheduleRepository;
        this.userRepository = userRepository;
        this.mapper = mapper;
    }

    public async Task<IEnumerable<DepartmentDto>> GetDepartmentsAsync()
    {
        var departments = await departmentRepository.GetAllAsync();
        return await MapDepartmentsAsync(departments);
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
        var employees = await employeeRepository.GetAllAsync();
        if (employees.Any(employee => employee.DepartmentId == id))
        {
            throw new InvalidOperationException("Nie mozna usunac dzialu, do ktorego przypisani sa pracownicy.");
        }

        return await departmentRepository.DeleteAsync(id);
    }

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesAsync()
    {
        var employees = await employeeRepository.GetAllAsync();
        return await MapEmployeesAsync(employees);
    }

    public async Task<IEnumerable<EmployeeDto>> GetEmployeesByDepartmentAsync(int departmentId)
    {
        var employees = await employeeRepository.GetAllAsync();
        return await MapEmployeesAsync(employees.Where(employee => employee.DepartmentId == departmentId));
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

    public async Task<IEnumerable<LeaveRequestDto>> GetLeaveRequestsAsync()
    {
        var leaveRequests = await leaveRequestRepository.GetAllAsync();
        return await MapLeaveRequestsAsync(leaveRequests);
    }

    public async Task<IEnumerable<LeaveRequestDto>> GetLeaveRequestsByEmployeeAsync(int employeeId)
    {
        var leaveRequests = await leaveRequestRepository.GetAllAsync();
        return await MapLeaveRequestsAsync(leaveRequests.Where(request => request.EmployeeId == employeeId));
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

    public async Task<IEnumerable<WorkScheduleDto>> GetWorkSchedulesAsync()
    {
        var schedules = await workScheduleRepository.GetAllAsync();
        return await MapWorkSchedulesAsync(schedules);
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

    private async Task<List<DepartmentDto>> MapDepartmentsAsync(IEnumerable<Department> departments)
    {
        var list = mapper.Map<List<DepartmentDto>>(departments);
        var employees = (await employeeRepository.GetAllAsync()).ToDictionary(employee => employee.Id);

        foreach (var department in list)
        {
            if (department.HeadEmployeeId.HasValue &&
                employees.TryGetValue(department.HeadEmployeeId.Value, out var head))
            {
                department.HeadEmployeeFullName = BuildEmployeeFullName(head);
            }
        }

        return list;
    }

    private async Task<List<EmployeeDto>> MapEmployeesAsync(IEnumerable<Employee> employees)
    {
        var list = mapper.Map<List<EmployeeDto>>(employees);
        var departments = (await departmentRepository.GetAllAsync()).ToDictionary(department => department.Id);

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
        var employees = (await employeeRepository.GetAllAsync()).ToDictionary(employee => employee.Id);

        foreach (var leaveRequest in list)
        {
            if (employees.TryGetValue(leaveRequest.EmployeeId, out var employee))
            {
                leaveRequest.EmployeeFullName = BuildEmployeeFullName(employee);
            }

            if (leaveRequest.ApprovedByEmployeeId.HasValue &&
                employees.TryGetValue(leaveRequest.ApprovedByEmployeeId.Value, out var approver))
            {
                leaveRequest.ApprovedByEmployeeFullName = BuildEmployeeFullName(approver);
            }
        }

        return list;
    }

    private async Task<List<WorkScheduleDto>> MapWorkSchedulesAsync(IEnumerable<WorkSchedule> schedules)
    {
        var list = mapper.Map<List<WorkScheduleDto>>(schedules);
        var employeesByUserId = (await employeeRepository.GetAllAsync())
            .GroupBy(employee => employee.UserId)
            .ToDictionary(group => group.Key, group => group.First());
        var users = (await userRepository.GetAllAsync()).ToDictionary(user => user.Id);

        foreach (var schedule in list)
        {
            if (employeesByUserId.TryGetValue(schedule.UserId, out var employee))
            {
                schedule.EmployeeFullName = BuildEmployeeFullName(employee);
            }
            else if (users.TryGetValue(schedule.UserId, out var user))
            {
                schedule.EmployeeFullName = BuildUserFullName(user);
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
