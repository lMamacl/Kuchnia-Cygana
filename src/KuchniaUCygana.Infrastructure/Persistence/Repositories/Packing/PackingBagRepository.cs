using Dapper;
using KuchniaUCygana.Domain.Entities.Packing;
using KuchniaUCygana.Domain.Enums;
using KuchniaUCygana.Domain.Interfaces.Packing;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using KuchniaUCygana.Domain.Interfaces;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories.Packing;

public sealed class PackingBagRepository : BaseRepository<PackingBag>, IPackingBagRepository
{
    public PackingBagRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService)
    {
    }

    public async Task<IEnumerable<PackingBag>> GetBySessionIdAsync(int packingSessionId)
    {
        using var db = Factory.CreateConnection();
        return await db.QueryAsync<PackingBag>(
            """
            SELECT *
            FROM PackingBags
            WHERE PackingSessionId = @packingSessionId
              AND IsDeleted = 0
            ORDER BY BagNumber, Id;
            """,
            new { packingSessionId });
    }

    public async Task<IReadOnlyList<PackingBag>> GetBySessionIdsAsync(IEnumerable<int> packingSessionIds)
    {
        var ids = packingSessionIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0)
        {
            return Array.Empty<PackingBag>();
        }

        using var db = Factory.CreateConnection();
        var bags = await db.QueryAsync<PackingBag>(
            """
            SELECT *
            FROM PackingBags
            WHERE PackingSessionId IN @ids
              AND IsDeleted = 0
            ORDER BY PackingSessionId, BagNumber, Id;
            """,
            new { ids });

        return bags.ToList();
    }

    public async Task<PackingBag?> GetByCodeAsync(string bagCode)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<PackingBag>(
            """
            SELECT TOP 1 *
            FROM PackingBags
            WHERE BagCode = @bagCode
              AND IsDeleted = 0
            ORDER BY Id DESC;
            """,
            new { bagCode });
    }

    public async Task<PackingBag?> GetDefaultForSessionAsync(int packingSessionId)
    {
        using var db = Factory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<PackingBag>(
            """
            SELECT TOP 1 *
            FROM PackingBags
            WHERE PackingSessionId = @packingSessionId
              AND IsDeleted = 0
              AND Status <> @damagedStatus
            ORDER BY BagNumber, Id;
            """,
            new { packingSessionId, damagedStatus = (int)PackingBagStatus.Damaged });
    }

    public async Task<PackingBagSearchResult> SearchBoardBagsAsync(PackingBagQuery query)
    {
        using var db = Factory.CreateConnection();
        var parameters = new DynamicParameters();
        AddBoardBagParameters(query, parameters);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        parameters.Add("Offset", (page - 1) * pageSize);
        parameters.Add("PageSize", pageSize);

        var filterWhere = BuildBoardBagFilterClause(query, parameters);
        var orderBy = ResolveBoardBagOrderBy(query.SortBy, query.SortDescending);
        var baseCte = BoardBagBaseCte;

        var sql = $"""
        {baseCte}
        SELECT
            COUNT(1) AS TotalBags,
            COALESCE(SUM(CASE WHEN Status IN (@PackedBagStatus, @LabeledBagStatus, @ManifestedBagStatus, @LoadedBagStatus, @DispatchedBagStatus) THEN 1 ELSE 0 END), 0) AS PackedBags,
            COALESCE(SUM(CASE WHEN Status IN (@LoadedBagStatus, @DispatchedBagStatus) THEN 1 ELSE 0 END), 0) AS LoadedBags
        FROM BagRows;

        {baseCte}
        SELECT
            RouteId,
            RouteName,
            VehicleId,
            VehicleRegistration,
            COUNT(1) AS TotalBags,
            COALESCE(SUM(CASE WHEN Status IN (@PackedBagStatus, @LabeledBagStatus, @ManifestedBagStatus, @LoadedBagStatus, @DispatchedBagStatus) THEN 1 ELSE 0 END), 0) AS PackedBags,
            COALESCE(SUM(CASE WHEN Status IN (@LoadedBagStatus, @DispatchedBagStatus) THEN 1 ELSE 0 END), 0) AS LoadedBags,
            COALESCE(SUM(CASE WHEN Status = @DispatchedBagStatus THEN 1 ELSE 0 END), 0) AS DispatchedBags,
            COALESCE(SUM(CASE WHEN TransportLabelId IS NULL THEN 1 ELSE 0 END), 0) AS MissingLabelBags,
            COALESCE(SUM(CASE WHEN TransportLabelId IS NOT NULL AND TransportLabelAttachedAt IS NULL THEN 1 ELSE 0 END), 0) AS UnattachedLabelBags
        FROM BagRows
        GROUP BY RouteId, RouteName, VehicleId, VehicleRegistration
        ORDER BY RouteName, RouteId;

        {baseCte}
        SELECT COUNT(1)
        FROM BagRows
        WHERE {filterWhere};

        {baseCte}
        SELECT
            PackingBagId,
            PackingSessionId,
            DeliveryCalendarId,
            BagNumber,
            BagCode,
            Status,
            SessionStatus,
            OrderId,
            ClientName,
            ClientPublicId,
            DietVariantId,
            Address,
            RouteId,
            RouteName,
            VehicleId,
            VehicleRegistration,
            StopNumber,
            DeliveryWindow,
            TotalBoxes,
            PackedBoxes,
            TransportLabelId,
            TransportLabelPrintNumber,
            TransportLabelAttachedAt,
            TransportLabelAttachedBy
        FROM BagRows
        WHERE {filterWhere}
        ORDER BY {orderBy}
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
        """;

        using var multi = await db.QueryMultipleAsync(sql, parameters);
        var summary = await multi.ReadSingleAsync<BoardBagSummaryRow>();
        var routes = (await multi.ReadAsync<PackingRouteSearchSummary>()).ToList();
        var totalCount = await multi.ReadSingleAsync<int>();
        var bags = (await multi.ReadAsync<PackingBagSearchRow>()).ToList();

        return new PackingBagSearchResult
        {
            Bags = bags,
            Routes = routes,
            TotalCount = totalCount,
            TotalBags = summary.TotalBags,
            PackedBags = summary.PackedBags,
            LoadedBags = summary.LoadedBags,
        };
    }

    private const string BoardBagBaseCte = """
        WITH BagRows AS (
            SELECT
                pb.Id AS PackingBagId,
                ps.Id AS PackingSessionId,
                ps.DeliveryCalendarId,
                pb.BagNumber,
                pb.BagCode,
                pb.Status,
                ps.Status AS SessionStatus,
                COALESCE(ps.OrderId, dc.OrderId, 0) AS OrderId,
                COALESCE(ps.ClientName, CONCAT(u.FirstName, ' ', u.LastName), '') AS ClientName,
                COALESCE(ps.ClientPublicId, LOWER(CONVERT(varchar(36), cp.PublicId))) AS ClientPublicId,
                COALESCE(itemStats.DietVariantId, 0) AS DietVariantId,
                COALESCE(
                    CASE
                        WHEN a.Id IS NULL THEN NULL
                        WHEN a.ApartmentNumber IS NULL OR a.ApartmentNumber = ''
                            THEN CONCAT(a.Street, ' ', a.BuildingNumber, ', ', a.PostalCode, ' ', a.City)
                        ELSE CONCAT(a.Street, ' ', a.BuildingNumber, '/', a.ApartmentNumber, ', ', a.PostalCode, ' ', a.City)
                    END,
                    '') AS Address,
                COALESCE(drs.RouteId, 0) AS RouteId,
                COALESCE(dr.Name, CASE WHEN drs.RouteId IS NULL THEN 'Bez trasy' ELSE CONCAT('Trasa #', drs.RouteId) END) AS RouteName,
                COALESCE(dr.VehicleId, 0) AS VehicleId,
                COALESCE(v.RegistrationNumber, '') AS VehicleRegistration,
                COALESCE(drs.SequenceNumber, 2147483647) AS StopNumber,
                COALESCE(
                    dw.Name,
                    CASE
                        WHEN dw.StartTime IS NULL AND dw.EndTime IS NULL THEN ''
                        ELSE CONCAT(CONVERT(varchar(5), dw.StartTime, 108), '-', CONVERT(varchar(5), dw.EndTime, 108))
                    END,
                    '') AS DeliveryWindow,
                COALESCE(itemStats.TotalBoxes, 0) AS TotalBoxes,
                COALESCE(itemStats.PackedBoxes, 0) AS PackedBoxes,
                latestLabel.Id AS TransportLabelId,
                COALESCE(latestLabel.PrintNumber, 0) AS TransportLabelPrintNumber,
                latestLabel.AttachedAt AS TransportLabelAttachedAt,
                latestLabel.AttachedBy AS TransportLabelAttachedBy
            FROM PackingBags pb
            INNER JOIN PackingSessions ps ON ps.Id = pb.PackingSessionId
            LEFT JOIN DeliveryCalendar dc ON dc.Id = ps.DeliveryCalendarId AND dc.IsDeleted = 0
            LEFT JOIN Orders o ON o.Id = COALESCE(ps.OrderId, dc.OrderId) AND o.IsDeleted = 0
            LEFT JOIN Users u ON u.Id = o.CustomerId
            LEFT JOIN CustomerProfiles cp ON cp.UserId = u.Id AND cp.IsDeleted = 0
            LEFT JOIN Addresses a ON a.Id = dc.AddressId AND a.IsDeleted = 0
            LEFT JOIN DeliveryWindows dw ON dw.Id = dc.DeliveryWindowId
            LEFT JOIN DeliveryRouteStops drs ON drs.DeliveryCalendarId = ps.DeliveryCalendarId AND drs.IsDeleted = 0
            LEFT JOIN DeliveryRoutes dr ON dr.Id = drs.RouteId AND dr.IsDeleted = 0
            LEFT JOIN Vehicles v ON v.Id = dr.VehicleId
            OUTER APPLY (
                SELECT TOP 1 pl.Id, pl.PrintNumber, pl.AttachedAt, pl.AttachedBy
                FROM PackingLabels pl
                WHERE pl.PackingBagId = pb.Id
                  AND pl.LabelType = @ShippingLabelType
                ORDER BY pl.PrintNumber DESC, pl.Id DESC
            ) latestLabel
            OUTER APPLY (
                SELECT
                    COUNT(1) AS TotalBoxes,
                    COALESCE(SUM(CASE WHEN pi.Status = @PackedItemStatus THEN 1 ELSE 0 END), 0) AS PackedBoxes,
                    MIN(NULLIF(pi.DietVariantId, 0)) AS DietVariantId
                FROM PackingItems pi
                WHERE pi.PackingSessionId = ps.Id
                  AND pi.IsDeleted = 0
                  AND (pi.PackingBagId = pb.Id OR (pi.PackingBagId IS NULL AND pb.BagNumber = 1))
            ) itemStats
            WHERE ps.PackingDate = @PackingDate
              AND ps.IsDeleted = 0
              AND pb.IsDeleted = 0
        )
        """;

    private static void AddBoardBagParameters(PackingBagQuery query, DynamicParameters parameters)
    {
        parameters.Add("PackingDate", query.PackingDate.ToDateTime(TimeOnly.MinValue));
        parameters.Add("ShippingLabelType", (int)LabelType.Shipping);
        parameters.Add("PackedItemStatus", (int)PackingItemStatus.Packed);
        parameters.Add("PackedBagStatus", (int)PackingBagStatus.Packed);
        parameters.Add("LabeledBagStatus", (int)PackingBagStatus.Labeled);
        parameters.Add("ManifestedBagStatus", (int)PackingBagStatus.Manifested);
        parameters.Add("LoadedBagStatus", (int)PackingBagStatus.Loaded);
        parameters.Add("DispatchedBagStatus", (int)PackingBagStatus.Dispatched);
    }

    private static string BuildBoardBagFilterClause(PackingBagQuery query, DynamicParameters parameters)
    {
        var clauses = new List<string> { "1 = 1" };

        if (query.RouteId.HasValue)
        {
            clauses.Add("RouteId = @RouteId");
            parameters.Add("RouteId", query.RouteId.Value);
        }

        if (query.Status.HasValue)
        {
            clauses.Add("Status = @BagStatus");
            parameters.Add("BagStatus", (int)query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            clauses.Add("""
                (
                    BagCode LIKE @SearchLike
                    OR ClientName LIKE @SearchLike
                    OR ClientPublicId LIKE @SearchLike
                    OR RouteName LIKE @SearchLike
                    OR VehicleRegistration LIKE @SearchLike
                    OR CONVERT(varchar(20), OrderId) = @SearchExact
                    OR CONVERT(varchar(20), DeliveryCalendarId) = @SearchExact
                )
                """);
            parameters.Add("SearchLike", $"%{search}%");
            parameters.Add("SearchExact", search);
        }

        switch (NormalizeLabelState(query.LabelState))
        {
            case "missing":
                clauses.Add("TransportLabelId IS NULL");
                break;
            case "generated":
                clauses.Add("TransportLabelId IS NOT NULL");
                break;
            case "attached":
                clauses.Add("TransportLabelId IS NOT NULL AND TransportLabelAttachedAt IS NOT NULL");
                break;
            case "not-attached":
                clauses.Add("TransportLabelId IS NOT NULL AND TransportLabelAttachedAt IS NULL");
                break;
        }

        return string.Join(" AND ", clauses);
    }

    private static string? NormalizeLabelState(string? labelState)
    {
        if (string.IsNullOrWhiteSpace(labelState) || labelState.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return labelState.Trim().ToLowerInvariant() switch
        {
            "missing" => "missing",
            "generated" => "generated",
            "attached" => "attached",
            "not-attached" => "not-attached",
            _ => null,
        };
    }

    private static string ResolveBoardBagOrderBy(string? sortBy, bool descending)
    {
        var direction = descending ? "DESC" : "ASC";
        var primary = sortBy?.Trim().ToLowerInvariant() switch
        {
            "bag" or "bagcode" => $"BagCode {direction}",
            "status" => $"Status {direction}",
            "client" => $"ClientName {direction}",
            "order" => $"OrderId {direction}",
            "stop" => $"StopNumber {direction}",
            "route" => $"RouteName {direction}, StopNumber {direction}",
            _ => $"RouteName {direction}, StopNumber {direction}, OrderId {direction}",
        };

        return $"{primary}, PackingBagId ASC";
    }

    private sealed class BoardBagSummaryRow
    {
        public int TotalBags { get; set; }

        public int PackedBags { get; set; }

        public int LoadedBags { get; set; }
    }
}


