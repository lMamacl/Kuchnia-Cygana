namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IInternalFileStorageService
{
    Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken = default);
}
