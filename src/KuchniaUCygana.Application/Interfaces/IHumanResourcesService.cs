using KuchniaUCygana.Application.DTOs.HR;

namespace KuchniaUCygana.Application.Interfaces;

public interface IHumanResourcesService
{
    Task<HumanResourcesSummaryDto> GetSummaryAsync(DateOnly today);

    Task<IReadOnlyList<DepartmentStaffSummaryDto>> GetDepartmentStaffSummariesAsync(string? search = null);

    Task<DepartmentPageDto> SearchDepartmentsAsync(DepartmentSearchRequest request);

    Task<DepartmentDto?> GetDepartmentByIdAsync(int id);

    Task<DepartmentDto> CreateDepartmentAsync(CreateDepartmentRequest request);

    Task<DepartmentDto?> UpdateDepartmentAsync(int id, UpdateDepartmentRequest request);

    Task<bool> DeleteDepartmentAsync(int id);

    Task<EmployeePageDto> SearchEmployeesAsync(EmployeeSearchRequest request);

    Task<IReadOnlyList<EmployeeDto>> GetEmployeeOptionsAsync(bool activeOnly, int limit = 500);

    Task<EmployeeDto?> GetEmployeeByUserIdAsync(int userId);

    Task<EmployeeDto?> GetEmployeeByIdAsync(int id);

    Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request);

    Task<EmployeeDto?> UpdateEmployeeAsync(int id, UpdateEmployeeRequest request);

    Task<EmployeeDto?> ChangeEmployeeAssignmentAsync(int id, int departmentId, string position);

    Task<bool> DeactivateEmployeeAsync(int id, DateOnly? terminationDate = null);

    Task<bool> DeleteEmployeeAsync(int id);

    Task<LeaveRequestPageDto> SearchLeaveRequestsAsync(LeaveRequestSearchRequest request);

    Task<IEnumerable<LeaveRequestDto>> GetLeaveRequestsByEmployeeAsync(int employeeId);

    Task<LeaveRequestDto?> GetLeaveRequestByIdAsync(int id);

    Task<LeaveRequestDto> CreateLeaveRequestAsync(CreateLeaveRequestRequest request);

    Task<LeaveRequestDto?> UpdateLeaveRequestAsync(int id, UpdateLeaveRequestRequest request);

    Task<LeaveRequestDto?> ReviewLeaveRequestAsync(ReviewLeaveRequestRequest request);

    Task<bool> DeleteLeaveRequestAsync(int id);

    Task<WorkSchedulePageDto> SearchWorkSchedulesAsync(WorkScheduleSearchRequest request);

    Task<IEnumerable<WorkScheduleDto>> GetWorkSchedulesByUserAsync(int userId);

    Task<IEnumerable<WorkScheduleDto>> GetWorkSchedulesByDateRangeAsync(DateOnly start, DateOnly end);

    Task<WorkScheduleDto?> GetWorkScheduleByIdAsync(int id);

    Task<WorkScheduleDto> CreateWorkScheduleAsync(CreateWorkScheduleRequest request);

    Task<WorkScheduleDto?> UpdateWorkScheduleAsync(int id, UpdateWorkScheduleRequest request);

    Task<bool> DeleteWorkScheduleAsync(int id);
}
