@startuml
skinparam sequenceArrowThickness 2
skinparam roundcorner 6

actor "Magazynier" as Mag
participant "Torba" as Torba
participant "Pudelko" as Pudelko
participant "EtykietaWysylkowa" as EWys
participant "ManifestZaladunkowy" as Manifest
participant "<<external>>\n Zamowienie" as Zam
participant "<<external>>\n Trasa" as Trasa
participant "<<external>>\n Stop" as Stop
participant "<<external>>\n Auto" as Auto
participant "Opakowanie" as Opak
participant "ZuzycieOpakowan" as ZuzOp

Mag -> Zam : pobierz zamowienia do skompletowania
Zam --> Mag : lista zamowien

Mag -> Trasa : pobierz trasy na dzis
Trasa --> Mag : lista tras z przypisanymi stopami

loop dla każdego Zamowienia
    Mag -> Torba : <<create>> Torba()
    Torba -> Zam : przypiszZamowienie(zam)
    loop dla każdego posiłku w zamówieniu (3-6 pudełek)
        Mag -> Pudelko : znajdz pasujace pudelko (Meal, DietVariant)
        Pudelko --> Mag : pudelko
        Mag -> Pudelko : weryfikujZgodnosc(torba)
        Pudelko --> Mag : OK
        Mag -> Torba : dodajPudelko(pudelko)
        Torba -> Pudelko : umiesc w torbie
    end
    Mag -> Torba : gotowaDoZaladunku()
    Mag -> EWys : <<create>> EtykietaWysylkowa(dane z Torby i Stopu)
    EWys -> Stop : pobierz okno_dostawy, adres
    EWys -> Torba : przypiszEtykiete()
    Mag -> Opak : rozlicz zuzycie (typ=tuba, pudelka)
    Opak -> ZuzOp : <<create>> ZuzycieOpakowan(ilosc, torba)
end

Mag -> Manifest : <<create>> ManifestZaladunkowy(dla danej Trasy)
Mag -> Trasa : przypisz trase
Trasa --> Manifest : 
loop dla każdej Torby gotowej na tej trasie
    Mag -> Manifest : dodajTorbe(torba)
    Manifest -> Torba : dodaj do listy
end

Mag -> Auto : przypisz Auto
Auto --> Manifest : 
Mag -> Manifest : sprawdzLIFO()  (kontrola kolejnosci ladowania)
Manifest --> Mag : OK
Mag -> Manifest : zatwierdzWydanie()
Manifest -> Mag : manifest gotowy (wydruk/PDF)

Mag -> Mag : przekaz Manifest Kierowcy
@enduml