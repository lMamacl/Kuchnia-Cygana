using Microsoft.AspNetCore.Mvc;

namespace KuchniaUCygana.Web.Controllers;

[Route("ingredients")]
public sealed class IngredientController : Controller
{
    /// <summary>
    /// Displays the ingredients catalog page.
    /// </summary>
    /// <remarks>
    /// Prepares view data keys "Title", "Section", and "Description" for the catalog view.
    /// </remarks>
    /// <returns>The view result for the ingredients catalog page.</returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Katalog skladnikow";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = "Wspolny katalog M2/M3 do wyszukiwania i zarzadzania skladnikami.";
        return View();
    }

    /// <summary>
    /// Renders the view for creating a new ingredient.
    /// </summary>
    /// <returns>The view result for the ingredient creation page.</returns>
    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "Dodaj skladnik";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = "Szkielet formularza tworzenia skladnika.";
        return View();
    }

    /// <summary>
    /// Shows the edit page for an ingredient.
    /// </summary>
    /// <param name="id">Optional ingredient identifier; when provided the page is prepared for editing that ingredient, otherwise a placeholder without an identifier is shown.</param>
    /// <returns>An <see cref="IActionResult"/> that renders the ingredient edit view.</returns>
    [HttpGet("{id:int?}")]
    public IActionResult Details(int? id)
    {
        ViewData["Title"] = "Edycja skladnika";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = id.HasValue
            ? $"Placeholder edycji skladnika #{id}."
            : "Placeholder edycji skladnika bez wybranego identyfikatora.";
        return View();
    }

    /// <summary>
    /// Shows the nutrition values view for an ingredient.
    /// </summary>
    /// <param name="id">Optional ingredient identifier; when provided the description includes the identifier.</param>
    /// <returns>An <see cref="IActionResult"/> that renders the nutrition view. <see cref="ViewData"/> keys "Title", "Section", and "Description" are populated for the view.</returns>
    [HttpGet("nutrition")]
    [HttpGet("{id:int}/nutrition")]
    public IActionResult Nutrition(int? id)
    {
        ViewData["Title"] = "Wartosci odzywcze";
        ViewData["Section"] = "Skladniki";
        ViewData["Description"] = id.HasValue
            ? $"Placeholder wartosci odzywczych skladnika #{id}."
            : "Placeholder wartosci odzywczych skladnika.";
        return View();
    }
}
