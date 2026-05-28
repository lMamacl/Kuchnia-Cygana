using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Infrastructure.FileStorage;

namespace KuchniaUCygana.Infrastructure.Adapters;

public sealed class InternalFileStorageAdapter : IInternalFileStorageService
{
    private readonly IFileStorageService fileStorageService;

    public InternalFileStorageAdapter(IFileStorageService fileStorageService)
    {
        this.fileStorageService = fileStorageService;
    }

    public Task<string> SaveAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        return this.fileStorageService.SaveAsync(stream, fileName, cancellationToken);
    }
}
