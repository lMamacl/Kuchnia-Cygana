using FluentAssertions;

namespace KuchniaUCygana.Tests.Unit.Infrastructure;

public sealed class ForbiddenPersistenceUsageTests
{
    [Fact]
    public void Source_Should_Not_Use_Removed_ServiceStack_Persistence_Apis()
    {
        var root = FindRepositoryRoot();
        var forbidden = new[]
        {
            "ServiceStack." + "OrmLite",
            "ServiceStack." + "DataAnnotations",
            "License" + "Utils",
            "__activated" + "License",
        };

        var files = Directory
            .EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path =>
                path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(path =>
                !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) &&
                !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var hits = files
            .SelectMany(path => forbidden
                .Where(term => File.ReadAllText(path).Contains(term, StringComparison.Ordinal))
                .Select(term => $"{Path.GetRelativePath(root, path)} contains {term}"))
            .ToArray();

        hits.Should().BeEmpty();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.GetFiles("KuchniaUCygana.sln").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }
}
