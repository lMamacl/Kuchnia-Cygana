using Dapper;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;

public sealed class PackingIncidentRepository : BaseRepository<PackingIncident>, IPackingIncidentRepository
{
    public PackingIncidentRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IReadOnlyList<PackingIncident>> SearchAsync(
        DateOnly? date,
        PackingIncidentStatus? status,
        PackingIncidentType? type,
        string? clientPublicId,
        int? deliveryCalendarId)
    {
        using var db = Factory.CreateConnection();
        var sql =
            """
            SELECT *
            FROM [PackingIncidents]
            WHERE [IsDeleted] = 0
              AND (@date IS NULL OR [PackingDate] = @date)
              AND (@status IS NULL OR [Status] = @status)
              AND (@type IS NULL OR [Type] = @type)
              AND (@clientPublicId IS NULL OR [ClientPublicId] LIKE '%' + @clientPublicId + '%')
              AND (@deliveryCalendarId IS NULL OR [DeliveryCalendarId] = @deliveryCalendarId)
            ORDER BY
                CASE WHEN [Status] = @resolved THEN 1 ELSE 0 END ASC,
                [ReportedAt] DESC,
                [Id] DESC;
            """;

        var rows = await db.QueryAsync<PackingIncident>(
            sql,
            new
            {
                date,
                status = status.HasValue ? (int?)status.Value : null,
                type = type.HasValue ? (int?)type.Value : null,
                clientPublicId = string.IsNullOrWhiteSpace(clientPublicId) ? null : clientPublicId.Trim(),
                deliveryCalendarId,
                resolved = (int)PackingIncidentStatus.Resolved,
            });

        return rows.ToList();
    }

    public async Task<IReadOnlyList<PackingIncident>> GetKitchenReworkAsync(DateOnly? date)
    {
        using var db = Factory.CreateConnection();
        var rows = await db.QueryAsync<PackingIncident>(
            """
            SELECT *
            FROM [PackingIncidents]
            WHERE [IsDeleted] = 0
              AND [ReplacementPackingItemId] IS NOT NULL
              AND [Status] <> @resolved
              AND (@date IS NULL OR [PackingDate] = @date)
            ORDER BY
                CASE WHEN [KitchenPreparedAt] IS NULL THEN 0 ELSE 1 END ASC,
                [ReportedAt] ASC,
                [Id] ASC;
            """,
            new
            {
                date,
                resolved = (int)PackingIncidentStatus.Resolved,
            });

        return rows.ToList();
    }
}


