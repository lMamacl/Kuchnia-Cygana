namespace KuchniaUCygana.Application.DTOs.Menu;

public sealed class UploadImageRequest
{
    public int MealId { get; set; }

    public bool IsMain { get; set; }
}
