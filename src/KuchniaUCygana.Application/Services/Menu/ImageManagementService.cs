using AutoMapper;
using KuchniaUCygana.Application.DTOs.Menu;
using KuchniaUCygana.Application.Interfaces.Menu;
using KuchniaUCygana.Domain.Entities.Menu;
using KuchniaUCygana.Domain.Interfaces.Repositories.Menu;

namespace KuchniaUCygana.Application.Services.Menu;

public sealed class ImageManagementService : IImageManagementService
{
    private readonly IMealImageRepository mealImageRepository;
    private readonly IInternalFileStorageService fileStorage;
    private readonly IMapper mapper;

    public ImageManagementService(
        IMealImageRepository mealImageRepository,
        IInternalFileStorageService fileStorage,
        IMapper mapper)
    {
        this.mealImageRepository = mealImageRepository;
        this.fileStorage = fileStorage;
        this.mapper = mapper;
    }

    public async Task<MealImageDto> UploadAsync(int mealId, Stream stream, string fileName, bool isMain = false)
    {
        var url = await this.fileStorage.SaveAsync(stream, fileName);
        var image = new MealImage
        {
            MealId = mealId,
            Url = url,
            FileName = fileName,
            IsMain = isMain,
            FileSizeBytes = stream.Length
        };
        await this.mealImageRepository.InsertAsync(image);
        return this.mapper.Map<MealImageDto>(image);
    }

    public async Task SetMainAsync(int imageId)
    {
        var image = await this.mealImageRepository.GetByIdAsync(imageId);
        if (image is null)
        {
            return;
        }

        var images = await this.mealImageRepository.GetByMealIdAsync(image.MealId);
        foreach (var img in images)
        {
            if (img.Id == imageId)
            {
                continue;
            }

            img.IsMain = false;
            await this.mealImageRepository.UpdateAsync(img);
        }

        image.IsMain = true;
        await this.mealImageRepository.UpdateAsync(image);
    }

    public async Task DeleteAsync(int imageId)
    {
        await this.mealImageRepository.DeleteAsync(imageId);
    }
}
