namespace KuchniaUCygana.Application.Services;

public static class TransportLabelCodeNormalizer
{
    public static string Normalize(string rawCode)
    {
        var value = rawCode.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            value = uri.Segments.LastOrDefault()?.Trim('/') ?? value;
        }
        else if (value.Contains('/', StringComparison.Ordinal))
        {
            value = value.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? value;
        }

        return Uri.UnescapeDataString(value).Trim();
    }
}
