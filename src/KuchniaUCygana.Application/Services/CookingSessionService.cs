using System.Text.Json;
using KuchniaUCygana.Application.DTOs.Production;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.External;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

namespace KuchniaUCygana.Application.Services;

public sealed class CookingSessionService : ICookingSessionService
{
    private const string NotStartedStatus = "NotStarted";
    private const string InProgressStatus = "InProgress";
    private const string CompletedStatus = "Completed";
    private const string CheckedStatus = "Checked";
    private const string PendingStatus = "Pending";

    private readonly IRepository<ProductionPlanItem> itemRepository;
    private readonly ICookingSessionRepository sessionRepository;
    private readonly ICookingSessionStepCheckRepository stepCheckRepository;

    public CookingSessionService(
        IRepository<ProductionPlanItem> itemRepository,
        ICookingSessionRepository sessionRepository,
        ICookingSessionStepCheckRepository stepCheckRepository)
    {
        this.itemRepository = itemRepository;
        this.sessionRepository = sessionRepository;
        this.stepCheckRepository = stepCheckRepository;
    }

    public async Task<CookingComponentSessionDto> GetComponentSessionAsync(
        int productionPlanItemId,
        int recipeComponentVersionId)
    {
        var session = await FindSessionAsync(productionPlanItemId, recipeComponentVersionId);
        if (session is null)
        {
            return new CookingComponentSessionDto { Status = NotStartedStatus };
        }

        return await BuildSessionDtoAsync(session);
    }

    public async Task<CookingComponentSessionDto> StartComponentSessionAsync(
        int productionPlanItemId,
        int recipeComponentVersionId,
        string operatorName)
    {
        var item = await GetItemAsync(productionPlanItemId);
        _ = FindSnapshotComponent(item, recipeComponentVersionId);

        if (!item.FefoDeductedAt.HasValue)
        {
            throw new InvalidOperationException("Sesje gotowania mozna uruchomic dopiero po idempotentnym zdjeciu FEFO dla pozycji.");
        }

        var now = DateTimeOffset.UtcNow;
        var session = await FindSessionAsync(productionPlanItemId, recipeComponentVersionId);
        if (session is null)
        {
            session = new CookingSession
            {
                ProductionPlanItemId = productionPlanItemId,
                RecipeComponentVersionId = recipeComponentVersionId,
                ProductionDate = GetSnapshot(item).PlanDate,
                Status = InProgressStatus,
                StartedAt = now,
                StartedBy = NormalizeOperator(operatorName),
                CreatedBy = NormalizeOperator(operatorName),
            };
            session.Id = await this.sessionRepository.InsertAsync(session);
        }
        else if (session.Status != CompletedStatus)
        {
            session.Status = InProgressStatus;
            session.StartedAt ??= now;
            session.StartedBy ??= NormalizeOperator(operatorName);
            session.UpdatedBy = NormalizeOperator(operatorName);
            await this.sessionRepository.UpdateAsync(session);
        }

        if (item.Status == ProductionItemStatus.Planned)
        {
            item.Status = ProductionItemStatus.Cooking;
            item.UpdatedBy = NormalizeOperator(operatorName);
            await this.itemRepository.UpdateAsync(item);
        }

        return await BuildSessionDtoAsync(session);
    }

