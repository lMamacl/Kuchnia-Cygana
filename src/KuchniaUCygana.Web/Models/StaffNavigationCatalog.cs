using System.Security.Claims;
using KuchniaUCygana.Domain.Enums;

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
            "Moj panel",
            "home",
            "Profil pracownika, grafik i wnioski urlopowe.",
            true,
            [
                new("Profil i grafik", "Account", "Profile", "home", "Osobisty panel pracownika.", "profil grafik urlopy pracownik"),
                new("Powiadomienia", "Notifications", "Index", "bell", "Lista powiadomien pracownika.", "powiadomienia alerty pracownik"),
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
                new("Ponowne przygotowanie", "Production", "Rework", "alert", "Zadania kuchni z awarii kompletacji.", "ponowne przygotowanie awarie kompletacja"),
                new("Foliowanie dań", "Production", "FoilPrinting", "tag", "Ekran foliowania dań i druku etykiet produktowych.", "foliowanie etykiety pakowanie"),
            ]),
        new(
            "warehouse",
            "Magazyn",
            "warehouse",
            "Stany, przyjecia, odpady, inwentaryzacja i HACCP.",
            true,
            [
                new("Stany i alerty", "Warehouse", "Index", "warehouse", "Pulpit magazynu z alertami.", "magazyn stany alerty"),
                new("Przyjecia dostaw", "Warehouse", "Receive", "truck", "Szkielet przyjmowania dostaw.", "dostawy przyjecia magazyn"),
                new("Wydania reczne", "Warehouse", "Issue", "box", "Rozchod magazynowy poza kompletacja.", "wydania reczne rozchod magazyn"),
                new("Raport FEFO", "Warehouse", "FefoReport", "report", "Kontrola partii wedlug FEFO.", "fefo partie terminy magazyn"),
                new("Historia transakcji", "Warehouse", "TransactionHistory", "clipboard", "Operacyjna historia ruchow magazynowych.", "historia transakcje magazyn ruchy"),
                new("Odpady", "Warehouse", "Waste", "trash", "Rejestr odpadow i strat.", "odpady straty magazyn"),
                new("Inwentaryzacja", "Warehouse", "Inventory", "checklist", "Ekran inwentaryzacji.", "inwentaryzacja remanent"),
                new("Temperatury HACCP", "Warehouse", "Temperatures", "temperature", "Monitoring temperatur HACCP.", "temperatury haccp"),
                new("Raport HACCP", "Warehouse", "HaccpReport", "report", "Raport kontrolny HACCP.", "raport haccp"),
                new("Lokalizacje HACCP", "Warehouse", "HaccpLocations", "map", "Punkty pomiarowe HACCP i przypisane kategorie.", "lokalizacje haccp punkty pomiarowe"),
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
                new("Zaladunek aut", "Loading", "Index", "truck", "Manifesty dostaw i zaladunek aut.", "zaladunek auta manifest dostawa"),
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
            "hr",
            "HR",
            "department",
            "Kadry, pracownicy, urlopy i grafik zmian.",
            true,
            [
                new("Dashboard HR", "HumanResources", "Index", "department", "Zbiorczy ekran dzialu HR.", "hr kadry pracownicy"),
                new("Pracownicy", "HumanResources", "Employees", "users", "Kartoteka pracownikow.", "pracownicy kartoteka"),
                new("Dzialy", "HumanResources", "Departments", "department", "Struktura organizacyjna.", "dzialy departamenty"),
                new("Urlopy", "HumanResources", "Leaves", "calendar", "Wnioski urlopowe.", "urlopy wnioski"),
                new("Grafik", "HumanResources", "Schedules", "checklist", "Grafik zmian pracownikow.", "grafik zmiany"),
            ]),
        new(
            "bok",
            "BOK",
            "support",
            "Zgloszenia klientow i przypisania do konsultantow.",
            true,
            [
                new("Dashboard BOK", "CustomerSupport", "Index", "support", "Zbiorczy ekran obslugi klienta.", "bok obsluga klienta"),
                new("Zgloszenia", "CustomerSupport", "Tickets", "support", "Lista zgloszen klientow.", "zgloszenia tickety klient"),
            ]),
        new(
            "admin",
            "Administracja",
            "admin",
            "Uzytkownicy, role, audyt i ustawienia systemu.",
            true,
            [
                new("Dashboard admina", "Admin", "Index", "admin", "Pulpit administracyjny.", "admin administracja"),
                new("Uzytkownicy", "Admin", "Users", "users", "Zarzadzanie uzytkownikami.", "uzytkownicy"),
                new("Role", "Admin", "Roles", "shield", "Role i uprawnienia.", "role uprawnienia"),
                new("Logi systemowe", "Admin", "Logs", "report", "Audyt zmian i zdarzen systemowych.", "logi audyt zmiany"),
                new("Awarie kompletacji", "Admin", "PackingIncidents", "alert", "Obsluga zgloszen z kompletacji.", "awarie kompletacji incydenty packing"),
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

    private static readonly string[] AllStaffRoles =
    [
        UserRoles.Admin,
        UserRoles.Kitchen,
        UserRoles.KitchenManager,
        UserRoles.Warehouse,
        UserRoles.WarehouseManager,
        UserRoles.Packing,
        UserRoles.PackingManager,
        UserRoles.Dietitian,
        UserRoles.Logistics,
        UserRoles.LogisticsManager,
        UserRoles.Driver,
        UserRoles.DriverManager,
        UserRoles.HR,
        UserRoles.HRManager,
        UserRoles.BOK,
        UserRoles.BOKManager,
    ];

    private static readonly IReadOnlyDictionary<string, string[]> SectionRoles =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["dashboard"] = AllStaffRoles,
            ["kitchen"] = [UserRoles.Kitchen, UserRoles.KitchenManager, UserRoles.Admin],
            ["warehouse"] = [UserRoles.Warehouse, UserRoles.WarehouseManager, UserRoles.Admin],
            ["ingredients"] = [UserRoles.Kitchen, UserRoles.KitchenManager, UserRoles.Warehouse, UserRoles.WarehouseManager, UserRoles.Dietitian, UserRoles.Admin],
            ["packing"] = [UserRoles.Packing, UserRoles.PackingManager, UserRoles.Admin],
            ["diets"] = [UserRoles.Dietitian, UserRoles.Admin],
            ["logistics"] = [UserRoles.Logistics, UserRoles.LogisticsManager, UserRoles.DriverManager, UserRoles.Admin],
            ["hr"] = [UserRoles.HR, UserRoles.HRManager, UserRoles.Admin],
            ["bok"] = [UserRoles.BOK, UserRoles.BOKManager, UserRoles.Admin],
            ["admin"] = [UserRoles.Admin],
            ["mobile"] = [UserRoles.Driver, UserRoles.DriverManager, UserRoles.LogisticsManager, UserRoles.Admin],
        };

    public static IReadOnlyList<StaffNavItem> Items { get; } = Sections
        .SelectMany(section => section.Items)
        .ToArray();

    public static IReadOnlyList<StaffNavSection> VisibleSections(ClaimsPrincipal user)
    {
        return Sections.Where(section => CanAccess(user, section)).ToArray();
    }

    public static bool CanAccess(ClaimsPrincipal user, StaffNavSection section)
    {
        if (user.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        return SectionRoles.TryGetValue(section.Key, out var roles) &&
            roles.Any(user.IsInRole);
    }

    public static StaffNavSection? FindSection(string? controller)
    {
        return Sections.FirstOrDefault(section => section.IsActive(controller));
    }

    public static StaffNavSection? FindSection(string? controller, ClaimsPrincipal user)
    {
        return VisibleSections(user).FirstOrDefault(section => section.IsActive(controller));
    }

    public static StaffNavItem? FindItem(string? controller, string? action)
    {
        return Items.FirstOrDefault(item => item.Matches(controller, action));
    }

    public static StaffNavItem? FindItem(string? controller, string? action, ClaimsPrincipal user)
    {
        return VisibleSections(user)
            .SelectMany(section => section.Items)
            .FirstOrDefault(item => item.Matches(controller, action));
    }
}
