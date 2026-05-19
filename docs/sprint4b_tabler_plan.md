# 🎨 Sprint 4B — Plan Integracji Tabler UI
## Strategia, Architektura Layoutów i Mapa Stron

> **Data:** 2026-05-19 | **Branch:** Mamac  
> **Dotyczy:** Warstwa prezentacji dla całej platformy

---

## 1. STAN OBECNY (Audyt)

### Co już mamy w Views:

```
Views/
├── Account/         ← M1/M5: Logowanie, rejestracja
├── Address/         ← M1: Zarządzanie adresami
├── Cart/            ← M1: Koszyk
├── Checkout/        ← M1: Płatności
├── Home/            ← Strona główna
├── Order/           ← M1: Zamówienia
├── Shared/
│   ├── _Layout.cshtml          ← Plain Bootstrap navbar (JEDEN layout)
│   ├── _Layout.cshtml.css
│   ├── _ValidationScriptsPartial.cshtml
│   └── Error.cshtml
├── _ViewImports.cshtml
└── _ViewStart.cshtml
```

### Co dodaje każdy branch do _Layout:
| Branch | Zmiana w `_Layout.cshtml` |
|--------|--------------------------|
| **Mamac** | Brak zmian (nie dotykaliśmy) |
| **Tomasz** | +1 link "Test geokodowania" w navbarze + widok `Logistics/Index.cshtml` |
| **Dawid** | Brak zmian w layoucie, dodał widoki `Address/Index`, `Order/Details`, `Order/Index` |
| **Gabriel** | Layout nietknięty, brak dodanych widoków |

### Co używamy w `wwwroot/lib`:
- Bootstrap 5 (standard)
- jQuery + jQuery Validation (formularze)
- HTMX (dynamiczne fragmenty)
- Alpine.js (reaktywność bez frameworka)

### Kluczowy wniosek:
**Nikt jeszcze nie zbudował poważnego layoutu.** Wszyscy korzystają z defaultowego navbara Bootstrap. To idealny moment na wprowadzenie Tablera — nie ma co nadpisywać.

---

## 2. PROBLEM ARCHITEKTONICZNY: Dwa Światy UI

Platforma cateringowa ma **dwa fundamentalnie różne interfejsy użytkownika**:

```
┌──────────────────────────────────────────────────────────────────────┐
│                         KUCHNIA U CYGANA                            │
│                                                                      │
│  ┌──────────────────┐  ┌────────────────────┐  ┌─────────────────┐  │
│  │ 🛒 STREFA KLIENTA│  │ 🏭 PANEL PRACOWNIK.│  │ 🚗 PANEL KIEROW.│  │
│  │                  │  │                    │  │                 │  │
│  │ - Zamówienia     │  │ - Produkcja (M3)   │  │ - Moja trasa    │  │
│  │ - Menu / Diety   │  │ - Magazyn (M3)     │  │ - Lista stopów  │  │
│  │ - Koszyk         │  │ - Kompletacja (M3) │  │ - Skan QR       │  │
│  │ - Płatności      │  │ - Edytor diet (M2) │  │ - Nawigacja GPS │  │
│  │ - Profil klienta │  │ - Logistyka (M4)   │  │                 │  │
│  │                  │  │ - Admin (M5)       │  │ Layout: mobile  │  │
│  │ Layout: _Layout  │  │                    │  │ Responsive-only │  │
│  │ Wygląd: e-comm.  │  │ Layout: _LayoutSt. │  │ Moduł: M4       │  │
│  │ Moduły: M1, M2   │  │ Moduły: M2-M5      │  │ Rola: Driver    │  │
│  └──────────────────┘  └────────────────────┘  └─────────────────┘  │
└──────────────────────────────────────────────────────────────────────┘
```

### Opcja A: Dwa osobne layouty (REKOMENDOWANE)

```
Views/Shared/
├── _Layout.cshtml              ← Strefa klienta (Bootstrap, navbar, e-commerce)
├── _LayoutStaff.cshtml         ← Panel pracowniczy (Tabler, sidebar, dashboard)
├── _SidebarPartial.cshtml      ← Menu boczne Tabler (role-based)
├── _HeaderStaffPartial.cshtml  ← Górna belka pracownicza (user, notyfikacje)
└── Error.cshtml
```

**Widoki M3/M4/M5** używają `_LayoutStaff`:
```cshtml
@{ Layout = "_LayoutStaff"; }
```