    public async Task ToggleStepAsync(ToggleCookingStepRequest request)
    {
        var item = await GetItemAsync(request.ProductionPlanItemId);
        var component = FindSnapshotComponent(item, request.RecipeComponentVersionId);
        var step = component.InstructionSections
            .SelectMany(section => section.Steps)
            .FirstOrDefault(step => step.StepId == request.StepId)
            ?? throw new InvalidOperationException(
                $"Krok {request.StepId} nie nalezy do skladowej {request.RecipeComponentVersionId} w snapshotcie M2.");
        var session = await FindSessionAsync(request.ProductionPlanItemId, request.RecipeComponentVersionId)
            ?? throw new InvalidOperationException("Najpierw uruchom sesje gotowania skladowej.");

        if (session.Status == CompletedStatus)
        {
            throw new InvalidOperationException("Nie mozna zmieniac krokow ukonczonej sesji gotowania.");
        }

        ValidateControlStep(step, request);

        var check = await this.stepCheckRepository.GetBySessionAndStepAsync(session.Id, step.StepId);
        if (check is null)
        {
            check = new CookingSessionStepCheck
            {
                CookingSessionId = session.Id,
                RecipeComponentInstructionStepId = step.StepId,
                CreatedBy = NormalizeOperator(request.OperatorName),
            };
            ApplyStepState(check, request);
            await this.stepCheckRepository.InsertAsync(check);
            return;
        }

        check.UpdatedBy = NormalizeOperator(request.OperatorName);
        ApplyStepState(check, request);
        await this.stepCheckRepository.UpdateAsync(check);
    }

    public async Task<CookingComponentSessionDto> CompleteComponentSessionAsync(
        int productionPlanItemId,
        int recipeComponentVersionId,
        string operatorName)
    {
        var item = await GetItemAsync(productionPlanItemId);
        var component = FindSnapshotComponent(item, recipeComponentVersionId);
        var session = await FindSessionAsync(productionPlanItemId, recipeComponentVersionId)
            ?? throw new InvalidOperationException("Najpierw uruchom sesje gotowania skladowej.");
        var checks = await this.stepCheckRepository.GetBySessionAsync(session.Id);
        var checkedStepIds = checks
            .Where(check => check.Status == CheckedStatus)
            .Select(check => check.RecipeComponentInstructionStepId)
            .ToHashSet();
        var requiredStepIds = component.InstructionSections
            .SelectMany(section => section.Steps)
            .Where(step => step.IsCritical || step.RequiresControl)
            .Select(step => step.StepId)
            .ToList();
        var missingRequiredSteps = requiredStepIds
            .Where(stepId => !checkedStepIds.Contains(stepId))
            .ToList();

        if (missingRequiredSteps.Count > 0)
        {
            throw new InvalidOperationException(
                "Nie mozna ukonczyc skladowej: wymagane kroki nie zostaly odznaczone.");
        }

        session.Status = CompletedStatus;
        session.CompletedAt = DateTimeOffset.UtcNow;
        session.CompletedBy = NormalizeOperator(operatorName);
        session.UpdatedBy = NormalizeOperator(operatorName);
        await this.sessionRepository.UpdateAsync(session);

        return await BuildSessionDtoAsync(session);
    }

    private static void ValidateControlStep(ComponentInstructionStepDto step, ToggleCookingStepRequest request)
    {
        if (!request.IsChecked || !step.RequiresControl)
        {
            return;
        }

        if (!request.ActualValue.HasValue || string.IsNullOrWhiteSpace(request.ActualUnit))
        {
            throw new InvalidOperationException("Krok kontrolny wymaga wartosci kontroli i jednostki.");
        }

        if (step.ExpectedValue.HasValue
            && IsMinimumThresholdControl(step.ControlType)
            && request.ActualValue.Value < step.ExpectedValue.Value)
        {
            throw new InvalidOperationException(
                $"Wartosc kontroli jest ponizej wymaganego progu {step.ExpectedValue.Value:0.##} {step.ExpectedUnit}.");
        }
    }

    private static bool IsMinimumThresholdControl(string? controlType)
    {
        if (string.IsNullOrWhiteSpace(controlType))
        {
            return true;
        }

        return controlType.Trim() switch
        {
            "CoreTemperature" or "Temperature" or "MinTemperature" => true,
            _ => false,
        };
    }

    public async Task StartComponentSessionAsync(int productionPlanItemId)
    {
        var item = await GetItemAsync(productionPlanItemId);
        if (!item.FefoDeductedAt.HasValue)
        {
            throw new InvalidOperationException("Sesje gotowania mozna uruchomic dopiero po idempotentnym zdjeciu FEFO dla pozycji.");
        }

        if (item.Status == ProductionItemStatus.Planned)
        {
            item.Status = ProductionItemStatus.Cooking;
            await this.itemRepository.UpdateAsync(item);
        }
    }

