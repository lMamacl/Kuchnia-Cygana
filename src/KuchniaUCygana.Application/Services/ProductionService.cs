using AutoMapper;
using System.Text.Json;
using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Production;
using KuchniaUCygana.Domain.Services;
using Microsoft.Extensions.Logging;

namespace KuchniaUCygana.Application.Services;

public sealed class ProductionService : IProductionService
{
    private readonly ProductionPlanGenerator _planGenerator;
    private readonly IProductionPlanRepository _planRepository;
    private readonly IRepository<ProductionPlanItem> _itemRepository;
    private readonly IDietDataProvider _dietProvider;
    private readonly IPackingService _packingService;
    private readonly FefoService _fefoService;
    private readonly IRepository<PlanChangeAlert> _planAlertRepository;
    private readonly IRepository<ProductionAdjustmentApproval> _adjustmentApprovalRepository;
    private readonly ICookingSessionService _cookingSessionService;
    private readonly IMapper _mapper;
    private readonly ILogger<ProductionService> _logger;

    public ProductionService(
        ProductionPlanGenerator planGenerator,
        IProductionPlanRepository planRepository,
        IRepository<ProductionPlanItem> itemRepository,
        IDietDataProvider dietProvider,
        IPackingService packingService,
        FefoService fefoService,
        IRepository<PlanChangeAlert> planAlertRepository,
        IRepository<ProductionAdjustmentApproval> adjustmentApprovalRepository,
        ICookingSessionService cookingSessionService,
        IMapper mapper,
        ILogger<ProductionService> logger)
    {
        _planGenerator = planGenerator;
        _planRepository = planRepository;
        _itemRepository = itemRepository;
        _dietProvider = dietProvider;
        _packingService = packingService;
        _fefoService = fefoService;
        _planAlertRepository = planAlertRepository;
        _adjustmentApprovalRepository = adjustmentApprovalRepository;
        _cookingSessionService = cookingSessionService;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<ProductionPlanDto> GenerateDailyPlanAsync(CreateProductionPlanRequest request)
    {
        var result = await _planGenerator.GeneratePlanAsync(request.ProductionDate, "System");

        foreach (var item in result.Items)
        {
            await _itemRepository.InsertAsync(item);
        }

        var dto = _mapper.Map<ProductionPlanDto>(result.Plan);
        dto.Items = _mapper.Map<List<ProductionPlanItemDto>>(result.Items);

        if (result.FoodCostReport != null)
        {
            dto.FoodCostReport = _mapper.Map<FoodCostReportDto>(result.FoodCostReport);
        }

        _logger.LogInformation("Wygenerowano plan produkcji na dzien {Date}", request.ProductionDate);

        return dto;
    }

    public async Task<ProductionPlanDto?> GetDailyPlanByDateAsync(DateOnly date)
    {
        var plan = await _planRepository.GetByDateAsync(date);
        if (plan == null) return null;

        var items = await _planRepository.GetPlanItemsAsync(plan.Id);

        var dto = _mapper.Map<ProductionPlanDto>(plan);
        dto.Items = _mapper.Map<List<ProductionPlanItemDto>>(items);

        return dto;
    }

    public async Task<ProductionPlanDto?> GetPlanByIdAsync(int planId)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan == null) return null;

        var items = await _planRepository.GetPlanItemsAsync(plan.Id);

        var dto = _mapper.Map<ProductionPlanDto>(plan);
        dto.Items = _mapper.Map<List<ProductionPlanItemDto>>(items);

