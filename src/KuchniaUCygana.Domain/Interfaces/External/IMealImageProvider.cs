namespace KuchniaUCygana.Domain.Interfaces.External;

public interface IMealImageProvider
{
    Task<string?> GetMainImageUrlAsync(int mealId);
}
