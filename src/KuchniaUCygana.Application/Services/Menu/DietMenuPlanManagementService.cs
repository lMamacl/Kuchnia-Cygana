using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Interfaces.Services.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class DietMenuPlanManagementService : IDietMenuPlanManagementService
{
    public static readonly IReadOnlyList<string> DefaultSlots =
    [
        "Breakfast",
        "Snack1",
        "Lunch",
        "Snack2",
        "Dinner",
    ];

    private readonly IDietMenuPlanRepository repository;
    private readonly IRecipeEngine recipeEngine;
    private readonly ICurrentUserService currentUser;

    public DietMenuPlanManagementService(
        IDietMenuPlanRepository repository,
        IRecipeEngine recipeEngine,
        ICurrentUserService currentUser)
    {
        this.repository = repository;
        this.recipeEngine = recipeEngine;
        this.currentUser = currentUser;
    }

    public async Task<DietMenuWeekDto> GetWeekAsync(DateOnly startDate)
    {
        var endDate = startDate.AddDays(6);
        var plans = await this.repository.GetPlansAsync(startDate, endDate);
        var byDate = plans.ToDictionary(p => p.PlanDate);
        var days = new List<DietMenuDayDto>();

        for (var offset = 0; offset < 7; offset++)
        {
            var date = startDate.AddDays(offset);
            if (!byDate.TryGetValue(date, out var row))
            {
                days.Add(new DietMenuDayDto
                {
                    PlanDate = date,
                    Status = "Missing",
                    CanEdit = true,
                    Validation = new DietMenuPlanValidationDto
                    {
                        Warnings = ["Brak draftu planu dla dnia."],
                    },
                });
                continue;
            }

            var day = await this.MapDayAsync(row);
            days.Add(day);
        }

        return new DietMenuWeekDto
        {
            StartDate = startDate,
            EndDate = endDate,
            Days = days,
        };
    }

    public async Task<DietMenuDayDto> GetDayAsync(DateOnly date)
    {
        var row = await this.repository.GetPlanByDateAsync(date);
        if (row is null)
        {
            return new DietMenuDayDto
            {
                PlanDate = date,
                Status = "Missing",
                CanEdit = true,
                Validation = new DietMenuPlanValidationDto
                {
                    Warnings = ["Utworz draft dnia, aby dodawac pozycje menu."],
                },
            };
        }

        return await this.MapDayAsync(row);
    }

    public async Task<int> CreateDayAsync(CreateDietMenuPlanRequest request)
    {
        return await this.repository.EnsurePlanAsync(
            request.PlanDate,
            NormalizeOptional(request.Notes),
            this.UserName());
    }

    public async Task AddItemAsync(AddDietMenuPlanItemRequest request)
    {
        var plan = await this.repository.GetPlanByIdAsync(request.DietMenuPlanId)
            ?? throw new InvalidOperationException($"Plan menu #{request.DietMenuPlanId} nie istnieje.");
        this.EnsureEditable(plan);
        this.ValidateItemInput(request.DietVariantId, request.MealId, request.ServingSizeMultiplier, request.MealSlot);

        await this.repository.AddItemAsync(new DietMenuPlanItem
        {
            DietMenuPlanId = request.DietMenuPlanId,
            DietVariantId = request.DietVariantId,
            MealId = request.MealId,
            MealVariantId = request.MealVariantId,
            MealSlot = request.MealSlot.Trim(),
            ServingSizeMultiplier = request.ServingSizeMultiplier,
            SortOrder = request.SortOrder,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = this.UserName(),
        });
    }

    public async Task UpdateItemAsync(UpdateDietMenuPlanItemRequest request)
    {
        var existing = await this.repository.GetPlanItemAsync(request.Id)
            ?? throw new InvalidOperationException($"Pozycja planu #{request.Id} nie istnieje.");
        var plan = await this.repository.GetPlanByIdAsync(existing.DietMenuPlanId)
            ?? throw new InvalidOperationException($"Plan menu #{existing.DietMenuPlanId} nie istnieje.");
        this.EnsureEditable(plan);
        this.ValidateItemInput(request.DietVariantId, request.MealId, request.ServingSizeMultiplier, request.MealSlot);

        await this.repository.UpdateItemAsync(new DietMenuPlanItem
        {
            Id = request.Id,
            DietMenuPlanId = existing.DietMenuPlanId,
            DietVariantId = request.DietVariantId,
            MealId = request.MealId,
            MealVariantId = request.MealVariantId,
            MealSlot = request.MealSlot.Trim(),
            ServingSizeMultiplier = request.ServingSizeMultiplier,
            SortOrder = request.SortOrder,
            IsActive = true,
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = this.UserName(),
        });
    }

    public async Task DeleteItemAsync(int itemId)
    {
        var existing = await this.repository.GetPlanItemAsync(itemId)
            ?? throw new InvalidOperationException($"Pozycja planu #{itemId} nie istnieje.");
        var plan = await this.repository.GetPlanByIdAsync(existing.DietMenuPlanId)
            ?? throw new InvalidOperationException($"Plan menu #{existing.DietMenuPlanId} nie istnieje.");
        this.EnsureEditable(plan);

        await this.repository.SoftDeleteItemAsync(itemId, this.UserName());
    }

    public async Task CopyDayAsync(CopyDietMenuDayRequest request)
    {
        var source = await this.repository.GetPlanByIdAsync(request.SourcePlanId)
            ?? throw new InvalidOperationException($"Plan zrodlowy #{request.SourcePlanId} nie istnieje.");
        var sourceItems = await this.repository.GetPlanItemsAsync(source.Id);
        if (sourceItems.Count == 0)
        {
            throw new InvalidOperationException("Nie mozna skopiowac pustego dnia menu.");
        }

        var target = await this.repository.GetPlanByDateAsync(request.TargetDate);
        if (target is not null)
        {
            this.EnsureEditable(target);
        }

        await this.repository.CopyDayAsync(source.Id, request.TargetDate, this.UserName(), request.ClearTargetDraft);
    }

    public async Task PublishAsync(PublishDietMenuPlanRequest request)
    {
        var plan = await this.repository.GetPlanByIdAsync(request.DietMenuPlanId)
            ?? throw new InvalidOperationException($"Plan menu #{request.DietMenuPlanId} nie istnieje.");
        this.EnsureEditable(plan);

        var validation = await this.ValidatePlanAsync(plan.Id);
        if (!validation.CanPublish)
        {
            throw new InvalidOperationException("Nie mozna opublikowac planu: " + string.Join("; ", validation.Warnings));
        }

        await this.repository.PublishAsync(plan.Id, this.UserName());
    }

    private async Task<DietMenuDayDto> MapDayAsync(DietMenuPlanDayRow row)
    {
        var editState = this.GetEditState(row);
        var items = await this.repository.GetPlanItemsAsync(row.Id);
        var itemDtos = new List<DietMenuPlanItemDto>();

        foreach (var item in items)
        {
            itemDtos.Add(await this.MapItemAsync(item));
        }

        return new DietMenuDayDto
        {
            Id = row.Id,
            PlanDate = row.PlanDate,
            Status = row.Status,
            Notes = row.Notes,
            PublishedAt = row.PublishedAt,
            PublishedBy = row.PublishedBy,
            CanEdit = editState.CanEdit,
            EditBlockReason = editState.Reason,
            Items = itemDtos,
            Validation = BuildValidation(itemDtos),
        };
    }

    private async Task<DietMenuPlanItemDto> MapItemAsync(DietMenuPlanItemRow row)
    {
        var warnings = new List<string>();
        var isMealPublished = IsPublishedMealStatus(row.MealStatus);
        if (!isMealPublished)
        {
            warnings.Add("posilek nie jest opublikowany");
        }

        if (row.ComponentCount == 0 && row.LegacyRecipeCount == 0)
        {
            warnings.Add("posilek nie ma skladowych ani legacy receptury");
        }

        if (!row.HasNutrition)
        {
            warnings.Add("brak kompletnego nutrition posilku");
        }

        if (row.AllergenCount == 0)
        {
            warnings.Add("brak alergenow posilku lub skladowych");
        }

        if (row.PackagingRequirementCount == 0)
        {
            warnings.Add("brak opakowania produkcyjnego");
        }

        if (row.MissingWarehouseCategoryCount > 0)
        {
            warnings.Add($"brak kategorii magazynowej dla {row.MissingWarehouseCategoryCount} skladnikow");
        }

        var isRecipeValid = row.ComponentCount > 0 || row.LegacyRecipeCount > 0
            ? await this.recipeEngine.ValidateRecipeAsync(row.MealId)
            : false;
        if (!isRecipeValid)
        {
            warnings.Add("receptura/skladowe nie sa kompletne produkcyjnie");
        }

        return new DietMenuPlanItemDto
        {
            Id = row.Id,
            DietMenuPlanId = row.DietMenuPlanId,
            DietVariantId = row.DietVariantId,
            DietName = row.DietName,
            VariantName = row.VariantName,
            MealId = row.MealId,
            MealVariantId = row.MealVariantId,
            MealVariantName = row.MealVariantName,
            MealName = row.MealName,
            MealStatus = row.MealStatus,
            MealSlot = row.MealSlot,
            ServingSizeMultiplier = row.ServingSizeMultiplier,
            SortOrder = row.SortOrder,
            ComponentCount = row.ComponentCount,
            LegacyRecipeCount = row.LegacyRecipeCount,
            IsMealPublished = isMealPublished,
            IsRecipeValid = isRecipeValid,
            ValidationWarnings = warnings.Distinct().ToList(),
        };
    }

    private async Task<DietMenuPlanValidationDto> ValidatePlanAsync(int planId)
    {
        var items = await this.repository.GetPlanItemsAsync(planId);
        if (items.Count == 0)
        {
            return new DietMenuPlanValidationDto
            {
                Warnings = ["plan dnia nie ma pozycji"],
            };
        }

        var mapped = new List<DietMenuPlanItemDto>();
        foreach (var item in items)
        {
            mapped.Add(await this.MapItemAsync(item));
        }

        return BuildValidation(mapped);
    }

    private static DietMenuPlanValidationDto BuildValidation(IReadOnlyList<DietMenuPlanItemDto> items)
    {
        var warnings = new List<string>();
        if (items.Count == 0)
        {
            warnings.Add("plan dnia nie ma pozycji");
        }

        foreach (var item in items)
        {
            foreach (var warning in item.ValidationWarnings)
            {
                warnings.Add($"{item.MealSlot} / {item.VariantName} / {item.MealName}: {warning}");
            }
        }

        return new DietMenuPlanValidationDto
        {
            Warnings = warnings.Distinct().ToList(),
        };
    }

    private void EnsureEditable(DietMenuPlanDayRow plan)
    {
        var editState = this.GetEditState(plan);
        if (!editState.CanEdit)
        {
            throw new InvalidOperationException(editState.Reason ?? "Plan nie moze byc edytowany.");
        }
    }

    private EditState GetEditState(DietMenuPlanDayRow plan)
    {
        if (plan.Status == "Draft")
        {
            return new EditState(true, null);
        }

        if (plan.Status != "Published")
        {
            return new EditState(false, $"Plan w statusie {plan.Status} nie jest edytowalny.");
        }

        var nowWarsaw = GetWarsawNow();
        var cutoff = plan.PlanDate.AddDays(-3).ToDateTime(TimeOnly.MinValue);
        if (nowWarsaw.DateTime < cutoff)
        {
            return new EditState(true, null);
        }

        return new EditState(false, "Edycja opublikowanego planu jest zablokowana po polnocy D-3. W kolejnym etapie obsluzy to override managera i alert M3.");
    }

    private void ValidateItemInput(int dietVariantId, int mealId, decimal multiplier, string? mealSlot)
    {
        if (dietVariantId <= 0)
        {
            throw new InvalidOperationException("Wariant diety jest wymagany.");
        }

        if (mealId <= 0)
        {
            throw new InvalidOperationException("Posilek jest wymagany.");
        }

        if (multiplier <= 0)
        {
            throw new InvalidOperationException("Mnoznik porcji musi byc wiekszy od zera.");
        }

        if (string.IsNullOrWhiteSpace(mealSlot))
        {
            throw new InvalidOperationException("Slot posilku jest wymagany.");
        }
    }

    private static bool IsPublishedMealStatus(string status)
    {
        return string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset GetWarsawNow()
    {
        TimeZoneInfo zone;
        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Warsaw");
        }
        catch (TimeZoneNotFoundException)
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        }

        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private string? UserName()
    {
        return this.currentUser.GetUserName();
    }

    private sealed record EditState(bool CanEdit, string? Reason);
}
