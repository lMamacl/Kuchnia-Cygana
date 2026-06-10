using Dapper;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class LogisticsSbdReportRepository : ILogisticsSbdReportRepository
{
    private readonly IDbConnectionFactory connectionFactory;

    public LogisticsSbdReportRepository(IDbConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<LogisticsDailyDispatchBoardReport> GetDailyDispatchBoardAsync(
        DateTime deliveryDate,
        decimal estimatedDeliveryWeightKg)
    {
        using var db = this.connectionFactory.CreateConnection();
        using var results = await db.QueryMultipleAsync(
            """
            EXEC [logistics_pkg].[usp_GetDailyDispatchBoard]
                @DeliveryDate = @DeliveryDate,
                @EstimatedDeliveryWeightKg = @EstimatedDeliveryWeightKg;
            """,
            new
            {
                DeliveryDate = deliveryDate.Date,
                EstimatedDeliveryWeightKg = estimatedDeliveryWeightKg,
            });

        var routes = (await results.ReadAsync<LogisticsDailyDispatchRouteRow>()).ToList();
        var summary = await results.ReadFirstOrDefaultAsync<LogisticsDailyDispatchSummaryRow>()
            ?? new LogisticsDailyDispatchSummaryRow { DeliveryDate = deliveryDate.Date };

        return new LogisticsDailyDispatchBoardReport
        {
            Routes = routes,
            Summary = summary,
        };
    }
}
