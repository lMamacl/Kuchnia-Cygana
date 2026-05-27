using KuchniaUCygana.Application.DTOs.Menu;

namespace KuchniaUCygana.Application.Interfaces.Menu;

public interface IImageManagementService
{
    Task<MealImageDto> UploadAsync(int mealId, Stream stream, string fileName, bool isMain = false);

    Task SetMainAsync(int imageId);

    Task DeleteAsync(int imageId);
}
