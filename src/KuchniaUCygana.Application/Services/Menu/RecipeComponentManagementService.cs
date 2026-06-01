using KuchniaUCygana.Application.DTOs.Menu;
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

    public async Task<IReadOnlyList<RecipeComponentListItemDto>> SearchAsync(string? query)
    {
        var rows = await this.repository.SearchComponentsAsync(query);
        return rows.Select(MapListItem).ToList();
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
            Name = component.Name,
            Description = component.Description,
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

        var ingredients = await this.repository.GetVersionIngredientsAsync(versionId);
        var packaging = await this.repository.GetVersionPackagingAsync(versionId);
        var warnings = BuildValidationWarnings(version, ingredients, packaging);

        return MapVersionDetail(version, ingredients, packaging, warnings);
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
            Description = request.Description?.Trim(),
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
        }

        return await this.repository.CreateVersionAsync(version, request.SourceVersionId);
    }

    public async Task UpdateVersionAsync(int versionId, UpdateRecipeComponentVersionRequest request)
    {
        var existing = await this.repository.GetVersionAsync(versionId)
            ?? throw new InvalidOperationException($"Wersja #{versionId} nie istnieje.");

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

    public async Task PublishVersionAsync(int versionId)
    {
        var version = await this.repository.GetVersionAsync(versionId)
            ?? throw new InvalidOperationException($"Wersja #{versionId} nie istnieje.");
        this.EnsureDraft(version);

        var ingredients = await this.repository.GetVersionIngredientsAsync(versionId);
        var packaging = await this.repository.GetVersionPackagingAsync(versionId);
        var warnings = BuildValidationWarnings(version, ingredients, packaging);
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
            Name = row.Name,
            Description = row.Description,
            IsActive = row.IsActive,
            VersionCount = row.VersionCount,
            LatestVersionId = row.LatestVersionId,
            LatestVersionNumber = row.LatestVersionNumber,
            LatestVersionStatus = row.LatestVersionStatus,
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

            summaries.Add(new RecipeComponentVersionSummaryDto
            {
                Id = version.Id,
                VersionNumber = version.VersionNumber,
                Status = version.Status,
                ChangeSummary = version.ChangeSummary,
                PublishedAt = version.PublishedAt,
                IsComplete = BuildValidationWarnings(version, ingredients, packaging).Count == 0,
            });
        }

        return summaries;
    }

    private static RecipeComponentVersionDetailDto MapVersionDetail(
        RecipeComponentVersionRow version,
        IReadOnlyList<RecipeComponentIngredientRow> ingredients,
        IReadOnlyList<PackagingRequirementRow> packaging,
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
            ChangeSummary = version.ChangeSummary,
            IsTechnologyChange = version.IsTechnologyChange,
            NonTechnologyChangeReason = version.NonTechnologyChangeReason,
            PublishedAt = version.PublishedAt,
            PublishedBy = version.PublishedBy,
            Ingredients = ingredients.Select(MapIngredient).ToList(),
            PackagingRequirements = packaging.Select(MapPackaging).ToList(),
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

    private static List<string> BuildValidationWarnings(
        RecipeComponentVersionRow version,
        IReadOnlyList<RecipeComponentIngredientRow> ingredients,
        IReadOnlyList<PackagingRequirementRow> packaging)
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
}
