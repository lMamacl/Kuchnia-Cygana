@startuml
skinparam sequenceArrowThickness 2
skinparam roundcorner 6

actor "Szef Kuchni" as Chef
participant "PlanProdukcji" as PP
participant "PozycjaPlanu" as Pozycja
participant "<<external>>\nMeal" as Meal
participant "SubRecipe" as SubR
participant "Pudelko" as Pudelko
participant "EtykietaProduktowa" as EProd
participant "Skladnik" as Skladnik
participant "PartiaSkladnika" as Partia

Chef -> PP : pobierzPlanNaDzis()
PP --> Chef : lista PozycjiPlanu

loop dla każdej PozycjiPlanu
    Chef -> Pozycja : rozpocznijProdukcje()
    alt posiłek wymaga półproduktów
        Chef -> SubR : pobierz SubRecipe dla Meal
        SubR --> Chef : lista potrzebnych Meal (polprodukty)
        note right: SubRecipe lączy Meal z Meal (polprodukt)
    end
    Chef -> Chef : ugotuj posiłek (lub polprodukt)
    loop dla każdej porcji (planowana_ilosc)
        Chef -> Pudelko : <<create>> Pudelko(data_waznosci)
        Pudelko -> Meal : przypiszMeal()
        Meal --> Pudelko : 
        Chef -> EProd : <<create>> EtykietaProduktowa(dane z Meal)
        EProd -> Pudelko : przypiszEtykiete()
        Pudelko --> Chef : pudełko gotowe
    end
    Chef -> Pozycja : ustawUgotowanaIlosc(ilosc)
    Pozycja -> Skladnik : zdejmijZeStanuFEFO(skladnik, ilosc)
    Skladnik -> Partia : wybierz partie wg daty waznosci (FEFO)
    Partia --> Skladnik : partia do zdjecia
    Skladnik -> Partia : zmniejsz ilosc
    Skladnik -> Pudelko : powiazZPartia(pudelko, partia)  <<HACCP>>
    note right: Sledzenie partii (Traceability)
end

Chef -> PP : zatwierdzUgotowanie() dla wszystkich pozycji
PP --> Chef : produkcja zakonczona
@enduml