using KuchniaUCygana.Application.DTOs.HR;

namespace KuchniaUCygana.Application.Interfaces;

public interface IHumanResourcesService
{
    Task<IEnumerable<DepartmentDto>> GetDepartmentsAsync();

    Task<DepartmentDto?> GetDepartmentByIdAsync(int id);

    Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentRequest request);

    Task<DepartmentDto?> UpdateDepartmentAsync(int id, UpdateDepartmentRequest request);

    Task<bool> DeleteDepartmentAsync(int id);

    Task<IEnumerable<EmployeeDto>> GetEmployeesAsync();

    Task<IEnumerable<EmployeeDto>> GetEmployeesByDepartmentAsync(int departmentId);

    Task<EmployeeDto?> GetEmployeeByIdAsync(int id);

    Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request);

    Task<EmployeeDto?> UpdateEmployeeAsync(int id, UpdateEmployeeRequest request);

    Task<EmployeeDto?> ChangeEmployeeDepartmentAsync(int id, int departmentId);

    Task<bool> DeactivateEmployeeAsync(int id, DateOnly? terminationDate = null);

    Task<bool> DeleteEmployeeAsync(int id);

    Task<IEnumerable<LeaveRequestDto>> GetLeaveRequestsAsync();

    Task<IEnumerable<LeaveRequestDto>> GetLeaveRequestsByEmployeeAsync(int employeeId);

    Task<LeaveRequestDto?> GetLeaveRequestByIdAsync(int id);

    Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestRequest request);

    Task<LeaveRequestDto?> UpdateLeaveRequestAsync(int id, UpdateLeaveRequestRequest request);

    Task<LeaveRequestDto?> ReviewLeaveRequestAsync(ReviewLeaveRequestRequest request);

    Task<bool> DeleteLeaveRequestAsync(int id);

    Task<IEnumerable<WorkScheduleDto>> GetWorkSchedulesAsync();

    Task<IEnumerable<WorkScheduleDto>> GetWorkSchedulesByUserAsync(int userId);

    Task<IEnumerable<WorkScheduleDto>> GetWorkSchedulesByDateRangeAsync(DateOnly start, DateOnly end);

    Task<WorkScheduleDto?> GetWorkScheduleByIdAsync(int id);

    Task<WorkScheduleDto> CreateWorkScheduleAsync(CreateWorkScheduleRequest request);

    Task<WorkScheduleDto?> UpdateWorkScheduleAsync(int id, UpdateWorkScheduleRequest request);

    Task<bool> DeleteWorkScheduleAsync(int id);
}
