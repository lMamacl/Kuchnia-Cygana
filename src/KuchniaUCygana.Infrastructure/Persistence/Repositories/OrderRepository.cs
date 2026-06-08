using System.Data;
using Dapper;
using KuchniaUCygana.Domain.Entities.Orders;
using KuchniaUCygana.Domain.Interfaces;
using KuchniaUCygana.Domain.Interfaces.Orders;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;
using Microsoft.Data.SqlClient;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository : BaseRepository<Order>, IOrderRepository
{
    private const int MaxCheckoutInsertAttempts = 3;

    public OrderRepository(IDbConnectionFactory factory, ICurrentUserService? currentUserService = null) : base(factory, currentUserService) { }

    public async Task<IEnumerable<Order>> GetByCustomerIdAsync(int customerId)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM Orders WHERE CustomerId = @CustomerId AND IsDeleted = 0 ORDER BY CreatedAt DESC";
        return await db.QueryAsync<Order>(sql, new { CustomerId = customerId });
    }

    public async Task<(IReadOnlyList<CustomerOrderSearchRow> Items, int TotalCount)> SearchByCustomerAsync(CustomerOrderSearchQuery query)
    {
        using var db = Factory.CreateConnection();

        var pageSize = Math.Clamp(query.PageSize <= 0 ? 10 : query.PageSize, 1, 100);
        var page = Math.Max(1, query.Page);
        var offset = (page - 1) * pageSize;
        var orderNumber = NormalizeSearch(query.OrderNumber);
        var dateFrom = query.DateFrom?.Date;
        var dateToExclusive = query.DateTo?.Date.AddDays(1);

        var parameters = new
        {
            CustomerId = query.CustomerId,
            Status = (int?)query.Status,
            DateFrom = dateFrom,
            DateToExclusive = dateToExclusive,
            OrderNumber = orderNumber,
            OrderNumberLike = $"%{orderNumber}%",
            Offset = offset,
            PageSize = pageSize,
        };

        const string whereSql = """
            FROM [Orders] o
            WHERE o.[CustomerId] = @CustomerId
              AND o.[IsDeleted] = 0
              AND (@Status IS NULL OR o.[Status] = @Status)
              AND (@DateFrom IS NULL OR o.[CreatedAt] >= @DateFrom)
              AND (@DateToExclusive IS NULL OR o.[CreatedAt] < @DateToExclusive)
              AND (@OrderNumber IS NULL OR o.[OrderNumber] LIKE @OrderNumberLike)
            """;

        var totalCount = await db.ExecuteScalarAsync<int>(
            $"SELECT COUNT(1) {whereSql};",
            parameters);

        var rows = (await db.QueryAsync<CustomerOrderSearchRow>(
            $"""
            SELECT
                o.[Id],
                o.[OrderNumber],
                o.[Status],
                o.[FinalPrice],
                o.[StartDate],
                o.[EndDate],
                o.[CreatedAt],
                (
                    SELECT COUNT(1)
                    FROM [OrderItems] oi
                    WHERE oi.[OrderId] = o.[Id]
                      AND oi.[IsDeleted] = 0
                ) AS [ItemCount]
            {whereSql}
            ORDER BY o.[CreatedAt] DESC, o.[Id] DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """,
            parameters)).ToList();

        return (rows, totalCount);
    }

    public async Task<Order?> GetWithItemsAndDeliveryAsync(int orderId)
    {
        using var db = Factory.CreateConnection();

        const string sqlOrder = "SELECT * FROM Orders WHERE Id = @OrderId AND IsDeleted = 0";
        var order = await db.QuerySingleOrDefaultAsync<Order>(sqlOrder, new { OrderId = orderId });

        if (order is null) return null;

        const string sqlItems = """
            SELECT *
            FROM OrderItems
            WHERE OrderId = @OrderId
              AND IsDeleted = 0
            ORDER BY
                CASE WHEN DeliveryDate IS NULL THEN 1 ELSE 0 END,
                DeliveryDate,
                DietName,
                DietVariantId,
                DietMenuPlanItemId,
                Id;
            """;
        order.Items = (await db.QueryAsync<OrderItem>(sqlItems, new { OrderId = orderId })).ToList();

        const string sqlDays = "SELECT * FROM DeliveryCalendar WHERE OrderId = @OrderId AND IsDeleted = 0 ORDER BY DeliveryDate";
        order.DeliveryDays = (await db.QueryAsync<DeliveryCalendar>(sqlDays, new { OrderId = orderId })).ToList();

        return order;
    }

    public async Task<IEnumerable<Order>> GetActiveOrdersForDateAsync(DateTime date)
    {
        using var db = Factory.CreateConnection();
        var dateOnly = date.Date;
        var nextDay = dateOnly.AddDays(1);

        const string sql = @"
            SELECT o.* FROM Orders o
            INNER JOIN DeliveryCalendar d ON o.Id = d.OrderId
            WHERE o.IsDeleted = 0
              AND d.DeliveryDate >= @DateOnly 
              AND d.DeliveryDate < @NextDay 
              AND d.IsDeleted = 0 
              AND d.IsSkipped = 0";

        return await db.QueryAsync<Order>(sql, new { DateOnly = dateOnly, NextDay = nextDay });
    }

    public async Task<Order?> GetByOrderNumberAsync(string orderNumber)
    {
        using var db = Factory.CreateConnection();
        const string sql = "SELECT * FROM Orders WHERE OrderNumber = @OrderNumber AND IsDeleted = 0";
        return await db.QuerySingleOrDefaultAsync<Order>(sql, new { OrderNumber = orderNumber });
    }

    public async Task<string> GenerateOrderNumberAsync()
    {
        using var db = Factory.CreateConnection();
        db.Open();
        using var transaction = db.BeginTransaction(IsolationLevel.Serializable);
        var orderNumber = await GenerateOrderNumberAsync(db, transaction, DateTime.UtcNow.Date);
        transaction.Commit();
        return orderNumber;
    }

    public async Task<int> InsertCheckoutAsync(
        Order order,
        IReadOnlyCollection<OrderItem> items,
        IReadOnlyCollection<DeliveryCalendar> deliveryDays)
    {
        if (items.Count == 0)
        {
            throw new InvalidOperationException("Zamowienie musi zawierac przynajmniej jedna pozycje.");
        }

        for (var attempt = 1; attempt <= MaxCheckoutInsertAttempts; attempt++)
        {
            using var db = Factory.CreateConnection();
            db.Open();
            using var transaction = db.BeginTransaction(IsolationLevel.Serializable);

            try
            {
                var now = DateTimeOffset.UtcNow;
                order.CreatedAt = order.CreatedAt == default ? now : order.CreatedAt;
                order.OrderNumber = await GenerateOrderNumberAsync(db, transaction, order.CreatedAt.UtcDateTime.Date);

                var orderId = await InsertOrderAsync(db, transaction, order);
                order.Id = orderId;

                foreach (var item in items)
                {
                    item.OrderId = orderId;
                    item.CreatedAt = item.CreatedAt == default ? now : item.CreatedAt;
                    item.Id = await InsertOrderItemAsync(db, transaction, item);
                }

                foreach (var day in deliveryDays)
                {
                    day.OrderId = orderId;
                    day.CreatedAt = day.CreatedAt == default ? now : day.CreatedAt;
                    day.Id = await InsertDeliveryCalendarAsync(db, transaction, day);
                }

                transaction.Commit();
                return orderId;
            }
            catch (SqlException ex) when (IsRetryableCheckoutInsert(ex) && attempt < MaxCheckoutInsertAttempts)
            {
                RollbackQuietly(transaction);
            }
            catch
            {
                RollbackQuietly(transaction);
                throw;
            }
        }

        throw new InvalidOperationException("Nie udalo sie utworzyc unikalnego numeru zamowienia. Sprobuj ponownie.");
    }

    private static async Task<string> GenerateOrderNumberAsync(IDbConnection db, IDbTransaction transaction, DateTime utcDate)
    {
        var prefix = $"ORD-{utcDate:yyyyMMdd}-";
        var lastSuffix = await db.ExecuteScalarAsync<int>(
            """
            SELECT ISNULL(MAX(TRY_CONVERT(int, RIGHT([OrderNumber], 4))), 0)
            FROM [Orders] WITH (UPDLOCK, HOLDLOCK)
            WHERE [OrderNumber] LIKE @PrefixLike;
            """,
            new { PrefixLike = prefix + "%" },
            transaction);

        return $"{prefix}{lastSuffix + 1:D4}";
    }

    private static Task<int> InsertOrderAsync(IDbConnection db, IDbTransaction transaction, Order order)
        => db.ExecuteScalarAsync<int>(
            """
            INSERT INTO [Orders]
                ([CustomerId], [OrderNumber], [Status], [TotalPrice], [DiscountAmount], [FinalPrice],
                 [DiscountCodeId], [Notes], [StartDate], [EndDate], [CreatedAt], [UpdatedAt],
                 [CreatedBy], [UpdatedBy], [IsDeleted], [DeletedAt], [DeletedBy])
            OUTPUT INSERTED.[Id]
            VALUES
                (@CustomerId, @OrderNumber, @Status, @TotalPrice, @DiscountAmount, @FinalPrice,
                 @DiscountCodeId, @Notes, @StartDate, @EndDate, @CreatedAt, @UpdatedAt,
                 @CreatedBy, @UpdatedBy, @IsDeleted, @DeletedAt, @DeletedBy);
            """,
            new
            {
                order.CustomerId,
                order.OrderNumber,
                Status = (int)order.Status,
                order.TotalPrice,
                order.DiscountAmount,
                order.FinalPrice,
                order.DiscountCodeId,
                order.Notes,
                order.StartDate,
                order.EndDate,
                CreatedAt = ToSqlDateTime(order.CreatedAt),
                UpdatedAt = ToSqlDateTime(order.UpdatedAt),
                order.CreatedBy,
                order.UpdatedBy,
                order.IsDeleted,
                order.DeletedAt,
                order.DeletedBy,
            },
            transaction);

    private static Task<int> InsertOrderItemAsync(IDbConnection db, IDbTransaction transaction, OrderItem item)
        => db.ExecuteScalarAsync<int>(
            """
            INSERT INTO [OrderItems]
                ([OrderId], [DietId], [DietVariantId], [MealId], [MealVariantId], [DietMenuPlanItemId],
                 [DietName], [VariantName], [MealSlot], [DeliveryDate], [CaloriesPerDay], [PricePerDay],
                 [TotalDays], [TotalPrice], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy],
                 [IsDeleted], [DeletedAt], [DeletedBy])
            OUTPUT INSERTED.[Id]
            VALUES
                (@OrderId, @DietId, @DietVariantId, @MealId, @MealVariantId, @DietMenuPlanItemId,
                 @DietName, @VariantName, @MealSlot, @DeliveryDate, @CaloriesPerDay, @PricePerDay,
                 @TotalDays, @TotalPrice, @CreatedAt, @UpdatedAt, @CreatedBy, @UpdatedBy,
                 @IsDeleted, @DeletedAt, @DeletedBy);
            """,
            new
            {
                item.OrderId,
                item.DietId,
                item.DietVariantId,
                item.MealId,
                item.MealVariantId,
                item.DietMenuPlanItemId,
                item.DietName,
                item.VariantName,
                item.MealSlot,
                item.DeliveryDate,
                item.CaloriesPerDay,
                item.PricePerDay,
                item.TotalDays,
                item.TotalPrice,
                CreatedAt = ToSqlDateTime(item.CreatedAt),
                UpdatedAt = ToSqlDateTime(item.UpdatedAt),
                item.CreatedBy,
                item.UpdatedBy,
                item.IsDeleted,
                item.DeletedAt,
                item.DeletedBy,
            },
            transaction);

    private static Task<int> InsertDeliveryCalendarAsync(IDbConnection db, IDbTransaction transaction, DeliveryCalendar day)
        => db.ExecuteScalarAsync<int>(
            """
            INSERT INTO [DeliveryCalendar]
                ([OrderId], [AddressId], [DeliveryWindowId], [DeliveryDate], [Status], [IsSkipped],
                 [SkipReason], [CutoffTime], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy],
                 [IsDeleted], [DeletedAt], [DeletedBy])
            OUTPUT INSERTED.[Id]
            VALUES
                (@OrderId, @AddressId, @DeliveryWindowId, @DeliveryDate, @Status, @IsSkipped,
                 @SkipReason, @CutoffTime, @CreatedAt, @UpdatedAt, @CreatedBy, @UpdatedBy,
                 @IsDeleted, @DeletedAt, @DeletedBy);
            """,
            new
            {
                day.OrderId,
                day.AddressId,
                day.DeliveryWindowId,
                day.DeliveryDate,
                Status = (int)day.Status,
                day.IsSkipped,
                day.SkipReason,
                day.CutoffTime,
                CreatedAt = ToSqlDateTime(day.CreatedAt),
                UpdatedAt = ToSqlDateTime(day.UpdatedAt),
                day.CreatedBy,
                day.UpdatedBy,
                day.IsDeleted,
                day.DeletedAt,
                day.DeletedBy,
            },
            transaction);

    private static bool IsRetryableCheckoutInsert(SqlException ex)
        => ex.Number is 1205 or 2601 or 2627;

    private static void RollbackQuietly(IDbTransaction transaction)
    {
        try
        {
            transaction.Rollback();
        }
        catch
        {
            // Rollback can fail when SQL Server already ended the transaction.
        }
    }

    private static DateTime ToSqlDateTime(DateTimeOffset value)
        => value.UtcDateTime;

    private static DateTime? ToSqlDateTime(DateTimeOffset? value)
        => value?.UtcDateTime;

    private static string? NormalizeSearch(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
