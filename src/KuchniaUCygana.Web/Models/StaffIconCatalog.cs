namespace KuchniaUCygana.Web.Models;

public static class StaffIconCatalog
{
    /// <summary>
    /// Get SVG markup for a named staff icon.
    /// </summary>
    /// <param name="key">The icon name from the catalog (e.g., "admin", "alert", "bell"). If the name is not recognized, a default plus-sign icon is returned.</param>
    /// <returns>An SVG string containing the icon markup with fixed attributes (20x20, viewBox 0 0 24 24, stroke set to currentColor).</returns>
    public static string Get(string key)
    {
        var path = key switch
        {
            "admin" => "<path d=\"M12 3l7 4v5c0 5-3.5 8-7 9c-3.5-1-7-4-7-9V7z\"/><path d=\"M9 12l2 2l4-4\"/>",
            "alert" => "<path d=\"M12 9v4\"/><path d=\"M12 17h.01\"/><path d=\"M10.3 3.9L2.7 17a2 2 0 0 0 1.7 3h15.2a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z\"/>",
            "bell" => "<path d=\"M15 17h5l-1.4-1.4A2 2 0 0 1 18 14.2V11a6 6 0 1 0-12 0v3.2a2 2 0 0 1-.6 1.4L4 17h5\"/><path d=\"M10 21h4\"/>",
            "box" => "<path d=\"M12 3l8 4.5v9L12 21l-8-4.5v-9z\"/><path d=\"M12 12l8-4.5\"/><path d=\"M12 12v9\"/><path d=\"M12 12L4 7.5\"/>",
            "calendar" => "<path d=\"M4 7h16\"/><path d=\"M8 3v4\"/><path d=\"M16 3v4\"/><path d=\"M5 5h14a1 1 0 0 1 1 1v14H4V6a1 1 0 0 1 1-1z\"/>",
            "checklist" => "<path d=\"M9 6h11\"/><path d=\"M9 12h11\"/><path d=\"M9 18h11\"/><path d=\"M4 6l1 1l2-2\"/><path d=\"M4 12l1 1l2-2\"/><path d=\"M4 18l1 1l2-2\"/>",
            "clipboard" => "<path d=\"M9 5h6\"/><path d=\"M9 3h6a2 2 0 0 1 2 2v1h1a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h1V5a2 2 0 0 1 2-2z\"/>",
            "clipboard-check" => "<path d=\"M9 5h6\"/><path d=\"M9 3h6a2 2 0 0 1 2 2v1h1a2 2 0 0 1 2 2v11a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h1V5a2 2 0 0 1 2-2z\"/><path d=\"M9 14l2 2l4-4\"/>",
            "diets" => "<path d=\"M5 21c8-3 13-8 14-16c-8 1-13 6-16 14c2-1 4-1 6-1\"/><path d=\"M9 15c1-4 3-7 6-9\"/>",
            "grid" => "<path d=\"M4 4h6v6H4z\"/><path d=\"M14 4h6v6h-6z\"/><path d=\"M4 14h6v6H4z\"/><path d=\"M14 14h6v6h-6z\"/>",
            "home" => "<path d=\"M3 11l9-8l9 8\"/><path d=\"M5 10v10h14V10\"/><path d=\"M9 20v-6h6v6\"/>",
            "ingredients" => "<path d=\"M12 21c4-4 6-8 6-12a6 6 0 0 0-12 0c0 4 2 8 6 12z\"/><path d=\"M12 9v6\"/><path d=\"M9 12h6\"/>",
            "kitchen" => "<path d=\"M6 3v8\"/><path d=\"M10 3v8\"/><path d=\"M8 3v18\"/><path d=\"M14 3v18\"/><path d=\"M14 3c4 2 5 8 0 10\"/>",
            "logistics" => "<path d=\"M5 18a2 2 0 1 0 0-4a2 2 0 0 0 0 4z\"/><path d=\"M19 18a2 2 0 1 0 0-4a2 2 0 0 0 0 4z\"/><path d=\"M7 16h10\"/><path d=\"M3 16V8h11v8\"/><path d=\"M14 10h4l3 3v3\"/>",
            "map" => "<path d=\"M9 18l-6 3V6l6-3l6 3l6-3v15l-6 3z\"/><path d=\"M9 3v15\"/><path d=\"M15 6v15\"/>",
            "meal" => "<path d=\"M4 3v8\"/><path d=\"M8 3v8\"/><path d=\"M6 3v18\"/><path d=\"M14 3c3 2 4 6 4 10h-4v8\"/>",
            "mobile" => "<path d=\"M8 3h8a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2z\"/><path d=\"M11 18h2\"/>",
            "moon" => "<path d=\"M18 15.6A7 7 0 0 1 8.4 6a7.6 7.6 0 1 0 9.6 9.6z\"/>",
            "nutrition" => "<path d=\"M4 20c4-7 10-12 16-16\"/><path d=\"M5 5c6 0 10 4 10 10c-6 0-10-4-10-10z\"/>",
            "packing" => "<path d=\"M4 7l8-4l8 4v10l-8 4l-8-4z\"/><path d=\"M4 7l8 4l8-4\"/><path d=\"M12 11v10\"/>",
            "plus" => "<path d=\"M12 5v14\"/><path d=\"M5 12h14\"/>",
            "recipes" => "<path d=\"M6 4h11a2 2 0 0 1 2 2v14H8a3 3 0 0 1 0-6h11\"/><path d=\"M8 4v10\"/><path d=\"M10 8h5\"/>",
            "report" => "<path d=\"M7 3h7l5 5v13H7z\"/><path d=\"M14 3v5h5\"/><path d=\"M9 14h6\"/><path d=\"M9 18h6\"/>",
            "route" => "<path d=\"M6 19a3 3 0 1 0 0-6a3 3 0 0 0 0 6z\"/><path d=\"M18 11a3 3 0 1 0 0-6a3 3 0 0 0 0 6z\"/><path d=\"M8.5 14.5l7-6\"/>",
            "search" => "<path d=\"M10 18a8 8 0 1 1 5.7-2.3L21 21\"/>",
            "settings" => "<path d=\"M12 15a3 3 0 1 0 0-6a3 3 0 0 0 0 6z\"/><path d=\"M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1l-2 3l-.2-.1a1.7 1.7 0 0 0-2 .1a1.7 1.7 0 0 0-.9 1.7V22h-3.4v-.3a1.7 1.7 0 0 0-.9-1.7a1.7 1.7 0 0 0-2-.1l-.2.1l-2-3l.1-.1a1.7 1.7 0 0 0 .3-1.9a1.7 1.7 0 0 0-1.5-1.1H5v-3.8h.1A1.7 1.7 0 0 0 6.6 9a1.7 1.7 0 0 0-.3-1.9L6.2 7l2-3l.2.1a1.7 1.7 0 0 0 2-.1a1.7 1.7 0 0 0 .9-1.7V2h3.4v.3a1.7 1.7 0 0 0 .9 1.7a1.7 1.7 0 0 0 2 .1l.2-.1l2 3l-.1.1a1.7 1.7 0 0 0-.3 1.9a1.7 1.7 0 0 0 1.5 1.1h.1v3.8h-.1a1.7 1.7 0 0 0-1.5 1.1z\"/>",
            "shield" => "<path d=\"M12 3l7 4v5c0 5-3.5 8-7 9c-3.5-1-7-4-7-9V7z\"/>",
            "tag" => "<path d=\"M4 4h7l9 9l-7 7l-9-9z\"/><path d=\"M8 8h.01\"/>",
            "temperature" => "<path d=\"M10 14.5V5a2 2 0 1 1 4 0v9.5a4 4 0 1 1-4 0z\"/>",
            "trash" => "<path d=\"M4 7h16\"/><path d=\"M10 11v6\"/><path d=\"M14 11v6\"/><path d=\"M6 7l1 14h10l1-14\"/><path d=\"M9 7V4h6v3\"/>",
            "truck" => "<path d=\"M3 6h11v10H3z\"/><path d=\"M14 10h4l3 3v3h-7z\"/><path d=\"M7 19a2 2 0 1 0 0-4a2 2 0 0 0 0 4z\"/><path d=\"M17 19a2 2 0 1 0 0-4a2 2 0 0 0 0 4z\"/>",
            "users" => "<path d=\"M9 11a4 4 0 1 0 0-8a4 4 0 0 0 0 8z\"/><path d=\"M3 21v-1a6 6 0 0 1 12 0v1\"/><path d=\"M16 3.1a4 4 0 0 1 0 7.8\"/><path d=\"M21 21v-1a6 6 0 0 0-4-5.7\"/>",
            "vehicle" => "<path d=\"M5 17a2 2 0 1 0 0-4a2 2 0 0 0 0 4z\"/><path d=\"M19 17a2 2 0 1 0 0-4a2 2 0 0 0 0 4z\"/><path d=\"M7 15h10\"/><path d=\"M3 15V9l2-4h10l4 4v6\"/>",
            "wand" => "<path d=\"M4 20l10-10\"/><path d=\"M13 5l1 3l3 1l-3 1l-1 3l-1-3l-3-1l3-1z\"/><path d=\"M19 3v4\"/><path d=\"M17 5h4\"/>",
            "warehouse" => "<path d=\"M3 21V8l9-5l9 5v13\"/><path d=\"M7 21v-8h10v8\"/><path d=\"M9 17h6\"/>",
            _ => "<path d=\"M12 5v14\"/><path d=\"M5 12h14\"/>",
        };

        return $"<svg class=\"icon\" width=\"20\" height=\"20\" viewBox=\"0 0 24 24\" aria-hidden=\"true\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\">{path}</svg>";
    }
}
