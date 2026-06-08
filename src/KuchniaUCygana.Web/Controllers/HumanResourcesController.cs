using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.DTOs.HR;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Web.Filters;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "HR,HRManager,Admin")]
[Route("hr")]
public sealed class HumanResourcesController : Controller
{
    private readonly IHumanResourcesService humanResourcesService;
    private readonly IStaffActivityService staffActivityService;
    private readonly IUserService userService;

    public HumanResourcesController(
        IHumanResourcesService humanResourcesService,
        IStaffActivityService staffActivityService,
        IUserService userService)
    {
        this.humanResourcesService = humanResourcesService;
        this.staffActivityService = staffActivityService;
        this.userService = userService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        SetViewData("HR", "Kadry", "Dashboard HR: pracownicy, dzialy, urlopy i grafik.");
        return View(await BuildModelAsync());
    }

    [HttpGet("departments")]
    public async Task<IActionResult> Departments([FromQuery] DepartmentListFilterViewModel filter)
    {
        SetViewData("Dzialy", "Kadry", "Struktura organizacyjna i odpowiedzialni pracownicy.");
        return View(await BuildModelAsync(departmentsFilter: filter));
    }

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment(CreateDepartmentRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Dzialy", "Kadry", "Struktura organizacyjna i odpowiedzialni pracownicy.");
            return View("Departments", await BuildModelAsync(newDepartment: request));
        }

