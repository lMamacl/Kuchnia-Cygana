using System.Threading;
using System.Threading.Tasks;

namespace KuchniaUCygana.Infrastructure.Persistence.Seeding;

public interface IDatabaseSeeder
{
    /// <summary>
/// Performs database seeding according to the specified seeding profile.
/// </summary>
/// <param name="profile">Describes which data and behaviors should be applied during seeding.</param>
/// <param name="cancellationToken">Token to observe while performing the operation to support cancellation.</param>
/// <returns>A task that completes when the seeding operation has finished.</returns>
Task SeedAsync(DatabaseSeedingProfile profile, CancellationToken cancellationToken = default);
}
