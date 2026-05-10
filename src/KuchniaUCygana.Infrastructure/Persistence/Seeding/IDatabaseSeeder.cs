using System.Threading;
using System.Threading.Tasks;

namespace KuchniaUCygana.Infrastructure.Persistence.Seeding;

public interface IDatabaseSeeder
{
    Task SeedAsync(DatabaseSeedingProfile profile, CancellationToken cancellationToken = default);
}
