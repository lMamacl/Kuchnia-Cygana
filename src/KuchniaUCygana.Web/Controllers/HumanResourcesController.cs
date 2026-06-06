using KuchniaUCygana.Application.DTOs.HR;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Notifications;
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
    public async Task<IActionResult> Departments(int page = 1, int pageSize = 10)
    {
        SetViewData("Dzialy", "Kadry", "Struktura organizacyjna i odpowiedzialni pracownicy.");
        return View(await BuildModelAsync(departmentsPage: page, pageSize: pageSize));
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
    public async Task<IActionResult> Employees(int page = 1, int pageSize = 10)
    {
        SetViewData("Pracownicy", "Kadry", "Kartoteka pracownikow i status zatrudnienia.");
        return View(await BuildModelAsync(employeesPage: page, pageSize: pageSize));
    }

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee(CreateEmployeeRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Pracownicy", "Kadry", "Kartoteka pracownikow i status zatrudnienia.");
            return View("Employees", await BuildModelAsync(newEmployee: request));
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
            return RedirectToAction(nameof(Employees));
        }
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
    public async Task<IActionResult> Leaves(int page = 1, int pageSize = 10)
    {
        SetViewData("Urlopy", "Kadry", "Wnioski urlopowe i decyzje kadrowe.");
        return View(await BuildModelAsync(leaveRequestsPage: page, pageSize: pageSize));
    }

    [HttpPost("leaves")]
    public async Task<IActionResult> CreateLeaveRequest(CreateLeaveRequestRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Urlopy", "Kadry", "Wnioski urlopowe i decyzje kadrowe.");
            return View("Leaves", await BuildModelAsync(newLeaveRequest: request));
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
            return RedirectToAction(nameof(Leaves));
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
    public async Task<IActionResult> Schedules(int page = 1, int pageSize = 10)
    {
        SetViewData("Grafik", "Kadry", "Zmiany pracownikow i role na zmianie.");
        return View(await BuildModelAsync(workSchedulesPage: page, pageSize: pageSize));
    }

    [HttpPost("schedules")]
    public async Task<IActionResult> CreateWorkSchedule(CreateWorkScheduleRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Grafik", "Kadry", "Zmiany pracownikow i role na zmianie.");
            return View("Schedules", await BuildModelAsync(newWorkSchedule: request));
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
            return RedirectToAction(nameof(Schedules));
        }
    }

    [Authorize(Roles = "HRManager,Admin")]
    [HttpPost("schedules/{id:int}/delete")]
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
        int departmentsPage = 1,
        int employeesPage = 1,
        int leaveRequestsPage = 1,
        int workSchedulesPage = 1,
        int pageSize = 10)
    {
        var departments = (await humanResourcesService.GetDepartmentsAsync()).ToArray();
        var employees = (await humanResourcesService.GetEmployeesAsync()).ToArray();
        var leaveRequests = (await humanResourcesService.GetLeaveRequestsAsync()).ToArray();
        var workSchedules = (await humanResourcesService.GetWorkSchedulesAsync()).ToArray();
        var users = (await userService.GetAllAsync()).ToArray();

        return new HumanResourcesDashboardViewModel
        {
            Departments = departments,
            Employees = employees,
            LeaveRequests = leaveRequests,
            WorkSchedules = workSchedules,
            Users = users,
            DepartmentsPage = PagedList<DepartmentDto>.Create(
                departments.OrderBy(department => department.Name),
                departmentsPage,
                pageSize),
            EmployeesPage = PagedList<EmployeeDto>.Create(
                employees.OrderBy(employee => employee.LastName).ThenBy(employee => employee.FirstName),
                employeesPage,
                pageSize),
            LeaveRequestsPage = PagedList<LeaveRequestDto>.Create(
                leaveRequests.OrderByDescending(request => request.CreatedAt),
                leaveRequestsPage,
                pageSize),
            WorkSchedulesPage = PagedList<WorkScheduleDto>.Create(
                workSchedules.OrderBy(schedule => schedule.ShiftDate).ThenBy(schedule => schedule.Shift),
                workSchedulesPage,
                pageSize),
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
