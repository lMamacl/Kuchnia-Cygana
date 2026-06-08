using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;
using KuchniaUCygana.Domain.Services;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class DietMenuPlanManagementService : IDietMenuPlanManagementService
{
    private const int MinDaysCount = 7;
    private const int MaxDaysCount = 31;

    public static readonly IReadOnlyList<string> DefaultSlots =
    [
        "Breakfast",
        "Snack1",
        "Lunch",
        "Snack2",
        "Dinner",
    ];

    private readonly IDietMenuPlanRepository repository;
    private readonly IMealManagementService mealManagementService;
    private readonly ICurrentUserService currentUser;

    public DietMenuPlanManagementService(
        IDietMenuPlanRepository repository,
        IMealManagementService mealManagementService,
        ICurrentUserService currentUser)
    {
        this.repository = repository;
        this.mealManagementService = mealManagementService;
        this.currentUser = currentUser;
    }

    public async Task<DietMenuWeekDto> GetWeekAsync(DateOnly startDate, int days = 7)
    {
        var daysCount = NormalizeDaysCount(days);
        var endDate = startDate.AddDays(daysCount - 1);
        var plans = await this.repository.GetPlanSummariesAsync(startDate, endDate);
        var byDate = plans.ToDictionary(p => p.PlanDate);
        var dayDtos = new List<DietMenuDaySummaryDto>();

        for (var offset = 0; offset < daysCount; offset++)
        {
            var date = startDate.AddDays(offset);
            if (!byDate.TryGetValue(date, out var row))
            {
                dayDtos.Add(new DietMenuDaySummaryDto
                {
                    PlanDate = date,
                    Status = "Missing",
                    CanEdit = true,
                    QuickWarningCount = 1,
                });
                continue;
            }

            dayDtos.Add(this.MapSummary(row));
        }

        return new DietMenuWeekDto
        {
            StartDate = startDate,
            EndDate = endDate,
            DaysCount = daysCount,
            Days = dayDtos,
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

    public async Task<DietMenuDayShellDto> GetDayShellAsync(DateOnly date)
    {
        var row = await this.repository.GetPlanByDateAsync(date);
        if (row is null)
        {
            return new DietMenuDayShellDto
            {
                PlanDate = date,
                Status = "Missing",
                CanEdit = true,
                QuickWarningCount = 1,
            };
        }

        var editState = this.GetEditState(row);
        var variantSummaries = await this.repository.GetPlanDietVariantSummariesAsync(row.Id);
        return new DietMenuDayShellDto
        {
            Id = row.Id,
            PlanDate = row.PlanDate,
            Status = row.Status,
            Notes = row.Notes,
            PublishedAt = row.PublishedAt,
            PublishedBy = row.PublishedBy,
            CanEdit = editState.CanEdit,
            EditBlockReason = editState.Reason,
            ActiveItemCount = variantSummaries.Sum(summary => summary.ActiveItemCount),
            QuickWarningCount = variantSummaries.Sum(summary => summary.QuickWarningCount),
            DietVariants = variantSummaries.Select(MapDietVariantSummary).ToList(),
        };
    }

    public async Task<DietMenuDietVariantItemsDto> GetDietVariantItemsAsync(int planId, int dietVariantId)
    {
        var plan = await this.repository.GetPlanByIdAsync(planId)
            ?? throw new InvalidOperationException($"Plan menu #{planId} nie istnieje.");
        var editState = this.GetEditState(plan);
        var summaries = await this.repository.GetPlanDietVariantSummariesAsync(planId);
        var selectedSummary = summaries.FirstOrDefault(summary => summary.DietVariantId == dietVariantId);
        var items = await this.repository.GetPlanItemsAsync(planId, dietVariantId);
        var itemDtos = await this.MapItemsAsync(items, MealVariantResultCacheMode.CachePreferred);

        return new DietMenuDietVariantItemsDto
        {
            DietMenuPlanId = plan.Id,
            PlanDate = plan.PlanDate,
            PlanStatus = plan.Status,
            CanEdit = editState.CanEdit,
            DietVariantId = dietVariantId,
            DietName = selectedSummary?.DietName ?? string.Empty,
            VariantName = selectedSummary?.VariantName ?? string.Empty,
            Items = itemDtos,
            Validation = BuildValidation(itemDtos),
        };
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
        await this.ValidateItemInputAsync(
            request.DietVariantId,
            request.MealId,
            request.MealVariantId,
            request.ServingSizeMultiplier,
            request.MealSlot);

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
        await this.ValidateItemInputAsync(
            request.DietVariantId,
            request.MealId,
            request.MealVariantId,
            request.ServingSizeMultiplier,
            request.MealSlot);

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

        var itemRows = await this.repository.GetPlanItemsAsync(plan.Id);
        var snapshots = await this.CreatePublishedSnapshotRowsAsync(plan, itemRows);
        var planSnapshotHash = ProductionSnapshotPayloadFactory.CreatePlanHash(
            snapshots.Select(snapshot => snapshot.SnapshotHash));

        await this.repository.PublishAsync(plan.Id, this.UserName(), snapshots, planSnapshotHash);
    }

    private async Task<DietMenuDayDto> MapDayAsync(DietMenuPlanDayRow row)
    {
        var editState = this.GetEditState(row);
        var items = await this.repository.GetPlanItemsAsync(row.Id);
        var itemDtos = await this.MapItemsAsync(items, MealVariantResultCacheMode.CachePreferred);

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

    private DietMenuDaySummaryDto MapSummary(DietMenuPlanDaySummaryRow row)
    {
        var editState = this.GetEditState(row.PlanDate, row.Status);
        return new DietMenuDaySummaryDto
        {
            Id = row.Id,
            PlanDate = row.PlanDate,
            Status = row.Status,
            PublishedAt = row.PublishedAt,
            PublishedBy = row.PublishedBy,
            CanEdit = editState.CanEdit,
            EditBlockReason = editState.Reason,
            ActiveItemCount = row.ActiveItemCount,
            QuickWarningCount = row.QuickWarningCount,
        };
    }

    private static DietMenuPlanDietVariantSummaryDto MapDietVariantSummary(DietMenuPlanDietVariantSummaryRow row)
        => new()
        {
            DietVariantId = row.DietVariantId,
            DietName = row.DietName,
            VariantName = row.VariantName,
            TargetCalories = row.TargetCalories,
            IsDefault = row.IsDefault,
            ActiveItemCount = row.ActiveItemCount,
            QuickWarningCount = row.QuickWarningCount,
        };

    private async Task<List<DietMenuPlanItemDto>> MapItemsAsync(
        IReadOnlyList<DietMenuPlanItemRow> rows,
        MealVariantResultCacheMode cacheMode)
    {
        var resultKeys = rows
            .Select(row => new MealVariantResultKey(row.MealId, row.MealVariantId))
            .Distinct()
            .ToList();
        var results = await this.mealManagementService.GetMealVariantResultsAsync(resultKeys, cacheMode);
        var mapped = new List<DietMenuPlanItemDto>();

        foreach (var row in rows)
        {
            var key = new MealVariantResultKey(row.MealId, row.MealVariantId);
            results.TryGetValue(key, out var result);
            mapped.Add(this.MapItem(row, result));
        }

        return mapped;
    }

    private DietMenuPlanItemDto MapItem(DietMenuPlanItemRow row, MealVariantResultDto? result)
    {
        var warnings = new List<string>();
        var isMealPublished = IsPublishedMealStatus(row.MealStatus);
        if (!isMealPublished)
        {
            warnings.Add("posilek nie jest opublikowany");
        }

        if (row.MealVariantId.HasValue && !IsPublishedMealStatus(row.MealVariantStatus ?? string.Empty))
        {
            warnings.Add("wariant dania nie jest opublikowany");
        }

        if (result is null)
        {
            warnings.Add(row.MealVariantId.HasValue
                ? "wskazany wariant dania nie istnieje dla wybranego posilku"
                : "brak wyniku kalkulatora dla posilku");
        }
        else if (!result.IsComplete)
        {
            warnings.AddRange(result.ValidationWarnings);
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
            MealVariantStatus = row.MealVariantStatus,
            MealName = row.MealName,
            MealStatus = row.MealStatus,
            MealSlot = row.MealSlot,
            ServingSizeMultiplier = row.ServingSizeMultiplier,
            FinalWeightGrams = result?.FinalWeightGrams,
            FinalWeightAfterMultiplierGrams = result?.FinalWeightGrams is null
                ? null
                : result.FinalWeightGrams.Value * row.ServingSizeMultiplier,
            CompletenessStatus = result?.CompletenessStatus ?? "Incomplete",
            SortOrder = row.SortOrder,
            ComponentCount = row.ComponentCount,
            LegacyRecipeCount = row.LegacyRecipeCount,
            IsMealPublished = isMealPublished,
            IsRecipeValid = result?.IsComplete ?? false,
            IsResultComplete = result?.IsComplete ?? false,
            ValidationWarnings = warnings.Distinct().ToList(),
        };
    }

    private async Task<List<DietMenuPlanPublishedSnapshotRow>> CreatePublishedSnapshotRowsAsync(
        DietMenuPlanDayRow plan,
        IReadOnlyList<DietMenuPlanItemRow> rows)
    {
        var resultKeys = rows
            .Select(row => new MealVariantResultKey(row.MealId, row.MealVariantId))
            .Distinct()
            .ToList();
        var results = await this.mealManagementService.GetMealVariantResultsAsync(
            resultKeys,
            MealVariantResultCacheMode.Fresh);
        var snapshots = new List<DietMenuPlanPublishedSnapshotRow>();

        foreach (var row in rows)
        {
            results.TryGetValue(new MealVariantResultKey(row.MealId, row.MealVariantId), out var result);
            var item = this.MapItem(row, result);
            snapshots.Add(CreatePublishedSnapshotRow(plan, item, result));
        }

        return snapshots;
    }

    private static DietMenuPlanPublishedSnapshotRow CreatePublishedSnapshotRow(
        DietMenuPlanDayRow plan,
        DietMenuPlanItemDto item,
        MealVariantResultDto? result)
    {
        var snapshot = new PublishedDietPlanItemDto
        {
            DietMenuPlanItemId = item.Id,
            DietMenuPlanId = item.DietMenuPlanId,
            PlanDate = plan.PlanDate,
            MealId = item.MealId,
            MealVariantId = item.MealVariantId,
            MealVariantName = item.MealVariantName,
            MealName = item.MealName,
            DietVariantId = item.DietVariantId,
            DietName = item.DietName,
            DietVariantName = item.VariantName,
            MealSlot = item.MealSlot,
            SortOrder = item.SortOrder,
            ServingMultiplier = item.ServingSizeMultiplier,
            RawWeightGrams = result?.FinalRawWeightGrams,
            CookedWeightGrams = result?.FinalCookedWeightGrams,
            FinalWeightGrams = result?.FinalWeightGrams,
            FinalWeightAfterMultiplierGrams = result?.FinalWeightGrams is null
                ? null
                : result.FinalWeightGrams.Value * item.ServingSizeMultiplier,
            NutritionSource = result?.NutritionSource ?? "Aggregated",
            NutritionOverrideReason = result?.OverrideReason,
            Nutrition = MapSnapshotNutrition(result, item.ServingSizeMultiplier),
            Allergens = result?.Allergens.Select(allergen => allergen.Name).Distinct().OrderBy(name => name).ToList()
                ?? new List<string>(),
            Components = result?.Components.Select(MapSnapshotComponent).ToList()
                ?? new List<MealComponentVersionDto>(),
            AggregateIngredients = result?.Ingredients.Select(MapSnapshotAggregateIngredient).ToList()
                ?? new List<AggregateIngredientDto>(),
            PackagingRequirements = result?.PackagingRequirements.Select(MapSnapshotPackaging).ToList()
                ?? new List<PackagingRequirementDto>(),
            RecipeComponentVersionIds = result?.Components
                .Select(component => component.RecipeComponentVersionId)
                .Where(id => id > 0)
                .Distinct()
                .OrderBy(id => id)
                .ToList() ?? new List<int>(),
            ValidationWarnings = item.ValidationWarnings,
            CompletenessStatus = item.CompletenessStatus,
            IsAggregated = result?.IsAggregated ?? false,
            IsCompleteForProduction = item.IsResultComplete,
        };

        var payload = ProductionSnapshotPayloadFactory.Create(snapshot);
        return new DietMenuPlanPublishedSnapshotRow
        {
            DietMenuPlanItemId = item.Id,
            SnapshotJson = payload.Json,
            SnapshotHash = payload.Hash,
        };
    }

    private static MealComponentVersionDto MapSnapshotComponent(MealVariantResultComponentDto component)
        => new()
        {
            RecipeComponentId = component.RecipeComponentId,
            RecipeComponentVersionId = component.RecipeComponentVersionId,
            ComponentName = component.ComponentName,
            VersionNumber = component.VersionNumber,
            VersionStatus = component.VersionStatus,
            Role = component.Role,
            QuantityPerServing = component.QuantityPerServing,
            Unit = component.Unit,
            SortOrder = component.SortOrder,
            IsOptional = component.IsOptional,
            YieldQuantity = component.YieldQuantity,
            YieldUnit = component.YieldUnit,
            RawWeightGrams = component.RawWeightGrams,
            CookedWeightGrams = component.CookedWeightGrams,
            FinalWeightGrams = component.FinalWeightGrams,
            ScaleFactor = component.ScaleFactor,
            Nutrition = MapSnapshotNutrition(component.NutritionPer100g, component.NutritionPerServing),
            Allergens = component.Allergens.Select(allergen => allergen.Name).Distinct().OrderBy(name => name).ToList(),
            Ingredients = component.Ingredients.Select(MapSnapshotComponentIngredient).ToList(),
            PackagingRequirements = component.PackagingRequirements.Select(MapSnapshotPackaging).ToList(),
            ValidationWarnings = component.ValidationWarnings,
            IsCompleteForProduction = component.IsComplete,
        };

    private static ComponentIngredientDto MapSnapshotComponentIngredient(MealVariantResultIngredientDto ingredient)
        => new()
        {
            IngredientId = ingredient.IngredientId,
            IngredientName = ingredient.IngredientName,
            StockItemId = ingredient.StockItemId,
            WarehouseCategoryId = ingredient.WarehouseCategoryId,
            WarehouseCategoryName = ingredient.WarehouseCategoryName,
            WeightInGrams = ingredient.NetWeightInGrams,
            YieldFactor = ingredient.YieldFactor,
            IsOptional = ingredient.IsOptional,
            Notes = ingredient.Notes,
        };

    private static AggregateIngredientDto MapSnapshotAggregateIngredient(MealVariantResultIngredientDto ingredient)
        => new()
        {
            IngredientId = ingredient.IngredientId,
            IngredientName = ingredient.IngredientName,
            StockItemId = ingredient.StockItemId,
            WarehouseCategoryId = ingredient.WarehouseCategoryId,
            WarehouseCategoryName = ingredient.WarehouseCategoryName,
            NetWeightInGrams = ingredient.NetWeightInGrams,
            GrossWeightInGrams = ingredient.GrossWeightInGrams,
            YieldFactor = ingredient.YieldFactor,
            IsOptional = ingredient.IsOptional,
            Notes = ingredient.Notes,
            SourceRecipeComponentVersionIds = ingredient.SourceRecipeComponentVersionIds,
            SourceComponentNames = ingredient.SourceComponentNames,
        };

    private static PackagingRequirementDto MapSnapshotPackaging(MealVariantResultPackagingDto packaging)
        => new()
        {
            OwnerType = packaging.OwnerType,
            MealId = packaging.MealId,
            MealVariantId = packaging.MealVariantId,
            RecipeComponentVersionId = packaging.RecipeComponentVersionId,
            StockItemId = packaging.StockItemId,
            WarehouseCategoryId = packaging.WarehouseCategoryId,
            ResourceName = packaging.ResourceName,
            Quantity = packaging.Quantity,
            Unit = packaging.Unit,
            ContainerRole = packaging.ContainerRole,
            IsCustomerFacing = packaging.IsCustomerFacing,
        };

    private static LabelNutritionDto? MapSnapshotNutrition(MealVariantResultDto? result, decimal servingMultiplier)
    {
        return result is null
            ? null
            : MapSnapshotNutrition(
                result.NutritionPer100g,
                result.NutritionPerServing is null ? null : ScaleNutrition(result.NutritionPerServing, servingMultiplier));
    }

    private static LabelNutritionDto? MapSnapshotNutrition(
        MealVariantNutritionDto? nutritionPer100g,
        MealVariantNutritionDto? nutritionPerServing)
    {
        return nutritionPer100g is null
            ? null
            : new LabelNutritionDto
            {
                CaloriesPer100g = nutritionPer100g.Calories,
                ProteinPer100g = nutritionPer100g.Protein,
                CarbohydratesPer100g = nutritionPer100g.Carbohydrates,
                FatPer100g = nutritionPer100g.Fat,
                FiberPer100g = nutritionPer100g.Fiber,
                CaloriesPerServing = nutritionPerServing?.Calories,
                ProteinPerServing = nutritionPerServing?.Protein,
                CarbohydratesPerServing = nutritionPerServing?.Carbohydrates,
                FatPerServing = nutritionPerServing?.Fat,
                FiberPerServing = nutritionPerServing?.Fiber,
            };
    }

    private static MealVariantNutritionDto ScaleNutrition(MealVariantNutritionDto nutrition, decimal factor)
        => new()
        {
            Calories = nutrition.Calories * factor,
            Protein = nutrition.Protein * factor,
            Carbohydrates = nutrition.Carbohydrates * factor,
            Fat = nutrition.Fat * factor,
            Fiber = nutrition.Fiber * factor,
        };

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

        var mapped = await this.MapItemsAsync(items, MealVariantResultCacheMode.Fresh);

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
        => this.GetEditState(plan.PlanDate, plan.Status);

    private EditState GetEditState(DateOnly planDate, string status)
    {
        if (status == "Draft")
        {
            return new EditState(true, null);
        }

        if (status != "Published")
        {
            return new EditState(false, $"Plan w statusie {status} nie jest edytowalny.");
        }

        var nowWarsaw = GetWarsawNow();
        var cutoff = planDate.AddDays(-3).ToDateTime(TimeOnly.MinValue);
        if (nowWarsaw.DateTime < cutoff)
        {
            return new EditState(true, null);
        }

        return new EditState(false, "Edycja opublikowanego planu jest zablokowana po polnocy D-3. W kolejnym etapie obsluzy to override managera i alert M3.");
    }

    private async Task ValidateItemInputAsync(
        int dietVariantId,
        int mealId,
        int? mealVariantId,
        decimal multiplier,
        string? mealSlot)
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

        if (mealVariantId.HasValue)
        {
            var result = await this.mealManagementService.GetMealVariantResultAsync(mealId, mealVariantId.Value);
            if (result is null)
            {
                throw new InvalidOperationException("Wariant dania nie nalezy do wybranego posilku.");
            }
        }
    }

    private static int NormalizeDaysCount(int days)
    {
        return Math.Clamp(days, MinDaysCount, MaxDaysCount);
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