**Widoki M1/M2** dalej używają `_Layout`:
```cshtml
@{ Layout = "_Layout"; }
```

| Zaleta | Wada |
|--------|------|
| ✅ Brak konfliktów — Dawid/Gabriel nie muszą nic zmieniać | ⚠️ Dwa zestawy CSS/JS do utrzymania |
| ✅ Każda strefa ma optymalny UX | |
| ✅ Tabler sidebar nie psuje widoków klienta | |
| ✅ Minimalne ryzyko merge conflicts | |

### Opcja B: Jeden layout z dynamicznym sidebar (NIE REKOMENDOWANE)

Jeden `_Layout.cshtml` z `@if (User.IsInRole("Kitchen") || ...)` sterującym sidebar vs navbar.

| Zaleta | Wada |
|--------|------|
| ✅ Jedno źródło prawdy | ❌ Ogromny plik layoutu |
| | ❌ Konflikty merge z każdym branchem |
| | ❌ Logika warunkowa w Razor = błędy |
| | ❌ Klient widzi migające sidebar |

> [!IMPORTANT]
> **Rekomendacja: Opcja A** — dwa layouty. Strefa klienta i panel pracowniczy to różne produkty UX. Próba upchnięcia ich w jeden layout zawsze kończy się kompromisem, który irytuje obie strony.

---

## 3. STRATEGIA BRANCHOWANIA

### Opcja 1: Na `develop` (REKOMENDOWANA)

```
develop  ──── commit: "feat(web): add Tabler staff layout" ──── ...
              ↑
              (Mamac cherry-pick lub rebase na develop)
              (Tomasz, Dawid, Gabriel — merge develop do swoich branchy)
```

| Aspekt | Ocena |
|--------|-------|
| Kto korzysta od razu | ✅ Wszyscy po `git merge develop` |
| Konflikty | ⚠️ Trzeba najpierw scalić obecne stany _Layout (trywialny diff Tomasza) |
| Ryzyko | 🟢 Niskie — dodajemy nowe pliki, nie zmieniamy starych |

**Procedura:**
1. Checkout `develop`, merge aktualnego Mamaca (commity S3 + S4A)
2. Dodaj Tabler assets + `_LayoutStaff.cshtml` + `_SidebarPartial.cshtml`
3. Commit na develop
4. Każdy team member: `git merge develop` do swojego brancha

### Opcja 2: Osobny branch `feature/tabler-layout` → develop → cherry-pick

```
feature/tabler-layout  ──── commit ──── PR → develop
                                          ↓
develop  ──────────────────────────── merge
```

| Aspekt | Ocena |
|--------|-------|
| Kto korzysta | ✅ Wszyscy po merge do develop |
| Konflikty | 🟢 Zero — osobny branch izoluje zmiany |
| Ryzyko | 🟢 Minimalne |
| Narzut | ⚠️ Dodatkowy branch do zarządzania |

### Opcja 3: Na Mamac → cherry-pick do develop

```
Mamac  ──── commit: "feat(web): add Tabler staff layout" ──── ...
                    ↓ cherry-pick
develop  ──── commit: "feat(web): add Tabler staff layout" ──── ...
```

| Aspekt | Ocena |
|--------|-------|
| Kto korzysta | ✅ Wszyscy po cherry-pick |
| Konflikty | ⚠️ Cherry-pick może powodować duplikaty commitów przy późniejszym merge Mamac → develop |
| Ryzyko | 🟡 Średnie — cherry-pick + późniejszy merge = potencjalne konflikty |

> [!TIP]
> **Rekomendacja: Opcja 1** (prosto na develop). Layout Tabler to **infrastruktura współdzielona**, nie feature jednego modułu. Powinien żyć na develop, żeby wszyscy mieli do niego dostęp. Przenosimy najpierw Mamaca na develop (bo i tak planowaliśmy), a potem dodajemy Tablera.

---

## 4. KOMPLETNA MAPA STRON — Wszystkie Moduły

### 4.1 Strefa Klienta (Layout: `_Layout.cshtml` — Bootstrap)