    public async Task ApproveComponentCookingAsync(int productionPlanItemId, decimal acceptedQuantity, string approvedBy)
    {
        var item = await GetItemAsync(productionPlanItemId);

        item.CookedQuantity = (int)acceptedQuantity;
        item.Status = ProductionItemStatus.Cooked;
        item.ActualReadyTime = TimeOnly.FromDateTime(DateTime.Now);
        item.UpdatedBy = NormalizeOperator(approvedBy);
        await this.itemRepository.UpdateAsync(item);
    }

    private async Task<ProductionPlanItem> GetItemAsync(int productionPlanItemId)
        => await this.itemRepository.GetByIdAsync(productionPlanItemId)
            ?? throw new InvalidOperationException($"Pozycja planu {productionPlanItemId} nie istnieje.");

    private async Task<CookingSession?> FindSessionAsync(int productionPlanItemId, int recipeComponentVersionId)
    {
        return await this.sessionRepository.GetLatestForComponentAsync(
            productionPlanItemId,
            recipeComponentVersionId);
    }

    private async Task<CookingComponentSessionDto> BuildSessionDtoAsync(CookingSession session)
    {
        var checks = (await this.stepCheckRepository.GetBySessionAsync(session.Id))
            .ToDictionary(
                check => check.RecipeComponentInstructionStepId,
                check => new CookingStepCheckDto
                {
                    StepId = check.RecipeComponentInstructionStepId,
                    Status = check.Status,
                    CheckedAt = check.CheckedAt,
                    CheckedBy = check.CheckedBy,
                    ActualValue = check.ActualValue,
                    ActualUnit = check.ActualUnit,
                    Notes = check.Notes,
                });

        return new CookingComponentSessionDto
        {
            SessionId = session.Id,
            Status = session.Status,
            StartedAt = session.StartedAt,
            StartedBy = session.StartedBy,
            CompletedAt = session.CompletedAt,
            CompletedBy = session.CompletedBy,
            StepChecksByStepId = checks,
        };
    }

    private static PublishedDietPlanItemDto GetSnapshot(ProductionPlanItem item)
    {
        if (string.IsNullOrWhiteSpace(item.M2SnapshotJson))
        {
            throw new InvalidOperationException("Brak snapshotu M2 dla pozycji planu produkcji.");
        }

        try
        {
            return JsonSerializer.Deserialize<PublishedDietPlanItemDto>(item.M2SnapshotJson)
                ?? throw new InvalidOperationException("Snapshot M2 jest pusty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Snapshot M2 dla pozycji planu jest uszkodzony.", ex);
        }
    }

    private static MealComponentVersionDto FindSnapshotComponent(ProductionPlanItem item, int recipeComponentVersionId)
    {
        var snapshot = GetSnapshot(item);
        return snapshot.Components.FirstOrDefault(component =>
                component.RecipeComponentVersionId == recipeComponentVersionId)
            ?? throw new InvalidOperationException(
                $"Snapshot M2 nie zawiera skladowej RecipeComponentVersionId {recipeComponentVersionId}.");
    }

    private static void ApplyStepState(CookingSessionStepCheck check, ToggleCookingStepRequest request)
    {
        if (request.IsChecked)
        {
            check.Status = CheckedStatus;
            check.CheckedAt = DateTimeOffset.UtcNow;
            check.CheckedBy = NormalizeOperator(request.OperatorName);
            check.ActualValue = request.ActualValue;
            check.ActualUnit = string.IsNullOrWhiteSpace(request.ActualUnit) ? null : request.ActualUnit.Trim();
            check.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            return;
        }

        check.Status = PendingStatus;
        check.CheckedAt = null;
        check.CheckedBy = null;
        check.ActualValue = null;
        check.ActualUnit = null;
        check.Notes = null;
    }

    private static string NormalizeOperator(string? operatorName)
        => string.IsNullOrWhiteSpace(operatorName) ? "Kitchen" : operatorName.Trim();
}
