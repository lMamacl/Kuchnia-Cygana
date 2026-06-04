using KuchniaUCygana.Application.Configuration;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.DTOs.Warehouse;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Constants;
using KuchniaUCygana.Domain.Entities.Notifications;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Packing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KuchniaUCygana.Application.Services;

public sealed class PackingIncidentService : IPackingIncidentService
{
    private readonly IPackingIncidentRepository incidentRepository;
    private readonly IPackingSessionRepository sessionRepository;
    private readonly IPackingBagRepository bagRepository;
    private readonly IRepository<PackingItem> itemRepository;
    private readonly IPackingService packingService;
    private readonly IWarehouseService warehouseService;
    private readonly INotificationService notificationService;
    private readonly ICurrentUserService currentUserService;
    private readonly PackingResourcesOptions options;
    private readonly ILogger<PackingIncidentService> logger;

    public PackingIncidentService(
        IPackingIncidentRepository incidentRepository,
        IPackingSessionRepository sessionRepository,
        IPackingBagRepository bagRepository,
        IRepository<PackingItem> itemRepository,
        IPackingService packingService,
        IWarehouseService warehouseService,
        INotificationService notificationService,
        ICurrentUserService currentUserService,
        IOptions<PackingResourcesOptions> options,
        ILogger<PackingIncidentService> logger)
    {
        this.incidentRepository = incidentRepository;
        this.sessionRepository = sessionRepository;
        this.bagRepository = bagRepository;
        this.itemRepository = itemRepository;
        this.packingService = packingService;
        this.warehouseService = warehouseService;
        this.notificationService = notificationService;
        this.currentUserService = currentUserService;
        this.options = options.Value;
        this.logger = logger;
    }

    public async Task<PackingIncidentDto> CreateItemIssueAsync(CreatePackingItemIssueRequest request)
    {
        var flags = NormalizeFlags(request.ReasonFlags);
        ValidateFlags(flags, request.Description);

        var item = await itemRepository.GetByIdAsync(request.PackingItemId)
            ?? throw new InvalidOperationException($"Pudełko {request.PackingItemId} nie istnieje.");
        var session = await sessionRepository.GetWithItemsAsync(item.PackingSessionId)
            ?? throw new InvalidOperationException($"Sesja kompletacji {item.PackingSessionId} nie istnieje.");

        var incidentType = DetermineItemIncidentType(flags);
        var packingIssueType = flags.HasFlag(PackingIncidentReasonFlag.BoxMissing)
            ? nameof(PackingItemStatus.Missing)
            : nameof(PackingItemStatus.Damaged);
        var reason = BuildOperatorReason(flags, request.Description);
        var replacement = await packingService.ReportPackingItemIssueAsync(new ReportPackingItemIssueRequest
        {
            PackingItemId = item.Id,
            IssueType = packingIssueType,
            Reason = TrimTo(reason, 250),
        });

        var incident = new PackingIncident
        {
            Type = incidentType,
            Status = PackingIncidentStatus.KitchenReworkRequested,
            ReasonFlags = flags,
            PackingDate = session.PackingDate,
            PackingSessionId = session.Id,
            PackingItemId = item.Id,
            PackingBagId = item.PackingBagId,
            ReplacementPackingItemId = replacement.Id,
            ClientPublicId = GetClientPublicId(session),
            DeliveryCalendarId = session.DeliveryCalendarId,
            MealId = item.MealId,
            MealName = item.MealName,
            BoxCode = item.BoxCode,
            Description = request.Description?.Trim() ?? string.Empty,
            ReportedAt = DateTimeOffset.UtcNow,
            ReportedByUserId = currentUserService.GetUserId(),
        };

        incident.Id = await incidentRepository.InsertAsync(incident);

        if (incidentType != PackingIncidentType.BoxMissing && options.BoxContainerStockItemId.HasValue)
        {
            await TryRegisterWasteAsync(incident, "Automatyczny odpis uszkodzonego pojemnika pudełka.");
        }

        await NotifyItemIncidentAsync(incident);
        return Map(incident);
    }

