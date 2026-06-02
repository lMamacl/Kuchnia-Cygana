using Dapper;
using KuchniaUCygana.Domain.Entities.Logistics;
using KuchniaUCygana.Domain.Interfaces.Logistics;
using KuchniaUCygana.Infrastructure.Persistence.ConnectionFactory;

namespace KuchniaUCygana.Infrastructure.Persistence.Repositories;

public sealed class DriverVehicleAssignmentRepository : IDriverVehicleAssignmentRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public DriverVehicleAssignmentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<DriverVehicleAssignment>> GetActiveAsync()
    {
        using var db = _connectionFactory.CreateConnection();
        var assignments = await db.QueryAsync<DriverVehicleAssignment>(
            "SELECT * FROM [DriverVehicleAssignments] WHERE [UnassignedAt] IS NULL;");
        return assignments.ToList();
    }

    public async Task<DriverVehicleAssignment?> GetActiveByDriverIdAsync(int driverId)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<DriverVehicleAssignment>(
            "SELECT * FROM [DriverVehicleAssignments] WHERE [DriverId] = @driverId AND [UnassignedAt] IS NULL;",
            new { driverId });
    }

    public async Task<DriverVehicleAssignment?> GetActiveByVehicleIdAsync(int vehicleId)
    {
        using var db = _connectionFactory.CreateConnection();
        return await db.QuerySingleOrDefaultAsync<DriverVehicleAssignment>(
            "SELECT * FROM [DriverVehicleAssignments] WHERE [VehicleId] = @vehicleId AND [UnassignedAt] IS NULL;",
            new { vehicleId });
    }

    public async Task AssignAsync(int driverId, int vehicleId)
    {
        using var db = _connectionFactory.CreateConnection();
        db.Open();
        using var transaction = db.BeginTransaction();

        var currentDriverAssignment = await db.QuerySingleOrDefaultAsync<DriverVehicleAssignment>(
            "SELECT * FROM [DriverVehicleAssignments] WHERE [DriverId] = @driverId AND [UnassignedAt] IS NULL;",
            new { driverId },
            transaction);
        if (currentDriverAssignment?.VehicleId == vehicleId)
        {
            transaction.Commit();
            return;
        }

        var currentVehicleAssignment = await db.QuerySingleOrDefaultAsync<DriverVehicleAssignment>(
            "SELECT * FROM [DriverVehicleAssignments] WHERE [VehicleId] = @vehicleId AND [UnassignedAt] IS NULL;",
            new { vehicleId },
            transaction);
        if (currentVehicleAssignment != null)
        {
            throw new InvalidOperationException("Wybrany pojazd jest juz przypisany do innego kierowcy.");
        }

        var now = DateTimeOffset.UtcNow;
        if (currentDriverAssignment != null)
        {
            await db.ExecuteAsync(
                """
                UPDATE [DriverVehicleAssignments]
                SET [UnassignedAt] = @now, [UpdatedAt] = @now
                WHERE [Id] = @id;
                """,
                new { now, id = currentDriverAssignment.Id },
                transaction);
        }

        await db.ExecuteAsync(
            """
            INSERT INTO [DriverVehicleAssignments]
                ([DriverId], [VehicleId], [AssignedAt], [UnassignedAt], [CreatedAt], [UpdatedAt])
            VALUES
                (@driverId, @vehicleId, @now, NULL, @now, NULL);
            """,
            new { driverId, vehicleId, now },
            transaction);
        transaction.Commit();
    }

    public Task<bool> UnassignDriverAsync(int driverId)
    {
        return UnassignAsync("[DriverId]", driverId);
    }

    public Task<bool> UnassignVehicleAsync(int vehicleId)
    {
        return UnassignAsync("[VehicleId]", vehicleId);
    }

    private async Task<bool> UnassignAsync(string column, int id)
    {
        using var db = _connectionFactory.CreateConnection();
        var now = DateTimeOffset.UtcNow;
        var affected = await db.ExecuteAsync(
            $"""
            UPDATE [DriverVehicleAssignments]
            SET [UnassignedAt] = @now, [UpdatedAt] = @now
            WHERE {column} = @id AND [UnassignedAt] IS NULL;
            """,
            new { id, now });
        return affected > 0;
    }
}
