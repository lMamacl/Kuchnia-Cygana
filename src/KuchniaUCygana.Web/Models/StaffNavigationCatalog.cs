using System.Security.Claims;
using KuchniaUCygana.Domain.Enums;

namespace KuchniaUCygana.Web.Models;

public sealed record StaffNavItem(
    string Label,
    string Controller,
    string Action,
    string Icon,
    string Description,
    string Keywords = "",
    string[]? AllowedRoles = null)
{
    /// <summary>
    /// Determines whether the item's Controller and Action match the provided values using case-insensitive ordinal comparison.
    /// </summary>
    /// <param name="controller">Controller name to compare; may be null.</param>
    /// <param name="action">Action name to compare; may be null.</param>
    /// <returns>`true` if both Controller and Action match the provided values using case-insensitive ordinal comparison, `false` otherwise.</returns>
    public bool Matches(string? controller, string? action)
    {
        return string.Equals(this.Controller, controller, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(this.Action, action, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Determines whether the item's Controller matches the specified controller name.
    /// </summary>
    /// <param name="controller">The controller name to compare; may be null.</param>
    /// <returns>`true` if the controller names are equal using a case-insensitive ordinal comparison, `false` otherwise.</returns>
    public bool MatchesController(string? controller)
    {
        return string.Equals(this.Controller, controller, StringComparison.OrdinalIgnoreCase);
    }

    public bool CanAccess(ClaimsPrincipal user, IReadOnlyCollection<string> sectionRoles)
    {
        var roles = this.AllowedRoles ?? sectionRoles;
        return roles.Any(user.IsInRole);
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

    /// <summary>
    /// Determines whether any navigation item in the section corresponds to the specified controller.
    /// </summary>
    /// <param name="controller">The controller name to check for activity; comparison is case-insensitive and the value may be null.</param>
    /// <returns>`true` if any item in the section matches the provided controller, `false` otherwise.</returns>
    public bool IsActive(string? controller)
    {
        return this.Items.Any(item => item.MatchesController(controller));
    }

    public bool IsActive(string? controller, string? action)
    {
        return this.Items.Any(item => item.Matches(controller, action));
    }
}

public static class StaffNavigationCatalog
{
    public static IReadOnlyList<StaffNavSection> Sections { get; } =
    [
        new(
            "dashboard",
            "Mój panel",
            "home",
            "Profil pracownika, grafik i wnioski urlopowe.",
            true,
            [
                new("Profil i grafik", "Account", "Profile", "home", "Osobisty panel pracownika.", "profil grafik urlopy pracownik"),
                new("Powiadomienia", "Notifications", "Index", "bell", "Lista powiadomień pracownika.", "powiadomienia alerty pracownik"),
            ]),
        new(
            "kitchen",
            "Kuchnia",
            "kitchen",
            "Plan dnia, generowanie produkcji i karty gotowania.",
            true,
            [
                new("Pulpit kuchni", "Production", "Index", "kitchen", "Zbiorczy ekran działu kuchni.", "produkcja kuchnia plan"),
                new("Generuj plan", "Production", "Generate", "wand", "Przygotowanie dziennego planu produkcji.", "generowanie planu produkcji"),
                new("Plan dnia", "Production", "Plan", "calendar", "Podgląd planu produkcyjnego.", "plan dnia produkcji"),
                new("Plan z M2", "Production", "M2Plan", "calendar", "Podgląd opublikowanego planu M2 i aktualności snapshotów.", "plan m2 7 dni snapshot refresh", [UserRoles.Kitchen, UserRoles.KitchenManager, UserRoles.Admin]),
                new("Karty gotowania", "Production", "CookingCards", "clipboard", "Lista kart gotowania do realizacji.", "karty gotowania receptury"),
                new("Karta gotowania", "Production", "CookingCard", "clipboard-check", "Podgląd pojedynczej karty gotowania.", "karta gotowania szczegóły"),
                new("Ponowne przygotowanie", "Production", "Rework", "alert", "Zadania kuchni z awarii kompletacji.", "ponowne przygotowanie awarie kompletacja"),
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
                new("Wydania ręczne", "Warehouse", "Issue", "box", "Rozchód magazynowy poza kompletacją.", "wydania ręczne rozchód magazyn"),
                new("Raport FEFO", "Warehouse", "FefoReport", "report", "Kontrola partii według FEFO.", "fefo partie terminy magazyn"),
                new("Historia transakcji", "Warehouse", "TransactionHistory", "clipboard", "Operacyjna historia ruchów magazynowych.", "historia transakcje magazyn ruchy"),
                new("Odpady", "Warehouse", "Waste", "trash", "Rejestr odpadów i strat.", "odpady straty magazyn"),
                new("Inwentaryzacja", "Warehouse", "Inventory", "checklist", "Ekran inwentaryzacji.", "inwentaryzacja remanent"),
                new("Temperatury HACCP", "Warehouse", "Temperatures", "temperature", "Monitoring temperatur HACCP.", "temperatury haccp"),
                new("Raport HACCP", "Warehouse", "HaccpReport", "report", "Raport kontrolny HACCP.", "raport haccp"),
                new("Lokalizacje HACCP", "Warehouse", "HaccpLocations", "map", "Punkty pomiarowe HACCP i przypisane kategorie.", "lokalizacje haccp punkty pomiarowe"),
                new("Plan z M2", "Production", "M2Plan", "calendar", "Podgląd opublikowanego planu M2 z perspektywy magazynu.", "plan m2 7 dni snapshot refresh", [UserRoles.Warehouse, UserRoles.WarehouseManager]),
                new("Zapotrzebowanie 7 dni", "Production", "WarehouseDemand", "report", "Agregacja składników i opakowań z planu M2 oraz zamówień M1.", "zapotrzebowanie magazyn 7 dni m2 m1", [UserRoles.Warehouse, UserRoles.WarehouseManager, UserRoles.Admin]),
            ]),
        new(
            "ingredients",
            "Składniki",
            "ingredients",
            "Katalog składników.",
            true,
            [
                new("Katalog składników", "Ingredient", "Index", "ingredients", "Lista składników do podpięcia z M2/M3.", "składniki katalog"),
                new("Dodaj składnik", "Ingredient", "Create", "plus", "Szkielet formularza składnika.", "nowy składnik dodaj"),
            ]),
        new(
            "packing",
            "Kompletacja",
            "packing",
            "Kompletacja toreb, załadunek aut i manifesty dostaw.",
            true,
            [
                new("Kompletacja toreb", "Packing", "Index", "packing", "Pakowanie pudełek do toreb.", "pakowanie kompletacja torby"),
                new("Załadunek aut", "Loading", "Index", "truck", "Manifesty dostaw i załadunek aut.", "załadunek auta manifest dostawa"),
                new("Etykiety transportowe", "Packing", "LabelsIndex", "tag", "Etykiety transportowe w trybie preview.", "etykiety qr transport"),
            ]),
        new(
            "diets",
            "Diety i przepisy",
            "diets",
            "Konfiguracja diet, posiłków i przepisów.",
            true,
            [
                new("Edytor diet", "DietEditor", "Index", "diets", "Dashboard dietetyka.", "diety edytor"),
                new("Nowa dieta", "DietEditor", "Create", "plus", "Kreator nowej diety.", "nowa dieta"),
                new("Plan menu M2", "DietMenuPlan", "Index", "calendar", "Tygodniowy plan diet do publikacji snapshotu.", "plan menu m2 7 dni diety"),
                new("Posiłki", "Meals", "Index", "meal", "Katalog posiłków i wariantów.", "posiłki dieta warianty"),
                new("Przepisy", "DietEditor", "Recipes", "recipes", "Baza przepisów składowych.", "przepisy receptury składowe"),
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
                new("Mapa tras", "Logistics", "RouteMap", "map", "Mapa tras i stopów.", "mapa tras"),
                new("Flota", "Logistics", "Vehicles", "vehicle", "Pojazdy i gotowość floty.", "flota pojazdy"),
                new("Kierowcy", "Logistics", "Drivers", "users", "Lista kierowców.", "kierowcy logistyka"),
            ]),
        new(
            "hr",
            "HR",
            "department",
            "Kadry, pracownicy, urlopy i grafik zmian.",
            true,
            [
                new("Dashboard HR", "HumanResources", "Index", "department", "Zbiorczy ekran działu HR.", "hr kadry pracownicy"),
                new("Pracownicy", "HumanResources", "Employees", "users", "Kartoteka pracowników.", "pracownicy kartoteka"),
                new("Nowy pracownik", "HumanResources", "NewEmployee", "plus", "Dodanie pracownika do kartoteki.", "nowy pracownik dodaj"),
                new("Działy", "HumanResources", "Departments", "department", "Struktura organizacyjna.", "działy departamenty"),
                new("Urlopy", "HumanResources", "Leaves", "calendar", "Wnioski urlopowe.", "urlopy wnioski"),
                new("Nowy wniosek", "HumanResources", "NewLeaveRequest", "plus", "Rejestracja wniosku urlopowego.", "nowy wniosek urlopowy"),
                new("Grafik", "HumanResources", "Schedules", "checklist", "Grafik zmian pracowników.", "grafik zmiany"),
                new("Nowa zmiana", "HumanResources", "NewWorkSchedule", "plus", "Dodanie zmiany do grafiku.", "nowa zmiana grafik"),
            ]),
        new(
            "bok",
            "BOK",
            "support",
            "Zgłoszenia klientów i przypisania do konsultantów.",
            true,
            [
                new("Dashboard BOK", "CustomerSupport", "Index", "support", "Zbiorczy ekran obsługi klienta.", "bok obsługa klienta"),
                new("Zgłoszenia", "CustomerSupport", "Tickets", "support", "Lista zgłoszeń klientów.", "zgłoszenia tickety klient"),
                new("Nowe zgłoszenie", "CustomerSupport", "NewTicket", "plus", "Rejestracja nowego zgłoszenia BOK.", "nowe zgłoszenie bok reklamacja"),
            ]),
        new(
            "admin",
            "Administracja",
            "admin",
            "Użytkownicy, role, audyt i ustawienia systemu.",
            true,
            [
                new("Dashboard admina", "Admin", "Index", "admin", "Pulpit administracyjny.", "admin administracja"),
                new("Użytkownicy", "Admin", "Users", "users", "Zarządzanie użytkownikami.", "użytkownicy"),
                new("Nowy użytkownik", "Admin", "NewUser", "plus", "Tworzenie konta użytkownika.", "nowy użytkownik konto"),
                new("Role", "Admin", "Roles", "shield", "Role i uprawnienia.", "role uprawnienia"),
                new("Logi systemowe", "Admin", "Logs", "report", "Audyt zmian i zdarzeń systemowych.", "logi audyt zmiany"),
                new("Awarie kompletacji", "Admin", "PackingIncidents", "alert", "Obsługa zgłoszeń z kompletacji.", "awarie kompletacji incydenty packing"),
                new("Ustawienia", "Admin", "Settings", "settings", "Ustawienia systemowe.", "ustawienia konfiguracja"),
            ]),
        new(
            "mobile",
            "Mobile",
            "mobile",
            "Mobilny podgląd pracy kierowcy.",
            false,
            [
                new("Panel kierowcy", "DriverMobile", "Index", "mobile", "Start widoku kierowcy.", "driver kierowca mobile trasa"),
                new("Stop", "DriverMobile", "Stop", "route", "Aktywny stop kierowcy.", "stop dostawa"),
                new("Potwierdzenie", "DriverMobile", "Confirm", "clipboard-check", "Potwierdzenie dostawy.", "potwierdzenie dostawy"),
                new("Problem", "DriverMobile", "Problem", "alert", "Zgłoszenie problemu na trasie.", "problem dostawa"),
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
        if (user.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        var visibleRouteKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visibleSections = new List<StaffNavSection>();

        foreach (var section in Sections)
        {
            if (!CanAccess(user, section) || !SectionRoles.TryGetValue(section.Key, out var sectionRoles))
            {
                continue;
            }

            var visibleItems = section.Items
                .Where(item => item.CanAccess(user, sectionRoles))
                .Where(item => visibleRouteKeys.Add(GetRouteKey(item)))
                .ToArray();

            if (visibleItems.Length > 0)
            {
                visibleSections.Add(section with { Items = visibleItems });
            }
        }

        return visibleSections;
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
        return FindSection(controller, null, user);
    }

    public static StaffNavSection? FindSection(string? controller, string? action, ClaimsPrincipal user)
    {
        var sections = VisibleSections(user);
        if (!string.IsNullOrWhiteSpace(action))
        {
            var exactSection = sections.FirstOrDefault(section => section.IsActive(controller, action));
            if (exactSection is not null)
            {
                return exactSection;
            }
        }

        return sections.FirstOrDefault(section => section.IsActive(controller));
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

    private static string GetRouteKey(StaffNavItem item)
        => $"{item.Controller}:{item.Action}";
}