| Ścieżka | Kontroler | Moduł | Opis | Stan |
|---------|-----------|-------|------|------|
| `/` | Home | — | Landing page / strona główna | ✅ Istnieje |
| `/menu` | Menu | M2 | Przeglądanie diet i jadłospisu (publiczny) | 🔲 Gabriel |
| `/menu/diet/{id}` | Menu | M2 | Szczegóły diety (kaloryka, składniki, cena) | 🔲 Gabriel |
| `/menu/week-plan/{dietId}` | Menu | M2 | Jadłospis tygodniowy (preview) | 🔲 Gabriel |
| `/cart` | Cart | M1 | Koszyk z zamówieniami | ✅ Istnieje (stub) |
| `/checkout` | Checkout | M1 | Finalizacja płatności | ✅ Istnieje (stub) |
| `/orders` | Order | M1 | Lista zamówień klienta | ✅ Dawid (widok) |
| `/orders/{id}` | Order | M1 | Szczegóły zamówienia | ✅ Dawid (widok) |
| `/account` | Account | M5 | Logowanie / Rejestracja | ✅ Istnieje (stub) |
| `/account/profile` | Account | M5 | Profil klienta | 🔲 Pawciot |
| `/account/addresses` | Address | M1 | Zarządzanie adresami | ✅ Dawid (widok) |

### 4.2 Panel Pracowniczy (Layout: `_LayoutStaff.cshtml` — Tabler)

| Ścieżka | Kontroler | Moduł | Rola | Opis |
|---------|-----------|-------|------|------|
| `/staff` | Staff | — | Wszystkie | Dashboard pracowniczy |
| | | | | |
| **PRODUKCJA (M3)** | | | | |
| `/production` | Production | M3 | Kitchen | Plan produkcji na dziś |
| `/production/generate` | Production | M3 | Kitchen | Generuj plan |
| `/production/plan/{id}` | Production | M3 | Kitchen | Szczegóły planu |
| `/production/cooking-card/{id}` | Production | M3 | Kitchen | Karta gotowania |
| | | | | |
| **MAGAZYN (M3)** | | | | |
| `/warehouse` | Warehouse | M3 | Warehouse | Stany magazynowe + alerty |
| `/warehouse/receive` | Warehouse | M3 | Warehouse | Przyjęcie dostawy |
| `/warehouse/waste` | Warehouse | M3 | Warehouse | Rejestracja odpadu |
| `/warehouse/inventory` | Warehouse | M3 | Warehouse | Inwentaryzacja |
| `/warehouse/temperatures` | Warehouse | M3 | Warehouse | Temperatury HACCP |
| `/warehouse/haccp-report` | Warehouse | M3 | Warehouse | Raport HACCP |
| | | | | |
| **KATALOG SKŁADNIKÓW (współdzielony M2↔M3)** | | | | |
| `/ingredients` | Ingredient | M2/M3 | Warehouse, Dietitian | Lista składników (wyszukiwarka, filtr po kategorii) |
| `/ingredients/create` | Ingredient | M2/M3 | Warehouse, Dietitian | Dodaj nowy składnik (nazwa, jednostka, koszt, alergeny) |
| `/ingredients/{id}` | Ingredient | M2/M3 | Warehouse, Dietitian | Edycja składnika |
| `/ingredients/{id}/nutrition` | Ingredient | M2/M3 | Dietitian | Wartości odżywcze (kcal, białko, tłuszcze, węgle) |
| | | | | |
| **KOMPLETACJA (M3)** | | | | |
| `/packing` | Packing | M3 | Packing | Sesje pakowania |
| `/packing/session/{id}` | Packing | M3 | Packing | Szczegóły sesji |
| `/packing/labels/{id}` | Packing | M3 | Packing | Etykiety QR |
| | | | | |
| **EDYTOR DIET (M2)** | | | | |
| `/diet-editor` | DietEditor | M2 | Dietitian | Dashboard — lista diet |
| `/diet-editor/create` | DietEditor | M2 | Dietitian | Kreator nowej diety |
| `/diet-editor/{id}` | DietEditor | M2 | Dietitian | Edycja istniejącej diety |
| `/diet-editor/{id}/meals` | DietEditor | M2 | Dietitian | Zarządzanie posiłkami |
| `/diet-editor/recipes` | DietEditor | M2 | Dietitian | Baza przepisów |
| `/diet-editor/recipes/{id}` | DietEditor | M2 | Dietitian | Edycja przepisu (wybiera z katalogu `/ingredients`) |
| | | | | |
| **LOGISTYKA — ZARZĄDZANIE (M4)** | | | | |
| `/logistics` | Logistics | M4 | Logistics | Dashboard logistyki (dzisiejsze trasy) |
| `/logistics/routes` | Logistics | M4 | Logistics | Lista tras (filtr: dzień) |
| `/logistics/routes/create` | Logistics | M4 | Logistics | Kreator trasy (drag & drop stopów) |
| `/logistics/routes/{id}` | Logistics | M4 | Logistics | Edycja trasy + podgląd mapy |
| `/logistics/routes/{id}/map` | Logistics | M4 | Logistics | Widok mapy trasy (Leaflet/OSM) |
| `/logistics/vehicles` | Logistics | M4 | Logistics | Zarządzanie flotą (pojazdy) |
| `/logistics/drivers` | Logistics | M4 | Logistics | Lista kierowców + przypisania |
| | | | | |
| **ADMINISTRACJA (M5)** | | | | |
| `/admin` | Admin | M5 | Admin | Dashboard admina |
| `/admin/users` | Admin | M5 | Admin | Zarządzanie użytkownikami |
| `/admin/roles` | Admin | M5 | Admin | Zarządzanie rolami |
| `/admin/settings` | Admin | M5 | Admin | Ustawienia systemu |

