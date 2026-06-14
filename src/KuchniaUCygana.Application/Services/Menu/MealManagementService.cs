using AutoMapper;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class MealManagementService : IMealManagementService
{
    private const string AggregatedNutritionSource = "Aggregated";
    private const string OverrideNutritionSource = "Override";

    private readonly IMealRepository mealRepository;
    private readonly IRecipeRepository recipeRepository;
    private readonly IRecipeComponentRepository recipeComponentRepository;
    private readonly IMealVariantRepository mealVariantRepository;
    private readonly IMealAllergenRepository mealAllergenRepository;
    private readonly IMealImageRepository mealImageRepository;
    private readonly IMealVariantResultCalculator resultCalculator;
    private readonly ICurrentUserService currentUser;
    private readonly IMapper mapper;
    private readonly IMenuPlanningCache planningCache;

    public MealManagementService(
        IMealRepository mealRepository,
        IRecipeRepository recipeRepository,
        IRecipeComponentRepository recipeComponentRepository,
        IMealVariantRepository mealVariantRepository,
        IMealAllergenRepository mealAllergenRepository,
        IMealImageRepository mealImageRepository,
        IMealVariantResultCalculator resultCalculator,
        ICurrentUserService currentUser,
        IMapper mapper,
        IMenuPlanningCache? planningCache = null)
    {
        this.mealRepository = mealRepository;
        this.recipeRepository = recipeRepository;
        this.recipeComponentRepository = recipeComponentRepository;
        this.mealVariantRepository = mealVariantRepository;
        this.mealAllergenRepository = mealAllergenRepository;
        this.mealImageRepository = mealImageRepository;
        this.resultCalculator = resultCalculator;
        this.currentUser = currentUser;
        this.mapper = mapper;
        this.planningCache = planningCache ?? new NullMenuPlanningCache();
    }

    public async Task<PagedResultDto<MealListItemDto>> SearchAsync(MealSearchFilterDto filter)
    {
        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 1, 100);

        var result = await this.mealRepository.SearchAsync(new MealSearchQuery
        {
            Search = Normalize(filter.Search),
            CategoryId = filter.CategoryId,
            Status = Normalize(filter.Status),
            AllergenId = filter.AllergenId,
            MissingPublicationData = filter.MissingPublicationData,
            HasVariants = filter.HasVariants,
            MissingPackaging = filter.MissingPackaging,
            Page = page,
            PageSize = pageSize,
        });

        filter.Page = page;
        filter.PageSize = pageSize;

        return new PagedResultDto<MealListItemDto>
        {
            Items = result.Items.Select(MapListItem).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = result.TotalCount,
        };
    }

    public async Task<IReadOnlyList<MenuPlanMealLookupDto>> SearchPlanningMealsAsync(string? query, int limit = 20)
    {
        var pageSize = Math.Clamp(limit <= 0 ? 20 : limit, 1, 50);
        var rows = await this.mealRepository.SearchPlanningAsync(Normalize(query), pageSize);

        return rows
            .Select(MapPlanningMealLookup)
            .ToList();
    }

    public async Task<MealDto?> GetMealAsync(int mealId)
    {
        var meal = await this.mealRepository.GetByIdAsync(mealId);
        return meal is null ? null : this.mapper.Map<MealDto>(meal);
    }

    public async Task<MealDetailDto?> GetMealWithDetailsAsync(int mealId)
    {
        var meal = await this.mealRepository.GetByIdAsync(mealId);
        if (meal is null)
        {
            return null;
        }

        var detail = this.mapper.Map<MealDetailDto>(meal);
        var recipes = await this.recipeRepository.GetByMealIdAsync(mealId);
        detail.Recipe = this.mapper.Map<List<RecipeItemDto>>(recipes);

        var componentRows = (await this.recipeComponentRepository.GetMealComponentDetailsAsync(mealId)).ToList();
        detail.Components = MapMealComponents(componentRows);
        detail.Result = await this.BuildMealVariantResultAsync(meal, null);

        var variants = await this.mealVariantRepository.GetByMealIdAsync(mealId);
        foreach (var variant in variants)
        {
            var components = await this.mealVariantRepository.GetComponentsAsync(variant.Id);
            var packaging = await this.mealVariantRepository.GetPackagingAsync(variant.Id);
            var result = await this.BuildMealVariantResultAsync(meal, variant);
            detail.Variants.Add(MapMealVariant(variant, components, packaging, result));
        }

        var images = await this.mealImageRepository.GetByMealIdAsync(mealId);
        detail.Images = this.mapper.Map<List<MealImageDto>>(images);

        var nutritionCost = await this.mealRepository.GetMealNutritionCostAsync(mealId);
        if (nutritionCost is not null)
        {
            detail.NutritionCost = new MealNutritionCostDto
            {
                MealId = nutritionCost.MealId,
                EstimatedCost = nutritionCost.EstimatedCost,
                Calories = nutritionCost.Calories,
                Protein = nutritionCost.Protein,
                Carbohydrates = nutritionCost.Carbohydrates,
                Fat = nutritionCost.Fat,
                Fiber = nutritionCost.Fiber
            };
        }

        return detail;
    }

    public async Task<MealVariantResultDto?> GetMealVariantResultAsync(int mealId, int? mealVariantId)
    {
        return await this.GetMealVariantResultFreshAsync(mealId, mealVariantId);
    }

    public async Task<IReadOnlyDictionary<MealVariantResultKey, MealVariantResultDto?>> GetMealVariantResultsAsync(
        IEnumerable<MealVariantResultKey> keys,
        MealVariantResultCacheMode cacheMode = MealVariantResultCacheMode.CachePreferred)
    {
        var normalizedKeys = keys
            .Where(key => key.MealId > 0)
            .Distinct()
            .ToList();
        var results = new Dictionary<MealVariantResultKey, MealVariantResultDto?>();

        var freshKeys = new List<(MealVariantResultKey Key, string CacheKey)>();
        foreach (var key in normalizedKeys)
        {
            var cacheKey = MenuPlanningCacheKeys.MealResult(key);
            if (cacheMode == MealVariantResultCacheMode.CachePreferred)
            {
                var cached = await this.planningCache.GetAsync<MealVariantResultDto>(cacheKey);
                if (cached is not null)
                {
                    results[key] = cached;
                    continue;
                }
            }

            freshKeys.Add((key, cacheKey));
        }

        var freshResults = await Task.WhenAll(freshKeys.Select(async item => new
        {
            item.Key,
            item.CacheKey,
            Result = await this.GetMealVariantResultFreshAsync(item.Key.MealId, item.Key.MealVariantId),
        }));

        foreach (var item in freshResults)
        {
            results[item.Key] = item.Result;
            if (item.Result is not null)
            {
                await this.planningCache.SetAsync(item.CacheKey, item.Result, TimeSpan.FromMinutes(10));
            }
        }

        return results;
    }

    private async Task<MealVariantResultDto?> GetMealVariantResultFreshAsync(int mealId, int? mealVariantId)
    {
        var meal = await this.mealRepository.GetByIdAsync(mealId);
        if (meal is null)
        {
            return null;
        }

        MealVariantRow? variant = null;
        if (mealVariantId.HasValue)
        {
            variant = await this.mealVariantRepository.GetByIdAsync(mealVariantId.Value);
            if (variant is null || variant.MealId != mealId)
            {
                return null;
            }
        }

        return await this.BuildMealVariantResultAsync(meal, variant);
    }

    public async Task<IReadOnlyList<MealVariantPlanOptionDto>> GetMealVariantOptionsAsync(int mealId)
    {
        var variants = await this.mealVariantRepository.GetByMealIdAsync(mealId);
        return variants
            .OrderByDescending(variant => variant.IsDefault)
            .ThenBy(variant => variant.Name)
            .Select(variant => new MealVariantPlanOptionDto
            {
                MealId = variant.MealId,
                MealVariantId = variant.Id,
                Name = variant.Name,
                Status = variant.Status,
                IsDefault = variant.IsDefault,
            })
            .ToList();
    }

    public async Task<IReadOnlyList<MenuPlanMealVariantLookupDto>> GetPlanningMealVariantOptionsAsync(int mealId)
    {
        var detail = await this.GetMealWithDetailsAsync(mealId);
        if (detail is null || !detail.IsActive || !IsPlanningLookupStatus(detail.Status))
        {
            return Array.Empty<MenuPlanMealVariantLookupDto>();
        }

        var options = new List<MenuPlanMealVariantLookupDto>();
        options.Add(MapPlanningMealVariantLookup(
            null,
            "wariant bazowy",
            detail.Status,
            true,
            detail.Result));

        options.AddRange(detail.Variants
            .Where(variant => IsPlanningLookupStatus(variant.Status))
            .OrderByDescending(variant => variant.IsDefault)
            .ThenBy(variant => variant.Name)
            .Select(variant => MapPlanningMealVariantLookup(
                variant.Id,
                variant.Name,
                variant.Status,
                variant.IsDefault,
                variant.Result)));

        return options;
    }

    public async Task<IEnumerable<MealDto>> GetPublishedMealsAsync()
    {
        var meals = await this.mealRepository.GetPublishedAsync();
        return this.mapper.Map<IEnumerable<MealDto>>(meals);
    }

    public async Task<MealDto> CreateMealAsync(CreateMealRequest request)
    {
        var meal = this.mapper.Map<Meal>(request);
        meal.Status = MealStatus.Draft;
        await this.mealRepository.InsertAsync(meal);
        return this.mapper.Map<MealDto>(meal);
    }

    public async Task<int> CreateMealVariantAsync(CreateMealVariantRequest request)
    {
        var meal = await this.mealRepository.GetByIdAsync(request.MealId)
            ?? throw new InvalidOperationException($"Posilek #{request.MealId} nie istnieje.");

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Nazwa wariantu jest wymagana.");
        }

        var sourceVariantId = request.SourceMealVariantId;
        if (!sourceVariantId.HasValue)
        {
            sourceVariantId = (await this.mealVariantRepository.GetDefaultByMealIdAsync(request.MealId))?.Id;
        }

        var createdId = await this.mealVariantRepository.CreateAsync(new MealVariant
        {
            MealId = request.MealId,
            Name = request.Name.Trim(),
            VariantType = string.IsNullOrWhiteSpace(request.VariantType) ? "Standard" : request.VariantType.Trim(),
            Status = "Draft",
            Description = $"Wariant utworzony dla posilku {meal.Name}.",
            IsDefault = false,
            RawWeightGrams = meal.RawWeightGrams,
            CookedWeightGrams = meal.CookedWeightGrams,
            NutritionSource = AggregatedNutritionSource,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = this.UserName(),
        }, sourceVariantId, this.UserName());
        await this.InvalidateMealResultCacheAsync();
        return createdId;
    }

    public async Task UpdateMealVariantAsync(int mealVariantId, UpdateMealVariantRequest request)
    {
        var row = await this.mealVariantRepository.GetByIdAsync(mealVariantId)
            ?? throw new InvalidOperationException($"Wariant #{mealVariantId} nie istnieje.");
        var meal = await this.mealRepository.GetByIdAsync(row.MealId)
            ?? throw new InvalidOperationException($"Posilek #{row.MealId} nie istnieje.");

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Nazwa wariantu jest wymagana.");
        }

        var nutritionSource = NormalizeNutritionSource(request.NutritionSource);
        var overrideReason = request.NutritionOverrideReason?.Trim();
        if (string.Equals(nutritionSource, OverrideNutritionSource, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(overrideReason))
        {
            throw new InvalidOperationException("NutritionSource Override wymaga powodu.");
        }

        var status = NormalizeVariantStatus(request.Status);
        var variant = RowToEntity(row);
        variant.Name = request.Name.Trim();
        variant.VariantType = string.IsNullOrWhiteSpace(request.VariantType) ? "Standard" : request.VariantType.Trim();
        variant.Status = status;
        variant.Description = request.Description?.Trim();
        variant.RawWeightGrams = request.RawWeightGrams;
        variant.CookedWeightGrams = request.CookedWeightGrams;
        variant.CaloriesPer100g = request.CaloriesPer100g;
        variant.ProteinPer100g = request.ProteinPer100g;
        variant.CarbohydratesPer100g = request.CarbohydratesPer100g;
        variant.FatPer100g = request.FatPer100g;
        variant.FiberPer100g = request.FiberPer100g;
        variant.NutritionSource = nutritionSource;
        variant.NutritionOverrideReason = overrideReason;
        variant.AllergensApproved = request.AllergensApproved;
        variant.AllergenOverrideReason = request.AllergenOverrideReason?.Trim();
        variant.UpdatedAt = DateTimeOffset.UtcNow;
        variant.UpdatedBy = this.UserName();

        IReadOnlyList<MealVariantAllergenRow>? allergensToPersist = null;
        if (string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase))
        {
            var result = await this.BuildMealVariantResultAsync(meal, ToRow(variant));
            EnsureResultCanPublish("wariantu", result);
            ApplyResultToVariant(variant, result);
            allergensToPersist = result.Allergens.Select(MapAllergenForPersistence).ToList();
        }
        else
        {
            variant.PublishedAt = string.Equals(row.Status, "Published", StringComparison.OrdinalIgnoreCase)
                ? null
                : row.PublishedAt;
            variant.PublishedBy = string.Equals(row.Status, "Published", StringComparison.OrdinalIgnoreCase)
                ? null
                : row.PublishedBy;
        }

        await this.mealVariantRepository.UpdateAsync(variant);
        if (allergensToPersist is not null)
        {
            await this.mealVariantRepository.ReplaceAllergensAsync(mealVariantId, allergensToPersist);
        }

        await this.InvalidateMealResultCacheAsync();
    }

    public async Task SaveMealVariantComponentAsync(int mealVariantId, SaveMealVariantComponentRequest request)
    {
        _ = await this.mealVariantRepository.GetByIdAsync(mealVariantId)
            ?? throw new InvalidOperationException($"Wariant #{mealVariantId} nie istnieje.");
        var version = await this.recipeComponentRepository.GetVersionAsync(request.RecipeComponentVersionId)
            ?? throw new InvalidOperationException($"Wersja #{request.RecipeComponentVersionId} nie istnieje.");

        if (!string.Equals(version.Status, "Published", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Do wariantu mozna dodac tylko opublikowana wersje skladowej.");
        }

        if (request.QuantityPerServing <= 0)
        {
            throw new InvalidOperationException("Ilosc skladowej na porcje musi byc wieksza od zera.");
        }

        await this.mealVariantRepository.SaveComponentAsync(new MealVariantComponent
        {
            Id = request.Id,
            MealVariantId = mealVariantId,
            RecipeComponentVersionId = request.RecipeComponentVersionId,
            Role = request.Role?.Trim(),
            QuantityPerServing = request.QuantityPerServing,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "portion" : request.Unit.Trim(),
            SortOrder = request.SortOrder,
            IsOptional = request.IsOptional,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = this.UserName(),
            UpdatedAt = request.Id > 0 ? DateTimeOffset.UtcNow : null,
            UpdatedBy = request.Id > 0 ? this.UserName() : null,
        });
        await this.InvalidateMealResultCacheAsync();
    }

    public async Task DeleteMealVariantComponentAsync(int componentId)
    {
        await this.mealVariantRepository.DeleteComponentAsync(componentId, this.UserName());
        await this.InvalidateMealResultCacheAsync();
    }

    public async Task SaveMealVariantPackagingAsync(int mealVariantId, SaveMealVariantPackagingRequest request)
    {
        var variant = await this.mealVariantRepository.GetByIdAsync(mealVariantId)
            ?? throw new InvalidOperationException($"Wariant #{mealVariantId} nie istnieje.");

        if (string.IsNullOrWhiteSpace(request.ResourceName))
        {
            throw new InvalidOperationException("Nazwa opakowania jest wymagana.");
        }

        if (request.Quantity <= 0)
        {
            throw new InvalidOperationException("Ilosc opakowania musi byc wieksza od zera.");
        }

        if (!request.StockItemId.HasValue && !request.WarehouseCategoryId.HasValue)
        {
            throw new InvalidOperationException("Opakowanie wymaga StockItemId albo kategorii magazynowej.");
        }

        await this.mealVariantRepository.SavePackagingAsync(new PackagingRequirement
        {
            Id = request.Id,
            OwnerType = "MealVariant",
            MealId = variant.MealId,
            MealVariantId = mealVariantId,
            StockItemId = request.StockItemId,
            WarehouseCategoryId = request.WarehouseCategoryId,
            ResourceName = request.ResourceName.Trim(),
            Quantity = request.Quantity,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "pcs" : request.Unit.Trim(),
            ContainerRole = request.ContainerRole?.Trim(),
            IsCustomerFacing = request.IsCustomerFacing,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = this.UserName(),
            UpdatedAt = request.Id > 0 ? DateTimeOffset.UtcNow : null,
            UpdatedBy = request.Id > 0 ? this.UserName() : null,
        });
        await this.InvalidateMealResultCacheAsync();
    }

    public async Task DeleteMealVariantPackagingAsync(int mealVariantId, int packagingRequirementId)
    {
        await this.mealVariantRepository.DeletePackagingAsync(
            mealVariantId,
            packagingRequirementId,
            this.UserName());
        await this.InvalidateMealResultCacheAsync();
    }

    public async Task UpdateMealAsync(int mealId, UpdateMealRequest request)
    {
        var meal = await this.mealRepository.GetByIdAsync(mealId);
        if (meal is null)
        {
            return;
        }

        this.mapper.Map(request, meal);
        await this.mealRepository.UpdateAsync(meal);
        await this.InvalidateMealResultCacheAsync();
    }

    public async Task DeleteMealAsync(int mealId)
    {
        await this.mealRepository.DeleteAsync(mealId);
        await this.InvalidateMealResultCacheAsync();
    }

    public async Task PublishMealAsync(int mealId)
    {
        var meal = await this.mealRepository.GetByIdAsync(mealId);
        if (meal is null)
        {
            return;
        }

        var result = await this.BuildMealVariantResultAsync(meal, null);
        EnsureResultCanPublish("posilku", result);

        meal.Status = MealStatus.Published;
        meal.RawWeightGrams = result.FinalRawWeightGrams;
        meal.CookedWeightGrams = result.FinalCookedWeightGrams;
        meal.UpdatedAt = DateTimeOffset.UtcNow;
        meal.UpdatedBy = this.UserName();
        await this.mealRepository.UpdateAsync(meal);
        await this.InvalidateMealResultCacheAsync();
    }

    public async Task PublishMealVariantAsync(int mealVariantId)
    {
        var row = await this.mealVariantRepository.GetByIdAsync(mealVariantId)
            ?? throw new InvalidOperationException($"Wariant #{mealVariantId} nie istnieje.");
        var meal = await this.mealRepository.GetByIdAsync(row.MealId)
            ?? throw new InvalidOperationException($"Posilek #{row.MealId} nie istnieje.");

        var result = await this.BuildMealVariantResultAsync(meal, row);
        EnsureResultCanPublish("wariantu", result);

        var variant = RowToEntity(row);
        variant.Status = "Published";
        variant.UpdatedAt = DateTimeOffset.UtcNow;
        variant.UpdatedBy = this.UserName();
        ApplyResultToVariant(variant, result);

        await this.mealVariantRepository.UpdateAsync(variant);
        await this.mealVariantRepository.ReplaceAllergensAsync(
            mealVariantId,
            result.Allergens.Select(MapAllergenForPersistence).ToList());
        await this.InvalidateMealResultCacheAsync();
    }

    public async Task ArchiveMealAsync(int mealId)
    {
        var meal = await this.mealRepository.GetByIdAsync(mealId);
        if (meal is null)
        {
            return;
        }

        meal.Status = MealStatus.Archived;
        meal.UpdatedAt = DateTimeOffset.UtcNow;
        meal.UpdatedBy = this.UserName();
        await this.mealRepository.UpdateAsync(meal);
        await this.InvalidateMealResultCacheAsync();
    }

    public async Task ArchiveMealVariantAsync(int mealVariantId)
    {
        var row = await this.mealVariantRepository.GetByIdAsync(mealVariantId)
            ?? throw new InvalidOperationException($"Wariant #{mealVariantId} nie istnieje.");
        var variant = RowToEntity(row);
        variant.Status = "Archived";
        variant.PublishedAt = null;
        variant.PublishedBy = null;
        variant.UpdatedAt = DateTimeOffset.UtcNow;
        variant.UpdatedBy = this.UserName();
        await this.mealVariantRepository.UpdateAsync(variant);
        await this.InvalidateMealResultCacheAsync();
    }

    private async Task InvalidateMealResultCacheAsync()
    {
        await this.planningCache.RemoveByPrefixAsync(MenuPlanningCacheKeys.MealResultPrefix);
    }

    private async Task<MealVariantResultDto> BuildMealVariantResultAsync(Meal meal, MealVariantRow? variant)
    {
        var baseComponents = await this.BuildBaseComponentInputsAsync(meal.Id);
        var variantComponents = variant is null
            ? Array.Empty<MealVariantResultComponentInputDto>()
            : await this.BuildVariantComponentInputsAsync(variant.Id);
        var mealPackaging = await this.recipeComponentRepository.GetMealPackagingAsync(meal.Id);
        var variantPackaging = variant is null
            ? Array.Empty<PackagingRequirementRow>()
            : await this.mealVariantRepository.GetPackagingAsync(variant.Id);
        var mealAllergens = await this.mealAllergenRepository.GetDetailsByMealIdAsync(meal.Id);
        var variantAllergens = variant is null
            ? Array.Empty<MealVariantAllergenRow>()
            : await this.mealVariantRepository.GetAllergensAsync(variant.Id);

        return this.resultCalculator.Calculate(new MealVariantResultCalculationRequest
        {
            MealId = meal.Id,
            MealName = meal.Name,
            MealVariantId = variant?.Id,
            MealVariantName = variant?.Name,
            VariantType = variant?.VariantType,
            NutritionSource = variant?.NutritionSource ?? AggregatedNutritionSource,
            OverrideReason = variant?.NutritionOverrideReason,
            ManualRawWeightGrams = variant?.RawWeightGrams ?? meal.RawWeightGrams,
            ManualCookedWeightGrams = variant?.CookedWeightGrams ?? meal.CookedWeightGrams,
            ManualNutritionPer100g = variant is null ? null : MapNutrition(variant),
            AllergensApproved = variant?.AllergensApproved ?? baseComponents.All(component => component.AllergensApproved),
            BaseComponents = baseComponents,
            VariantComponents = variantComponents,
            MealPackagingRequirements = mealPackaging.Select(MapPackagingInput).ToList(),
            VariantPackagingRequirements = variantPackaging.Select(MapPackagingInput).ToList(),
            MealAllergens = mealAllergens.Select(MapMealAllergenInput).ToList(),
            VariantAllergens = variantAllergens.Select(MapVariantAllergenInput).ToList(),
        });
    }

    private async Task<IReadOnlyList<MealVariantResultComponentInputDto>> BuildBaseComponentInputsAsync(int mealId)
    {
        var rows = (await this.recipeComponentRepository.GetMealComponentDetailsAsync(mealId)).ToList();
        var bulk = await this.recipeComponentRepository.GetVersionDetailsBulkAsync(
            rows.Select(row => row.RecipeComponentVersionId));
        var inputs = new List<MealVariantResultComponentInputDto>();

        foreach (var group in rows.GroupBy(row => row.RecipeComponentVersionId))
        {
            var first = group.First();
            var ingredients = first.RecipeComponentVersionId > 0
                ? GetBulkIngredients(bulk, first.RecipeComponentVersionId)
                : group.Where(row => row.IngredientId > 0).Select(MapLegacyIngredientRow).ToList();
            var packaging = first.RecipeComponentVersionId > 0
                ? GetBulkPackaging(bulk, first.RecipeComponentVersionId)
                : Array.Empty<PackagingRequirementRow>();
            var allergens = first.RecipeComponentVersionId > 0
                ? GetBulkAllergens(bulk, first.RecipeComponentVersionId)
                : Array.Empty<RecipeComponentAllergenRow>();

            inputs.Add(MapComponentInput(first, ingredients, packaging, allergens));
        }

        return inputs
            .OrderBy(component => component.SortOrder)
            .ThenBy(component => component.RecipeComponentVersionId)
            .ToList();
    }

    private async Task<IReadOnlyList<MealVariantResultComponentInputDto>> BuildVariantComponentInputsAsync(int mealVariantId)
    {
        var components = await this.mealVariantRepository.GetComponentsAsync(mealVariantId);
        var bulk = await this.recipeComponentRepository.GetVersionDetailsBulkAsync(
            components.Select(component => component.RecipeComponentVersionId));
        var inputs = new List<MealVariantResultComponentInputDto>();

        foreach (var component in components)
        {
            var ingredients = GetBulkIngredients(bulk, component.RecipeComponentVersionId);
            var packaging = GetBulkPackaging(bulk, component.RecipeComponentVersionId);
            var allergens = GetBulkAllergens(bulk, component.RecipeComponentVersionId);
            inputs.Add(MapComponentInput(component, ingredients, packaging, allergens));
        }

        return inputs;
    }

    private static IReadOnlyList<RecipeComponentIngredientRow> GetBulkIngredients(
        RecipeComponentVersionDetailsBulkRow bulk,
        int versionId)
    {
        return bulk.IngredientsByVersionId.TryGetValue(versionId, out var rows)
            ? rows
            : Array.Empty<RecipeComponentIngredientRow>();
    }

    private static IReadOnlyList<PackagingRequirementRow> GetBulkPackaging(
        RecipeComponentVersionDetailsBulkRow bulk,
        int versionId)
    {
        return bulk.PackagingByVersionId.TryGetValue(versionId, out var rows)
            ? rows
            : Array.Empty<PackagingRequirementRow>();
    }

    private static IReadOnlyList<RecipeComponentAllergenRow> GetBulkAllergens(
        RecipeComponentVersionDetailsBulkRow bulk,
        int versionId)
    {
        return bulk.AllergensByVersionId.TryGetValue(versionId, out var rows)
            ? rows
            : Array.Empty<RecipeComponentAllergenRow>();
    }

    private static MealVariantResultComponentInputDto MapComponentInput(
        MealRecipeComponentDetailsRow row,
        IReadOnlyList<RecipeComponentIngredientRow> ingredients,
        IReadOnlyList<PackagingRequirementRow> packaging,
        IReadOnlyList<RecipeComponentAllergenRow> allergens)
    {
        return new MealVariantResultComponentInputDto
        {
            RecipeComponentId = row.RecipeComponentId,
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            ComponentName = row.ComponentName,
            VersionNumber = row.VersionNumber,
            VersionStatus = row.VersionStatus,
            Role = row.Role,
            QuantityPerServing = row.QuantityPerServing,
            Unit = row.Unit,
            SortOrder = row.SortOrder,
            IsOptional = row.IsOptional,
            YieldQuantity = row.YieldQuantity <= 0 ? 1.0m : row.YieldQuantity,
            YieldUnit = string.IsNullOrWhiteSpace(row.YieldUnit) ? "portion" : row.YieldUnit,
            RawWeightGrams = row.RawWeightGrams,
            CookedWeightGrams = row.CookedWeightGrams,
            NutritionPer100g = MapNutrition(row),
            AllergensApproved = row.AllergensApproved,
            Ingredients = ingredients.Select(MapIngredientInput).ToList(),
            PackagingRequirements = packaging.Select(MapPackagingInput).ToList(),
            Allergens = allergens.Select(MapComponentAllergenInput).ToList(),
        };
    }

    private static MealVariantResultComponentInputDto MapComponentInput(
        MealVariantComponentRow row,
        IReadOnlyList<RecipeComponentIngredientRow> ingredients,
        IReadOnlyList<PackagingRequirementRow> packaging,
        IReadOnlyList<RecipeComponentAllergenRow> allergens)
    {
        return new MealVariantResultComponentInputDto
        {
            RecipeComponentId = row.RecipeComponentId,
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            ComponentName = row.ComponentName,
            VersionNumber = row.VersionNumber,
            VersionStatus = row.VersionStatus,
            Role = row.Role,
            QuantityPerServing = row.QuantityPerServing,
            Unit = row.Unit,
            SortOrder = row.SortOrder,
            IsOptional = row.IsOptional,
            YieldQuantity = row.YieldQuantity <= 0 ? 1.0m : row.YieldQuantity,
            YieldUnit = string.IsNullOrWhiteSpace(row.YieldUnit) ? "portion" : row.YieldUnit,
            RawWeightGrams = row.RawWeightGrams,
            CookedWeightGrams = row.CookedWeightGrams,
            NutritionPer100g = MapNutrition(row),
            AllergensApproved = row.AllergensApproved,
            Ingredients = ingredients.Select(MapIngredientInput).ToList(),
            PackagingRequirements = packaging.Select(MapPackagingInput).ToList(),
            Allergens = allergens.Select(MapComponentAllergenInput).ToList(),
        };
    }

    private static List<MealRecipeComponentDto> MapMealComponents(IReadOnlyList<MealRecipeComponentDetailsRow> componentRows)
    {
        return componentRows
            .GroupBy(row => row.RecipeComponentVersionId)
            .Select(group =>
            {
                var first = group.First();
                return new MealRecipeComponentDto
                {
                    RecipeComponentId = first.RecipeComponentId,
                    RecipeComponentVersionId = first.RecipeComponentVersionId,
                    ComponentName = first.ComponentName,
                    VersionNumber = first.VersionNumber,
                    VersionStatus = first.VersionStatus,
                    Role = first.Role,
                    QuantityPerServing = first.QuantityPerServing,
                    Unit = first.Unit,
                    SortOrder = first.SortOrder,
                    Instructions = first.Instructions,
                    ShelfLifeHours = first.ShelfLifeHours,
                    UseEarliestIngredientExpiry = first.UseEarliestIngredientExpiry,
                    Ingredients = group
                        .Where(row => row.IngredientId > 0)
                        .Select(row => new RecipeComponentIngredientDto
                        {
                            IngredientId = row.IngredientId,
                            IngredientName = row.IngredientName,
                            StockItemId = row.StockItemId,
                            WarehouseCategoryId = row.WarehouseCategoryId,
                            WarehouseCategoryName = row.WarehouseCategoryName,
                            WeightInGrams = row.WeightInGrams,
                            YieldFactor = row.YieldFactor <= 0 ? 1.0m : row.YieldFactor,
                            IsOptional = row.IsOptional,
                            Notes = row.Notes,
                        })
                        .ToList(),
                };
            })
            .OrderBy(component => component.SortOrder)
            .ToList();
    }

    private static MealVariantDto MapMealVariant(
        MealVariantRow row,
        IReadOnlyList<MealVariantComponentRow> components,
        IReadOnlyList<PackagingRequirementRow> packaging,
        MealVariantResultDto result)
    {
        return new MealVariantDto
        {
            Id = row.Id,
            MealId = row.MealId,
            Name = row.Name,
            VariantType = row.VariantType,
            Status = row.Status,
            Description = row.Description,
            IsDefault = row.IsDefault,
            RawWeightGrams = row.RawWeightGrams,
            CookedWeightGrams = row.CookedWeightGrams,
            CaloriesPer100g = row.CaloriesPer100g,
            ProteinPer100g = row.ProteinPer100g,
            CarbohydratesPer100g = row.CarbohydratesPer100g,
            FatPer100g = row.FatPer100g,
            FiberPer100g = row.FiberPer100g,
            NutritionSource = row.NutritionSource,
            NutritionOverrideReason = row.NutritionOverrideReason,
            AllergensApproved = row.AllergensApproved,
            AllergenOverrideReason = row.AllergenOverrideReason,
            PublishedAt = row.PublishedAt,
            PublishedBy = row.PublishedBy,
            ValidationWarnings = result.ValidationWarnings.ToList(),
            IsComplete = result.IsComplete,
            Allergens = result.Allergens.Select(allergen => allergen.Name).ToList(),
            Result = result,
            PackagingRequirements = packaging.Select(MapPackagingEdit).ToList(),
            Components = components.Select(component => new MealVariantComponentDto
            {
                Id = component.Id,
                MealVariantId = component.MealVariantId,
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
            }).ToList(),
        };
    }

    private static RecipeComponentIngredientRow MapLegacyIngredientRow(MealRecipeComponentDetailsRow row)
        => new()
        {
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            IngredientId = row.IngredientId,
            IngredientName = row.IngredientName,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            WarehouseCategoryName = row.WarehouseCategoryName,
            WeightInGrams = row.WeightInGrams,
            YieldFactor = row.YieldFactor <= 0 ? 1.0m : row.YieldFactor,
            IsOptional = row.IsOptional,
            Notes = row.Notes,
        };

    private static MealVariantResultIngredientInputDto MapIngredientInput(RecipeComponentIngredientRow row)
        => new()
        {
            IngredientId = row.IngredientId,
            IngredientName = row.IngredientName,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            WarehouseCategoryName = row.WarehouseCategoryName,
            WeightInGrams = row.WeightInGrams,
            YieldFactor = row.YieldFactor <= 0 ? 1.0m : row.YieldFactor,
            IsOptional = row.IsOptional,
            Notes = row.Notes,
        };

    private static MealVariantResultPackagingInputDto MapPackagingInput(PackagingRequirementRow row)
        => new()
        {
            OwnerType = row.OwnerType,
            MealId = row.MealId,
            MealVariantId = row.MealVariantId,
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            ResourceName = row.ResourceName,
            Quantity = row.Quantity,
            Unit = row.Unit,
            ContainerRole = row.ContainerRole,
            IsCustomerFacing = row.IsCustomerFacing,
        };

    private static MealVariantResultAllergenInputDto MapComponentAllergenInput(RecipeComponentAllergenRow row)
        => new()
        {
            AllergenId = row.AllergenId,
            Name = row.Name,
            IsTrace = row.IsTrace,
            SourceType = row.SourceType,
            SourceName = row.SourceName,
        };

    private static MealVariantResultAllergenInputDto MapMealAllergenInput(MealAllergenRow row)
        => new()
        {
            AllergenId = row.AllergenId,
            Name = row.Name,
            IsTrace = row.IsTrace,
            SourceType = "Meal",
        };

    private static MealVariantResultAllergenInputDto MapVariantAllergenInput(MealVariantAllergenRow row)
        => new()
        {
            AllergenId = row.AllergenId,
            Name = row.Name,
            IsTrace = row.IsTrace,
            SourceType = row.SourceType,
            SourceName = row.SourceName,
        };

    private static MealVariantAllergenRow MapAllergenForPersistence(MealVariantResultAllergenDto allergen)
        => new()
        {
            AllergenId = allergen.AllergenId,
            Name = allergen.Name,
            IsTrace = allergen.IsTrace,
            SourceType = "Aggregated",
        };

    private static MealListItemDto MapListItem(MealListRow row)
        => new()
        {
            Id = row.Id,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            Name = row.Name,
            Description = row.Description,
            MarketingDescription = row.MarketingDescription,
            Status = row.Status,
            PreparationTimeMinutes = row.PreparationTimeMinutes,
            IsActive = row.IsActive,
            RawWeightGrams = row.RawWeightGrams,
            CookedWeightGrams = row.CookedWeightGrams,
            CaloriesPer100g = row.CaloriesPer100g,
            ProteinPer100g = row.ProteinPer100g,
            CarbohydratesPer100g = row.CarbohydratesPer100g,
            FatPer100g = row.FatPer100g,
            FiberPer100g = row.FiberPer100g,
            VariantCount = row.VariantCount,
            PublishedVariantCount = row.PublishedVariantCount,
            ComponentCount = row.ComponentCount,
            PackagingRequirementCount = row.PackagingRequirementCount,
            AllergenNames = row.AllergenNames,
            HasNutrition = row.HasNutrition,
            MissingPackaging = row.MissingPackaging,
            MissingPublicationData = row.MissingPublicationData,
        };

    private static MenuPlanMealLookupDto MapPlanningMealLookup(MealListRow row)
    {
        var warnings = BuildMealPlanningWarnings(row);
        return new MenuPlanMealLookupDto
        {
            MealId = row.Id,
            MealName = row.Name,
            CategoryName = row.CategoryName,
            Status = row.Status,
            VariantCount = row.VariantCount,
            WarningCount = warnings.Count,
            CompletenessStatus = warnings.Count == 0 ? "Complete" : "Incomplete",
            Warnings = warnings,
        };
    }

    private static MenuPlanMealVariantLookupDto MapPlanningMealVariantLookup(
        int? mealVariantId,
        string name,
        string status,
        bool isDefault,
        MealVariantResultDto? result)
    {
        var warnings = BuildMealVariantPlanningWarnings(status, result);
        return new MenuPlanMealVariantLookupDto
        {
            MealVariantId = mealVariantId,
            Name = name,
            Status = status,
            IsDefault = isDefault,
            FinalWeightGrams = result?.FinalWeightGrams,
            WarningCount = warnings.Count,
            CompletenessStatus = warnings.Count == 0 ? "Complete" : result?.CompletenessStatus ?? "Incomplete",
            Warnings = warnings,
        };
    }

    private static List<string> BuildMealPlanningWarnings(MealListRow row)
    {
        var warnings = new List<string>();
        if (row.ComponentCount <= 0)
        {
            warnings.Add("brak skladowych");
        }

        if (row.LegacyRecipeCount > 0)
        {
            warnings.Add("legacy Recipes - brak wersjonowanych skladowych");
        }

        if (row.VariantCount <= 0)
        {
            warnings.Add("brak wariantow dania");
        }

        if (!HasPositiveWeight(row.RawWeightGrams, row.CookedWeightGrams))
        {
            warnings.Add("brak gramatury");
        }

        if (!row.HasNutrition)
        {
            warnings.Add("brak nutrition");
        }

        if (row.MissingPackaging)
        {
            warnings.Add("brak opakowan");
        }

        if (row.MissingPublicationData && warnings.Count == 0)
        {
            warnings.Add("braki publikacyjne");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> BuildMealVariantPlanningWarnings(string status, MealVariantResultDto? result)
    {
        var warnings = new List<string>();
        if (!IsPlanningLookupStatus(status))
        {
            warnings.Add($"status wariantu: {status}");
        }

        if (result is null)
        {
            warnings.Add("brak wyniku kalkulatora");
        }
        else if (!result.IsComplete)
        {
            warnings.AddRange(result.ValidationWarnings);
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool IsPlanningLookupStatus(string? status)
        => string.Equals(status, "Published", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase);

    private static bool HasPositiveWeight(params decimal?[] values)
        => values.Any(value => value.HasValue && value.Value > 0);

    private static PackagingRequirementEditDto MapPackagingEdit(PackagingRequirementRow row)
        => new()
        {
            Id = row.Id,
            OwnerType = row.OwnerType,
            MealId = row.MealId,
            MealVariantId = row.MealVariantId,
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            ResourceName = row.ResourceName,
            Quantity = row.Quantity,
            Unit = row.Unit,
            ContainerRole = row.ContainerRole,
            IsCustomerFacing = row.IsCustomerFacing,
        };

    private static MealVariantNutritionDto? MapNutrition(MealVariantRow row)
        => HasCompleteNutrition(row.CaloriesPer100g, row.ProteinPer100g, row.CarbohydratesPer100g, row.FatPer100g, row.FiberPer100g)
            ? new MealVariantNutritionDto
            {
                Calories = row.CaloriesPer100g!.Value,
                Protein = row.ProteinPer100g!.Value,
                Carbohydrates = row.CarbohydratesPer100g!.Value,
                Fat = row.FatPer100g!.Value,
                Fiber = row.FiberPer100g!.Value,
            }
            : null;

    private static MealVariantNutritionDto? MapNutrition(MealRecipeComponentDetailsRow row)
        => HasCompleteNutrition(row.CaloriesPer100g, row.ProteinPer100g, row.CarbohydratesPer100g, row.FatPer100g, row.FiberPer100g)
            ? new MealVariantNutritionDto
            {
                Calories = row.CaloriesPer100g!.Value,
                Protein = row.ProteinPer100g!.Value,
                Carbohydrates = row.CarbohydratesPer100g!.Value,
                Fat = row.FatPer100g!.Value,
                Fiber = row.FiberPer100g!.Value,
            }
            : null;

    private static MealVariantNutritionDto? MapNutrition(MealVariantComponentRow row)
        => HasCompleteNutrition(row.CaloriesPer100g, row.ProteinPer100g, row.CarbohydratesPer100g, row.FatPer100g, row.FiberPer100g)
            ? new MealVariantNutritionDto
            {
                Calories = row.CaloriesPer100g!.Value,
                Protein = row.ProteinPer100g!.Value,
                Carbohydrates = row.CarbohydratesPer100g!.Value,
                Fat = row.FatPer100g!.Value,
                Fiber = row.FiberPer100g!.Value,
            }
            : null;

    private static bool HasCompleteNutrition(params decimal?[] values)
        => values.All(value => value.HasValue);

    private static MealVariant RowToEntity(MealVariantRow row)
        => new()
        {
            Id = row.Id,
            MealId = row.MealId,
            Name = row.Name,
            VariantType = row.VariantType,
            Status = row.Status,
            Description = row.Description,
            IsDefault = row.IsDefault,
            RawWeightGrams = row.RawWeightGrams,
            CookedWeightGrams = row.CookedWeightGrams,
            CaloriesPer100g = row.CaloriesPer100g,
            ProteinPer100g = row.ProteinPer100g,
            CarbohydratesPer100g = row.CarbohydratesPer100g,
            FatPer100g = row.FatPer100g,
            FiberPer100g = row.FiberPer100g,
            NutritionSource = row.NutritionSource,
            NutritionOverrideReason = row.NutritionOverrideReason,
            AllergensApproved = row.AllergensApproved,
            AllergenOverrideReason = row.AllergenOverrideReason,
            PublishedAt = row.PublishedAt,
            PublishedBy = row.PublishedBy,
        };

    private static MealVariantRow ToRow(MealVariant variant)
        => new()
        {
            Id = variant.Id,
            MealId = variant.MealId,
            Name = variant.Name,
            VariantType = variant.VariantType,
            Status = variant.Status,
            Description = variant.Description,
            IsDefault = variant.IsDefault,
            RawWeightGrams = variant.RawWeightGrams,
            CookedWeightGrams = variant.CookedWeightGrams,
            CaloriesPer100g = variant.CaloriesPer100g,
            ProteinPer100g = variant.ProteinPer100g,
            CarbohydratesPer100g = variant.CarbohydratesPer100g,
            FatPer100g = variant.FatPer100g,
            FiberPer100g = variant.FiberPer100g,
            NutritionSource = variant.NutritionSource,
            NutritionOverrideReason = variant.NutritionOverrideReason,
            AllergensApproved = variant.AllergensApproved,
            AllergenOverrideReason = variant.AllergenOverrideReason,
            PublishedAt = variant.PublishedAt,
            PublishedBy = variant.PublishedBy,
        };

    private static void ApplyResultToVariant(MealVariant variant, MealVariantResultDto result)
    {
        variant.RawWeightGrams = result.FinalRawWeightGrams;
        variant.CookedWeightGrams = result.FinalCookedWeightGrams;
        if (result.NutritionPer100g is not null)
        {
            variant.CaloriesPer100g = result.NutritionPer100g.Calories;
            variant.ProteinPer100g = result.NutritionPer100g.Protein;
            variant.CarbohydratesPer100g = result.NutritionPer100g.Carbohydrates;
            variant.FatPer100g = result.NutritionPer100g.Fat;
            variant.FiberPer100g = result.NutritionPer100g.Fiber;
        }

        variant.PublishedAt = DateTimeOffset.UtcNow;
        variant.PublishedBy = variant.UpdatedBy;
    }

    private static void EnsureResultCanPublish(string targetName, MealVariantResultDto result)
    {
        if (!result.IsComplete)
        {
            throw new InvalidOperationException(
                $"Nie mozna opublikowac {targetName}: {string.Join("; ", result.ValidationWarnings)}");
        }
    }

    private static string NormalizeNutritionSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return AggregatedNutritionSource;
        }

        var normalized = source.Trim();
        return string.Equals(normalized, "Manual", StringComparison.OrdinalIgnoreCase)
            ? OverrideNutritionSource
            : normalized;
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeVariantStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return "Draft";
        }

        var normalized = status.Trim();
        var allowed = new[] { "Draft", "Ready", "Published", "Archived" };
        if (!allowed.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Nieznany status wariantu '{status}'.");
        }

        return allowed.First(value => string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase));
    }

    private string? UserName() => this.currentUser.GetUserName();
}
