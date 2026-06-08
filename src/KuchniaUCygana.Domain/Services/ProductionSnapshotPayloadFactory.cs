using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using KuchniaUCygana.Domain.Interfaces.External;

namespace KuchniaUCygana.Domain.Services;

public static class ProductionSnapshotPayloadFactory
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
    };

    public static ProductionSnapshotPayload Create(PublishedDietPlanItemDto item)
    {
        var json = JsonSerializer.Serialize(item, SnapshotJsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return new ProductionSnapshotPayload(json, hash);
    }

    public static string CreatePlanHash(IEnumerable<string> itemHashes)
    {
        var payload = string.Join(
            "\n",
            itemHashes
                .Where(hash => !string.IsNullOrWhiteSpace(hash))
                .OrderBy(hash => hash, StringComparer.Ordinal));
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

public sealed record ProductionSnapshotPayload(string Json, string Hash);
