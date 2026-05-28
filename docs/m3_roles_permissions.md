# 🔐 Moduł 3 — Rozpiska Ról i Uprawnień
## Dla Pawciota (Moduł 5)

> **Od:** Mamac | **Data:** 19.05.2026  
> **Model:** 1 pracownik = 1 stanowisko, poziom Worker/Manager, Admin ma wszystko

---

## Wymagane Role (Tabele `AspNetRoles`)

W naszym kodzie zabezpieczyliśmy endpointy konkretnymi stringami. Musisz dodać dokładnie takie role do bazy:

| Branża | Zwykły pracownik | Kierownik (Szef) |
|--------|------------------|------------------|
| Kuchnia | `Kitchen` | `KitchenManager` |
| Magazyn | `Warehouse` | `WarehouseManager` |
| Kompletacja | `Packing` | `PackingManager` |
| System | - | `Admin` (ma dostęp do wszystkiego) |

---

## Co może Pracownik, a co Szef?

### Kuchnia

| Akcja / Endpoint | `Kitchen` | `KitchenManager` | `Admin` |
|------------------|:---------:|:----------------:|:-------:|
| Podgląd planu (`GET /production/plan/{id}`) | ✅ | ✅ | ✅ |
| Podgląd karty gotowania (`GET /production/cooking-card/{id}`) | ✅ | ✅ | ✅ |
| **Generowanie planu** (`POST /production/generate`) | ❌ | ✅ | ✅ |
| **Zatwierdzenie gotowania** (`POST /production/approve-cooking`) | ❌ | ✅ | ✅ |
| **Uruchomienie dedukcji** (`POST /production/produce/{id}`) | ❌ | ✅ | ✅ |

### Magazyn

| Akcja / Endpoint | `Warehouse` | `WarehouseManager` | `Admin` |
|------------------|:-----------:|:------------------:|:-------:|
| Podgląd stanów (`GET /warehouse`) | ✅ | ✅ | ✅ |
| Podgląd alertów (`GET /warehouse/alerts`) | ✅ | ✅ | ✅ |
| Przyjęcie dostawy (`POST /warehouse/receive`) | ✅ | ✅ | ✅ |
| Rejestracja odpadu (`POST /warehouse/waste`) | ✅ | ✅ | ✅ |
| Logowanie temperatury (`POST /warehouse/temperatures`) | ✅ | ✅ | ✅ |
| Wypełnianie inwentaryzacji (`GET /warehouse/inventory`) | ✅ | ✅ | ✅ |
| **Zatwierdzenie inwentaryzacji** (`POST /warehouse/inventory`) | ❌ | ✅ | ✅ |
| **Raport HACCP** (`GET /warehouse/haccp-report`) | ❌ | ✅ | ✅ |

### Kompletacja

| Akcja / Endpoint | `Packing` | `PackingManager` | `Admin` |
|------------------|:---------:|:----------------:|:-------:|
| Podgląd sesji (`GET /packing`) | ✅ | ✅ | ✅ |
| Rozpoczęcie sesji (`POST /packing/start`) | ✅ | ✅ | ✅ |
| Pakowanie diety (`POST /packing/pack-client`) | ✅ | ✅ | ✅ |
| Druk etykiet QR (`GET /packing/labels/{id}`) | ✅ | ✅ | ✅ |
| **Zatwierdzenie wysyłki** (`POST /packing/dispatch/{id}`) | ❌ | ✅ | ✅ |

### Katalog składników (`/ingredients` — współdzielony z M2)

| Akcja / Endpoint | `Warehouse` / `Kitchen` | `Dietitian` | Manager (`*Manager`) | `Admin` |
|------------------|:-----------------------:|:-----------:|:--------------------:|:-------:|
| Przeglądanie katalogu (`GET /ingredients`) | ✅ | ✅ | ✅ | ✅ |
| Dodawanie/edycja składnika (`POST /ingredients/save`) | ✅ | ✅ | ✅ | ✅ |
| Edycja wartości odżywczych (`POST /ingredients/nutrition`) | ❌ | ✅ | ✅ | ✅ |

---

## Jak to wygląda w kodzie (dla orientacji)

Kontrolery M3 używają atrybutów `[Authorize]`. Managerowie mają swoje role z dodatkiem `Manager` do nazwy swojej branży np.: `KitchenManager`, `WarehouseManager`, `PackingManager`

```
ProductionController  →  [Authorize(Roles = "Kitchen,KitchenManager,Admin")]
  └─ approve/generate →  [Authorize(Roles = "KitchenManager,Admin")]

WarehouseController   →  [Authorize(Roles = "Warehouse,WarehouseManager,Admin")]
  └─ haccp/inventory  →  [Authorize(Roles = "WarehouseManager,Admin")]

PackingController     →  [Authorize(Roles = "Packing,PackingManager,Admin")]
  └─ dispatch         →  [Authorize(Roles = "PackingManager,Admin")]
```

---
