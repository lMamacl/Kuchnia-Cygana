using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class RecipeComponentManagementService : IRecipeComponentManagementService
{
    private readonly IRecipeComponentRepository repository;
    private readonly ICurrentUserService currentUser;

    public RecipeComponentManagementService(
        IRecipeComponentRepository repository,
        ICurrentUserService currentUser)
    {
        this.repository = repository;
        this.currentUser = currentUser;
    }

    public async Task<PagedResultDto<RecipeComponentListItemDto>> SearchAsync(RecipeComponentSearchFilterDto filter)
    {
        var page = filter.Page <= 0 ? 1 : filter.Page;
        var pageSize = Math.Clamp(filter.PageSize <= 0 ? 25 : filter.PageSize, 1, 100);
        var rows = await this.repository.SearchComponentsAsync(new RecipeComponentSearchQuery
        {
            Query = Normalize(filter.Query),
            CategoryId = filter.CategoryId,
            VersionStatus = Normalize(filter.VersionStatus),
            AllergenId = filter.AllergenId,
            MissingPublicationData = filter.MissingPublicationData,
            Page = page,
            PageSize = pageSize,
        });

        filter.Page = page;
        filter.PageSize = pageSize;

        return new PagedResultDto<RecipeComponentListItemDto>
        {
            Items = rows.Items.Select(MapListItem).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = rows.TotalCount,
        };
    }

    public async Task<IReadOnlyList<RecipeComponentVersionOptionDto>> GetPublishedVersionOptionsAsync()
    {
        var rows = await this.repository.GetPublishedVersionOptionsAsync();
        return rows.Select(row => new RecipeComponentVersionOptionDto
        {
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            RecipeComponentId = row.RecipeComponentId,
            ComponentName = row.ComponentName,
            VersionNumber = row.VersionNumber,
        }).ToList();
    }

    public async Task<RecipeComponentDetailDto?> GetComponentAsync(int componentId)
    {
        var component = await this.repository.GetComponentAsync(componentId);
        if (component is null)
        {
            return null;
        }

        var versions = await this.repository.GetComponentVersionsAsync(componentId);

        return new RecipeComponentDetailDto
        {
            Id = component.Id,
            CategoryId = component.CategoryId,
            CategoryName = component.CategoryName,
            Name = component.Name,
            Description = component.Description,
            ImageUrl = component.ImageUrl,
            PreparationTimeMinutes = component.PreparationTimeMinutes,
            IsActive = component.IsActive,
            Versions = await this.MapVersionSummariesAsync(versions),
        };
    }

    public async Task<RecipeComponentVersionDetailDto?> GetVersionAsync(int versionId)
    {
        var version = await this.repository.GetVersionAsync(versionId);
        if (version is null)
        {
            return null;
        }

        var ingredients = await this.repository.GetVersionIngredientsAsync(versionId) ?? Array.Empty<RecipeComponentIngredientRow>();
        var packaging = await this.repository.GetVersionPackagingAsync(versionId) ?? Array.Empty<PackagingRequirementRow>();
        var sections = await this.repository.GetVersionInstructionSectionsAsync(versionId) ?? Array.Empty<RecipeComponentInstructionSectionRow>();
        var warnings = BuildValidationWarnings(version, ingredients, packaging, sections);

        return MapVersionDetail(version, ingredients, packaging, sections, warnings);
    }

    public async Task<int> CreateComponentAsync(CreateRecipeComponentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Nazwa skladowej jest wymagana.");
        }

        return await this.repository.CreateComponentAsync(new RecipeComponent
        {
            Name = request.Name.Trim(),
            CategoryId = request.CategoryId,
            Description = request.Description?.Trim(),
            ImageUrl = request.ImageUrl?.Trim(),
            PreparationTimeMinutes = Math.Max(0, request.PreparationTimeMinutes),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = this.UserName(),
        });
    }

    public async Task<int> CreateVersionAsync(CreateRecipeComponentVersionRequest request)
    {
        var component = await this.repository.GetComponentAsync(request.RecipeComponentId)
            ?? throw new InvalidOperationException($"Skladowa #{request.RecipeComponentId} nie istnieje.");

        var version = new RecipeComponentVersion
        {
            RecipeComponentId = component.Id,
            Status = "Draft",
            YieldQuantity = 1.0m,
            YieldUnit = "portion",
            ChangeSummary = request.ChangeSummary?.Trim(),
            IsTechnologyChange = true,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = this.UserName(),
        };

        if (request.SourceVersionId.HasValue)
        {
            var source = await this.repository.GetVersionAsync(request.SourceVersionId.Value)
                ?? throw new InvalidOperationException($"Wersja zrodlowa #{request.SourceVersionId.Value} nie istnieje.");

            if (source.RecipeComponentId != component.Id)
            {
                throw new InvalidOperationException("Wersja zrodlowa nalezy do innej skladowej.");
            }

            version.Instructions = source.Instructions;
            version.YieldQuantity = source.YieldQuantity;
            version.YieldUnit = source.YieldUnit;
            version.RawWeightGrams = source.RawWeightGrams;
            version.CookedWeightGrams = source.CookedWeightGrams;
            version.CaloriesPer100g = source.CaloriesPer100g;
            version.ProteinPer100g = source.ProteinPer100g;
            version.CarbohydratesPer100g = source.CarbohydratesPer100g;
            version.FatPer100g = source.FatPer100g;
            version.FiberPer100g = source.FiberPer100g;
            version.ShelfLifeHours = source.ShelfLifeHours;
            version.UseEarliestIngredientExpiry = source.UseEarliestIngredientExpiry;
            version.NutritionSource = source.NutritionSource;
            version.NutritionOverrideReason = source.NutritionOverrideReason;
            version.AllergensApproved = source.AllergensApproved;
            version.AllergenOverrideReason = source.AllergenOverrideReason;
            version.AllergensApprovedAt = source.AllergensApprovedAt;
            version.AllergensApprovedBy = source.AllergensApprovedBy;
        }

        return await this.repository.CreateVersionAsync(version, request.SourceVersionId);
    }

    public async Task UpdateVersionAsync(int versionId, UpdateRecipeComponentVersionRequest request)
    {
        var existing = await this.repository.GetVersionAsync(versionId)
            ?? throw new InvalidOperationException($"Wersja #{versionId} nie istnieje.");
        var nutritionSource = NormalizeNutritionSource(request.NutritionSource);
        var nutritionOverrideReason = request.NutritionOverrideReason?.Trim();
        var allergenApprovalSource = NormalizeAllergenApprovalSource(request.AllergenApprovalSource);
        var allergenOverrideReason = request.AllergenOverrideReason?.Trim();

        if (nutritionSource == "Override" && string.IsNullOrWhiteSpace(nutritionOverrideReason))
        {
            throw new InvalidOperationException("NutritionSource Override wymaga powodu.");
        }

        if (request.AllergensApproved
            && allergenApprovalSource == "Override"
            && string.IsNullOrWhiteSpace(allergenOverrideReason))
        {
            throw new InvalidOperationException("Zatwierdzenie alergenow poza danymi skladnikow wymaga powodu.");
        }

        if (existing.Status == "Draft")
        {
            await this.repository.UpdateDraftVersionAsync(new RecipeComponentVersion
            {
                Id = versionId,
                Instructions = request.Instructions,
                YieldQuantity = request.YieldQuantity,
                YieldUnit = string.IsNullOrWhiteSpace(request.YieldUnit) ? "portion" : request.YieldUnit.Trim(),
                RawWeightGrams = request.RawWeightGrams,
                CookedWeightGrams = request.CookedWeightGrams,
                CaloriesPer100g = request.CaloriesPer100g,
                ProteinPer100g = request.ProteinPer100g,
                CarbohydratesPer100g = request.CarbohydratesPer100g,
                FatPer100g = request.FatPer100g,
                FiberPer100g = request.FiberPer100g,
                ShelfLifeHours = request.ShelfLifeHours,
                UseEarliestIngredientExpiry = request.UseEarliestIngredientExpiry,
                NutritionSource = nutritionSource,
                NutritionOverrideReason = nutritionOverrideReason,
                AllergensApproved = request.AllergensApproved,
                AllergenOverrideReason = allergenOverrideReason,
                AllergensApprovedAt = request.AllergensApproved ? DateTimeOffset.UtcNow : null,
                AllergensApprovedBy = request.AllergensApproved ? this.UserName() : null,
                ChangeSummary = request.ChangeSummary?.Trim(),
                IsTechnologyChange = request.IsTechnologyChange,
                NonTechnologyChangeReason = request.NonTechnologyChangeReason?.Trim(),
                UpdatedAt = DateTimeOffset.UtcNow,
                UpdatedBy = this.UserName(),
            });
            return;
        }

        if (existing.Status == "Published" && !request.IsTechnologyChange)
        {
            if (string.IsNullOrWhiteSpace(request.NonTechnologyChangeReason))
            {
                throw new InvalidOperationException("Zmiana nietechnologiczna opublikowanej wersji wymaga powodu.");
            }

            await this.repository.UpdatePublishedNonTechnologyAsync(
                versionId,
                request.Instructions,
                request.ChangeSummary?.Trim(),
                request.NonTechnologyChangeReason.Trim(),
                this.UserName());
            return;
        }

        throw new InvalidOperationException("Zmiana technologiczna opublikowanej lub zarchiwizowanej wersji wymaga utworzenia nowej wersji.");
    }

    public async Task SaveIngredientAsync(SaveComponentIngredientRequest request)
    {
        var version = await this.repository.GetVersionAsync(request.RecipeComponentVersionId)
            ?? throw new InvalidOperationException($"Wersja #{request.RecipeComponentVersionId} nie istnieje.");

        this.EnsureDraft(version);

        if (request.IngredientId <= 0)
        {
            throw new InvalidOperationException("Wybierz skladnik.");
        }

        if (request.WeightInGrams <= 0)
        {
            throw new InvalidOperationException("Gramatura skladnika musi byc wieksza od zera.");
        }

        if (!request.WarehouseCategoryId.HasValue)
        {
            throw new InvalidOperationException("Skladnik wymaga kategorii magazynowej.");
        }

        await this.repository.SaveIngredientAsync(new RecipeComponentIngredient
        {
            Id = request.Id,
            RecipeComponentVersionId = request.RecipeComponentVersionId,
            IngredientId = request.IngredientId,
            StockItemId = request.StockItemId,
            WarehouseCategoryId = request.WarehouseCategoryId,
            WeightInGrams = request.WeightInGrams,
            YieldFactor = request.YieldFactor <= 0 ? 1.0m : request.YieldFactor,
            IsOptional = request.IsOptional,
            Notes = request.Notes?.Trim(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = this.UserName(),
            UpdatedAt = request.Id > 0 ? DateTimeOffset.UtcNow : null,
            UpdatedBy = request.Id > 0 ? this.UserName() : null,
        });
    }

    public async Task DeleteIngredientAsync(int ingredientId)
    {
        await this.repository.DeleteIngredientAsync(ingredientId, this.UserName());
    }

    public async Task SavePackagingAsync(SavePackagingRequirementRequest request)
    {
        if (!request.RecipeComponentVersionId.HasValue)
        {
            throw new InvalidOperationException("Opakowanie musi byc przypisane do wersji skladowej.");
        }

        var version = await this.repository.GetVersionAsync(request.RecipeComponentVersionId.Value)
            ?? throw new InvalidOperationException($"Wersja #{request.RecipeComponentVersionId.Value} nie istnieje.");

        this.EnsureDraft(version);

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

        await this.repository.SavePackagingAsync(new PackagingRequirement
        {
            Id = request.Id,
            OwnerType = "RecipeComponentVersion",
            MealVariantId = request.MealVariantId,
            RecipeComponentVersionId = request.RecipeComponentVersionId,
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
    }

    public async Task DeletePackagingAsync(int packagingRequirementId)
    {
        await this.repository.DeletePackagingAsync(packagingRequirementId, this.UserName());
    }

    public async Task SaveInstructionSectionAsync(SaveInstructionSectionRequest request)
    {
        var version = await this.repository.GetVersionAsync(request.RecipeComponentVersionId)
            ?? throw new InvalidOperationException($"Wersja #{request.RecipeComponentVersionId} nie istnieje.");
        this.EnsureDraft(version);

        await this.repository.SaveInstructionSectionAsync(new RecipeComponentInstructionSection
        {
            Id = request.Id,
            RecipeComponentVersionId = request.RecipeComponentVersionId,
            Title = request.Title?.Trim(),
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = this.UserName(),
            UpdatedAt = request.Id > 0 ? DateTimeOffset.UtcNow : null,
            UpdatedBy = request.Id > 0 ? this.UserName() : null,
        });
    }

    public async Task SaveInstructionStepAsync(SaveInstructionStepRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StepText))
        {
            throw new InvalidOperationException("Treść kroku jest wymagana.");
        }

        await this.repository.SaveInstructionStepAsync(new RecipeComponentInstructionStep
        {
            Id = request.Id,
            RecipeComponentInstructionSectionId = request.RecipeComponentInstructionSectionId,
            StepText = request.StepText.Trim(),
            SortOrder = request.SortOrder,
            RequiresControl = request.RequiresControl,
            ControlType = request.ControlType?.Trim(),
            ExpectedValue = request.ExpectedValue,
            ExpectedUnit = request.ExpectedUnit?.Trim(),
            IsCritical = request.IsCritical,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = this.UserName(),
            UpdatedAt = request.Id > 0 ? DateTimeOffset.UtcNow : null,
            UpdatedBy = request.Id > 0 ? this.UserName() : null,
        });
    }

    public async Task DeleteInstructionSectionAsync(int sectionId)
    {
        await this.repository.DeleteInstructionSectionAsync(sectionId, this.UserName());
    }

    public async Task DeleteInstructionStepAsync(int stepId)
    {
        await this.repository.DeleteInstructionStepAsync(stepId, this.UserName());
    }

    public async Task PublishVersionAsync(int versionId)
    {
        var version = await this.repository.GetVersionAsync(versionId)
            ?? throw new InvalidOperationException($"Wersja #{versionId} nie istnieje.");
        this.EnsureDraft(version);

        var ingredients = await this.repository.GetVersionIngredientsAsync(versionId);
        var packaging = await this.repository.GetVersionPackagingAsync(versionId);
        var sections = await this.repository.GetVersionInstructionSectionsAsync(versionId);
        var warnings = BuildValidationWarnings(version, ingredients, packaging, sections);
        if (warnings.Count > 0)
        {
            throw new InvalidOperationException("Nie mozna opublikowac wersji: " + string.Join("; ", warnings));
        }

        await this.repository.PublishVersionAsync(versionId, this.UserName());
    }

    public async Task AttachComponentToMealAsync(AttachComponentToMealRequest request)
    {
        if (request.MealId <= 0)
        {
            throw new InvalidOperationException("Brak posilku do przypiecia skladowej.");
        }

        var version = await this.repository.GetVersionAsync(request.RecipeComponentVersionId)
            ?? throw new InvalidOperationException($"Wersja #{request.RecipeComponentVersionId} nie istnieje.");

        if (version.Status != "Published")
        {
            throw new InvalidOperationException("Do posilku mozna przypiac tylko opublikowana wersje skladowej.");
        }

        if (request.QuantityPerServing <= 0)
        {
            throw new InvalidOperationException("Ilosc skladowej na porcje musi byc wieksza od zera.");
        }

        await this.repository.AttachComponentToMealAsync(new MealRecipeComponent
        {
            MealId = request.MealId,
            RecipeComponentVersionId = request.RecipeComponentVersionId,
            Role = request.Role?.Trim(),
            QuantityPerServing = request.QuantityPerServing,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "portion" : request.Unit.Trim(),
            SortOrder = request.SortOrder,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = this.UserName(),
        });
    }

    private static RecipeComponentListItemDto MapListItem(RecipeComponentListRow row)
    {
        return new RecipeComponentListItemDto
        {
            Id = row.Id,
            CategoryId = row.CategoryId,
            CategoryName = row.CategoryName,
            Name = row.Name,
            Description = row.Description,
            ImageUrl = row.ImageUrl,
            PreparationTimeMinutes = row.PreparationTimeMinutes,
            IsActive = row.IsActive,
            VersionCount = row.VersionCount,
            LatestVersionId = row.LatestVersionId,
            LatestVersionNumber = row.LatestVersionNumber,
            LatestVersionStatus = row.LatestVersionStatus,
            HasPublicationGaps = row.HasPublicationGaps,
            PublicationGapCount = row.PublicationGapCount,
        };
    }

    private async Task<List<RecipeComponentVersionSummaryDto>> MapVersionSummariesAsync(
        IReadOnlyList<RecipeComponentVersionRow> versions)
    {
        var summaries = new List<RecipeComponentVersionSummaryDto>();
        foreach (var version in versions)
        {
            var ingredients = await this.repository.GetVersionIngredientsAsync(version.Id);
            var packaging = await this.repository.GetVersionPackagingAsync(version.Id);
            var sections = await this.repository.GetVersionInstructionSectionsAsync(version.Id);

            summaries.Add(new RecipeComponentVersionSummaryDto
            {
                Id = version.Id,
                VersionNumber = version.VersionNumber,
                Status = version.Status,
                ChangeSummary = version.ChangeSummary,
                PublishedAt = version.PublishedAt,
                IsComplete = BuildValidationWarnings(version, ingredients, packaging, sections).Count == 0,
            });
        }

        return summaries;
    }

    private static RecipeComponentVersionDetailDto MapVersionDetail(
        RecipeComponentVersionRow version,
        IReadOnlyList<RecipeComponentIngredientRow> ingredients,
        IReadOnlyList<PackagingRequirementRow> packaging,
        IReadOnlyList<RecipeComponentInstructionSectionRow> sections,
        List<string> warnings)
    {
        return new RecipeComponentVersionDetailDto
        {
            Id = version.Id,
            RecipeComponentId = version.RecipeComponentId,
            ComponentName = version.ComponentName,
            VersionNumber = version.VersionNumber,
            Status = version.Status,
            Instructions = version.Instructions,
            YieldQuantity = version.YieldQuantity,
            YieldUnit = version.YieldUnit,
            RawWeightGrams = version.RawWeightGrams,
            CookedWeightGrams = version.CookedWeightGrams,
            CaloriesPer100g = version.CaloriesPer100g,
            ProteinPer100g = version.ProteinPer100g,
            CarbohydratesPer100g = version.CarbohydratesPer100g,
            FatPer100g = version.FatPer100g,
            FiberPer100g = version.FiberPer100g,
            ShelfLifeHours = version.ShelfLifeHours,
            UseEarliestIngredientExpiry = version.UseEarliestIngredientExpiry,
            NutritionSource = version.NutritionSource,
            NutritionOverrideReason = version.NutritionOverrideReason,
            AllergensApproved = version.AllergensApproved,
            AllergenApprovalSource = string.IsNullOrWhiteSpace(version.AllergenOverrideReason)
                ? "DerivedFromIngredients"
                : "Override",
            AllergenOverrideReason = version.AllergenOverrideReason,
            AllergensApprovedAt = version.AllergensApprovedAt,
            AllergensApprovedBy = version.AllergensApprovedBy,
            ChangeSummary = version.ChangeSummary,
            IsTechnologyChange = version.IsTechnologyChange,
            NonTechnologyChangeReason = version.NonTechnologyChangeReason,
            PublishedAt = version.PublishedAt,
            PublishedBy = version.PublishedBy,
            Ingredients = ingredients.Select(MapIngredient).ToList(),
            PackagingRequirements = packaging.Select(MapPackaging).ToList(),
            InstructionSections = sections.Select(MapInstructionSection).ToList(),
            Comparison = BuildComparison(version, ingredients),
            ValidationWarnings = warnings,
        };
    }

    private static RecipeComponentIngredientEditDto MapIngredient(RecipeComponentIngredientRow row)
    {
        return new RecipeComponentIngredientEditDto
        {
            Id = row.Id,
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            IngredientId = row.IngredientId,
            IngredientName = row.IngredientName,
            StockItemId = row.StockItemId,
            WarehouseCategoryId = row.WarehouseCategoryId,
            WarehouseCategoryName = row.WarehouseCategoryName,
            WeightInGrams = row.WeightInGrams,
            YieldFactor = row.YieldFactor,
            IsOptional = row.IsOptional,
            Notes = row.Notes,
        };
    }

    private static PackagingRequirementEditDto MapPackaging(PackagingRequirementRow row)
    {
        return new PackagingRequirementEditDto
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
    }

    private static RecipeComponentInstructionSectionDto MapInstructionSection(RecipeComponentInstructionSectionRow row)
    {
        return new RecipeComponentInstructionSectionDto
        {
            Id = row.Id,
            RecipeComponentVersionId = row.RecipeComponentVersionId,
            Title = row.Title,
            SortOrder = row.SortOrder,
            Steps = row.Steps.Select(step => new RecipeComponentInstructionStepDto
            {
                Id = step.Id,
                RecipeComponentInstructionSectionId = step.RecipeComponentInstructionSectionId,
                StepText = step.StepText,
                SortOrder = step.SortOrder,
                RequiresControl = step.RequiresControl,
                ControlType = step.ControlType,
                ExpectedValue = step.ExpectedValue,
                ExpectedUnit = step.ExpectedUnit,
                IsCritical = step.IsCritical,
            }).ToList(),
        };
    }

    private static List<string> BuildValidationWarnings(
        RecipeComponentVersionRow version,
        IReadOnlyList<RecipeComponentIngredientRow> ingredients,
        IReadOnlyList<PackagingRequirementRow> packaging,
        IReadOnlyList<RecipeComponentInstructionSectionRow> sections)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(version.ComponentName))
        {
            warnings.Add("brak nazwy skladowej");
        }

        if (version.YieldQuantity <= 0)
        {
            warnings.Add("brak poprawnego yield");
        }

        if (!version.CaloriesPer100g.HasValue
            || !version.ProteinPer100g.HasValue
            || !version.CarbohydratesPer100g.HasValue
            || !version.FatPer100g.HasValue
            || !version.FiberPer100g.HasValue)
        {
            warnings.Add("brak pelnego nutrition na 100 g");
        }

        if (string.Equals(version.NutritionSource, "Override", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(version.NutritionOverrideReason))
        {
            warnings.Add("override nutrition wymaga powodu");
        }

        if (ingredients.Count == 0)
        {
            warnings.Add("brak skladnikow");
        }

        foreach (var ingredient in ingredients)
        {
            if (ingredient.WeightInGrams <= 0)
            {
                warnings.Add($"{ingredient.IngredientName}: gramatura musi byc wieksza od zera");
            }

            if (!ingredient.WarehouseCategoryId.HasValue)
            {
                warnings.Add($"{ingredient.IngredientName}: brak kategorii magazynowej");
            }
        }

        if (packaging.Count == 0)
        {
            warnings.Add("brak opakowania produkcyjnego");
        }

        foreach (var requirement in packaging)
        {
            if (string.IsNullOrWhiteSpace(requirement.ResourceName) || requirement.Quantity <= 0)
            {
                warnings.Add("niekompletne opakowanie");
            }
        }

        if (!version.AllergensApproved)
        {
            warnings.Add("alergeny nie zostaly zatwierdzone");
        }

        if (sections.Count == 0 || sections.All(section => section.Steps.Count == 0))
        {
            warnings.Add("brak sekcji i krokow instrukcji");
        }

        return warnings.Distinct().ToList();
    }

    private void EnsureDraft(RecipeComponentVersionRow version)
    {
        if (version.Status != "Draft")
        {
            throw new InvalidOperationException("Edytowac skladniki, opakowania i parametry technologiczne mozna tylko w wersji roboczej.");
        }
    }

    private string? UserName()
    {
        return this.currentUser.GetUserName();
    }

    private static string NormalizeNutritionSource(string? value)
    {
        var source = Normalize(value) ?? "Manual";
        return source.Equals("Override", StringComparison.OrdinalIgnoreCase)
            ? "Override"
            : source.Equals("Aggregated", StringComparison.OrdinalIgnoreCase)
                ? "Aggregated"
                : "Manual";
    }

    private static string NormalizeAllergenApprovalSource(string? value)
    {
        var source = Normalize(value) ?? "DerivedFromIngredients";
        return source.Equals("Override", StringComparison.OrdinalIgnoreCase)
            ? "Override"
            : "DerivedFromIngredients";
    }

    private static RecipeComponentVersionComparisonDto BuildComparison(
        RecipeComponentVersionRow version,
        IReadOnlyList<RecipeComponentIngredientRow> ingredients)
    {
        var ingredientWeight = ingredients.Sum(ingredient => ingredient.WeightInGrams);
        var grossWeight = ingredients.Sum(ingredient =>
            ingredient.YieldFactor > 0
                ? ingredient.WeightInGrams / ingredient.YieldFactor
                : ingredient.WeightInGrams);
        var ingredientReferenceWeight = ingredientWeight > 0 ? (decimal?)ingredientWeight : null;
        var referenceWeight = version.CookedWeightGrams
            ?? version.RawWeightGrams
            ?? ingredientReferenceWeight;
        var missingNutrition = ingredients.Count(ingredient =>
            !ingredient.CaloriesPer100g.HasValue
            || !ingredient.ProteinPer100g.HasValue
            || !ingredient.CarbohydratesPer100g.HasValue
            || !ingredient.FatPer100g.HasValue
            || !ingredient.FiberPer100g.HasValue);

        return new RecipeComponentVersionComparisonDto
        {
            IngredientWeightGrams = ingredientWeight,
            IngredientGrossWeightGrams = grossWeight,
            ReferenceWeightGrams = referenceWeight,
            ReferenceWeightSource = version.CookedWeightGrams.HasValue
                ? "Cooked"
                : version.RawWeightGrams.HasValue
                    ? "Raw"
                    : "Ingredients",
            YieldQuantity = version.YieldQuantity,
            YieldUnit = version.YieldUnit,
            ManualNutritionPer100g = new RecipeComponentNutritionValuesDto
            {
                CaloriesPer100g = version.CaloriesPer100g,
                ProteinPer100g = version.ProteinPer100g,
                CarbohydratesPer100g = version.CarbohydratesPer100g,
                FatPer100g = version.FatPer100g,
                FiberPer100g = version.FiberPer100g,
            },
            CalculatedNutritionPer100g = CalculateIngredientNutritionPer100g(ingredients, referenceWeight, missingNutrition),
            MissingNutritionIngredientCount = missingNutrition,
        };
    }

    private static RecipeComponentNutritionValuesDto? CalculateIngredientNutritionPer100g(
        IReadOnlyList<RecipeComponentIngredientRow> ingredients,
        decimal? referenceWeight,
        int missingNutrition)
    {
        if (!referenceWeight.HasValue || referenceWeight.Value <= 0 || ingredients.Count == 0 || missingNutrition > 0)
        {
            return null;
        }

        var factor = 100m / referenceWeight.Value;
        return new RecipeComponentNutritionValuesDto
        {
            CaloriesPer100g = ingredients.Sum(i => i.CaloriesPer100g!.Value * i.WeightInGrams / 100m) * factor,
            ProteinPer100g = ingredients.Sum(i => i.ProteinPer100g!.Value * i.WeightInGrams / 100m) * factor,
            CarbohydratesPer100g = ingredients.Sum(i => i.CarbohydratesPer100g!.Value * i.WeightInGrams / 100m) * factor,
            FatPer100g = ingredients.Sum(i => i.FatPer100g!.Value * i.WeightInGrams / 100m) * factor,
            FiberPer100g = ingredients.Sum(i => i.FiberPer100g!.Value * i.WeightInGrams / 100m) * factor,
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
