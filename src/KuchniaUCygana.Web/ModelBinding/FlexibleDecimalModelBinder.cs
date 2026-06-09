using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace KuchniaUCygana.Web.ModelBinding;

public sealed class FlexibleDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelType = Nullable.GetUnderlyingType(context.Metadata.ModelType) ?? context.Metadata.ModelType;
        return modelType == typeof(decimal) ? new FlexibleDecimalModelBinder() : null;
    }
}

public sealed class FlexibleDecimalModelBinder : IModelBinder
{
    public static bool TryParseDecimal(string rawValue, out decimal value)
    {
        var normalized = rawValue
            .Trim()
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal);

        var lastComma = normalized.LastIndexOf(',');
        var lastDot = normalized.LastIndexOf('.');

        if (lastComma >= 0 && lastDot >= 0)
        {
            normalized = lastComma > lastDot
                ? normalized.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.')
                : normalized.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (lastComma >= 0)
        {
            normalized = normalized.Replace(',', '.');
        }

        return decimal.TryParse(
            normalized,
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out value);
    }

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var rawValue = valueProviderResult.FirstValue;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (Nullable.GetUnderlyingType(bindingContext.ModelType) is not null)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }
            else
            {
                bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Wartość jest wymagana.");
            }

            return Task.CompletedTask;
        }

        if (TryParseDecimal(rawValue, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
            return Task.CompletedTask;
        }

        bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Nieprawidłowy format liczby.");
        return Task.CompletedTask;
    }
}
