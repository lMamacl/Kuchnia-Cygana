namespace KuchniaUCygana.Infrastructure.FileStorage;

public interface IFileStorageService
{
    Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
}
