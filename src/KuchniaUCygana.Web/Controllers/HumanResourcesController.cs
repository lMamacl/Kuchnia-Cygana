using KuchniaUCygana.Application.DTOs.HR;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Authorize(Roles = "HR,HRManager,Admin")]
[Route("hr")]
public sealed class HumanResourcesController : Controller
{
    private readonly IHumanResourcesService humanResourcesService;
    private readonly IUserService userService;

    public HumanResourcesController(
        IHumanResourcesService humanResourcesService,
        IUserService userService)
    {
        this.humanResourcesService = humanResourcesService;
        this.userService = userService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        SetViewData("HR", "Kadry", "Dashboard HR: pracownicy, dzialy, urlopy i grafik.");
        return View(await BuildModelAsync());
    }

    [HttpGet("departments")]
    public async Task<IActionResult> Departments()
    {
        SetViewData("Dzialy", "Kadry", "Struktura organizacyjna i odpowiedzialni pracownicy.");
        return View(await BuildModelAsync());
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
            await humanResourcesService.CreateDepartmentAsync(request);
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
    public async Task<IActionResult> Employees()
    {
        SetViewData("Pracownicy", "Kadry", "Kartoteka pracownikow i status zatrudnienia.");
        return View(await BuildModelAsync());
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
            await humanResourcesService.CreateEmployeeAsync(request);
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
        TempData[deactivated ? "Success" : "Error"] = deactivated
            ? "Pracownik zostal dezaktywowany."
            : "Nie znaleziono pracownika.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpGet("leaves")]
    public async Task<IActionResult> Leaves()
    {
        SetViewData("Urlopy", "Kadry", "Wnioski urlopowe i decyzje kadrowe.");
        return View(await BuildModelAsync());
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
            await humanResourcesService.CreateLeaveRequestAsync(request);
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
        return RedirectToAction(nameof(Leaves));
    }

    [HttpGet("schedules")]
    public async Task<IActionResult> Schedules()
    {
        SetViewData("Grafik", "Kadry", "Zmiany pracownikow i role na zmianie.");
        return View(await BuildModelAsync());
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
            await humanResourcesService.CreateWorkScheduleAsync(request);
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
        TempData[deleted ? "Success" : "Error"] = deleted
            ? "Zmiana zostala usunieta z grafiku."
            : "Nie znaleziono zmiany.";
        return RedirectToAction(nameof(Schedules));
    }

    private async Task<HumanResourcesDashboardViewModel> BuildModelAsync(
        CreateDepartmentRequest? newDepartment = null,
        CreateEmployeeRequest? newEmployee = null,
        CreateLeaveRequestRequest? newLeaveRequest = null,
        CreateWorkScheduleRequest? newWorkSchedule = null)
    {
        return new HumanResourcesDashboardViewModel
        {
            Departments = (await humanResourcesService.GetDepartmentsAsync()).ToArray(),
            Employees = (await humanResourcesService.GetEmployeesAsync()).ToArray(),
            LeaveRequests = (await humanResourcesService.GetLeaveRequestsAsync()).ToArray(),
            WorkSchedules = (await humanResourcesService.GetWorkSchedulesAsync()).ToArray(),
            Users = (await userService.GetAllAsync()).ToArray(),
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
}