    public async Task<PackingIncidentDto> CreateBagIssueAsync(CreatePackingBagIssueRequest request)
    {
        var flags = NormalizeFlags(request.ReasonFlags);
        ValidateFlags(flags, request.Description);

        var bag = await bagRepository.GetByIdAsync(request.PackingBagId)
            ?? throw new InvalidOperationException($"Torba {request.PackingBagId} nie istnieje.");
        var session = await sessionRepository.GetWithItemsAsync(bag.PackingSessionId)
            ?? throw new InvalidOperationException($"Sesja kompletacji {bag.PackingSessionId} nie istnieje.");

        var incidentType = flags.HasFlag(PackingIncidentReasonFlag.BagMissing)
            ? PackingIncidentType.BagMissing
            : PackingIncidentType.BagDamaged;
        var reason = BuildOperatorReason(flags, request.Description);
        var replacementBag = await packingService.ReportPackingBagDamageAsync(new ReportPackingBagDamageRequest
        {
            PackingBagId = bag.Id,
            Reason = TrimTo(reason, 500),
            AllowVerifiedManifestChange = request.AllowVerifiedManifestChange,
        });

        var incident = new PackingIncident
        {
            Type = incidentType,
            Status = PackingIncidentStatus.New,
            ReasonFlags = flags,
            PackingDate = session.PackingDate,
            PackingSessionId = session.Id,
            PackingBagId = bag.Id,
            ReplacementPackingBagId = replacementBag.PackingBagId,
            ClientPublicId = GetClientPublicId(session),
            DeliveryCalendarId = session.DeliveryCalendarId,
            BagCode = bag.BagCode,
            Description = request.Description?.Trim() ?? string.Empty,
            ReportedAt = DateTimeOffset.UtcNow,
            ReportedByUserId = currentUserService.GetUserId(),
        };

        incident.Id = await incidentRepository.InsertAsync(incident);
        await TryRegisterWasteAsync(incident, "Automatyczny odpis uszkodzonej torby transportowej.");
        await NotifyBagIncidentAsync(incident);
        return Map(incident);
    }