### 4.3 Panel Kierowcy (Layout: `_LayoutDriver.cshtml` — Tabler mobile)

Kierowca to **osobny przypadek UX** — pracuje w terenie, na telefonie, często jedną ręką. Nie potrzebuje sidebara z 20 pozycjami. Potrzebuje **jednego ekranu z listą stopów + nawigacją**.

| Ścieżka | Kontroler | Moduł | Opis |
|---------|-----------|-------|------|
| `/driver` | DriverMobile | M4 | Ekran główny: dzisiejsza trasa |
| `/driver/stop/{id}` | DriverMobile | M4 | Szczegóły stopu (adres, klient, paczki) |
| `/driver/stop/{id}/confirm` | DriverMobile | M4 | Potwierdzenie dostawy (skan QR + podpis) |
| `/driver/stop/{id}/problem` | DriverMobile | M4 | Zgłoszenie problemu (nieobecny, odmowa) |
| `/driver/summary` | DriverMobile | M4 | Podsumowanie dnia (ile dostarczono) |

**Koncept UX kierowcy:**

```
┌─────────────────────────┐
│  🚚 Moja trasa — 19.05  │  ← Nagłówek z datą
│  Mokotów-Południe        │  ← Nazwa trasy
│  WA 12345                │  ← Pojazd
│─────────────────────────│
│  ✅ 1. Kowalski J.       │  ← Dostarczony (zielony)
│     ul. Puławska 15      │
│     📦 2 torby           │
│─────────────────────────│
│  ▶️ 2. Nowak A.          │  ← AKTYWNY STOP (podświetlony)
│     ul. Marszałkowska 42 │
│     📦 1 torba           │
│     [🗺 Nawiguj] [📷 Skan QR] [✅ Dostarcz]
│─────────────────────────│
│  ⏳ 3. Wiśniewski P.     │  ← Oczekujący (szary)
│     ul. Hoża 8           │
│     📦 3 torby           │
│─────────────────────────│
│  ⏳ 4. Wójcik M.         │
│     ...                  │
│─────────────────────────│
│  📊 Postęp: 1/8 (13%)   │  ← Progress bar na dole
└─────────────────────────┘
```

**Kluczowe decyzje dla panelu kierowcy:**

| Aspekt | Rekomendacja |
|--------|--------------|
| Layout | Osobny `_LayoutDriver.cshtml` — bez sidebara, mobile-first |
| Nawigacja GPS | Link `geo:` lub integracja z Google Maps / Waze (deeplink) |
| Skan QR | Kamera telefonu → JS library (np. `html5-qrcode`) |
| Podpis klienta | Canvas HTML5 touch (opcjonalnie) |
| Offline | Nie na MVP — wymaga Service Worker / PWA |

---

## 5. NAWIGACJA SIDEBAR — Tabler

Struktura menu bocznego w panelu pracowniczym, z podziałem na role:

