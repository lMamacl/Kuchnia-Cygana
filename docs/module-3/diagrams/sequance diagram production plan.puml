@startuml
skinparam sequenceArrowThickness 2
skinparam roundcorner 6

actor "System\n(Zadanie cykliczne)" as Sys
participant "PlanProdukcji" as PP
participant "PozycjaPlanu" as Pozycja
participant "<<external>>\nZamowienie\n(Moduł 1)" as Zam
participant "<<external>>\nDietVariant\n(Moduł 2)" as DV
participant "<<external>>\nMeal\n(Moduł 2)" as Meal
participant "Skladnik\n(Moduł 3)" as Skladnik

Sys -> PP : <<create>> nowy PlanProdukcji(data, status="generowany")
activate PP

loop dla każdego aktywnego Zamowienia
    Sys -> Zam : pobierz zamowienia na nadchodzace dni
    Zam --> Sys : lista zamowien (id, DietVariant)
    Sys -> DV : pobierz DietVariant dla zamowienia
    DV --> Sys : wariant (kalorycznosc)
    Sys -> Meal : pobierz posilki przypisane do wariantu
    Meal --> Sys : lista posilkow
    loop dla każdego Meal w zamówieniu
        Sys -> PP : dodajPozycje(Meal, ilosc)
        PP -> Pozycja : <<create>> PozycjaPlanu(meal, planowana_ilosc)
        Pozycja --> PP : 
    end
end

Sys -> PP : obliczFoodCost()
PP -> Pozycja : dla kazdej pozycji pobierz recepture (przez Meal)
Pozycja -> Meal : receptura (lista Skladnikow z gramatura)
Meal --> Pozycja : składniki
Pozycja -> Skladnik : sprawdzDostepnosc(skladnik, ilosc)
Skladnik --> Pozycja : dostepny / brak

alt jeśli wszystkie składniki dostępne
    PP -> Sys : plan gotowy
    Sys -> PP : udostepnij plan Szefowi Kuchni
else jakieś braki
    PP -> Sys : raport o brakach
    Sys -> Sys : <<async>> wyslij alert do zaopatrzeniowca
end

deactivate PP
@enduml