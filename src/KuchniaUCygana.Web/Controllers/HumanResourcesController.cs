using KuchniaUCygana.Application.DTOs;
using KuchniaUCygana.Application.DTOs.HR;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Enums;
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
        return View(await BuildDashboardModelAsync());
    }

    [HttpGet("departments")]
    public async Task<IActionResult> Departments([FromQuery] DepartmentListFilterViewModel filter)
    {
        SetViewData("Dzialy", "Kadry", "Struktura organizacyjna i odpowiedzialni pracownicy.");
        return View(await BuildDepartmentsModelAsync(filter));
    }

    [HttpPost("departments")]
    public async Task<IActionResult> CreateDepartment(CreateDepartmentRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Dzialy", "Kadry", "Struktura organizacyjna i odpowiedzialni pracownicy.");
            return View("Departments", await BuildDepartmentsModelAsync(new DepartmentListFilterViewModel(), request));
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
        return View(await BuildEmployeesModelAsync(filter));
    }

    [HttpGet("employees/new")]
    public async Task<IActionResult> NewEmployee()
    {
        SetViewData("Nowy pracownik", "Kadry", "Dodanie pracownika do kartoteki HR.");
        return View(await BuildNewEmployeeModelAsync());
    }

    [HttpPost("employees")]
    public async Task<IActionResult> CreateEmployee(CreateEmployeeRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Nowy pracownik", "Kadry", "Dodanie pracownika do kartoteki HR.");
            return View("NewEmployee", await BuildNewEmployeeModelAsync(request));
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
        return View(await BuildLeavesModelAsync(filter));
    }

    [HttpGet("leaves/new")]
    [AllowOutsideShift]
    public async Task<IActionResult> NewLeaveRequest()
    {
        SetViewData("Nowy wniosek", "Kadry", "Rejestracja wniosku urlopowego.");
        return View(await BuildNewLeaveRequestModelAsync());
    }

    [HttpPost("leaves")]
    [AllowOutsideShift]
    public async Task<IActionResult> CreateLeaveRequest(CreateLeaveRequestRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Nowy wniosek", "Kadry", "Rejestracja wniosku urlopowego.");
            return View("NewLeaveRequest", await BuildNewLeaveRequestModelAsync(request));
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
        return View(await BuildSchedulesModelAsync(filter));
    }

    [HttpGet("schedules/new")]
    [AllowOutsideShift]
    public async Task<IActionResult> NewWorkSchedule()
    {
        SetViewData("Nowa zmiana", "Kadry", "Dodanie zmiany do grafiku.");
        return View(await BuildNewWorkScheduleModelAsync());
    }

    [HttpPost("schedules")]
    [AllowOutsideShift]
    public async Task<IActionResult> CreateWorkSchedule(CreateWorkScheduleRequest request)
    {
        if (!ModelState.IsValid)
        {
            SetViewData("Nowa zmiana", "Kadry", "Dodanie zmiany do grafiku.");
            return View("NewWorkSchedule", await BuildNewWorkScheduleModelAsync(request));
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

    private async Task<HumanResourcesDashboardViewModel> BuildDashboardModelAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var pendingLeaves = await humanResourcesService.SearchLeaveRequestsAsync(new LeaveRequestSearchRequest
        {
            Status = 0,
            Page = 1,
            PageSize = 8,
        });
        var todaySchedules = await humanResourcesService.SearchWorkSchedulesAsync(new WorkScheduleSearchRequest
        {
            From = today,
            To = today,
            Page = 1,
            PageSize = 8,
        });

        return new HumanResourcesDashboardViewModel
        {
            Summary = await humanResourcesService.GetSummaryAsync(today),
            DepartmentSummaries = await humanResourcesService.GetDepartmentStaffSummariesAsync(),
            LeaveRequests = pendingLeaves.Items,
            WorkSchedules = todaySchedules.Items,
        };
    }

    private async Task<HumanResourcesDashboardViewModel> BuildDepartmentsModelAsync(
        DepartmentListFilterViewModel filter,
        CreateDepartmentRequest? newDepartment = null)
    {
        filter ??= new DepartmentListFilterViewModel();
        var page = await humanResourcesService.SearchDepartmentsAsync(new DepartmentSearchRequest
        {
            Search = filter.Search,
            Page = filter.Page,
            PageSize = filter.PageSize,
        });
        SyncFilter(filter, page.Page, page.PageSize);

        return new HumanResourcesDashboardViewModel
        {
            Departments = page.Items,
            Employees = await humanResourcesService.GetEmployeeOptionsAsync(activeOnly: true),
            DepartmentSummaries = await humanResourcesService.GetDepartmentStaffSummariesAsync(filter.Search),
            DepartmentsPage = ToPagedList(page.Items, page.Page, page.PageSize, page.TotalCount),
            DepartmentsFilter = filter,
            NewDepartment = newDepartment ?? new CreateDepartmentRequest(),
        };
    }

    private async Task<HumanResourcesDashboardViewModel> BuildEmployeesModelAsync(EmployeeListFilterViewModel filter)
    {
        filter ??= new EmployeeListFilterViewModel();
        var page = await humanResourcesService.SearchEmployeesAsync(new EmployeeSearchRequest
        {
            Search = filter.Search,
            DepartmentId = filter.DepartmentId,
            Status = filter.Status,
            Role = filter.Role,
            Page = filter.Page,
            PageSize = filter.PageSize,
        });
        SyncFilter(filter, page.Page, page.PageSize);

        return new HumanResourcesDashboardViewModel
        {
            Departments = await GetDepartmentOptionsAsync(),
            Employees = page.Items,
            EmployeesPage = ToPagedList(page.Items, page.Page, page.PageSize, page.TotalCount),
            EmployeesFilter = filter,
            AvailableRoles = StaffUserRoles,
        };
    }

    private async Task<HumanResourcesDashboardViewModel> BuildNewEmployeeModelAsync(CreateEmployeeRequest? newEmployee = null)
    {
        return new HumanResourcesDashboardViewModel
        {
            Departments = await GetDepartmentOptionsAsync(),
            Users = await userService.GetByRolesAsync(StaffUserRoles),
            NewEmployee = newEmployee ?? new CreateEmployeeRequest
            {
                HireDate = DateOnly.FromDateTime(DateTime.Today),
                IsActive = true,
            },
        };
    }

    private async Task<HumanResourcesDashboardViewModel> BuildLeavesModelAsync(LeaveRequestListFilterViewModel filter)
    {
        filter ??= new LeaveRequestListFilterViewModel();
        var page = await humanResourcesService.SearchLeaveRequestsAsync(new LeaveRequestSearchRequest
        {
            Search = filter.Search,
            Status = filter.Status,
            LeaveType = filter.LeaveType,
            Page = filter.Page,
            PageSize = filter.PageSize,
        });
        SyncFilter(filter, page.Page, page.PageSize);

        return new HumanResourcesDashboardViewModel
        {
            Employees = await humanResourcesService.GetEmployeeOptionsAsync(activeOnly: true),
            LeaveRequests = page.Items,
            LeaveRequestsPage = ToPagedList(page.Items, page.Page, page.PageSize, page.TotalCount),
            LeaveRequestsFilter = filter,
        };
    }

    private async Task<HumanResourcesDashboardViewModel> BuildNewLeaveRequestModelAsync(CreateLeaveRequestRequest? newLeaveRequest = null)
    {
        return new HumanResourcesDashboardViewModel
        {
            Employees = await humanResourcesService.GetEmployeeOptionsAsync(activeOnly: true),
            NewLeaveRequest = newLeaveRequest ?? new CreateLeaveRequestRequest
            {
                StartDate = DateOnly.FromDateTime(DateTime.Today),
                EndDate = DateOnly.FromDateTime(DateTime.Today),
            },
        };
    }

    private async Task<HumanResourcesDashboardViewModel> BuildSchedulesModelAsync(WorkScheduleListFilterViewModel filter)
    {
        filter ??= new WorkScheduleListFilterViewModel();
        var page = await humanResourcesService.SearchWorkSchedulesAsync(new WorkScheduleSearchRequest
        {
            Search = filter.Search,
            From = filter.From,
            To = filter.To,
            Shift = ParseWorkShift(filter.Shift),
            Role = filter.Role,
            Page = filter.Page,
            PageSize = filter.PageSize,
        });
        SyncFilter(filter, page.Page, page.PageSize);

        return new HumanResourcesDashboardViewModel
        {
            WorkSchedules = page.Items,
            WorkSchedulesPage = ToPagedList(page.Items, page.Page, page.PageSize, page.TotalCount),
            WorkSchedulesFilter = filter,
            AvailableRoles = StaffUserRoles,
        };
    }

    private async Task<HumanResourcesDashboardViewModel> BuildNewWorkScheduleModelAsync(CreateWorkScheduleRequest? newWorkSchedule = null)
    {
        return new HumanResourcesDashboardViewModel
        {
            Users = await userService.GetByRolesAsync(StaffUserRoles),
            NewWorkSchedule = newWorkSchedule ?? new CreateWorkScheduleRequest
            {
                ShiftDate = DateOnly.FromDateTime(DateTime.Today),
            },
        };
    }

    private static WorkShift? ParseWorkShift(string? shift)
        => Enum.TryParse<WorkShift>(shift, ignoreCase: true, out var parsed)
            ? parsed
            : null;

    private async Task<IReadOnlyList<DepartmentDto>> GetDepartmentOptionsAsync()
    {
        var page = await humanResourcesService.SearchDepartmentsAsync(new DepartmentSearchRequest
        {
            Page = 1,
            PageSize = 200,
        });

        return page.Items;
    }

    private static PagedList<T> ToPagedList<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount)
        => new()
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
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

    private static readonly string[] StaffUserRoles =
    [
        UserRoles.Kitchen,
        UserRoles.KitchenManager,
        UserRoles.Warehouse,
        UserRoles.WarehouseManager,
        UserRoles.Packing,
        UserRoles.PackingManager,
        UserRoles.Dietitian,
        UserRoles.Logistics,
        UserRoles.LogisticsManager,
        UserRoles.Driver,
        UserRoles.DriverManager,
        UserRoles.Admin,
        UserRoles.HR,
        UserRoles.HRManager,
        UserRoles.BOK,
        UserRoles.BOKManager,
    ];

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
