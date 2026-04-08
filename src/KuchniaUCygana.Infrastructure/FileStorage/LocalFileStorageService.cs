namespace KuchniaUCygana.Infrastructure.FileStorage;

public sealed class LocalFileStorageService : IFileStorageService
{
    public async Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        var uploadsPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsPath);
        var filePath = Path.Combine(uploadsPath, fileName);

        await using var output = File.Create(filePath);
        await stream.CopyToAsync(output, cancellationToken);
        return filePath;
    }
}