        try
        {
            var department = await humanResourcesService.CreateDepartmentAsync(request);
            await staffActivityService.RecordAsync(
                "HR.CreateDepartment",
                "Department",
                department.Id.ToString(),
                newValue: new { department.Id, department.Name, department.HeadEmployeeId },
                notification: BuildNotification(
                    "HR",
                    NotificationSeverity.Info,
                    "Nowy dzial w HR",
                    $"Dodano dzial: {department.Name}.",
                    "/hr/departments",
                    "Department",
                    department.Id),
                notifyRoles: HrNotificationRoles);
            TempData["Success"] = "Dzial zostal dodany.";
            return RedirectToAction(nameof(Departments));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Departments));
        }
    }

    [HttpGet("employees")]
    public async Task<IActionResult> Employees([FromQuery] EmployeeListFilterViewModel filter)
    {
        SetViewData("Pracownicy", "Kadry", "Kartoteka pracownikow i status zatrudnienia.");
        return View(await BuildModelAsync(employeesFilter: filter));
    }

    [HttpGet("employees/new")]
    public async Task<IActionResult> NewEmployee()
    {
        SetViewData("Nowy pracownik", "Kadry", "Dodanie pracownika do kartoteki HR.");
        return View(await BuildModelAsync());
    }

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee(CreateEmployeeRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Nowy pracownik", "Kadry", "Dodanie pracownika do kartoteki HR.");
            return View("NewEmployee", await BuildModelAsync(newEmployee: request));
        }

        try
        {
            var employee = await humanResourcesService.CreateEmployeeAsync(request);
            await staffActivityService.RecordAsync(
                "HR.CreateEmployee",
                "Employee",
                employee.Id.ToString(),
                newValue: new
                {
                    employee.Id,
                    employee.FullName,
                    employee.Email,
                    employee.DepartmentName,
                    employee.Position,
                },
                notification: BuildNotification(
                    "HR",
                    NotificationSeverity.Success,
                    "Dodano pracownika",
                    $"{employee.FullName} dolaczyl(a) do dzialu {employee.DepartmentName ?? "bez nazwy"}.",
                    "/hr/employees",
                    "Employee",
                    employee.Id),
                notifyRoles: HrNotificationRoles);
            TempData["Success"] = "Pracownik zostal dodany.";
            return RedirectToAction(nameof(Employees));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(NewEmployee));
        }
    }

    [HttpPost("employees/{id:int}/assignment")]
    public async Task<IActionResult> ChangeEmployeeAssignment(int id, int departmentId, string position)
    {
        var normalizedPosition = position?.Trim() ?? string.Empty;
        var before = await humanResourcesService.GetEmployeeByIdAsync(id);
        if (before is not null &&
            before.DepartmentId == departmentId &&
            string.Equals(before.Position, normalizedPosition, StringComparison.Ordinal))
        {
            TempData["Success"] = "Pracownik ma juz wybrany dzial i stanowisko.";
            return RedirectToAction(nameof(Employees));
        }

        try
        {
            var employee = await humanResourcesService.ChangeEmployeeAssignmentAsync(id, departmentId, normalizedPosition);
            if (employee is null)
            {
                TempData["Error"] = "Nie znaleziono pracownika.";
                return RedirectToAction(nameof(Employees));
            }

            await staffActivityService.RecordAsync(
                "HR.ChangeEmployeeAssignment",
                "Employee",
                employee.Id.ToString(),
                oldValue: new
                {
                    Id = before?.Id ?? employee.Id,
                    DepartmentId = before?.DepartmentId,
                    DepartmentName = before?.DepartmentName,
                    Position = before?.Position,
                },
                newValue: new
                {
                    employee.Id,
                    employee.FullName,
                    employee.DepartmentId,
                    employee.DepartmentName,
                    employee.Position,
                },
                notification: BuildNotification(
                    "HR",
                    NotificationSeverity.Info,
                    "Zmieniono przypisanie pracownika",
                    $"{employee.FullName}: {employee.DepartmentName ?? "bez nazwy"}, {employee.Position}.",
                    "/hr/employees",
                    "Employee",
                    employee.Id),
                notifyRoles: HrNotificationRoles);
            TempData["Success"] = "Dzial i stanowisko pracownika zostaly zaktualizowane.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Employees));
    }

    [Authorize(Roles = "HRManager,Admin")]
    [HttpPost("employees/{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateEmployee(int id)
    {
        var deactivated = await humanResourcesService.DeactivateEmployeeAsync(id);
        if (deactivated)
        {
            await staffActivityService.RecordAsync(
                "HR.DeactivateEmployee",
                "Employee",
                id.ToString(),
                newValue: new { Id = id, IsActive = false },
                notification: BuildNotification(
                    "HR",
                    NotificationSeverity.Warning,
                    "Pracownik dezaktywowany",
                    $"Kartoteka pracownika #{id} zostala dezaktywowana.",
                    "/hr/employees",
                    "Employee",
                    id),
                notifyRoles: HrNotificationRoles);
        }

        TempData[deactivated ? "Success" : "Error"] = deactivated
            ? "Pracownik zostal dezaktywowany."
            : "Nie znaleziono pracownika.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpGet("leaves")]
    public async Task<IActionResult> Leaves([FromQuery] LeaveRequestListFilterViewModel filter)
    {
        SetViewData("Urlopy", "Kadry", "Wnioski urlopowe i decyzje kadrowe.");
        return View(await BuildModelAsync(leaveRequestsFilter: filter));
    }

    [HttpGet("leaves/new")]
    [AllowOutsideShift]
    public async Task<IActionResult> NewLeaveRequest()
    {
        SetViewData("Nowy wniosek", "Kadry", "Rejestracja wniosku urlopowego.");
        return View(await BuildModelAsync());
    }

    [HttpPost("leaves")]
    [AllowOutsideShift]
    public async Task<IActionResult> CreateLeaveRequest(CreateLeaveRequestRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Nowy wniosek", "Kadry", "Rejestracja wniosku urlopowego.");
            return View("NewLeaveRequest", await BuildModelAsync(newLeaveRequest: request));
        }

        try
        {
            var leaveRequest = await humanResourcesService.CreateLeaveRequestAsync(request);
            await staffActivityService.RecordAsync(
                "HR.CreateLeaveRequest",
                "LeaveRequest",
                leaveRequest.Id.ToString(),
                newValue: new
                {
                    leaveRequest.Id,
                    leaveRequest.EmployeeId,
                    leaveRequest.EmployeeFullName,
                    leaveRequest.StartDate,
                    leaveRequest.EndDate,
                    leaveRequest.LeaveType,
                },
                notification: BuildNotification(
                    "HR",
                    NotificationSeverity.Info,
                    "Nowy wniosek urlopowy",
                    $"{leaveRequest.EmployeeFullName ?? $"Pracownik #{leaveRequest.EmployeeId}"} dodal(a) wniosek urlopowy.",
                    "/hr/leaves",
                    "LeaveRequest",
                    leaveRequest.Id),
                notifyRoles: HrManagerNotificationRoles);
            TempData["Success"] = "Wniosek urlopowy zostal dodany.";
            return RedirectToAction(nameof(Leaves));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(NewLeaveRequest));
        }
    }

    [Authorize(Roles = "HRManager,Admin")]
    [HttpPost("leaves/{id:int}/review")]
    public async Task<IActionResult> ReviewLeaveRequest(
        int id,
        int status,
        int? approvedByEmployeeId,
        string? rejectionReason)
    {
        var reviewed = await humanResourcesService.ReviewLeaveRequestAsync(new ReviewLeaveRequestRequest
        {
            Id = id,
            Status = status,
            ApprovedByEmployeeId = approvedByEmployeeId,
            RejectionReason = rejectionReason,
        });

        TempData[reviewed is null ? "Error" : "Success"] = reviewed is null
            ? "Nie znaleziono wniosku urlopowego."
            : "Wniosek urlopowy zostal zaktualizowany.";
        if (reviewed is not null)
        {
            await staffActivityService.RecordAsync(
                "HR.ReviewLeaveRequest",
                "LeaveRequest",
                reviewed.Id.ToString(),
                newValue: new
                {
                    reviewed.Id,
                    reviewed.EmployeeId,
                    reviewed.EmployeeFullName,
                    reviewed.Status,
                    reviewed.ApprovedByEmployeeId,
                    reviewed.RejectionReason,
                },
                notification: BuildNotification(
                    "HR",
                    reviewed.Status == 1 ? NotificationSeverity.Success : NotificationSeverity.Warning,
                    "Decyzja urlopowa",
                    $"Wniosek urlopowy #{reviewed.Id} zostal rozpatrzony.",
                    "/hr/leaves",
                    "LeaveRequest",
                    reviewed.Id),
                notifyRoles: HrNotificationRoles);
        }

        return RedirectToAction(nameof(Leaves));
    }

    [HttpGet("schedules")]
    public async Task<IActionResult> Schedules([FromQuery] WorkScheduleListFilterViewModel filter)
    {
        SetViewData("Grafik", "Kadry", "Zmiany pracownikow i role na zmianie.");
        return View(await BuildModelAsync(workSchedulesFilter: filter));
    }

    [HttpGet("schedules/new")]
    [AllowOutsideShift]
    public async Task<IActionResult> NewWorkSchedule()
    {
        SetViewData("Nowa zmiana", "Kadry", "Dodanie zmiany do grafiku.");
        return View(await BuildModelAsync());
    }

    [HttpPost("schedules")]
    [AllowOutsideShift]
    public async Task<IActionResult> CreateWorkSchedule(CreateWorkScheduleRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Nowa zmiana", "Kadry", "Dodanie zmiany do grafiku.");
            return View("NewWorkSchedule", await BuildModelAsync(newWorkSchedule: request));
        }

        try
        {
            var schedule = await humanResourcesService.CreateWorkScheduleAsync(request);
            await staffActivityService.RecordAsync(
                "HR.CreateWorkSchedule",
                "WorkSchedule",
                schedule.Id.ToString(),
                newValue: new
                {
                    schedule.Id,
                    schedule.UserId,
                    schedule.EmployeeFullName,
                    schedule.ShiftDate,
                    schedule.Shift,
                    schedule.RoleAtShift,
                },
                notification: BuildNotification(
                    "HR",
                    NotificationSeverity.Info,
                    "Nowa zmiana w grafiku",
                    $"{schedule.EmployeeFullName ?? $"Uzytkownik #{schedule.UserId}"} ma nowa zmiane {schedule.ShiftDate:dd.MM.yyyy}.",
                    "/hr/schedules",
                    "WorkSchedule",
                    schedule.Id),
                notifyRoles: HrNotificationRoles);
            TempData["Success"] = "Zmiana zostala dodana do grafiku.";
            return RedirectToAction(nameof(Schedules));
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(NewWorkSchedule));
        }
    }

    [HttpPost("schedules/{id:int}/update")]
    [AllowOutsideShift]
    public async Task<IActionResult> UpdateWorkSchedule(int id, UpdateWorkScheduleRequest request)
    {
        request.Id = id;
        var before = await humanResourcesService.GetWorkScheduleByIdAsync(id);

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Niepoprawne dane zmiany.";
            return RedirectToAction(nameof(Schedules));
        }

        try
        {
            var schedule = await humanResourcesService.UpdateWorkScheduleAsync(id, request);
            if (schedule is null)
            {
                TempData["Error"] = "Nie znaleziono zmiany w grafiku.";
                return RedirectToAction(nameof(Schedules));
            }

            await staffActivityService.RecordAsync(
                "HR.UpdateWorkSchedule",
                "WorkSchedule",
                schedule.Id.ToString(),
                oldValue: before is null
                    ? null
                    : new
                    {
                        before.Id,
                        before.UserId,
                        before.EmployeeFullName,
                        before.ShiftDate,
                        before.Shift,
                        before.RoleAtShift,
                    },
                newValue: new
                {
                    schedule.Id,
                    schedule.UserId,
                    schedule.EmployeeFullName,
                    schedule.ShiftDate,
                    schedule.Shift,
                    schedule.RoleAtShift,
                },
                notification: BuildNotification(
                    "HR",
                    NotificationSeverity.Info,
                    "Zmieniono grafik",
                    $"{schedule.EmployeeFullName ?? $"Uzytkownik #{schedule.UserId}"} ma zaktualizowana zmiane {schedule.ShiftDate:dd.MM.yyyy}.",
                    "/hr/schedules",
                    "WorkSchedule",
                    schedule.Id),
                notifyRoles: HrNotificationRoles);
            TempData["Success"] = "Zmiana w grafiku zostala zaktualizowana.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Schedules));
    }

    [Authorize(Roles = "HRManager,Admin")]
    [HttpPost("schedules/{id:int}/delete")]
    [AllowOutsideShift]
    public async Task<IActionResult> DeleteWorkSchedule(int id)
    {
        var deleted = await humanResourcesService.DeleteWorkScheduleAsync(id);
        if (deleted)
        {
            await staffActivityService.RecordAsync(
                "HR.DeleteWorkSchedule",
                "WorkSchedule",
                id.ToString(),
                oldValue: new { Id = id },
                notification: BuildNotification(
                    "HR",
                    NotificationSeverity.Warning,
                    "Usunieto zmiane z grafiku",
                    $"Zmiana #{id} zostala usunieta z grafiku.",
                    "/hr/schedules",
                    "WorkSchedule",
                    id),
                notifyRoles: HrNotificationRoles);
        }

        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Zmiana zostala usunieta z grafiku."
            : "Nie znaleziono zmiany.";
        return RedirectToAction(nameof(Schedules));
    }

    private async Task<HumanResourcesDashboardViewModel> BuildModelAsync(
        CreateDepartmentRequest? newDepartment = null,
        CreateEmployeeRequest? newEmployee = null,
        CreateLeaveRequestRequest? newLeaveRequest = null,
        CreateWorkScheduleRequest? newWorkSchedule = null,
        DepartmentListFilterViewModel? departmentsFilter = null,
        EmployeeListFilterViewModel? employeesFilter = null,
        LeaveRequestListFilterViewModel? leaveRequestsFilter = null,
        WorkScheduleListFilterViewModel? workSchedulesFilter = null)
    {
        var departments = (await humanResourcesService.GetDepartmentsAsync()).ToArray();
        var employees = (await humanResourcesService.GetEmployeesAsync()).ToArray();
        var leaveRequests = (await humanResourcesService.GetLeaveRequestsAsync()).ToArray();
        var workSchedules = (await humanResourcesService.GetWorkSchedulesAsync()).ToArray();
        var users = (await userService.GetAllAsync()).ToArray();
        var usersById = users.ToDictionary(user => user.Id);

        departmentsFilter ??= new DepartmentListFilterViewModel();
        employeesFilter ??= new EmployeeListFilterViewModel();
        leaveRequestsFilter ??= new LeaveRequestListFilterViewModel();
        workSchedulesFilter ??= new WorkScheduleListFilterViewModel();

        var departmentsPageModel = PagedList<DepartmentDto>.Create(
            FilterDepartments(departments, employees, departmentsFilter).OrderBy(department => department.Name),
            departmentsFilter.Page,
            departmentsFilter.PageSize);
        var employeesPageModel = PagedList<EmployeeDto>.Create(
            FilterEmployees(employees, usersById, employeesFilter)
                .OrderBy(employee => employee.LastName)
                .ThenBy(employee => employee.FirstName),
            employeesFilter.Page,
            employeesFilter.PageSize);
        var leaveRequestsPageModel = PagedList<LeaveRequestDto>.Create(
            FilterLeaveRequests(leaveRequests, leaveRequestsFilter).OrderByDescending(request => request.CreatedAt),
            leaveRequestsFilter.Page,
            leaveRequestsFilter.PageSize);
        var workSchedulesPageModel = PagedList<WorkScheduleDto>.Create(
            FilterWorkSchedules(workSchedules, usersById, workSchedulesFilter)
                .OrderBy(schedule => schedule.ShiftDate)
                .ThenBy(schedule => schedule.Shift),
            workSchedulesFilter.Page,
            workSchedulesFilter.PageSize);

        SyncFilter(departmentsFilter, departmentsPageModel.Page, departmentsPageModel.PageSize);
        SyncFilter(employeesFilter, employeesPageModel.Page, employeesPageModel.PageSize);
        SyncFilter(leaveRequestsFilter, leaveRequestsPageModel.Page, leaveRequestsPageModel.PageSize);
        SyncFilter(workSchedulesFilter, workSchedulesPageModel.Page, workSchedulesPageModel.PageSize);

        return new HumanResourcesDashboardViewModel
        {
            Departments = departments,
            Employees = employees,
            LeaveRequests = leaveRequests,
            WorkSchedules = workSchedules,
            Users = users,
            DepartmentsPage = departmentsPageModel,
            EmployeesPage = employeesPageModel,
            LeaveRequestsPage = leaveRequestsPageModel,
            WorkSchedulesPage = workSchedulesPageModel,
            DepartmentsFilter = departmentsFilter,
            EmployeesFilter = employeesFilter,
            LeaveRequestsFilter = leaveRequestsFilter,
            WorkSchedulesFilter = workSchedulesFilter,
            NewDepartment = newDepartment ?? new CreateDepartmentRequest(),
            NewEmployee = newEmployee ?? new CreateEmployeeRequest
            {
                HireDate = DateOnly.FromDateTime(DateTime.Today),
                IsActive = true,
            },
            NewLeaveRequest = newLeaveRequest ?? new CreateLeaveRequestRequest
            {
                StartDate = DateOnly.FromDateTime(DateTime.Today),
                EndDate = DateOnly.FromDateTime(DateTime.Today),
            },
            NewWorkSchedule = newWorkSchedule ?? new CreateWorkScheduleRequest
            {
                ShiftDate = DateOnly.FromDateTime(DateTime.Today),
            },
        };
    }

    private static IEnumerable<DepartmentDto> FilterDepartments(
        IEnumerable<DepartmentDto> departments,
        IReadOnlyCollection<EmployeeDto> employees,
        DepartmentListFilterViewModel filter)
    {
        var query = departments;
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(department =>
            {
                var searchValues = new List<string?>
                {
                    department.Name,
                    department.Description,
                    department.HeadEmployeeFullName,
                };
                searchValues.AddRange(employees
                    .Where(employee => employee.DepartmentId == department.Id)
                    .Select(employee => employee.FullName));

                return MatchesSearch(filter.Search, searchValues.ToArray());
            });
        }

        return query;
    }

    private static IEnumerable<EmployeeDto> FilterEmployees(
        IEnumerable<EmployeeDto> employees,
        IReadOnlyDictionary<int, UserDto> usersById,
        EmployeeListFilterViewModel filter)
    {
        var query = employees;
        if (filter.DepartmentId is > 0)
        {
            query = query.Where(employee => employee.DepartmentId == filter.DepartmentId.Value);
        }

        if (string.Equals(filter.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(employee => employee.IsActive);
        }
        else if (string.Equals(filter.Status, "Inactive", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(employee => !employee.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            query = query.Where(employee =>
                usersById.TryGetValue(employee.UserId, out var user) &&
                string.Equals(user.Role, filter.Role, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(employee =>
            {
                usersById.TryGetValue(employee.UserId, out var user);
                return MatchesSearch(
                    filter.Search,
                    employee.FullName,
                    employee.Email,
                    employee.PhoneNumber,
                    employee.DepartmentName,
                    employee.Position,
                    user?.Role,
                    employee.Id.ToString(),
                    employee.UserId.ToString());
            });
        }

        return query;
    }

    private static IEnumerable<LeaveRequestDto> FilterLeaveRequests(
        IEnumerable<LeaveRequestDto> leaveRequests,
        LeaveRequestListFilterViewModel filter)
    {
        var query = leaveRequests;
        if (filter.Status.HasValue)
        {
            query = query.Where(request => request.Status == filter.Status.Value);
        }

        if (filter.LeaveType.HasValue)
        {
            query = query.Where(request => request.LeaveType == filter.LeaveType.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(request => MatchesSearch(
                filter.Search,
                request.EmployeeFullName,
                request.ApprovedByEmployeeFullName,
                request.RejectionReason,
                LeaveTypeName(request.LeaveType),
                LeaveStatusName(request.Status),
                request.Id.ToString(),
                request.EmployeeId.ToString()));
        }

        return query;
    }

    private static IEnumerable<WorkScheduleDto> FilterWorkSchedules(
        IEnumerable<WorkScheduleDto> workSchedules,
        IReadOnlyDictionary<int, UserDto> usersById,
        WorkScheduleListFilterViewModel filter)
    {
        var query = workSchedules;
        if (filter.From.HasValue)
        {
            query = query.Where(schedule => schedule.ShiftDate >= filter.From.Value);
        }

        if (filter.To.HasValue)
        {
            query = query.Where(schedule => schedule.ShiftDate <= filter.To.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Shift))
        {
            query = query.Where(schedule =>
                string.Equals(schedule.Shift, filter.Shift, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            query = query.Where(schedule =>
                usersById.TryGetValue(schedule.UserId, out var user) &&
                string.Equals(user.Role, filter.Role, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(schedule =>
            {
                usersById.TryGetValue(schedule.UserId, out var user);
                return MatchesSearch(
                    filter.Search,
                    schedule.EmployeeFullName,
                    schedule.Shift,
                    schedule.RoleAtShift,
                    user?.Email,
                    user?.FullName,
                    user?.Role,
                    schedule.Id.ToString(),
                    schedule.UserId.ToString());
            });
        }

        return query;
    }

    private static bool MatchesSearch(string? search, params string?[] values)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        var normalizedSearch = search.Trim();
        return values.Any(value =>
            !string.IsNullOrWhiteSpace(value) &&
            value.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase));
    }

    private static string LeaveTypeName(int value) => value switch
    {
        0 => "Wypoczynkowy",
        1 => "Chorobowy",
        2 => "Okolicznosciowy",
        _ => $"Typ {value}",
    };

    private static string LeaveStatusName(int value) => value switch
    {
        0 => "Oczekuje",
        1 => "Zaakceptowany",
        2 => "Odrzucony",
        _ => $"Status {value}",
    };

    private static void SyncFilter(StaffListFilterViewModel filter, int page, int pageSize)
    {
        filter.Page = page;
        filter.PageSize = pageSize;
    }

    private void SetViewData(string title, string section, string description)
    {
        ViewData["Title"] = title;
        ViewData["Section"] = section;
        ViewData["Description"] = description;
    }

    private static readonly string[] HrNotificationRoles = ["HR", "HRManager", "Admin"];

    private static readonly string[] HrManagerNotificationRoles = ["HRManager", "Admin"];

    private static Notification BuildNotification(
        string type,
        string severity,
        string title,
        string message,
        string linkUrl,
        string sourceType,
        long sourceId)
        => new()
        {
            Type = type,
            Severity = severity,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            SourceType = sourceType,
            SourceId = sourceId,
        };
}
