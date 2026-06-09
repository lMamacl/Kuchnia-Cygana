using System.Text.Json;
using KuchniaUCygana.Application.DTOs.Packing;
using KuchniaUCygana.Application.Interfaces;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Entities.Production;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Domain.Interfaces.Production;

namespace KuchniaUCygana.Application.Services;

public sealed class BoxLabelService : IBoxLabelService
{
    private readonly IRepository<PackingItem> _itemRepository;
    private readonly IPackingSessionRepository _sessionRepository;
    private readonly IBoxLabelRepository _boxLabelRepository;

    public BoxLabelService(
        IRepository<PackingItem> itemRepository,
        IPackingSessionRepository sessionRepository,
        IBoxLabelRepository boxLabelRepository)
    {
        _itemRepository = itemRepository;
        _sessionRepository = sessionRepository;
        _boxLabelRepository = boxLabelRepository;
    }

    public async Task<PackingLabelDto> PrintBoxLabelAsync(
        int packingItemId,
        string operatorName,
        string? reprintReason = null)
    {
        var item = await _itemRepository.GetByIdAsync(packingItemId)
            ?? throw new InvalidOperationException($"Pudełko #{packingItemId} nie istnieje.");

        var printNumber = await _boxLabelRepository.GetPrintCountAsync(packingItemId) + 1;
        if (printNumber > 1 && string.IsNullOrWhiteSpace(reprintReason))
        {
            throw new InvalidOperationException("Powód redruku etykiety produktowej jest wymagany.");
        }

        reprintReason = printNumber > 1 ? reprintReason!.Trim() : null;
        var ingredients = (await _sessionRepository.GetMealIngredientsAsync(item.MealId)).ToList();
        var allergens = (await _sessionRepository.GetMealAllergensAsync(item.MealId)).ToList();
        var calories = await _sessionRepository.GetMealCaloriesAsync(item.MealId);
        var qrCode = item.BoxCode ?? $"BOX-{item.Id:D6}";
        var printedAt = DateTimeOffset.UtcNow;

        var labelData = new
        {
            qrCode,
            item.Id,
            item.PackingSessionId,
            item.PackingBagId,
            item.MealId,
            item.MealName,
            item.DietVariantId,
            ingredients,
            allergens,
            kcal = calories,
            printNumber,
            reprintReason,
            printedAt,
            printedBy = string.IsNullOrWhiteSpace(operatorName) ? "Kuchnia" : operatorName,
        };

        var label = new BoxLabel
        {
            PackingItemId = item.Id,
            QrCode = qrCode,
            LabelDataJson = JsonSerializer.Serialize(labelData),
            PrintNumber = printNumber,
            ReprintReason = reprintReason,
            PrintedAt = printedAt,
            PrintedBy = string.IsNullOrWhiteSpace(operatorName) ? "Kuchnia" : operatorName,
        };

        var id = await _boxLabelRepository.InsertAsync(label);
        label.Id = id;

        return ToDto(label, item, ingredients, allergens, calories);
    }

    public async Task<PackingLabelDto?> GetLatestBoxLabelAsync(int packingItemId)
    {
        var label = await _boxLabelRepository.GetLatestForPackingItemAsync(packingItemId);
        if (label is null)
        {
            return null;
        }

        var item = await _itemRepository.GetByIdAsync(packingItemId);
        return ToDto(label, item);
    }

    private static PackingLabelDto ToDto(
        BoxLabel label,
        PackingItem? item,
        IReadOnlyCollection<string>? ingredients = null,
        IReadOnlyCollection<string>? allergens = null,
        int? calories = null)
    {
        return new PackingLabelDto
        {
            Id = label.Id,
            PackingItemId = label.PackingItemId,
            PackingSessionId = item?.PackingSessionId,
            PackingBagId = item?.PackingBagId,
            LabelType = LabelType.Product.ToString(),
            QrCode = label.QrCode,
            DishName = item?.MealName,
            Allergens = allergens is { Count: > 0 } ? string.Join(", ", allergens) : null,
            Kcal = calories,
            Ingredients = ingredients is { Count: > 0 } ? string.Join(", ", ingredients) : null,
            ReprintReason = label.ReprintReason,
            PrintNumber = label.PrintNumber,
            PrintedAt = label.PrintedAt,
            PrintedBy = label.PrintedBy,
            LabelDataJson = label.LabelDataJson,
        };
    }
}