        return dto;
    }

    public async Task<ProductionPlanDetailDto?> GetPlanDetailAsync(int planId, KitchenDashboardFilterDto filter)
    {
        var plan = await _planRepository.GetByIdAsync(planId);
        if (plan is null || plan.IsDeleted)
        {
            return null;
        }

        var normalized = NormalizeKitchenDashboardFilter(new KitchenDashboardFilterDto
        {
            Date = plan.ProductionDate,
            Search = filter.Search,
            Status = filter.Status,
            ProductionGroup = filter.ProductionGroup,
            Fefo = filter.Fefo,
            Packaging = filter.Packaging,
            Snapshot = filter.Snapshot,
            SortBy = filter.SortBy,
            SortDirection = filter.SortDirection,
            Page = filter.Page,
            PageSize = filter.PageSize,
        });

        var itemQuery = new ProductionPlanItemQuery
        {
            PlanId = plan.Id,
            Search = normalized.Search,
            Status = ParseProductionStatus(normalized.Status),
            ProductionGroup = normalized.ProductionGroup,
            FefoDeducted = ParseStateFilter(normalized.Fefo),
            PackagingDeducted = ParseStateFilter(normalized.Packaging),
            HasSnapshot = ParseSnapshotFilter(normalized.Snapshot),
            Page = normalized.Page,
            PageSize = normalized.PageSize,
            SortBy = normalized.SortBy,
            SortDescending = string.Equals(normalized.SortDirection, "desc", StringComparison.OrdinalIgnoreCase),
        };

        var (items, totalCount) = await _planRepository.SearchPlanItemsAsync(itemQuery);
        var summary = await _planRepository.GetPlanItemSummaryAsync(plan.Id);

        return new ProductionPlanDetailDto
        {
            Plan = _mapper.Map<ProductionPlanDto>(plan),
            Filter = normalized,
            Items = new PagedResultDto<ProductionPlanItemDto>
            {
                Items = _mapper.Map<List<ProductionPlanItemDto>>(items),
                Page = normalized.Page,
                PageSize = normalized.PageSize,
                TotalCount = totalCount,
            },
            Summary = MapKitchenSummary(summary),
        };
    }

    public async Task<KitchenDashboardDto> GetKitchenDashboardAsync(KitchenDashboardFilterDto filter)
    {
        var normalized = NormalizeKitchenDashboardFilter(filter);
        var dashboard = new KitchenDashboardDto
        {
            Filter = normalized,
            Items = new PagedResultDto<KitchenDashboardItemDto>
            {
                Page = normalized.Page,
                PageSize = normalized.PageSize,
                TotalCount = 0,
            },
        };

        var plan = await _planRepository.GetByDateAsync(normalized.Date);
        if (plan is null)
        {
            return dashboard;
        }

        dashboard.Plan = _mapper.Map<ProductionPlanDto>(plan);
        dashboard.Plan.Items = new List<ProductionPlanItemDto>();

        var itemQuery = new ProductionPlanItemQuery
        {
            PlanId = plan.Id,
            Search = normalized.Search,
            Status = ParseProductionStatus(normalized.Status),
            ProductionGroup = normalized.ProductionGroup,
            FefoDeducted = ParseStateFilter(normalized.Fefo),
            PackagingDeducted = ParseStateFilter(normalized.Packaging),
            HasSnapshot = ParseSnapshotFilter(normalized.Snapshot),
            Page = normalized.Page,
            PageSize = normalized.PageSize,
            SortBy = normalized.SortBy,
            SortDescending = string.Equals(normalized.SortDirection, "desc", StringComparison.OrdinalIgnoreCase),
        };

        var (items, totalCount) = await _planRepository.SearchPlanItemsAsync(itemQuery);
        var summary = await _planRepository.GetPlanItemSummaryAsync(plan.Id);

        dashboard.Items = new PagedResultDto<KitchenDashboardItemDto>
        {
            Items = items.Select(MapKitchenDashboardItem).ToList(),
            Page = normalized.Page,
            PageSize = normalized.PageSize,
            TotalCount = totalCount,
        };
        dashboard.Summary = MapKitchenSummary(summary);

        return dashboard;
    }

    public async Task<ProductionM2PlanOverviewDto> GetM2PlanOverviewAsync(ProductionM2PlanFilterDto filter)
    {
        var normalizedFilter = NormalizeProductionM2PlanFilter(filter);
        var startDate = normalizedFilter.StartDate;
        var normalizedDays = normalizedFilter.Days;
        var overview = new ProductionM2PlanOverviewDto
        {
            Filter = normalizedFilter,
            StartDate = startDate,
            TotalDays = normalizedDays,
            Page = normalizedFilter.Page,
            PageSize = normalizedFilter.PageSize,
        };

        for (var offset = 0; offset < normalizedDays; offset++)
        {
            var planDate = startDate.AddDays(offset);
            var snapshot = await _dietProvider.GetPublishedPlanSnapshotAsync(planDate);
            var productionPlan = await _planRepository.GetByDateAsync(planDate);
            var productionItems = productionPlan is null
                ? new List<ProductionPlanItem>()
                : (await _planRepository.GetPlanItemsAsync(productionPlan.Id)).ToList();

            var day = new ProductionM2PlanDayDto
            {
                PlanDate = planDate,
                DietMenuPlanId = snapshot?.DietMenuPlanId,
                PlanStatus = snapshot?.PlanStatus ?? "Missing",
                IsPublished = snapshot is not null
                    && string.Equals(snapshot.PlanStatus, "Published", StringComparison.OrdinalIgnoreCase),
                PublishedAt = snapshot?.PublishedAt,
                PublishedBy = snapshot?.PublishedBy,
                ProductionPlanId = productionPlan?.Id,
                Alerts = snapshot?.Alerts.ToList() ?? new List<PlanChangeAlertDto>(),
            };
            var refreshBlockers = GetRefreshBlockers(productionItems);

            if (snapshot is not null)
            {
                foreach (var snapshotItem in snapshot.Items
                    .OrderBy(item => item.PlanDate)
                    .ThenBy(item => item.SortOrder)
                .ThenBy(item => item.DietMenuPlanItemId))
                {
                    var productionItem = FindProductionItemForSnapshotItem(productionItems, snapshotItem);
                    var freshSnapshot = ProductionSnapshotPayloadFactory.Create(snapshotItem);
                    var itemAlerts = day.Alerts
                        .Where(alert => AlertMatchesSnapshotItem(alert, snapshotItem))
                        .ToList();
                    var missingDetails = BuildM2PlanMissingDetails(snapshotItem).ToList();
                    var hasProductionSnapshot = productionItem is not null
                        && !string.IsNullOrWhiteSpace(productionItem.M2SnapshotJson);
                    var isCurrent = hasProductionSnapshot
                        && string.Equals(productionItem!.M2SnapshotHash, freshSnapshot.Hash, StringComparison.OrdinalIgnoreCase);
                    day.Items.Add(new ProductionM2PlanItemDto
                    {
                        DietMenuPlanItemId = snapshotItem.DietMenuPlanItemId,
                        MealId = snapshotItem.MealId,
                        MealVariantId = snapshotItem.MealVariantId,
                        MealVariantName = snapshotItem.MealVariantName,
                        MealName = snapshotItem.MealName,
                        CategoryName = snapshotItem.CategoryName,
                        DietVariantId = snapshotItem.DietVariantId,
                        DietName = snapshotItem.DietName,
                        DietVariantName = snapshotItem.DietVariantName,
                        MealSlot = snapshotItem.MealSlot,
                        SortOrder = snapshotItem.SortOrder,
                        ServingMultiplier = snapshotItem.ServingMultiplier,
                        RawWeightGrams = snapshotItem.RawWeightGrams,
                        CookedWeightGrams = snapshotItem.CookedWeightGrams,
                        FinalWeightGrams = snapshotItem.FinalWeightGrams,
                        FinalWeightAfterMultiplierGrams = snapshotItem.FinalWeightAfterMultiplierGrams,
                        CompletenessStatus = snapshotItem.CompletenessStatus,
                        IsCompleteForProduction = snapshotItem.IsCompleteForProduction,
                        ComponentCount = snapshotItem.Components.Count,
                        IngredientCount = snapshotItem.AggregateIngredients.Count > 0
                            ? snapshotItem.AggregateIngredients.Count
                            : snapshotItem.Components.Sum(component => component.Ingredients.Count),
                        PackagingRequirementCount = snapshotItem.PackagingRequirements.Count
                            + snapshotItem.Components.Sum(component => component.PackagingRequirements.Count),
                        ValidationWarningCount = snapshotItem.ValidationWarnings.Count,
                        ComponentNames = snapshotItem.Components
                            .OrderBy(component => component.SortOrder)
                            .Select(component => $"{component.ComponentName} v{component.VersionNumber}")
                            .Distinct()
                            .ToList(),
                        IngredientNames = BuildM2PlanIngredientNames(snapshotItem).ToList(),
                        PackagingNames = BuildM2PlanPackagingNames(snapshotItem).ToList(),
                        ValidationWarnings = snapshotItem.ValidationWarnings.ToList(),
                        MissingDetails = missingDetails,
                        Alerts = itemAlerts,
                        HasProductionSnapshot = hasProductionSnapshot,
                        ProductionPlanItemId = productionItem?.Id,
                        PlannedQuantity = productionItem?.PlannedQuantity,
                        ProductionStatus = productionItem?.Status.ToString(),
                        SnapshotHash = productionItem?.M2SnapshotHash,
                        FreshSnapshotHash = freshSnapshot.Hash,
                        IsProductionSnapshotCurrent = isCurrent,
                        NeedsRefresh = !isCurrent,
                        CanRefresh = refreshBlockers.Count == 0,
                        RefreshBlockers = refreshBlockers.ToList(),
                    });
                }
            }

            day.TotalItems = day.Items.Count;
            day.SnapshotItemCount = day.Items.Count(item => item.HasProductionSnapshot);
            day.MissingSnapshotItemCount = day.TotalItems - day.SnapshotItemCount;
            day.AlertCount = day.Alerts.Count;
            day.UnacknowledgedAlertCount = day.Alerts.Count(alert =>
                alert.RequiresAcknowledgement && !alert.AcknowledgedAt.HasValue);

            overview.Days.Add(day);
        }

        overview.PublishedDays = overview.Days.Count(day => day.IsPublished);
        overview.MissingPublishedDays = overview.TotalDays - overview.PublishedDays;
        overview.TotalPlanItems = overview.Days.Sum(day => day.TotalItems);
        overview.SnapshotItemCount = overview.Days.Sum(day => day.SnapshotItemCount);
        overview.MissingSnapshotItemCount = overview.Days.Sum(day => day.MissingSnapshotItemCount);
        overview.AlertCount = overview.Days.Sum(day => day.AlertCount);
        overview.UnacknowledgedAlertCount = overview.Days.Sum(day => day.UnacknowledgedAlertCount);

        return ApplyM2PlanFilters(overview, normalizedFilter);
    }

    public async Task<M2PlanOverviewDto> GetM2PlanOverviewAsync(DateOnly startDate, int days)
        => ToLegacyM2PlanOverview(await GetM2PlanOverviewAsync(new ProductionM2PlanFilterDto
        {
            StartDate = startDate,
            Days = days,
        }));

    public async Task<ProductionPlanRefreshResultDto> RefreshProductionPlanFromM2Async(
        DateOnly date,
        string requestedBy)
    {
        var actor = string.IsNullOrWhiteSpace(requestedBy) ? "Admin" : requestedBy.Trim();
        var existingPlan = await _planRepository.GetByDateAsync(date);

        if (existingPlan is null)
        {
            var generated = await _planGenerator.GeneratePlanAsync(date, actor);
            foreach (var item in generated.Items)
            {
                await _itemRepository.InsertAsync(item);
            }

            return new ProductionPlanRefreshResultDto
            {
                ProductionDate = date,
                ProductionPlanId = generated.Plan.Id,
                Status = "Created",
                ItemCount = generated.Items.Count,
            };
        }

        var existingItems = (await _planRepository.GetPlanItemsAsync(existingPlan.Id)).ToList();
        var blockers = GetRefreshBlockers(existingItems);
        if (blockers.Count > 0)
        {
            return new ProductionPlanRefreshResultDto
            {
                ProductionDate = date,
                ProductionPlanId = existingPlan.Id,
                Status = "Blocked",
                ItemCount = existingItems.Count,
                Blockers = blockers,
            };
        }

        var generatedForExisting = await _planGenerator.GeneratePlanItemsForExistingPlanAsync(date, existingPlan.Id);
        if (IsSameProductionSnapshotSet(existingItems, generatedForExisting.Items))
        {
            return new ProductionPlanRefreshResultDto
            {
                ProductionDate = date,
                ProductionPlanId = existingPlan.Id,
                Status = "Skipped",
                ItemCount = existingItems.Count,
            };
        }

        await _planRepository.ReplacePlanItemsAsync(existingPlan.Id, generatedForExisting.Items, actor);

        return new ProductionPlanRefreshResultDto
        {
            ProductionDate = date,
            ProductionPlanId = existingPlan.Id,
            Status = "Refreshed",
            ItemCount = generatedForExisting.Items.Count,
        };
    }

    public async Task<ProductionPlanRefreshRangeResultDto> RefreshProductionPlansFromM2Async(
        DateOnly startDate,
        int days,
        string requestedBy)
    {
        var normalizedDays = Math.Clamp(days <= 0 ? 7 : days, 1, 31);
        var result = new ProductionPlanRefreshRangeResultDto
        {
            StartDate = startDate,
            Days = normalizedDays,
        };

        for (var offset = 0; offset < normalizedDays; offset++)
        {
            var dayResult = await RefreshProductionPlanFromM2Async(startDate.AddDays(offset), requestedBy);
            result.Results.Add(dayResult);
        }

        result.CreatedCount = result.Results.Count(item => item.Status == "Created");
        result.RefreshedCount = result.Results.Count(item => item.Status == "Refreshed");
        result.SkippedCount = result.Results.Count(item => item.Status == "Skipped");
        result.BlockedCount = result.Results.Count(item => item.Status == "Blocked");
        return result;
    }

    public async Task AcknowledgePlanAlertAsync(int alertId, string acknowledgedBy)
    {
        var alert = await _planAlertRepository.GetByIdAsync(alertId)
            ?? throw new InvalidOperationException($"Alert planu M2 {alertId} nie istnieje.");

        if (alert.AcknowledgedAt.HasValue)
        {
            return;
        }

        alert.AcknowledgedAt = DateTimeOffset.UtcNow;
        alert.AcknowledgedBy = string.IsNullOrWhiteSpace(acknowledgedBy)
            ? "Kitchen"
            : acknowledgedBy.Trim();

        await _planAlertRepository.UpdateAsync(alert);
    }

    public async Task<IReadOnlyList<ProductionAdjustmentApprovalDto>> GetProductionAdjustmentApprovalsAsync(int planItemId)
    {
        var approvals = (await _adjustmentApprovalRepository.GetAllAsync())
            .Where(approval => approval.ProductionPlanItemId == planItemId)
            .OrderByDescending(approval => approval.RequestedAt)
            .ThenByDescending(approval => approval.Id)
            .Select(MapAdjustmentApproval)
            .ToList();

        return approvals;
    }

    public async Task<ProductionAdjustmentApprovalDto> RequestProductionAdjustmentApprovalAsync(
        ProductionAdjustmentApprovalRequestDto request)
    {
        var item = await _itemRepository.GetByIdAsync(request.ProductionPlanItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {request.ProductionPlanItemId} nie istnieje.");
        var adjustmentType = NormalizeAdjustmentType(request.AdjustmentType);
        var reason = NormalizeRequiredText(request.Reason, "Powód korekty jest wymagany.");
        var requestedBy = NormalizeActor(request.RequestedBy);
        var plannedValue = GetPlannedAdjustmentValue(item, adjustmentType);

        if (requestedBy.Length == 0)
        {
            requestedBy = "Kuchnia";
        }

        if (request.RequestedValue < 0)
        {
            throw new InvalidOperationException("Wartość korekty nie może być ujemna.");
        }

        if (request.RequestedValue == plannedValue)
        {
            throw new InvalidOperationException("Korekta nie jest wymagana, bo wartość faktyczna jest zgodna z planem.");
        }

        var approval = new ProductionAdjustmentApproval
        {
            ProductionPlanItemId = item.Id,
            AdjustmentType = adjustmentType,
            Status = "Pending",
            PlannedValue = plannedValue,
            RequestedValue = request.RequestedValue,
            Unit = GetAdjustmentUnit(adjustmentType),
            Reason = reason,
            RequestedBy = requestedBy,
            RequestedAt = DateTimeOffset.UtcNow,
        };

        approval.Id = await _adjustmentApprovalRepository.InsertAsync(approval);
        return MapAdjustmentApproval(approval);
    }

    public async Task<ProductionAdjustmentApprovalDto> ApproveProductionAdjustmentAsync(
        ProductionAdjustmentApprovalDecisionDto decision)
    {
        var approval = await _adjustmentApprovalRepository.GetByIdAsync(decision.ApprovalId)
            ?? throw new InvalidOperationException($"Korekta produkcyjna #{decision.ApprovalId} nie istnieje.");

        if (!string.Equals(approval.Status, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Tylko korekta oczekująca może zostać zaakceptowana.");
        }

        approval.Status = "Approved";
        approval.ApprovedAt = DateTimeOffset.UtcNow;
        approval.ApprovedBy = NormalizeActor(decision.ApprovedBy);
        approval.ApprovalNote = NormalizeOptionalText(decision.ApprovalNote);

        await _adjustmentApprovalRepository.UpdateAsync(approval);
        return MapAdjustmentApproval(approval);
    }

    public async Task<CookingCardDto> GetCookingCardAsync(int planItemId)
    {
        var item = await _itemRepository.GetByIdAsync(planItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {planItemId} nie istnieje.");

        var storedSnapshot = TryDeserializeSnapshotItem(item);
        if (storedSnapshot is not null)
        {
            var snapshotCard = BuildCookingCardFromSnapshot(item, storedSnapshot, null);
            await EnrichComponentSessionProgressAsync(snapshotCard);
            snapshotCard.AdjustmentApprovals = (await GetProductionAdjustmentApprovalsAsync(planItemId)).ToList();
            return snapshotCard;
        }

        var details = await _dietProvider.GetMealCookingDetailsAsync(item.MealId);
        var recipe = (await _dietProvider.GetRecipeForMealAsync(item.MealId)).ToList();

        var card = new CookingCardDto
        {
            PlanItemId = item.Id,
            MealId = item.MealId,
            MealName = details?.MealName ?? item.MealName,
            CategoryName = details?.CategoryName,
            Description = details?.Description,
            PreparationInstructions = details?.PreparationInstructions,
            MainImageUrl = details?.MainImageUrl,
            PreparationTimeMinutes = details?.PreparationTimeMinutes ?? 0,
            DietVariantId = item.DietVariantId,
            PlannedQuantity = item.PlannedQuantity,
            ProductionGroup = item.ProductionGroup,
            EstimatedReadyTime = item.EstimatedReadyTime.HasValue
                ? item.EstimatedReadyTime.Value.ToString("HH:mm")
                : null,
            Status = item.Status.ToString(),
            FefoDeductedAt = item.FefoDeductedAt,
            PackagingDeductedAt = item.PackagingDeductedAt,
            HasM2Snapshot = !string.IsNullOrWhiteSpace(item.M2SnapshotJson),
            RawWeightGrams = details?.RawWeightGrams,
            CookedWeightGrams = details?.CookedWeightGrams,
            RequiresCoreTemperatureCheck = details?.RequiresCoreTemperatureCheck == true
                || recipe.Any(r => r.RequiresCoreTemperatureCheck),
            MinimumCoreTemperatureCelsius = details?.MinimumCoreTemperatureCelsius
                ?? recipe.Where(r => r.MinimumCoreTemperatureCelsius.HasValue)
                    .Select(r => r.MinimumCoreTemperatureCelsius)
                    .DefaultIfEmpty()
                    .Max(),
            NutritionFacts = details?.NutritionFacts is null
                ? null
                : new CookingCardNutritionDto
                {
                    CaloriesPer100g = details.NutritionFacts.CaloriesPer100g,
                    ProteinPer100g = details.NutritionFacts.ProteinPer100g,
                    CarbohydratesPer100g = details.NutritionFacts.CarbohydratesPer100g,
                    FatPer100g = details.NutritionFacts.FatPer100g,
                    FiberPer100g = details.NutritionFacts.FiberPer100g,
                },
            Allergens = details?.Allergens ?? Array.Empty<string>(),
            MissingWarehouseMappings = recipe
                .Where(r => !r.StockItemId.HasValue)
                .Select(r => $"{r.IngredientName} (ID {r.IngredientId})")
                .ToList(),
        };

        foreach (var ingredient in recipe)
        {
            card.Ingredients.Add(new CookingCardIngredientDto
            {
                IngredientId = ingredient.IngredientId,
                StockItemId = ingredient.StockItemId,
                IngredientName = ingredient.IngredientName,
                WarehouseCategoryName = ingredient.WarehouseCategoryName,
                WeightPerServing = ingredient.WeightInGrams,
                TotalWeight = ingredient.WeightInGrams * item.PlannedQuantity,
                YieldFactor = ingredient.YieldFactor,
                RequiresCoreTemperatureCheck = ingredient.RequiresCoreTemperatureCheck,
                MinimumCoreTemperatureCelsius = ingredient.MinimumCoreTemperatureCelsius,
                IsOptional = ingredient.IsOptional,
            });
        }

        card.AdjustmentApprovals = (await GetProductionAdjustmentApprovalsAsync(planItemId)).ToList();
        return card;
    }

    public async Task<CookingComponentCardDto> GetCookingComponentCardAsync(
        int planItemId,
        int recipeComponentVersionId)
    {
        var item = await _itemRepository.GetByIdAsync(planItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {planItemId} nie istnieje.");
        var snapshotItem = TryDeserializeSnapshotItem(item)
            ?? throw new InvalidOperationException("Brak snapshotu M2 dla pozycji planu produkcji.");
        var component = snapshotItem.Components
            .FirstOrDefault(c => c.RecipeComponentVersionId == recipeComponentVersionId)
            ?? throw new InvalidOperationException(
                $"Snapshot M2 nie zawiera skladowej RecipeComponentVersionId {recipeComponentVersionId}.");
        var plan = await _planRepository.GetByIdAsync(item.ProductionPlanId);

        var card = new CookingComponentCardDto
        {
            PlanItemId = item.Id,
            ProductionPlanId = item.ProductionPlanId,
            ProductionDate = plan?.ProductionDate ?? snapshotItem.PlanDate,
            MealId = item.MealId,
            MealName = snapshotItem.MealName,
            MealVariantId = snapshotItem.MealVariantId,
            MealVariantName = snapshotItem.MealVariantName,
            DietVariantId = item.DietVariantId,
            PlannedQuantity = item.PlannedQuantity,
            ProductionStatus = item.Status.ToString(),
            FefoDeductedAt = item.FefoDeductedAt,
            PackagingDeductedAt = item.PackagingDeductedAt,
            RecipeComponentId = component.RecipeComponentId,
            RecipeComponentVersionId = component.RecipeComponentVersionId,
            ComponentName = component.ComponentName,
            VersionNumber = component.VersionNumber,
            VersionStatus = component.VersionStatus,
            Role = component.Role,
            QuantityPerServing = component.QuantityPerServing * snapshotItem.ServingMultiplier,
            TotalQuantity = component.QuantityPerServing * snapshotItem.ServingMultiplier * item.PlannedQuantity,
            Unit = component.Unit,
            RawWeightGrams = component.RawWeightGrams,
            CookedWeightGrams = component.CookedWeightGrams,
            RequiresCoreTemperatureCheck = component.Ingredients.Any(i => i.RequiresCoreTemperatureCheck),
            MinimumCoreTemperatureCelsius = MaxTemperature(component.Ingredients),
            Allergens = component.Allergens,
            NutritionFacts = component.Nutrition is null
                ? null
                : new CookingCardNutritionDto
                {
                    CaloriesPer100g = component.Nutrition.CaloriesPer100g,
                    ProteinPer100g = component.Nutrition.ProteinPer100g,
                    CarbohydratesPer100g = component.Nutrition.CarbohydratesPer100g,
                    FatPer100g = component.Nutrition.FatPer100g,
                    FiberPer100g = component.Nutrition.FiberPer100g,
                },
            InstructionSections = component.InstructionSections.Select(MapInstructionSection).ToList(),
        };

        foreach (var ingredient in component.Ingredients)
        {
            var weightPerServing = ingredient.WeightInGrams * component.QuantityPerServing * snapshotItem.ServingMultiplier;
            card.Ingredients.Add(new CookingComponentIngredientDto
            {
                IngredientId = ingredient.IngredientId,
                StockItemId = ingredient.StockItemId,
                IngredientName = ingredient.IngredientName,
                WarehouseCategoryName = ingredient.WarehouseCategoryName,
                WeightPerServing = weightPerServing,
                TotalWeight = weightPerServing * item.PlannedQuantity,
                YieldFactor = ingredient.YieldFactor,
                RequiresCoreTemperatureCheck = ingredient.RequiresCoreTemperatureCheck,
                MinimumCoreTemperatureCelsius = ingredient.MinimumCoreTemperatureCelsius,
                IsOptional = ingredient.IsOptional,
            });
        }

        foreach (var packaging in component.PackagingRequirements)
        {
            var quantityPerServing = packaging.Quantity * component.QuantityPerServing * snapshotItem.ServingMultiplier;
            card.PackagingRequirements.Add(MapPackaging(packaging, quantityPerServing, item.PlannedQuantity));
        }

        return card;
    }

    public async Task ApproveCookingAsync(int planItemId, decimal actualQuantity)
    {
        var item = await _itemRepository.GetByIdAsync(planItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {planItemId} nie istnieje.");
        var approvedAdjustment = await RequireApprovedAdjustmentIfNeededAsync(item, actualQuantity, "CookedQuantity");

        await DeductPackagingIfNeededAsync(item, actualQuantity);

        item.CookedQuantity = (int)actualQuantity;
        item.Status = ProductionItemStatus.Cooked;
        item.ActualReadyTime = TimeOnly.FromDateTime(DateTime.Now);

        await _itemRepository.UpdateAsync(item);
        if (approvedAdjustment is not null)
        {
            approvedAdjustment.Status = "Applied";
            approvedAdjustment.AppliedAt = DateTimeOffset.UtcNow;
            await _adjustmentApprovalRepository.UpdateAsync(approvedAdjustment);
        }

        var plan = await _planRepository.GetByIdAsync(item.ProductionPlanId);
        if (plan is not null)
        {
            var sessions = (await _packingService.GetSessionsByDateAsync(plan.ProductionDate)).ToList();
            foreach (var session in sessions.Where(s => s.Items.Count == 0 && s.OrderId.HasValue))
            {
                await _packingService.PrepareOrderBoxesAsync(session.Id);
            }
        }

        if (item.CookedQuantity < item.PlannedQuantity * 0.9m)
        {
            _logger.LogWarning(
                "ALERT PRODUKCYJNY: Ugotowano zbyt malo porcji. Danie {Meal}, Plan: {Plan}, Realizacja: {Actual}",
                item.MealName,
                item.PlannedQuantity,
                item.CookedQuantity);
        }

        _logger.LogInformation(
            "Zatwierdzono produkcje pozycji {PlanItemId}: wyprodukowano {ActualQuantity} porcji",
            planItemId,
            actualQuantity);
    }

    public async Task ProduceSemiFinishedAsync(int planId)
    {
        var plan = await _planRepository.GetByIdAsync(planId)
            ?? throw new InvalidOperationException($"Plan produkcji {planId} nie istnieje.");

        var planItems = (await _planRepository.GetPlanItemsAsync(planId)).ToList();
        var pendingItems = planItems.Where(item => !item.FefoDeductedAt.HasValue).ToList();

        if (pendingItems.Count == 0)
        {
            _logger.LogInformation("FEFO dla planu {PlanId} bylo juz wykonane - pominieto ponowne zdejmowanie.", planId);
        }
        else
        {
            var snapshot = BuildStoredSnapshot(plan.ProductionDate, pendingItems);
            if (snapshot is null)
            {
                snapshot = await _dietProvider.GetPublishedPlanSnapshotAsync(plan.ProductionDate);
            }

            if (snapshot is not null)
            {
                await ProduceFromSnapshotAsync(planId, pendingItems, snapshot);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Nie mozna wykonac FEFO dla planu produkcji {planId}. " +
                    $"Brak opublikowanego snapshotu M2 dla dnia {plan.ProductionDate:yyyy-MM-dd}. " +
                    "Wygeneruj plan z opublikowanego M2; legacy Recipes nie sa dopuszczone w normalnym trybie produkcji.");
            }
        }

        plan.Status = ProductionPlanStatus.InProgress;
        await _planRepository.UpdateAsync(plan);

        _logger.LogInformation("Utworzono polprodukty dla planu {PlanId} i zaktualizowano magazyn", planId);
    }

    private async Task ProduceFromLegacyRecipesAsync(int planId, List<ProductionPlanItem> pendingItems)
    {
        var recipesByItem = new Dictionary<int, List<RecipeIngredientEntry>>();
        var requiredByStockItem = new Dictionary<int, decimal>();
        var missingMappings = new List<string>();

        foreach (var item in pendingItems)
        {
            var recipe = (await _dietProvider.GetRecipeForMealAsync(item.MealId)).ToList();
            if (recipe.Count == 0)
            {
                throw new InvalidOperationException($"Brak receptury M2 dla posilku {item.MealName} (ID {item.MealId}).");
            }

            recipesByItem[item.Id] = recipe;

            foreach (var ingredient in recipe)
            {
                if (!ingredient.StockItemId.HasValue)
                {
                    missingMappings.Add($"{ingredient.IngredientName} (IngredientId {ingredient.IngredientId})");
                    continue;
                }

                var totalQuantity = ingredient.WeightInGrams * item.PlannedQuantity;
                requiredByStockItem[ingredient.StockItemId.Value] =
                    requiredByStockItem.GetValueOrDefault(ingredient.StockItemId.Value) + totalQuantity;
            }
        }

        if (missingMappings.Count > 0)
        {
            throw new InvalidOperationException(
                "Nie mozna wykonac FEFO. Brak mapowania skladnikow M2 do magazynu: " +
                string.Join(", ", missingMappings.Distinct()));
        }

        var shortages = new List<string>();
        foreach (var (stockItemId, requiredQuantity) in requiredByStockItem)
        {
            var available = await _fefoService.GetAvailableQuantityAsync(stockItemId);
            if (available < requiredQuantity)
            {
                shortages.Add($"StockItemId {stockItemId}: potrzeba {requiredQuantity:0.##}, dostepne {available:0.##}");
            }
        }

        if (shortages.Count > 0)
        {
            throw new InvalidOperationException(
                "Nie mozna wykonac FEFO. Braki magazynowe: " + string.Join("; ", shortages));
        }

        foreach (var item in pendingItems)
        {
            var referenceDocument = $"PLAN-{planId}-ITEM-{item.Id}";

            foreach (var ingredient in recipesByItem[item.Id])
            {
                var totalQuantity = ingredient.WeightInGrams * item.PlannedQuantity;

                await _fefoService.DeductByFefoAsync(
                    ingredient.StockItemId!.Value,
                    totalQuantity,
                    $"Produkcja polproduktow plan {planId}, posilek {item.MealId}",
                    referenceDocument);
            }

            await MarkItemFefoDeductedAsync(item, referenceDocument);
        }
    }

    private async Task ProduceFromSnapshotAsync(
        int planId,
        List<ProductionPlanItem> pendingItems,
        PublishedDietPlanSnapshotDto snapshot)
    {
        var requirementsByItem = new Dictionary<int, List<SnapshotIngredientRequirement>>();
        var missingMappings = new List<string>();

        foreach (var item in pendingItems)
        {
            var snapshotItem = FindSnapshotItemForProductionItem(snapshot, item);
            if (snapshotItem is null)
            {
                throw new InvalidOperationException(
                    $"Brak pozycji snapshotu M2 dla posilku {item.MealName} (MealId {item.MealId}, DietVariantId {item.DietVariantId}).");
            }

            var requirements = new List<SnapshotIngredientRequirement>();
            if (snapshotItem.AggregateIngredients.Count > 0)
            {
                foreach (var ingredient in snapshotItem.AggregateIngredients)
                {
                    if (!ingredient.StockItemId.HasValue && !ingredient.WarehouseCategoryId.HasValue)
                    {
                        missingMappings.Add($"{ingredient.IngredientName} (IngredientId {ingredient.IngredientId})");
                        continue;
                    }

                    requirements.Add(new SnapshotIngredientRequirement(
                        ingredient.SourceRecipeComponentVersionIds.Count > 0
                            ? ingredient.SourceRecipeComponentVersionIds[0]
                            : null,
                        ingredient.SourceComponentNames.FirstOrDefault() ?? "Aggregate",
                        ingredient.IngredientId,
                        ingredient.IngredientName,
                        ingredient.StockItemId,
                        ingredient.WarehouseCategoryId,
                        ingredient.NetWeightInGrams
                            * snapshotItem.ServingMultiplier
                            * item.PlannedQuantity));
                }
            }
            else
            {
                foreach (var component in snapshotItem.Components)
                {
                    foreach (var ingredient in component.Ingredients)
                    {
                        if (!ingredient.StockItemId.HasValue && !ingredient.WarehouseCategoryId.HasValue)
                        {
                            missingMappings.Add($"{ingredient.IngredientName} (IngredientId {ingredient.IngredientId})");
                            continue;
                        }

                        requirements.Add(new SnapshotIngredientRequirement(
                            component.RecipeComponentVersionId,
                            component.ComponentName,
                            ingredient.IngredientId,
                            ingredient.IngredientName,
                            ingredient.StockItemId,
                            ingredient.WarehouseCategoryId,
                            ingredient.WeightInGrams
                                * component.QuantityPerServing
                                * snapshotItem.ServingMultiplier
                                * item.PlannedQuantity));
                    }
                }
            }

            requirementsByItem[item.Id] = requirements;
        }

        if (missingMappings.Count > 0)
        {
            throw new InvalidOperationException(
                "Nie mozna wykonac FEFO. Brak mapowania skladnikow M2 do magazynu: " +
                string.Join(", ", missingMappings.Distinct()));
        }

        var shortages = new List<string>();
        foreach (var requirement in requirementsByItem.Values.SelectMany(r => r).GroupBy(CreateRequirementKey))
        {
            var first = requirement.First();
            var requiredQuantity = requirement.Sum(r => r.RequiredQuantity);
            var available = first.StockItemId.HasValue
                ? await _fefoService.GetAvailableQuantityAsync(first.StockItemId.Value)
                : await _fefoService.GetAvailableQuantityByCategoryAsync(first.WarehouseCategoryId!.Value);

            if (available < requiredQuantity)
            {
                var key = first.StockItemId.HasValue
                    ? $"StockItemId {first.StockItemId.Value}"
                    : $"WarehouseCategoryId {first.WarehouseCategoryId!.Value}";
                shortages.Add($"{key}: potrzeba {requiredQuantity:0.##}, dostepne {available:0.##}");
            }
        }

        if (shortages.Count > 0)
        {
            throw new InvalidOperationException(
                "Nie mozna wykonac FEFO. Braki magazynowe: " + string.Join("; ", shortages));
        }

        foreach (var item in pendingItems)
        {
            foreach (var requirementGroup in requirementsByItem[item.Id].GroupBy(CreateRequirementKey))
            {
                var requirement = requirementGroup.First();
                var requiredQuantity = requirementGroup.Sum(r => r.RequiredQuantity);
                var referenceDocument = CreateFefoReferenceDocument(planId, item.Id, requirement);
                var reason = requirementGroup.Count() == 1
                    ? $"Produkcja skladowej {requirement.ComponentName} plan {planId}, posilek {item.MealId}"
                    : $"Produkcja skladnikow plan {planId}, posilek {item.MealId}";

                if (requirement.StockItemId.HasValue)
                {
                    await _fefoService.DeductByFefoAsync(
                        requirement.StockItemId.Value,
                        requiredQuantity,
                        reason,
                        referenceDocument);
                }
                else
                {
                    await _fefoService.DeductByFefoCategoryAsync(
                        requirement.WarehouseCategoryId!.Value,
                        requiredQuantity,
                        reason,
                        referenceDocument);
                }
            }

            await MarkItemFefoDeductedAsync(item, $"P{planId}-I{item.Id}-FEFO");
        }
    }

    private async Task DeductPackagingIfNeededAsync(ProductionPlanItem item, decimal actualQuantity)
    {
        if (item.PackagingDeductedAt.HasValue)
        {
            _logger.LogInformation(
                "Opakowania dla pozycji {PlanItemId} byly juz zdjete - pominieto ponowne zdejmowanie.",
                item.Id);
            return;
        }

        var snapshotItem = TryDeserializeSnapshotItem(item);
        if (snapshotItem is null)
        {
            throw new InvalidOperationException(
                $"Nie mozna zatwierdzic gotowania pozycji {item.Id} ({item.MealName}). " +
                "Brak snapshotu M2 z wymaganiami opakowan. Wygeneruj plan z opublikowanego M2 przed zatwierdzeniem produkcji.");
        }

        var requirements = BuildPackagingRequirements(snapshotItem, actualQuantity).ToList();
        if (requirements.Count == 0)
        {
            throw new InvalidOperationException(
                $"Nie mozna zatwierdzic gotowania. Snapshot M2 dla posilku {item.MealName} nie zawiera wymaganych opakowan.");
        }

        var shortages = new List<string>();
        foreach (var group in requirements.GroupBy(CreatePackagingRequirementKey))
        {
            var first = group.First();
            var requiredQuantity = group.Sum(r => r.RequiredQuantity);
            if (requiredQuantity <= 0)
            {
                continue;
            }

            var available = first.StockItemId.HasValue
                ? await _fefoService.GetAvailableQuantityAsync(first.StockItemId.Value)
                : await _fefoService.GetAvailableQuantityByCategoryAsync(first.WarehouseCategoryId!.Value);

            if (available < requiredQuantity)
            {
                var key = first.StockItemId.HasValue
                    ? $"StockItemId {first.StockItemId.Value}"
                    : $"WarehouseCategoryId {first.WarehouseCategoryId!.Value}";
                shortages.Add($"{first.ResourceName} ({key}): potrzeba {requiredQuantity:0.##}, dostepne {available:0.##}");
            }
        }

        if (shortages.Count > 0)
        {
            throw new InvalidOperationException(
                "Nie mozna zatwierdzic gotowania. Braki opakowan: " + string.Join("; ", shortages));
        }

        var groupedRequirements = requirements
            .Where(r => r.RequiredQuantity > 0)
            .GroupBy(CreatePackagingRequirementKey)
            .Select(group => new
            {
                Requirement = group.First(),
                RequiredQuantity = group.Sum(r => r.RequiredQuantity),
            })
            .ToList();

        foreach (var group in groupedRequirements)
        {
            var requirement = group.Requirement;
            var referenceDocument = CreatePackagingReferenceDocument(item, requirement);
            var reason = $"Opakowania po gotowaniu plan {item.ProductionPlanId}, posilek {item.MealId}";
            if (requirement.StockItemId.HasValue)
            {
                await _fefoService.DeductByFefoAsync(
                    requirement.StockItemId.Value,
                    group.RequiredQuantity,
                    reason,
                    referenceDocument);
            }
            else
            {
                await _fefoService.DeductByFefoCategoryAsync(
                    requirement.WarehouseCategoryId!.Value,
                    group.RequiredQuantity,
                    reason,
                    referenceDocument);
            }
        }

        item.PackagingDeductedAt = DateTimeOffset.UtcNow;
        item.PackagingReferenceDocument = $"P{item.ProductionPlanId}-I{item.Id}-PACK";
    }

    private static List<string> GetRefreshBlockers(IEnumerable<ProductionPlanItem> items)
    {
        var blockers = new List<string>();
        foreach (var item in items)
        {
            if (item.Status != ProductionItemStatus.Planned)
            {
                blockers.Add($"Pozycja #{item.Id} ma status {item.Status}.");
            }

            if (item.FefoDeductedAt.HasValue)
            {
                blockers.Add($"Pozycja #{item.Id} ma wykonane FEFO.");
            }

            if (item.PackagingDeductedAt.HasValue)
            {
                blockers.Add($"Pozycja #{item.Id} ma zdjete opakowania.");
            }

            if (item.CookedQuantity > 0)
            {
                blockers.Add($"Pozycja #{item.Id} ma ugotowana ilosc {item.CookedQuantity}.");
            }
        }

        return blockers.Distinct().ToList();
    }

    private static IEnumerable<string> BuildM2PlanIngredientNames(PublishedDietPlanItemDto snapshotItem)
    {
        var ingredients = snapshotItem.AggregateIngredients.Count > 0
            ? snapshotItem.AggregateIngredients.Select(FormatIngredient)
            : snapshotItem.Components
                .SelectMany(component => component.Ingredients)
                .Select(ingredient => ingredient.IngredientName);

        return ingredients
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value);
    }

    private static IEnumerable<string> BuildM2PlanPackagingNames(PublishedDietPlanItemDto snapshotItem)
    {
        return snapshotItem.PackagingRequirements
            .Concat(snapshotItem.Components.SelectMany(component => component.PackagingRequirements))
            .Select(FormatPackaging)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value);
    }

    private static IEnumerable<string> BuildM2PlanMissingDetails(PublishedDietPlanItemDto snapshotItem)
    {
        foreach (var warning in snapshotItem.ValidationWarnings)
        {
            yield return warning;
        }

        foreach (var warning in snapshotItem.Components.SelectMany(component => component.ValidationWarnings))
        {
            yield return warning;
        }

        if (snapshotItem.Components.Count == 0)
        {
            yield return "Brak skladowych receptury.";
        }

        if (snapshotItem.PackagingRequirements.Count == 0
            && snapshotItem.Components.All(component => component.PackagingRequirements.Count == 0))
        {
            yield return "Brak opakowan.";
        }

        if (!snapshotItem.FinalWeightAfterMultiplierGrams.HasValue
            && !snapshotItem.FinalWeightGrams.HasValue)
        {
            yield return "Brak gramatury.";
        }
    }

    private static bool AlertMatchesSnapshotItem(PlanChangeAlertDto alert, PublishedDietPlanItemDto snapshotItem)
    {
        if (alert.DietMenuPlanItemId.HasValue)
        {
            return alert.DietMenuPlanItemId.Value == snapshotItem.DietMenuPlanItemId;
        }

        if (alert.MealId.HasValue)
        {
            return alert.MealId.Value == snapshotItem.MealId;
        }

        return false;
    }

    private static string FormatIngredient(AggregateIngredientDto ingredient)
    {
        var name = ingredient.IngredientName;
        if (ingredient.NetWeightInGrams <= 0)
        {
            return name;
        }

        return $"{name} {ingredient.NetWeightInGrams:0.##} g";
    }

    private static string FormatPackaging(PackagingRequirementDto packaging)
    {
        var name = packaging.ResourceName;
        if (packaging.Quantity <= 0)
        {
            return name;
        }

        return $"{name} x {packaging.Quantity:0.##} {packaging.Unit}".Trim();
    }

    private static M2PlanOverviewDto ToLegacyM2PlanOverview(ProductionM2PlanOverviewDto source)
        => new()
        {
            StartDate = source.StartDate,
            TotalDays = source.TotalDays,
            PublishedDays = source.PublishedDays,
            MissingPublishedDays = source.MissingPublishedDays,
            TotalPlanItems = source.TotalPlanItems,
            SnapshotItemCount = source.SnapshotItemCount,
            MissingSnapshotItemCount = source.MissingSnapshotItemCount,
            AlertCount = source.AlertCount,
            UnacknowledgedAlertCount = source.UnacknowledgedAlertCount,
            Days = source.Days.Select(ToLegacyM2PlanDay).ToList(),
        };

    private static M2PlanOverviewDayDto ToLegacyM2PlanDay(ProductionM2PlanDayDto source)
        => new()
        {
            PlanDate = source.PlanDate,
            DietMenuPlanId = source.DietMenuPlanId,
            PlanStatus = source.PlanStatus,
            IsPublished = source.IsPublished,
            PublishedAt = source.PublishedAt,
            PublishedBy = source.PublishedBy,
            ProductionPlanId = source.ProductionPlanId,
            TotalItems = source.TotalItems,
            SnapshotItemCount = source.SnapshotItemCount,
            MissingSnapshotItemCount = source.MissingSnapshotItemCount,
            AlertCount = source.AlertCount,
            UnacknowledgedAlertCount = source.UnacknowledgedAlertCount,
            Alerts = source.Alerts.ToList(),
            Items = source.Items.Select(ToLegacyM2PlanItem).ToList(),
        };

    private static M2PlanOverviewItemDto ToLegacyM2PlanItem(ProductionM2PlanItemDto source)
        => new()
        {
            DietMenuPlanItemId = source.DietMenuPlanItemId,
            MealId = source.MealId,
            MealVariantId = source.MealVariantId,
            MealVariantName = source.MealVariantName,
            MealName = source.MealName,
            CategoryName = source.CategoryName,
            DietVariantId = source.DietVariantId,
            DietName = source.DietName,
            DietVariantName = source.DietVariantName,
            MealSlot = source.MealSlot,
            SortOrder = source.SortOrder,
            ServingMultiplier = source.ServingMultiplier,
            RawWeightGrams = source.RawWeightGrams,
            CookedWeightGrams = source.CookedWeightGrams,
            FinalWeightGrams = source.FinalWeightGrams,
            FinalWeightAfterMultiplierGrams = source.FinalWeightAfterMultiplierGrams,
            CompletenessStatus = source.CompletenessStatus,
            IsCompleteForProduction = source.IsCompleteForProduction,
            ComponentCount = source.ComponentCount,
            IngredientCount = source.IngredientCount,
            PackagingRequirementCount = source.PackagingRequirementCount,
            ValidationWarningCount = source.ValidationWarningCount,
            ComponentNames = source.ComponentNames.ToList(),
            IngredientNames = source.IngredientNames.ToList(),
            PackagingNames = source.PackagingNames.ToList(),
            ValidationWarnings = source.ValidationWarnings.ToList(),
            MissingDetails = source.MissingDetails.ToList(),
            Alerts = source.Alerts.ToList(),
            HasProductionSnapshot = source.HasProductionSnapshot,
            ProductionPlanItemId = source.ProductionPlanItemId,
            PlannedQuantity = source.PlannedQuantity,
            ProductionStatus = source.ProductionStatus,
            SnapshotHash = source.SnapshotHash,
            FreshSnapshotHash = source.FreshSnapshotHash,
            IsProductionSnapshotCurrent = source.IsProductionSnapshotCurrent,
            NeedsRefresh = source.NeedsRefresh,
            CanRefresh = source.CanRefresh,
            RefreshBlockers = source.RefreshBlockers.ToList(),
        };

    private static bool IsSameProductionSnapshotSet(
        IReadOnlyCollection<ProductionPlanItem> existingItems,
        IReadOnlyCollection<ProductionPlanItem> generatedItems)
    {
        if (existingItems.Count != generatedItems.Count)
        {
            return false;
        }

        var existingByKey = existingItems.ToDictionary(CreateProductionSnapshotComparisonKey);
        foreach (var generated in generatedItems)
        {
            var key = CreateProductionSnapshotComparisonKey(generated);
            if (!existingByKey.TryGetValue(key, out var existing))
            {
                return false;
            }

            if (existing.PlannedQuantity != generated.PlannedQuantity ||
                !string.Equals(existing.M2SnapshotHash, generated.M2SnapshotHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static string CreateProductionSnapshotComparisonKey(ProductionPlanItem item)
        => item.DietMenuPlanItemId.HasValue
            ? $"P:{item.DietMenuPlanItemId.Value}"
            : $"M:{item.MealId}:D:{item.DietVariantId}:H:{item.M2SnapshotHash}";

    private async Task EnrichComponentSessionProgressAsync(CookingCardDto card)
    {
        foreach (var component in card.Components)
        {
            var session = await _cookingSessionService.GetComponentSessionAsync(
                card.PlanItemId,
                component.RecipeComponentVersionId);
            var steps = component.InstructionSections
                .SelectMany(section => section.Steps)
                .ToList();
            var requiredSteps = steps
                .Where(step => step.IsCritical || step.RequiresControl)
                .ToList();

            component.SessionStatus = session.Status;
            component.TotalStepCount = steps.Count;
            component.CheckedStepCount = steps.Count(step =>
                session.StepChecksByStepId.TryGetValue(step.StepId, out var check)
                && check.IsChecked);
            component.RequiredStepCount = requiredSteps.Count;
            component.RequiredCheckedStepCount = requiredSteps.Count(step =>
                session.StepChecksByStepId.TryGetValue(step.StepId, out var check)
                && check.IsChecked);
        }
    }

    private static ProductionPlanItem? FindProductionItemForSnapshotItem(
        IReadOnlyCollection<ProductionPlanItem> productionItems,
        PublishedDietPlanItemDto snapshotItem)
    {
        var byPlanItemId = productionItems.FirstOrDefault(item =>
            item.DietMenuPlanItemId == snapshotItem.DietMenuPlanItemId);
        if (byPlanItemId is not null)
        {
            return byPlanItemId;
        }

        if (snapshotItem.MealVariantId.HasValue)
        {
            var byVariant = productionItems.FirstOrDefault(item =>
            {
                if (item.DietMenuPlanItemId.HasValue)
                {
                    return false;
                }

                var storedSnapshot = TryDeserializeSnapshotItem(item);
                return item.MealId == snapshotItem.MealId
                    && item.DietVariantId == snapshotItem.DietVariantId
                    && storedSnapshot?.MealVariantId == snapshotItem.MealVariantId;
            });
            if (byVariant is not null)
            {
                return byVariant;
            }
        }

        return productionItems.FirstOrDefault(item =>
            !item.DietMenuPlanItemId.HasValue &&
            item.MealId == snapshotItem.MealId
            && item.DietVariantId == snapshotItem.DietVariantId);
    }

    private static CookingCardDto BuildCookingCardFromSnapshot(
        ProductionPlanItem item,
        PublishedDietPlanItemDto snapshotItem,
        MealCookingDetailsEntry? details)
    {
        var hasAggregateIngredients = snapshotItem.AggregateIngredients.Count > 0;
        var hasAggregatePackaging = HasAggregatePackaging(snapshotItem);
        var card = new CookingCardDto
        {
            PlanItemId = item.Id,
            MealId = item.MealId,
            MealName = snapshotItem.MealName,
            CategoryName = snapshotItem.CategoryName ?? details?.CategoryName,
            Description = details?.Description,
            PreparationInstructions = details?.PreparationInstructions,
            MainImageUrl = details?.MainImageUrl,
            PreparationTimeMinutes = details?.PreparationTimeMinutes ?? 0,
            DietVariantId = item.DietVariantId,
            PlannedQuantity = item.PlannedQuantity,
            ProductionGroup = item.ProductionGroup,
            EstimatedReadyTime = item.EstimatedReadyTime.HasValue
                ? item.EstimatedReadyTime.Value.ToString("HH:mm")
                : null,
            Status = item.Status.ToString(),
            FefoDeductedAt = item.FefoDeductedAt,
            PackagingDeductedAt = item.PackagingDeductedAt,
            HasM2Snapshot = true,
            RawWeightGrams = snapshotItem.RawWeightGrams,
            CookedWeightGrams = snapshotItem.CookedWeightGrams,
            NutritionFacts = snapshotItem.Nutrition is null
                ? null
                : new CookingCardNutritionDto
                {
                    CaloriesPer100g = snapshotItem.Nutrition.CaloriesPer100g,
                    ProteinPer100g = snapshotItem.Nutrition.ProteinPer100g,
                    CarbohydratesPer100g = snapshotItem.Nutrition.CarbohydratesPer100g,
                    FatPer100g = snapshotItem.Nutrition.FatPer100g,
                    FiberPer100g = snapshotItem.Nutrition.FiberPer100g,
                },
            Allergens = snapshotItem.Allergens,
        };

        foreach (var component in snapshotItem.Components.OrderBy(c => c.SortOrder))
        {
            var componentDto = new CookingCardComponentDto
            {
                RecipeComponentVersionId = component.RecipeComponentVersionId,
                ComponentName = component.ComponentName,
                Role = component.Role,
                QuantityPerServing = component.QuantityPerServing * snapshotItem.ServingMultiplier,
                Unit = component.Unit,
                TotalQuantity = component.QuantityPerServing * snapshotItem.ServingMultiplier * item.PlannedQuantity,
                Instructions = component.Instructions,
                InstructionSections = component.InstructionSections.Select(MapInstructionSection).ToList(),
                RequiresCoreTemperatureCheck = component.Ingredients.Any(i => i.RequiresCoreTemperatureCheck),
                MinimumCoreTemperatureCelsius = MaxTemperature(component.Ingredients),
            };

            foreach (var ingredient in component.Ingredients)
            {
                var weightPerServing = ingredient.WeightInGrams * component.QuantityPerServing * snapshotItem.ServingMultiplier;
                var ingredientDto = new CookingCardIngredientDto
                {
                    IngredientId = ingredient.IngredientId,
                    StockItemId = ingredient.StockItemId,
                    IngredientName = ingredient.IngredientName,
                    WarehouseCategoryName = ingredient.WarehouseCategoryName,
                    WeightPerServing = weightPerServing,
                    TotalWeight = weightPerServing * item.PlannedQuantity,
                    YieldFactor = ingredient.YieldFactor,
                    RequiresCoreTemperatureCheck = ingredient.RequiresCoreTemperatureCheck,
                    MinimumCoreTemperatureCelsius = ingredient.MinimumCoreTemperatureCelsius,
                    IsOptional = ingredient.IsOptional,
                };

                componentDto.Ingredients.Add(ingredientDto);
                if (!hasAggregateIngredients)
                {
                    AddAggregatedIngredient(card.Ingredients, ingredientDto);
                }
            }

            foreach (var packaging in component.PackagingRequirements)
            {
                var quantityPerServing = packaging.Quantity * component.QuantityPerServing * snapshotItem.ServingMultiplier;
                componentDto.PackagingRequirements.Add(MapPackaging(packaging, quantityPerServing, item.PlannedQuantity));
            }

            card.Components.Add(componentDto);
        }

        if (hasAggregateIngredients)
        {
            foreach (var ingredient in snapshotItem.AggregateIngredients)
            {
                var weightPerServing = ingredient.NetWeightInGrams * snapshotItem.ServingMultiplier;
                card.Ingredients.Add(new CookingCardIngredientDto
                {
                    IngredientId = ingredient.IngredientId,
                    StockItemId = ingredient.StockItemId,
                    IngredientName = ingredient.IngredientName,
                    WarehouseCategoryName = ingredient.WarehouseCategoryName,
                    WeightPerServing = weightPerServing,
                    TotalWeight = weightPerServing * item.PlannedQuantity,
                    YieldFactor = ingredient.YieldFactor,
                    IsOptional = ingredient.IsOptional,
                });
            }
        }

        foreach (var packaging in snapshotItem.PackagingRequirements)
        {
            card.PackagingRequirements.Add(MapPackaging(
                packaging,
                GetPackagingQuantityPerServing(snapshotItem, packaging),
                item.PlannedQuantity));
        }

        if (!hasAggregatePackaging)
        {
            foreach (var component in card.Components)
            {
                card.PackagingRequirements.AddRange(component.PackagingRequirements);
            }
        }

        card.RequiresCoreTemperatureCheck = card.Components.Any(c => c.RequiresCoreTemperatureCheck);
        card.MinimumCoreTemperatureCelsius = MaxTemperature(card.Ingredients);
        card.MissingWarehouseMappings = hasAggregateIngredients
            ? snapshotItem.AggregateIngredients
                .Where(i => !i.StockItemId.HasValue && !i.WarehouseCategoryId.HasValue)
                .Select(i => $"{i.IngredientName} (ID {i.IngredientId})")
                .Distinct()
                .ToList()
            : snapshotItem.Components
                .SelectMany(c => c.Ingredients)
                .Where(i => !i.StockItemId.HasValue && !i.WarehouseCategoryId.HasValue)
                .Select(i => $"{i.IngredientName} (ID {i.IngredientId})")
                .Distinct()
                .ToList();

        return card;
    }

    private static IEnumerable<PackagingRequirement> BuildPackagingRequirements(
        PublishedDietPlanItemDto snapshotItem,
        decimal actualQuantity)
    {
        var hasAggregatePackaging = HasAggregatePackaging(snapshotItem);
        foreach (var packaging in snapshotItem.PackagingRequirements)
        {
            yield return CreatePackagingRequirement(
                packaging,
                GetPackagingQuantityPerServing(snapshotItem, packaging) * actualQuantity);
        }

        if (hasAggregatePackaging)
        {
            yield break;
        }

        foreach (var component in snapshotItem.Components)
        {
            foreach (var packaging in component.PackagingRequirements)
            {
                yield return CreatePackagingRequirement(
                    packaging,
                    packaging.Quantity * component.QuantityPerServing * snapshotItem.ServingMultiplier * actualQuantity);
            }
        }
    }

    private static PackagingRequirement CreatePackagingRequirement(PackagingRequirementDto packaging, decimal requiredQuantity)
    {
        if (!packaging.StockItemId.HasValue && !packaging.WarehouseCategoryId.HasValue)
        {
            throw new InvalidOperationException(
                $"Opakowanie '{packaging.ResourceName}' nie ma mapowania StockItemId ani WarehouseCategoryId.");
        }

        return new PackagingRequirement(
            packaging.ResourceName,
            packaging.StockItemId,
            packaging.WarehouseCategoryId,
            requiredQuantity);
    }

    private static bool HasAggregatePackaging(PublishedDietPlanItemDto snapshotItem)
        => snapshotItem.PackagingRequirements.Any(packaging =>
            packaging.RecipeComponentVersionId.HasValue);

    private static decimal GetPackagingQuantityPerServing(
        PublishedDietPlanItemDto snapshotItem,
        PackagingRequirementDto packaging)
        => packaging.RecipeComponentVersionId.HasValue
            ? packaging.Quantity * snapshotItem.ServingMultiplier
            : packaging.Quantity;

    private static PublishedDietPlanSnapshotDto? BuildStoredSnapshot(DateOnly planDate, List<ProductionPlanItem> pendingItems)
    {
        var snapshotItems = pendingItems
            .Select(TryDeserializeSnapshotItem)
            .Where(item => item is not null)
            .Select(item => item!)
            .ToList();

        if (snapshotItems.Count == 0)
        {
            return null;
        }

        if (snapshotItems.Count != pendingItems.Count)
        {
            throw new InvalidOperationException(
                "Plan produkcji ma czesciowy snapshot M2. Nie mozna mieszac live danych M2 z zamrozonym snapshotem.");
        }

        return new PublishedDietPlanSnapshotDto
        {
            PlanDate = planDate,
            PlanStatus = "Snapshot",
            Items = snapshotItems,
        };
    }

    private static PublishedDietPlanItemDto? FindSnapshotItemForProductionItem(
        PublishedDietPlanSnapshotDto snapshot,
        ProductionPlanItem item)
    {
        if (item.DietMenuPlanItemId.HasValue)
        {
            var planItemMatch = snapshot.Items.FirstOrDefault(snapshotItem =>
                snapshotItem.DietMenuPlanItemId == item.DietMenuPlanItemId.Value);
            if (planItemMatch is not null)
            {
                return planItemMatch;
            }
        }

        var storedSnapshot = TryDeserializeSnapshotItem(item);
        if (storedSnapshot?.MealVariantId is int mealVariantId)
        {
            var variantMatch = snapshot.Items.FirstOrDefault(snapshotItem =>
                snapshotItem.MealId == item.MealId &&
                snapshotItem.DietVariantId == item.DietVariantId &&
                snapshotItem.MealVariantId == mealVariantId);
            if (variantMatch is not null)
            {
                return variantMatch;
            }
        }

        return snapshot.Items.FirstOrDefault(snapshotItem =>
            snapshotItem.MealId == item.MealId &&
            snapshotItem.DietVariantId == item.DietVariantId);
    }

    private static PublishedDietPlanItemDto? TryDeserializeSnapshotItem(ProductionPlanItem item)
    {
        if (string.IsNullOrWhiteSpace(item.M2SnapshotJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PublishedDietPlanItemDto>(item.M2SnapshotJson);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Snapshot M2 dla pozycji planu {item.Id} jest uszkodzony i nie moze zostac uzyty operacyjnie.",
                ex);
        }
    }

    private static void AddAggregatedIngredient(List<CookingCardIngredientDto> target, CookingCardIngredientDto ingredient)
    {
        var existing = target.FirstOrDefault(i =>
            i.IngredientId == ingredient.IngredientId
            && i.StockItemId == ingredient.StockItemId
            && i.WarehouseCategoryName == ingredient.WarehouseCategoryName);

        if (existing is null)
        {
            target.Add(ingredient);
            return;
        }

        existing.WeightPerServing += ingredient.WeightPerServing;
        existing.TotalWeight += ingredient.TotalWeight;
        existing.RequiresCoreTemperatureCheck |= ingredient.RequiresCoreTemperatureCheck;
        existing.MinimumCoreTemperatureCelsius = MaxTemperature(existing.MinimumCoreTemperatureCelsius, ingredient.MinimumCoreTemperatureCelsius);
    }

    private static CookingCardPackagingDto MapPackaging(PackagingRequirementDto packaging, decimal quantityPerServing, int plannedQuantity)
        => new()
        {
            ResourceName = packaging.ResourceName,
            StockItemId = packaging.StockItemId,
            WarehouseCategoryId = packaging.WarehouseCategoryId,
            QuantityPerServing = quantityPerServing,
            TotalQuantity = quantityPerServing * plannedQuantity,
            Unit = packaging.Unit,
            ContainerRole = packaging.ContainerRole,
        };

    private static CookingComponentInstructionSectionDto MapInstructionSection(ComponentInstructionSectionDto section)
        => new()
        {
            SectionId = section.SectionId,
            Title = section.Title,
            SortOrder = section.SortOrder,
            Steps = section.Steps
                .OrderBy(step => step.SortOrder)
                .ThenBy(step => step.StepId)
                .Select(step => new CookingComponentInstructionStepDto
                {
                    StepId = step.StepId,
                    StepText = step.StepText,
                    SortOrder = step.SortOrder,
                    RequiresControl = step.RequiresControl,
                    ControlType = step.ControlType,
                    ExpectedValue = step.ExpectedValue,
                    ExpectedUnit = step.ExpectedUnit,
                    IsCritical = step.IsCritical,
                })
                .ToList(),
        };

    private static decimal? MaxTemperature(IEnumerable<ComponentIngredientDto> ingredients)
    {
        var values = ingredients
            .Where(i => i.RequiresCoreTemperatureCheck && i.MinimumCoreTemperatureCelsius.HasValue)
            .Select(i => i.MinimumCoreTemperatureCelsius!.Value)
            .ToList();

        return values.Count == 0 ? null : values.Max();
    }

    private static decimal? MaxTemperature(IEnumerable<CookingCardIngredientDto> ingredients)
    {
        var values = ingredients
            .Where(i => i.RequiresCoreTemperatureCheck && i.MinimumCoreTemperatureCelsius.HasValue)
            .Select(i => i.MinimumCoreTemperatureCelsius!.Value)
            .ToList();

        return values.Count == 0 ? null : values.Max();
    }

    private static decimal? MaxTemperature(decimal? first, decimal? second)
    {
        if (!first.HasValue)
        {
            return second;
        }

        if (!second.HasValue)
        {
            return first;
        }

        return Math.Max(first.Value, second.Value);
    }

    private static string CreatePackagingRequirementKey(PackagingRequirement requirement)
        => requirement.StockItemId.HasValue
            ? $"S:{requirement.StockItemId.Value}"
            : $"C:{requirement.WarehouseCategoryId!.Value}";

    private static string CreateFefoReferenceDocument(
        int planId,
        int planItemId,
        SnapshotIngredientRequirement requirement)
        => requirement.StockItemId.HasValue
            ? $"P{planId}-I{planItemId}-S{requirement.StockItemId.Value}"
            : $"P{planId}-I{planItemId}-C{requirement.WarehouseCategoryId!.Value}";

    private static string CreatePackagingReferenceDocument(
        ProductionPlanItem item,
        PackagingRequirement requirement)
        => requirement.StockItemId.HasValue
            ? $"P{item.ProductionPlanId}-I{item.Id}-PK-S{requirement.StockItemId.Value}"
            : $"P{item.ProductionPlanId}-I{item.Id}-PK-C{requirement.WarehouseCategoryId!.Value}";

    private async Task MarkItemFefoDeductedAsync(ProductionPlanItem item, string referenceDocument)
    {
        item.FefoDeductedAt = DateTimeOffset.UtcNow;
        item.FefoReferenceDocument = referenceDocument;
        if (item.Status == ProductionItemStatus.Planned)
        {
            item.Status = ProductionItemStatus.Cooking;
        }

        await _itemRepository.UpdateAsync(item);
    }

    private static string CreateRequirementKey(SnapshotIngredientRequirement requirement)
        => requirement.StockItemId.HasValue
            ? $"S:{requirement.StockItemId.Value}"
            : $"C:{requirement.WarehouseCategoryId!.Value}";

    private static ProductionM2PlanOverviewDto ApplyM2PlanFilters(
        ProductionM2PlanOverviewDto overview,
        ProductionM2PlanFilterDto filter)
    {
        var entries = overview.Days
            .SelectMany(day => day.Items.Select(item => new M2PlanItemEntry(day.PlanDate, item)))
            .Where(entry => MatchesM2PlanFilter(entry.Item, filter))
            .ToList();

        var filteredCount = entries.Count;
        var totalPages = filteredCount == 0
            ? 0
            : (int)Math.Ceiling((double)filteredCount / filter.PageSize);
        var page = totalPages == 0
            ? 1
            : Math.Min(filter.Page, totalPages);

        var pageEntries = entries
            .Skip((page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        var pageItemsByDate = pageEntries
            .GroupBy(entry => entry.PlanDate)
            .ToDictionary(group => group.Key, group => group.Select(entry => entry.Item).ToList());

        foreach (var day in overview.Days)
        {
            day.Items = pageItemsByDate.TryGetValue(day.PlanDate, out var items)
                ? items
                : new List<ProductionM2PlanItemDto>();
        }

        filter.Page = page;
        overview.Filter = filter;
        overview.FilteredItemCount = filteredCount;
        overview.Page = page;
        overview.PageSize = filter.PageSize;
        return overview;
    }

    private static bool MatchesM2PlanFilter(ProductionM2PlanItemDto item, ProductionM2PlanFilterDto filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Search) && !M2PlanSearchText(item).Contains(filter.Search, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (filter.Snapshot is "missing" && item.HasProductionSnapshot)
        {
            return false;
        }

        if (filter.Snapshot is "present" && !item.HasProductionSnapshot)
        {
            return false;
        }

        if (filter.Snapshot is "current" && !item.IsProductionSnapshotCurrent)
        {
            return false;
        }

        if (filter.Snapshot is "stale" && (!item.HasProductionSnapshot || item.IsProductionSnapshotCurrent))
        {
            return false;
        }

        if (filter.Refresh is "needs" && !item.NeedsRefresh)
        {
            return false;
        }

        if (filter.Refresh is "can" && !item.CanRefresh)
        {
            return false;
        }

        if (filter.Refresh is "blocked" && item.CanRefresh)
        {
            return false;
        }

        if (filter.Completeness is "complete" && !item.IsCompleteForProduction)
        {
            return false;
        }

        if (filter.Completeness is "incomplete"
            && item.IsCompleteForProduction
            && item.ValidationWarningCount == 0
            && item.MissingDetails.Count == 0)
        {
            return false;
        }

        return true;
    }

    private static string M2PlanSearchText(ProductionM2PlanItemDto item)
        => string.Join(" ", new[]
            {
                item.DietMenuPlanItemId.ToString(),
                item.ProductionPlanItemId?.ToString(),
                item.MealId.ToString(),
                item.MealVariantId?.ToString(),
                item.MealName,
                item.MealVariantName,
                item.CategoryName,
                item.DietName,
                item.DietVariantName,
                item.MealSlot,
                item.SnapshotHash,
                item.FreshSnapshotHash,
            }
            .Concat(item.ComponentNames)
            .Concat(item.IngredientNames)
            .Concat(item.PackagingNames)
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    private static ProductionM2PlanFilterDto NormalizeProductionM2PlanFilter(ProductionM2PlanFilterDto filter)
        => new()
        {
            StartDate = filter.StartDate == default ? DateOnly.FromDateTime(DateTime.Today) : filter.StartDate,
            Days = Math.Clamp(filter.Days <= 0 ? 7 : filter.Days, 1, 31),
            Search = NormalizeSearch(filter.Search),
            Snapshot = NormalizeM2SnapshotFilter(filter.Snapshot),
            Refresh = NormalizeM2RefreshFilter(filter.Refresh),
            Completeness = NormalizeM2CompletenessFilter(filter.Completeness),
            Page = Math.Max(filter.Page, 1),
            PageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 10, 100),
        };

    private static string? NormalizeM2SnapshotFilter(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "missing" or "brak" => "missing",
            "present" or "has" or "odebrany" => "present",
            "current" or "actual" or "aktualny" => "current",
            "stale" or "outdated" or "nieaktualny" => "stale",
            _ => null,
        };
    }

    private static string? NormalizeM2RefreshFilter(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "needs" or "refresh" or "needed" or "do-odswiezenia" => "needs",
            "can" or "allowed" or "mozna" => "can",
            "blocked" or "zablokowane" => "blocked",
            _ => null,
        };
    }

    private static string? NormalizeM2CompletenessFilter(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "complete" or "ok" or "kompletne" => "complete",
            "incomplete" or "missing" or "braki" => "incomplete",
            _ => null,
        };
    }

    private sealed record M2PlanItemEntry(DateOnly PlanDate, ProductionM2PlanItemDto Item);

    private static KitchenDashboardFilterDto NormalizeKitchenDashboardFilter(KitchenDashboardFilterDto filter)
    {
        var normalized = new KitchenDashboardFilterDto
        {
            Date = filter.Date == default ? DateOnly.FromDateTime(DateTime.Today) : filter.Date,
            Search = NormalizeSearch(filter.Search),
            Status = ParseProductionStatus(filter.Status)?.ToString(),
            ProductionGroup = filter.ProductionGroup,
            Fefo = NormalizeStateFilter(filter.Fefo),
            Packaging = NormalizeStateFilter(filter.Packaging),
            Snapshot = NormalizeSnapshotFilter(filter.Snapshot),
            SortBy = NormalizeSortBy(filter.SortBy),
            SortDirection = string.Equals(filter.SortDirection, "desc", StringComparison.OrdinalIgnoreCase)
                ? "desc"
                : "asc",
            Page = Math.Max(filter.Page, 1),
            PageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 10, 100),
        };

        return normalized;
    }

    private static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var trimmed = search.Trim();
        return trimmed.Length > 120 ? trimmed[..120] : trimmed;
    }

    private static string? NormalizeSortBy(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            "meal" or "mealname" => "meal",
            "planned" or "plannedquantity" => "planned",
            "cooked" or "cookedquantity" => "cooked",
            "status" => "status",
            "group" or "productiongroup" => "group",
            "ready" or "estimatedreadytime" => "ready",
            _ => "id",
        };
    }

    private static ProductionItemStatus? ParseProductionStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status) || status.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Enum.TryParse<ProductionItemStatus>(status.Trim(), ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    private static string? NormalizeStateFilter(string? value)
    {
        return ParseStateFilter(value) switch
        {
            true => "done",
            false => "pending",
            _ => null,
        };
    }

    private static bool? ParseStateFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "done" or "deducted" or "yes" or "true")
        {
            return true;
        }

        if (normalized is "pending" or "missing" or "no" or "false")
        {
            return false;
        }

        return null;
    }

    private static string? NormalizeSnapshotFilter(string? value)
    {
        return ParseSnapshotFilter(value) switch
        {
            true => "present",
            false => "missing",
            _ => null,
        };
    }

    private static bool? ParseSnapshotFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized is "present" or "has" or "yes" or "true")
        {
            return true;
        }

        if (normalized is "missing" or "none" or "no" or "false")
        {
            return false;
        }

        return null;
    }

    private static KitchenDashboardSummaryDto MapKitchenSummary(ProductionPlanItemSummary summary)
        => new()
        {
            TotalItems = summary.TotalItems,
            TotalPlannedQuantity = summary.TotalPlannedQuantity,
            TotalCookedQuantity = summary.TotalCookedQuantity,
            PlannedItems = summary.PlannedItems,
            CookingItems = summary.CookingItems,
            CookedItems = summary.CookedItems,
            FailedItems = summary.FailedItems,
            PendingFefoItems = summary.PendingFefoItems,
            FefoDeductedItems = summary.FefoDeductedItems,
            PendingPackagingItems = summary.PendingPackagingItems,
            PackagingDeductedItems = summary.PackagingDeductedItems,
            SnapshotItems = summary.SnapshotItems,
        };

    private static KitchenDashboardItemDto MapKitchenDashboardItem(ProductionPlanItem item)
    {
        var snapshot = TryReadSnapshotForDashboard(item, out var snapshotWarning);
        var packagingCount = snapshot is null
            ? 0
            : snapshot.PackagingRequirements.Count
                + snapshot.Components.Sum(component => component.PackagingRequirements.Count);
        var validationWarningCount = snapshot is null
            ? 0
            : snapshot.ValidationWarnings.Count
                + snapshot.Components.Sum(component => component.ValidationWarnings.Count);

        return new KitchenDashboardItemDto
        {
            Id = item.Id,
            ProductionPlanId = item.ProductionPlanId,
            MealId = item.MealId,
            MealName = snapshot?.MealName ?? item.MealName,
            DietVariantId = item.DietVariantId,
            DietMenuPlanItemId = item.DietMenuPlanItemId,
            MealVariantId = snapshot?.MealVariantId,
            MealVariantName = snapshot?.MealVariantName,
            CategoryName = snapshot?.CategoryName,
            MealSlot = snapshot?.MealSlot,
            PlannedQuantity = item.PlannedQuantity,
            CookedQuantity = item.CookedQuantity,
            Status = item.Status.ToString(),
            ProductionGroup = item.ProductionGroup,
            EstimatedReadyTime = item.EstimatedReadyTime?.ToString("HH:mm"),
            ActualReadyTime = item.ActualReadyTime?.ToString("HH:mm"),
            FefoDeductedAt = item.FefoDeductedAt,
            PackagingDeductedAt = item.PackagingDeductedAt,
            M2SnapshotHash = item.M2SnapshotHash,
            HasM2Snapshot = !string.IsNullOrWhiteSpace(item.M2SnapshotJson),
            ComponentCount = snapshot?.Components.Count ?? CountComponentIds(item.RecipeComponentVersionIds),
            PackagingRequirementCount = packagingCount,
            ValidationWarningCount = validationWarningCount,
            HasMissingWarehouseMappings = snapshot is not null && HasMissingWarehouseMappings(snapshot),
            SnapshotWarning = snapshotWarning,
        };
    }

    private static bool HasMissingWarehouseMappings(PublishedDietPlanItemDto snapshot)
        => snapshot.AggregateIngredients.Count > 0
            ? snapshot.AggregateIngredients.Any(ingredient =>
                !ingredient.StockItemId.HasValue && !ingredient.WarehouseCategoryId.HasValue)
            : snapshot.Components
                .SelectMany(component => component.Ingredients)
                .Any(ingredient => !ingredient.StockItemId.HasValue && !ingredient.WarehouseCategoryId.HasValue);

    private static PublishedDietPlanItemDto? TryReadSnapshotForDashboard(
        ProductionPlanItem item,
        out string? warning)
    {
        warning = null;
        if (string.IsNullOrWhiteSpace(item.M2SnapshotJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PublishedDietPlanItemDto>(item.M2SnapshotJson);
        }
        catch (JsonException)
        {
            warning = "Snapshot M2 jest uszkodzony.";
            return null;
        }
    }

    private static int CountComponentIds(string? recipeComponentVersionIds)
        => string.IsNullOrWhiteSpace(recipeComponentVersionIds)
            ? 0
            : recipeComponentVersionIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Length;

    private async Task<ProductionAdjustmentApproval?> RequireApprovedAdjustmentIfNeededAsync(
        ProductionPlanItem item,
        decimal actualValue,
        string adjustmentType)
    {
        var plannedValue = GetPlannedAdjustmentValue(item, adjustmentType);
        if (actualValue == plannedValue)
        {
            return null;
        }

        var allowedTypes = adjustmentType == "CookedQuantity"
            ? new[] { "CookedQuantity", "PackagingQuantity", "LabelQuantity" }
            : new[] { adjustmentType };

        var approvedAdjustment = (await _adjustmentApprovalRepository.GetAllAsync())
            .Where(approval =>
                approval.ProductionPlanItemId == item.Id &&
                allowedTypes.Any(type => string.Equals(approval.AdjustmentType, type, StringComparison.OrdinalIgnoreCase)) &&
                string.Equals(approval.Status, "Approved", StringComparison.OrdinalIgnoreCase) &&
                approval.RequestedValue == actualValue)
            .OrderByDescending(approval => approval.ApprovedAt ?? approval.RequestedAt)
            .FirstOrDefault();

        if (approvedAdjustment is null)
        {
            throw new InvalidOperationException(
                "Zmiana faktycznej ilości, liczby pudełek albo etykiet wymaga akceptacji managera/admina przed zatwierdzeniem.");
        }

        return approvedAdjustment;
    }

    private static ProductionAdjustmentApprovalDto MapAdjustmentApproval(ProductionAdjustmentApproval approval)
        => new()
        {
            Id = approval.Id,
            ProductionPlanItemId = approval.ProductionPlanItemId,
            AdjustmentType = approval.AdjustmentType,
            Status = approval.Status,
            PlannedValue = approval.PlannedValue,
            RequestedValue = approval.RequestedValue,
            Unit = approval.Unit,
            Reason = approval.Reason,
            RequestedBy = approval.RequestedBy,
            RequestedAt = approval.RequestedAt,
            ApprovedBy = approval.ApprovedBy,
            ApprovedAt = approval.ApprovedAt,
            ApprovalNote = approval.ApprovalNote,
            AppliedAt = approval.AppliedAt,
        };

    private static string NormalizeAdjustmentType(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return "CookedQuantity";
        }

        return normalized.ToLowerInvariant() switch
        {
            "cooked" or "cookedquantity" or "quantity" or "ilosc" or "ilość" => "CookedQuantity",
            "packaging" or "packagingquantity" or "boxes" or "pudelka" or "pudełka" => "PackagingQuantity",
            "labels" or "labelquantity" or "etykiety" => "LabelQuantity",
            _ => throw new InvalidOperationException("Nieobsługiwany typ korekty produkcyjnej."),
        };
    }

    private static decimal GetPlannedAdjustmentValue(ProductionPlanItem item, string adjustmentType)
        => adjustmentType switch
        {
            "CookedQuantity" => item.PlannedQuantity,
            "PackagingQuantity" => item.PlannedQuantity,
            "LabelQuantity" => item.PlannedQuantity,
            _ => item.PlannedQuantity,
        };

    private static string GetAdjustmentUnit(string adjustmentType)
        => adjustmentType switch
        {
            "PackagingQuantity" => "container",
            "LabelQuantity" => "label",
            _ => "portion",
        };

    private static string NormalizeRequiredText(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        var trimmed = value.Trim();
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    private static string NormalizeActor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 120 ? trimmed[..120] : trimmed;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }

    private sealed record SnapshotIngredientRequirement(
        int? RecipeComponentVersionId,
        string ComponentName,
        int IngredientId,
        string IngredientName,
        int? StockItemId,
        int? WarehouseCategoryId,
        decimal RequiredQuantity);

    private sealed record PackagingRequirement(
        string ResourceName,
        int? StockItemId,
        int? WarehouseCategoryId,
        decimal RequiredQuantity);
}