```
┌─────────────────────────────────────┐
│  🍳 Kuchnia u Cygana                │
│  Panel Pracowniczy                  │
│─────────────────────────────────────│
│                                     │
│  📊 Dashboard                       │  ← Wszystkie role
│                                     │
│  ── PRODUKCJA ──────────── Kitchen  │
│  📋 Plan dnia                       │
│  🍲 Karty gotowania                 │
│                                     │
│  ── MAGAZYN ──────────── Warehouse  │
│  📦 Stany magazynowe                │
│  🚛 Przyjęcia dostaw                │
│  🗑️ Odpisy                          │
│  📊 Inwentaryzacja                  │
│  🌡️ Temperatury HACCP               │
│                                     │
│  ── SKŁADNIKI ── Warehouse+Dietit.  │
│  🥕 Katalog składników              │  ← Współdzielony M2↔M3
│  ➕ Dodaj składnik                   │
│                                     │
│  ── KOMPLETACJA ──────── Packing    │
│  🏷️ Sesje pakowania                 │
│  🏷️ Etykiety                        │
│                                     │
│  ── DIETY ────────────── Dietitian  │
│  📝 Edytor diet                     │
│  🍽️ Przepisy                        │
│                                     │
│  ── LOGISTYKA ──────── Logistics    │
│  🚚 Trasy                           │
│  🗺️ Mapa tras                       │
│  🚗 Flota                           │
│  👥 Kierowcy                        │
│                                     │
│  ── ADMIN ────────────── Admin      │
│  👥 Użytkownicy                     │
│  🔐 Role                            │
│  ⚙️ Ustawienia                       │
│                                     │
│─────────────────────────────────────│
│  👤 Jan Kowalski                    │
│  Kitchen, Warehouse                 │
│  [Wyloguj]                          │
└─────────────────────────────────────┘
```

---

## 6. PLAN TECHNICZNY — Co Konkretnie Dodać

### 6.1 Pliki do dodania (Tabler setup)

```
wwwroot/
├── lib/
│   └── tabler/                         ← NOWY — Tabler CSS + JS
│       ├── css/
│       │   └── tabler.min.css
│       ├── js/
│       │   └── tabler.min.js
│       └── fonts/                      ← Tabler Icons (wbudowane)
│
Views/Shared/
├── _LayoutStaff.cshtml                 ← NOWY — Layout panelu pracowniczego
├── _SidebarPartial.cshtml              ← NOWY — Menu boczne (role-based)
├── _HeaderStaffPartial.cshtml          ← NOWY — Górna belka (user info, notyfikacje)
├── _AlertsPartial.cshtml               ← NOWY — Komponent TempData alertów
│
Views/Staff/
└── Index.cshtml                        ← NOWY — Dashboard pracowniczy
```

### 6.2 Pliki do ZMODYFIKOWANIA

```
Views/Shared/_Layout.cshtml             ← BEZ ZMIAN (zostaje dla klienta)
Views/_ViewStart.cshtml                 ← BEZ ZMIAN (default = _Layout, staff nadpisuje)
```

### 6.3 Zależności (CDN vs lokalne)

| Paczka | Wersja | Źródło | Rozmiar |
|--------|--------|--------|---------|
| Tabler CSS | 1.0.0-beta21 | npm / CDN | ~280 KB (min) |
| Tabler JS | 1.0.0-beta21 | npm / CDN | ~45 KB (min) |
| Tabler Icons | 3.x | CDN | ~15 KB (CSS) |

> [!NOTE]
> **Tabler zawiera Bootstrap 5** w swoim CSS. Na stronach pracowniczych nie potrzeba osobnego `bootstrap.min.css`. Na stronach klienckich zostaje oryginalny Bootstrap.
>
> HTMX i Alpine.js zostają — **Tabler jest z nimi w pełni kompatybilny** (to zwykły CSS/JS, nie framework SPA).

### 6.4 Szkielet `_LayoutStaff.cshtml`

