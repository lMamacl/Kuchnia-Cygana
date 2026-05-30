namespace KuchniaUCygana.Web.Models;

public sealed record StaffNavItem(
    string Label,
    string Controller,
    string Action,
    string Icon,
    string Description,
    string Keywords = "")
{
    public bool Matches(string? controller, string? action)
    {
        return string.Equals(this.Controller, controller, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(this.Action, action, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchesController(string? controller)
    {
        return string.Equals(this.Controller, controller, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record StaffNavSection(
    string Key,
    string Label,
    string Icon,
    string Description,
    bool ShowInTopbar,
    IReadOnlyList<StaffNavItem> Items)
{
    public StaffNavItem PrimaryItem => this.Items[0];

    public bool IsActive(string? controller)
    {
        return this.Items.Any(item => item.MatchesController(controller));
    }
}

public static class StaffNavigationCatalog
{
    public static IReadOnlyList<StaffNavSection> Sections { get; } =
    [
        new(
            "dashboard",
            "Dashboard",
            "home",
            "Centralna mapa dzialow Sprint 4B.",
            true,
            [
                new("Mapa dzialow", "Staff", "Index", "home", "Start panelu pracowniczego i szybkie przejscia.", "staff dashboard home mapa dzialow"),
            ]),
        new(
            "kitchen",
            "Kuchnia",
            "kitchen",
            "Plan dnia, generowanie produkcji i karty gotowania.",
            true,
            [
                new("Pulpit kuchni", "Production", "Index", "kitchen", "Zbiorczy ekran dzialu kuchni.", "produkcja kuchnia plan"),
                new("Generuj plan", "Production", "Generate", "wand", "Przygotowanie dziennego planu produkcji.", "generowanie planu produkcji"),
                new("Plan dnia", "Production", "Plan", "calendar", "Podglad planu produkcyjnego.", "plan dnia produkcji"),
                new("Karty gotowania", "Production", "CookingCards", "clipboard", "Lista kart gotowania do realizacji.", "karty gotowania receptury"),
                new("Karta gotowania", "Production", "CookingCard", "clipboard-check", "Podglad pojedynczej karty gotowania.", "karta gotowania szczegoly"),
                new("Foliowanie dań", "Production", "FoilPrinting", "tag", "Ekran foliowania dań i druku etykiet produktowych.", "foliowanie etykiety pakowanie"),
            ]),
        new(
            "warehouse",
            "Magazyn",
            "warehouse",
            "Stany, przyjęcia, odpady, inwentaryzacja i HACCP.",
            true,
            [
                new("Stany i alerty", "Warehouse", "Index", "warehouse", "Pulpit magazynu z alertami.", "magazyn stany alerty"),
                new("Przyjęcia dostaw", "Warehouse", "Receive", "truck", "Szkielet przyjmowania dostaw.", "dostawy przyjęcia magazyn"),
                new("Wydanie ręczne", "Warehouse", "Issue", "plus", "Ręczne wydanie składnika.", "wydanie ręczne magazyn rozchód"),
                new("Raport FEFO", "Warehouse", "FefoReport", "calendar", "Kontrola ważności partii.", "fefo ważność przeterminowanie"),
                new("Historia transakcji", "Warehouse", "TransactionHistory", "history", "Historia operacji magazynowych.", "transakcje historia dziennik"),
                new("Odpady", "Warehouse", "Waste", "trash", "Rejestr odpadów i strat.", "odpady straty magazyn"),
                new("Inwentaryzacja", "Warehouse", "Inventory", "checklist", "Ekran inwentaryzacji.", "inwentaryzacja remanent"),
                new("Dziennik i Raport HACCP", "Warehouse", "HaccpReport", "report", "Dziennik pomiarów oraz raport kontrolny HACCP.", "raport haccp"),
            ]),
        new(
            "ingredients",
            "Skladniki",
            "ingredients",
            "Katalog skladnikow i wartosci odzywcze.",
            true,
            [
                new("Katalog skladnikow", "Ingredient", "Index", "ingredients", "Lista skladnikow do podpiecia z M2/M3.", "skladniki katalog"),
                new("Dodaj skladnik", "Ingredient", "Create", "plus", "Szkielet formularza skladnika.", "nowy skladnik dodaj"),
                new("Wartosci odzywcze", "Ingredient", "Nutrition", "nutrition", "Makro i alergeny skladnika.", "wartosci odzywcze makro alergeny"),
            ]),
        new(
            "packing",
            "Kompletacja",
            "packing",
            "Kompletacja toreb, zaladunek aut i manifesty dostaw.",
            true,
            [
                new("Kompletacja toreb", "Packing", "Index", "packing", "Pakowanie pudelek do toreb.", "pakowanie kompletacja torby"),
                new("Zaladunek aut", "Packing", "Loading", "truck", "Manifesty dostaw i zaladunek aut.", "zaladunek auta manifest dostawa"),
                new("Etykiety transportowe", "Packing", "LabelsIndex", "tag", "Etykiety transportowe w trybie preview.", "etykiety qr transport"),
            ]),
        new(
            "diets",
            "Diety i przepisy",
            "diets",
            "Konfiguracja diet, posilkow i przepisow.",
            true,
            [
                new("Edytor diet", "DietEditor", "Index", "diets", "Dashboard dietetyka.", "diety edytor"),
                new("Nowa dieta", "DietEditor", "Create", "plus", "Kreator nowej diety.", "nowa dieta"),
                new("Posilki", "DietEditor", "Meals", "meal", "Posilki w diecie.", "posilki dieta"),
                new("Przepisy", "DietEditor", "Recipes", "recipes", "Baza przepisow.", "przepisy receptury"),
                new("Edycja przepisu", "DietEditor", "Recipe", "recipes", "Podglad pojedynczego przepisu.", "przepis edycja"),
            ]),
        new(
            "logistics",
            "Logistyka",
            "logistics",
            "Trasy, mapa, flota i kierowcy.",
            true,
            [
                new("Dashboard logistyki", "Logistics", "Index", "logistics", "Zbiorczy ekran logistyki.", "logistyka dashboard"),
                new("Trasy", "Logistics", "Routes", "route", "Lista tras.", "trasy dostawy"),
                new("Nowa trasa", "Logistics", "CreateRoute", "plus", "Kreator trasy.", "nowa trasa"),
                new("Mapa tras", "Logistics", "RouteMap", "map", "Mapa tras i stopow.", "mapa tras"),
                new("Flota", "Logistics", "Vehicles", "vehicle", "Pojazdy i gotowosc floty.", "flota pojazdy"),
                new("Kierowcy", "Logistics", "Drivers", "users", "Lista kierowcow.", "kierowcy logistyka"),
            ]),
        new(
            "admin",
            "Administracja",
            "admin",
            "Uzytkownicy, role i ustawienia systemu.",
            true,
            [
                new("Dashboard admina", "Admin", "Index", "admin", "Pulpit administracyjny.", "admin administracja"),
                new("Uzytkownicy", "Admin", "Users", "users", "Zarzadzanie uzytkownikami.", "uzytkownicy"),
                new("Role", "Admin", "Roles", "shield", "Role i uprawnienia.", "role uprawnienia"),
                new("Ustawienia", "Admin", "Settings", "settings", "Ustawienia systemowe.", "ustawienia konfiguracja"),
            ]),
        new(
            "mobile",
            "Mobile",
            "mobile",
            "Mobilny podglad pracy kierowcy.",
            false,
            [
                new("Panel kierowcy", "DriverMobile", "Index", "mobile", "Start widoku kierowcy.", "driver kierowca mobile trasa"),
                new("Stop", "DriverMobile", "Stop", "route", "Aktywny stop kierowcy.", "stop dostawa"),
                new("Potwierdzenie", "DriverMobile", "Confirm", "clipboard-check", "Potwierdzenie dostawy.", "potwierdzenie dostawy"),
                new("Problem", "DriverMobile", "Problem", "alert", "Zgloszenie problemu na trasie.", "problem dostawa"),
                new("Podsumowanie", "DriverMobile", "Summary", "report", "Podsumowanie dnia kierowcy.", "podsumowanie kierowcy"),
            ]),
    ];

    public static IReadOnlyList<StaffNavItem> Items { get; } = Sections
        .SelectMany(section => section.Items)
        .ToArray();

    public static StaffNavSection? FindSection(string? controller)
    {
        return Sections.FirstOrDefault(section => section.IsActive(controller));
    }

    public static StaffNavItem? FindItem(string? controller, string? action)
    {
        return Items.FirstOrDefault(item => item.Matches(controller, action));
    }
}
