@startuml
skinparam classAttributeIconSize 0
skinparam packageStyle rectangle
skinparam class {
    BackgroundColor White
    ArrowColor DarkBlue
    BorderColor DarkBlue
}

' ==========================================
' MODUL 1: ZAMOWIENIA I KLIENCI
' ==========================================
package "Modul 1: Zamowienia i Klienci" #FFEEDD {
    class Klient {
        + id_klienta : int
        + imie_nazwisko : string
    }
    class Zamowienie {
        + id_zamowienia : int
    }
    Klient "1" -- "*" Zamowienie : sklada >
}

' ==========================================
' MODUL 2: KUCHNIA (POSILKI I DIETY)
' ==========================================
package "Modul 2: Kuchnia (Posilki i Diety)" #FFF6E5 {
    class DietVariant {
        + Id : int
        + CaloriesTarget : int
    }
    class Meal {
        + Id : int
        + Name : string
        + IsSemiFinished : bool
    }
}

' ==========================================
' MODUL 4: LOGISTYKA (TRASY I AUTA)
' ==========================================
package "Modul 4: Logistyka (Trasy i Auta)" #E0F0FF {
    class Trasa {
        + id_trasy : int
    }
    class Stop {
        + id_stop : int
        + numer_kolejnosci : int
        + okno_od : Time
        + okno_do : Time
    }
    class Auto {
        + rejestracja : string
        + ladownosc : int
    }
    Trasa "1" *-- "1..*" Stop : sklada sie z >
}

' ==========================================
' MODUL 3: PRODUKCJA I MAGAZYN (bez zmian)
' ==========================================
package "Modul 3: Produkcja i Magazyn (ERP)" #F4F9FF {

    ' --- Sekcja 1: Produkcja ---
    package "Sekcja 1: Produkcja i Planowanie" #E8F4F8 {
        
        class PozycjaPlanu {
            + id_pozycji : int
            + planowana_ilosc : int
            + ugotowana_ilosc : int
        }
        class PlanProdukcji {
            + id_planu : int
            + data_realizacji : DateTime
            + status : string
            + generujPlan7Dni()
            + zatwierdzUgotowanie()
        }
        PlanProdukcji "1" *-- "*" PozycjaPlanu : zawiera >
        PozycjaPlanu "*" -- "1" Meal : okresla do ugotowania >
        PozycjaPlanu "*" -- "1" DietVariant : dla wariantu kalorycznego >
    }

    ' --- Sekcja 2: Kompletacja i Zaladunek ---
    package "Sekcja 2: Kompletacja i Zaladunek" #E8F8EE {
        class Pudelko {
            + id_pudelka : int
            + data_waznosci : DateTime
            + weryfikujZgodnosc(Torba t) : boolean
            + zglosUszkodzenie()
        }
        class EtykietaProduktowa {
            + kod_QR : string
            + nazwa_dania : string
            + alergeny : string
            + kcal : int
            + drukuj()
        }
        class Torba {
            + id_torby : int
            + status : string
            + przypiszZamowienie()
            + gotowaDoZaladunku() : boolean
        }
        class EtykietaWysylkowa {
            + kod_QR_torby : string
            + trasa : string
            + stop : int
            + klient : string
            + okno_dostawy : string
            + drukuj()
        }
        class ManifestZaladunkowy {
            + id_manifestu : int
            + data_utworzenia : DateTime
            + status : string
            + dodajTorbe(Torba t)
            + sprawdzLIFO() : boolean
            + zatwierdzWydanie()
        }

        Pudelko "1" *-- "1" EtykietaProduktowa : posiada >
        Torba "1" *-- "1" EtykietaWysylkowa : posiada >
        Torba "1" o-- "3..6" Pudelko : zawiera >
        Meal "1" -- "*" Pudelko : zafoliowane jako >

        ' Manifest laczy trase, torby i auto
        ManifestZaladunkowy "1" -- "1" Trasa : sporzadzony dla >
        ManifestZaladunkowy "1" *-- "*" Torba : zawiera torby >
        ManifestZaladunkowy "*" -- "1" Auto : przypisany do >

        ' Torba odwoluje sie do zamowienia (posrednio klient/stop)
        Torba "1" -- "1" Zamowienie : realizowane przez >
    }

    ' --- Sekcja 3: Smart Inventory i HACCP ---
    package "Sekcja 3 & 4: Inventory & HACCP" #FFF2E6 {
        class Skladnik {
            + id_skladnika : int
            + nazwa : string
            + stan_aktualny : decimal
            + poziom_minimum : decimal
            + zdejmijZeStanuFEFO(ilosc: decimal)
            + generujAlert()
        }
        class PartiaSkladnika {
            + numer_partii : string
            + data_waznosci : DateTime
            + ilosc_w_partii : decimal
        }
        class Dostawa {
            + id_dostawy : int
            + data_przyjecia : DateTime
            + wprowadzDostawe()
        }
        class RaportHACCP {
            + data_raportu : DateTime
            + powiazPudelkoZPartia(Pudelko p, PartiaSkladnika partia)
            + zarejestrujTemperature()
        }
        class TemperatureLog {
            + id_logu : int
            + data_pomiaru : DateTime
            + wartosc : decimal
            + lokalizacja : string
            + urzadzenie : string
        }

        Skladnik "1" *-- "*" PartiaSkladnika : dzieli sie na >
        Dostawa "1" -- "*" PartiaSkladnika : dostarcza >
        Pudelko "*" -- "*" PartiaSkladnika : sledzenie (Traceability) >
        PartiaSkladnika "1" -- "*" TemperatureLog : monitorowana >
    }

    ' --- Sekcja: Polprodukty ---
    package "Polprodukty" #FFF3E0 {
        class SubRecipe {
            + MealId : int FK
            + SubMealId : int FK
            + WeightInGrams : decimal
        }
        Meal "1" -- "*" SubRecipe : uzywany jako polprodukt <
    }

    ' --- Sekcja: Opakowania ---
    package "Opakowania" #F5F5F5 {
        class Opakowanie {
            + id_opakowania : int
            + typ : string
            + stan_magazynowy : int
        }
        class ZuzycieOpakowan {
            + id_zuzycia : int
            + ilosc : int
            + data_zuzycia : DateTime
            + zwiazanaTorba : int
        }
        Opakowanie "1" -- "*" ZuzycieOpakowan : ewidencjonuje >
        Torba "1" -- "*" ZuzycieOpakowan : zuzywa >
    }
}

' ==========================================
' RELACJE MIEDZY MODULAMI (mostki)
' (krótkie, bo pakiety są blisko siebie)
' ==========================================
Zamowienie "*" -- "1" Stop : dostarczane na >
Zamowienie "*" -- "1" DietVariant : realizuje >
EtykietaWysylkowa -[hidden]right-> Trasa
EtykietaWysylkowa -[hidden]right-> PozycjaPlanu
Torba -[hidden]right--> Zamowienie
Torba -[hidden]right--> Meal

PozycjaPlanu -[hidden]down--> PlanProdukcji
PlanProdukcji -[hidden]down--> SubRecipe
PozycjaPlanu -[hidden]right--> Pudelko

@enduml