```html
<!DOCTYPE html>
<html lang="pl">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>@ViewData["Title"] - Panel | Kuchnia u Cygana</title>

    <!-- Tabler CSS (zawiera Bootstrap 5) -->
    <link rel="stylesheet" href="~/lib/tabler/css/tabler.min.css" />
    <!-- Tabler Icons -->
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/@tabler/icons-webfont@latest/tabler-icons.min.css" />

    @RenderSection("Styles", required: false)
</head>
<body class="layout-fluid">
    <div class="page">
        <!-- Sidebar -->
        <aside class="navbar navbar-vertical navbar-expand-lg">
            @await Html.PartialAsync("_SidebarPartial")
        </aside>

        <div class="page-wrapper">
            <!-- Header -->
            @await Html.PartialAsync("_HeaderStaffPartial")

            <!-- Content -->
            <div class="page-body">
                <div class="container-xl">
                    @* Alerty TempData *@
                    @if (TempData["Success"] != null)
                    {
                        <div class="alert alert-success alert-dismissible">
                            @TempData["Success"]
                            <a class="btn-close" data-bs-dismiss="alert"></a>
                        </div>
                    }

                    @RenderBody()
                </div>
            </div>
        </div>
    </div>

    <!-- Tabler JS -->
    <script src="~/lib/tabler/js/tabler.min.js"></script>
    <!-- HTMX + Alpine (reuse) -->
    <script src="~/lib/htmx/htmx.min.js"></script>
    <script src="~/js/htmx-config.js"></script>
    <script defer src="~/lib/alpinejs/alpine.min.js"></script>

    @RenderSection("Scripts", required: false)
</body>
</html>
```

---

## 7. PYTANIE: Co z Modułem 1 — Wspólny czy Osobny?

### Scenariusz A: M1 robi swoje widoki na `_Layout` (REKOMENDOWANE)

Dawid kontynuuje budowanie widoków klienckich (zamówienia, koszyk, checkout) na obecnym layoucie Bootstrap. My budujemy panel pracowniczy na Tablerze.

- ✅ Zero konfliktów
- ✅ Każdy zespół jest autonomiczny
- ✅ UX klienta ≠ UX pracownika
- ⚠️ Dawid może później też przemigrować na Tabler dla klienta, ale to osobna decyzja

### Scenariusz B: Wszystko na Tablerze

Strefa klienta też na Tablerze, ale z innym layoutem (bez sidebara, z publicznym navbarem).

- ⚠️ Wymaga przebudowy widoków Dawida
- ⚠️ Tabler jest zoptymalizowany pod dashboardy, nie e-commerce
- ❌ Blokuje Dawida do czasu zakończenia migracji

> [!IMPORTANT]
> **Rekomendacja: Scenariusz A.** Dawid robi swoje, my robimy swoje. Punkt styku to tylko wspólny `_ViewStart.cshtml` (który nie wymaga zmian) i ewentualnie link "Panel pracowniczy" w navbarze klienckim dla zalogowanych pracowników.

---

## 8. MACIERZ DECYZJI — Podsumowanie

| Decyzja | Opcja rekomendowana | Alternatywa |
|---------|---------------------|-------------|
| **Layouty** | Dwa: `_Layout` (klient) + `_LayoutStaff` (pracownik) | Jeden dynamiczny layout |
| **Branch** | Prosto na `develop` po merge Mamaca | Osobny `feature/tabler-layout` |
| **M1 koordynacja** | Niezależne — M1 używa `_Layout`, M3/M4/M5 używa `_LayoutStaff` | Wszystko Tabler |
| **Tabler źródło** | CDN + lokalna kopia CSS/JS | npm (wymaga build pipeline) |
| **Sidebar** | Role-based z `@if (User.IsInRole(...))` | Policy-based (overkill na MVP) |

---

## 9. TIMELINE IMPLEMENTACJI

| Krok | Co | Czas | Gdzie |
|------|----|------|-------|
| 1 | Merge Mamac → develop (commity S3 + S4A) | 15 min | develop |
| 2 | Dodaj Tabler assets (`wwwroot/lib/tabler/`) | 10 min | develop |
| 3 | Utwórz `_LayoutStaff.cshtml` z Tabler combo layout | 30 min | develop |
| 4 | Utwórz `_SidebarPartial.cshtml` (role-based, sekcje z §5) | 30 min | develop |
| 5 | Utwórz `_HeaderStaffPartial.cshtml` + `_AlertsPartial.cshtml` | 15 min | develop |
| 6 | Utwórz `StaffController` + `Views/Staff/Index.cshtml` (dashboard) | 20 min | develop |
| 7 | Zaktualizuj M3 kontrolery — `Layout = "_LayoutStaff"` w widokach | 10 min | develop |
| 8 | Build + test wizualny | 10 min | develop |
| 9 | Commit + push develop | 5 min | develop |
| **Suma** | | **~2.5h** | |

> [!TIP]
> Po kroku 9, każdy team member robi `git merge develop` i od razu ma sidebar Tabler dostępny w swoich widokach. Wystarczy, że w swoich `Views/NazwaKontrolera/Index.cshtml` doda `@{ Layout = "_LayoutStaff"; }`.
