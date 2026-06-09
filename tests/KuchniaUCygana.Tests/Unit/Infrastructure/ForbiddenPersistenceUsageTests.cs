using FluentAssertions;

namespace KuchniaUCygana.Tests.Unit.Infrastructure;

public sealed class ForbiddenPersistenceUsageTests
{
    [Fact]
    public void SourceFiles_ShouldNotUseRemovedOrmOrLicenseBypass()
    {
        var root = FindRepositoryRoot();
        var forbiddenTerms = new[]
        {
            "ServiceStack." + "OrmLite",
            "ServiceStack." + "DataAnnotations",
            "License" + "Utils",
            "__activated" + "License",
            "LicenseType." + "Enterprise",
        };

        var files = Directory.EnumerateFiles(root.FullName, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                           path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        var violations = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (var term in forbiddenTerms)
            {
                if (text.Contains(term, StringComparison.Ordinal))
                {
                    violations.Add($"{Path.GetRelativePath(root.FullName, file)} contains {term}");
                }
            }
        }

        violations.Should().BeEmpty();
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "KuchniaUCygana.sln")))
        {
            directory = directory.Parent;
        }

        return directory ?? throw new InvalidOperationException("Cannot locate repository root.");
    }
}
