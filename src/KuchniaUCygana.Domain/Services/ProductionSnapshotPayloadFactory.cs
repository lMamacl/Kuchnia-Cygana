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
}

public sealed record ProductionSnapshotPayload(string Json, string Hash);