    public async Task<IReadOnlyList<PackingIncidentDto>> SearchAsync(PackingIncidentFilterDto filter)
    {
        var incidents = await incidentRepository.SearchAsync(
            filter.Date,
            filter.Status,
            filter.Type,
            filter.ClientPublicId,
            filter.DeliveryCalendarId);

        return incidents.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<PackingIncidentDto>> GetKitchenReworkAsync(DateOnly? date)
    {
        var incidents = await incidentRepository.GetKitchenReworkAsync(date);
        var result = new List<PackingIncidentDto>();
        foreach (var incident in incidents)
        {
            var dto = Map(incident);
            if (incident.ReplacementPackingItemId.HasValue)
            {
                var replacement = await itemRepository.GetByIdAsync(incident.ReplacementPackingItemId.Value);
                if (replacement is not null)
                {
                    dto.ReplacementPackingItemStatus = replacement.Status;
                    dto.ReplacementFoilPrintedAt = replacement.FoilPrintedAt;
                }
            }

            result.Add(dto);
        }

        return result;
    }

    public async Task AssignToCurrentUserAsync(int incidentId)
    {
        var incident = await GetIncidentOrThrowAsync(incidentId);
        incident.AssignedToUserId = currentUserService.GetUserId();
        if (incident.Status == PackingIncidentStatus.New)
        {
            incident.Status = PackingIncidentStatus.InProgress;
        }

        await incidentRepository.UpdateAsync(incident);
    }

    public async Task AddAdminNoteAsync(HandlePackingIncidentRequest request)
    {
        var incident = await GetIncidentOrThrowAsync(request.IncidentId);
        AppendNote(incident, request.Notes);
        await incidentRepository.UpdateAsync(incident);
    }

    public async Task RegisterWasteAsync(RegisterIncidentWasteRequest request)
    {
        var incident = await GetIncidentOrThrowAsync(request.IncidentId);
        await RegisterWasteCoreAsync(incident, request.Notes, throwOnFailure: true);
    }

    public async Task RequestKitchenReworkAsync(HandlePackingIncidentRequest request)
    {
        var incident = await GetIncidentOrThrowAsync(request.IncidentId);
        if (!incident.ReplacementPackingItemId.HasValue)
        {
            throw new InvalidOperationException("To zgłoszenie nie ma pudełka zastępczego dla kuchni.");
        }

        incident.Status = PackingIncidentStatus.KitchenReworkRequested;
        AppendNote(incident, request.Notes);
        await incidentRepository.UpdateAsync(incident);
        await NotifyKitchenReworkAsync(incident);
    }

    public async Task MarkKitchenInProgressAsync(HandlePackingIncidentRequest request)
    {
        var incident = await GetIncidentOrThrowAsync(request.IncidentId);
        if (!incident.ReplacementPackingItemId.HasValue)
        {
            throw new InvalidOperationException("To zgłoszenie nie wymaga ponownego przygotowania w kuchni.");
        }

        incident.Status = PackingIncidentStatus.InProgress;
        incident.KitchenStartedAt ??= DateTimeOffset.UtcNow;
        AppendNote(incident, request.Notes);
        await incidentRepository.UpdateAsync(incident);
    }

    public async Task MarkKitchenPreparedAsync(HandlePackingIncidentRequest request)
    {
        var incident = await GetIncidentOrThrowAsync(request.IncidentId);
        if (!incident.ReplacementPackingItemId.HasValue)
        {
            throw new InvalidOperationException("To zgłoszenie nie wymaga ponownego przygotowania w kuchni.");
        }

        incident.KitchenPreparedAt = DateTimeOffset.UtcNow;
        incident.KitchenStartedAt ??= incident.KitchenPreparedAt;
        if (incident.Status == PackingIncidentStatus.KitchenReworkRequested)
        {
            incident.Status = PackingIncidentStatus.InProgress;
        }

        AppendNote(incident, request.Notes);
        await incidentRepository.UpdateAsync(incident);
    }

    public async Task ResolveAsync(ResolvePackingIncidentRequest request)
    {
        var incident = await GetIncidentOrThrowAsync(request.IncidentId);
        incident.Status = PackingIncidentStatus.Resolved;
        incident.ResolvedAt = DateTimeOffset.UtcNow;
        incident.ResolvedByUserId = currentUserService.GetUserId();
        AppendNote(incident, request.ResolutionNotes);
        await incidentRepository.UpdateAsync(incident);
    }

    private async Task TryRegisterWasteAsync(PackingIncident incident, string notes)
    {
        try
        {
            await RegisterWasteCoreAsync(incident, notes, throwOnFailure: false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Nie udało się automatycznie zarejestrować rozchodu dla awarii kompletacji {IncidentId}.", incident.Id);
        }
    }

    private async Task RegisterWasteCoreAsync(PackingIncident incident, string? notes, bool throwOnFailure)
    {
        var (stockItemId, quantity) = GetWasteResource(incident);
        if (!stockItemId.HasValue || stockItemId.Value <= 0)
        {
            incident.Status = PackingIncidentStatus.WarehouseActionRequired;
            incident.WarehouseWasteError = "Brak skonfigurowanej pozycji magazynowej dla tego typu zgłoszenia.";
            await incidentRepository.UpdateAsync(incident);
            if (throwOnFailure)
            {
                throw new InvalidOperationException(incident.WarehouseWasteError);
            }

            return;
        }

        try
        {
            await warehouseService.RegisterWasteAsync(new RegisterWasteRequest
            {
                StockItemId = stockItemId.Value,
                Quantity = quantity,
                Reason = $"Awaria kompletacji #{incident.Id}: {GetTypeLabel(incident.Type)}",
                Notes = string.IsNullOrWhiteSpace(notes) ? incident.Description : notes,
            });

            incident.WarehouseWasteRegisteredAt = DateTimeOffset.UtcNow;
            incident.WarehouseWasteError = null;
            if (incident.Status == PackingIncidentStatus.WarehouseActionRequired)
            {
                incident.Status = incident.ReplacementPackingItemId.HasValue
                    ? PackingIncidentStatus.KitchenReworkRequested
                    : PackingIncidentStatus.New;
            }

            await incidentRepository.UpdateAsync(incident);
        }
        catch (Exception ex)
        {
            incident.Status = PackingIncidentStatus.WarehouseActionRequired;
            incident.WarehouseWasteError = ex.Message;
            await incidentRepository.UpdateAsync(incident);
            if (throwOnFailure)
            {
                throw;
            }
        }
    }

    private (int? StockItemId, decimal Quantity) GetWasteResource(PackingIncident incident)
    {
        return incident.Type switch
        {
            PackingIncidentType.BagDamaged or PackingIncidentType.BagMissing =>
                (options.TransportBagStockItemId, options.TransportBagWasteQuantity <= 0 ? 1m : options.TransportBagWasteQuantity),
            PackingIncidentType.BoxDamaged or PackingIncidentType.LabelProblem or PackingIncidentType.WrongMeal or PackingIncidentType.Other =>
                (options.BoxContainerStockItemId, options.BoxContainerWasteQuantity <= 0 ? 1m : options.BoxContainerWasteQuantity),
            _ => (null, 0m),
        };
    }

    private async Task<PackingIncident> GetIncidentOrThrowAsync(int incidentId)
    {
        return await incidentRepository.GetByIdAsync(incidentId)
            ?? throw new InvalidOperationException($"Zgłoszenie awarii #{incidentId} nie istnieje.");
    }

    private static PackingIncidentReasonFlag NormalizeFlags(IReadOnlyCollection<PackingIncidentReasonFlag> reasonFlags)
    {
        var flags = PackingIncidentReasonFlag.None;
        foreach (var flag in reasonFlags.Where(flag => flag != PackingIncidentReasonFlag.None))
        {
            flags |= flag;
        }

        return flags;
    }

    private static void ValidateFlags(PackingIncidentReasonFlag flags, string? description)
    {
        if (flags == PackingIncidentReasonFlag.None)
        {
            throw new InvalidOperationException("Wybierz co najmniej jeden powód zgłoszenia.");
        }

        if (flags.HasFlag(PackingIncidentReasonFlag.Other) && string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidOperationException("Opis zgłoszenia jest wymagany dla powodu „Inne”.");
        }
    }

    private static PackingIncidentType DetermineItemIncidentType(PackingIncidentReasonFlag flags)
    {
        if (flags.HasFlag(PackingIncidentReasonFlag.BoxMissing))
        {
            return PackingIncidentType.BoxMissing;
        }

        if (flags.HasFlag(PackingIncidentReasonFlag.WrongMeal))
        {
            return PackingIncidentType.WrongMeal;
        }

        if (flags.HasFlag(PackingIncidentReasonFlag.LabelUnreadable))
        {
            return PackingIncidentType.LabelProblem;
        }

        if (flags.HasFlag(PackingIncidentReasonFlag.Other)
            && !flags.HasFlag(PackingIncidentReasonFlag.BoxDamaged)
            && !flags.HasFlag(PackingIncidentReasonFlag.BoxLeaking))
        {
            return PackingIncidentType.Other;
        }

        return PackingIncidentType.BoxDamaged;
    }

    private static string BuildOperatorReason(PackingIncidentReasonFlag flags, string? description)
    {
        var summary = BuildReasonSummary(flags);
        return string.IsNullOrWhiteSpace(description)
            ? summary
            : $"{summary}. Opis: {description.Trim()}";
    }

    private static string BuildReasonSummary(PackingIncidentReasonFlag flags)
    {
        var labels = new List<string>();
        foreach (PackingIncidentReasonFlag flag in Enum.GetValues(typeof(PackingIncidentReasonFlag)))
        {
            if (flag == PackingIncidentReasonFlag.None || !flags.HasFlag(flag))
            {
                continue;
            }

            labels.Add(GetReasonLabel(flag));
        }

        return labels.Count == 0 ? "Brak powodu" : string.Join(", ", labels);
    }

    private static string GetReasonLabel(PackingIncidentReasonFlag flag)
    {
        return flag switch
        {
            PackingIncidentReasonFlag.BoxDamaged => "Pudełko uszkodzone",
            PackingIncidentReasonFlag.BoxLeaking => "Pudełko przecieka",
            PackingIncidentReasonFlag.BoxMissing => "Brak pudełka",
            PackingIncidentReasonFlag.LabelUnreadable => "Nieczytelna etykieta",
            PackingIncidentReasonFlag.WrongMeal => "Nieprawidłowe danie",
            PackingIncidentReasonFlag.BagTorn => "Torba rozerwana",
            PackingIncidentReasonFlag.BagDirty => "Torba zabrudzona",
            PackingIncidentReasonFlag.BagClosureDamaged => "Uszkodzone zamknięcie/uchwyt",
            PackingIncidentReasonFlag.TransportLabelDamaged => "Etykieta transportowa uszkodzona",
            PackingIncidentReasonFlag.BagMissing => "Brak torby",
            PackingIncidentReasonFlag.Other => "Inne",
            _ => flag.ToString(),
        };
    }

    private static string GetTypeLabel(PackingIncidentType type)
    {
        return type switch
        {
            PackingIncidentType.BagDamaged => "Uszkodzona torba",
            PackingIncidentType.BagMissing => "Brak torby",
            PackingIncidentType.BoxDamaged => "Uszkodzone pudełko",
            PackingIncidentType.BoxMissing => "Brak pudełka",
            PackingIncidentType.LabelProblem => "Problem z etykietą",
            PackingIncidentType.WrongMeal => "Nieprawidłowe danie",
            PackingIncidentType.Other => "Inne",
            _ => type.ToString(),
        };
    }

    private static string GetClientPublicId(PackingSession session)
    {
        if (!string.IsNullOrWhiteSpace(session.ClientPublicId))
        {
            return session.ClientPublicId;
        }

        if (session.DeliveryCalendarId.HasValue)
        {
            return $"DC-{session.DeliveryCalendarId.Value}";
        }

        return session.OrderId.HasValue ? $"ORD-{session.OrderId.Value}" : $"SESSION-{session.Id}";
    }

    private static string TrimTo(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static void AppendNote(PackingIncident incident, string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return;
        }

        var line = $"{DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm}: {note.Trim()}";
        incident.AdminNotes = string.IsNullOrWhiteSpace(incident.AdminNotes)
            ? line
            : $"{incident.AdminNotes}{Environment.NewLine}{line}";
    }

    private async Task NotifyItemIncidentAsync(PackingIncident incident)
    {
        await notificationService.CreateForRolesAsync(
            CreateNotification(
                incident,
                NotificationSeverity.Danger,
                "Zgłoszono problem z pudełkiem",
                $"Dostawa {incident.DeliveryCalendarId?.ToString() ?? "-"}, klient {incident.ClientPublicId ?? "-"}, danie: {incident.MealName ?? "-"}",
                "/admin/packing-incidents"),
            new[] { AppRoles.Admin, AppRoles.PackingManager, AppRoles.KitchenManager });

        await NotifyKitchenReworkAsync(incident);
    }

    private async Task NotifyBagIncidentAsync(PackingIncident incident)
    {
        await notificationService.CreateForRolesAsync(
            CreateNotification(
                incident,
                NotificationSeverity.Warning,
                "Zgłoszono problem z torbą",
                $"Torba {incident.BagCode ?? "-"}, dostawa {incident.DeliveryCalendarId?.ToString() ?? "-"}, klient {incident.ClientPublicId ?? "-"}",
                "/admin/packing-incidents"),
            new[] { AppRoles.Admin, AppRoles.PackingManager, AppRoles.WarehouseManager });
    }

    private async Task NotifyKitchenReworkAsync(PackingIncident incident)
    {
        if (!incident.ReplacementPackingItemId.HasValue)
        {
            return;
        }

        await notificationService.CreateForRolesAsync(
            CreateNotification(
                incident,
                NotificationSeverity.Warning,
                "Danie do ponownego przygotowania",
                $"{incident.MealName ?? "Pudełko"} dla klienta {incident.ClientPublicId ?? "-"} wymaga zamiennika.",
                "/production/rework"),
            new[] { AppRoles.Admin, AppRoles.KitchenManager });
    }

    private static Notification CreateNotification(
        PackingIncident incident,
        string severity,
        string title,
        string message,
        string linkUrl)
    {
        return new Notification
        {
            Type = "PackingIncident",
            Severity = severity,
            Title = title,
            Message = message,
            LinkUrl = linkUrl,
            DeduplicationKey = $"packing-incident-{incident.Id}-{title}",
            SourceType = nameof(PackingIncident),
            SourceId = incident.Id,
        };
    }

    private static PackingIncidentDto Map(PackingIncident incident)
    {
        return new PackingIncidentDto
        {
            Id = incident.Id,
            Type = incident.Type,
            Status = incident.Status,
            ReasonFlags = incident.ReasonFlags,
            ReasonSummary = BuildReasonSummary(incident.ReasonFlags),
            PackingDate = incident.PackingDate,
            PackingSessionId = incident.PackingSessionId,
            PackingItemId = incident.PackingItemId,
            PackingBagId = incident.PackingBagId,
            ReplacementPackingItemId = incident.ReplacementPackingItemId,
            ReplacementPackingBagId = incident.ReplacementPackingBagId,
            ClientPublicId = incident.ClientPublicId,
            DeliveryCalendarId = incident.DeliveryCalendarId,
            MealId = incident.MealId,
            MealName = incident.MealName,
            BoxCode = incident.BoxCode,
            BagCode = incident.BagCode,
            Description = incident.Description,
            ReportedAt = incident.ReportedAt,
            ReportedByUserId = incident.ReportedByUserId,
            AssignedToUserId = incident.AssignedToUserId,
            AdminNotes = incident.AdminNotes,
            WarehouseWasteRegisteredAt = incident.WarehouseWasteRegisteredAt,
            WarehouseWasteError = incident.WarehouseWasteError,
            KitchenStartedAt = incident.KitchenStartedAt,
            KitchenPreparedAt = incident.KitchenPreparedAt,
            ResolvedAt = incident.ResolvedAt,
        };
    }
}
