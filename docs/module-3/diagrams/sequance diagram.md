@startuml
' Ustawienia globalne dla czytelnosci
skinparam linetype ortho
skinparam padding 10
skinparam nodesep 80
skinparam ranksep 60
skinparam defaultFontSize 11

skinparam state {
    BackgroundColor #F4F9FF
    BorderColor #3465A4
    FontColor #000000
    ArrowColor #3465A4
}

top to bottom direction

state "Nowa" as Nowa
state "W kompletacji" as Kompletacja
state "Skompletowana" as GotowaPudelka
state "Oznakowana" as Oznakowana
state "Gotowa do załadunku" as GotowaZaladunek
state "Załadowana" as Zaladowana
state "Anulowana" as Anulowana
state "Oczekuje na rozpatrzenie" as Problem

' --- Przejscia główne (czyste, bez nakładek) ---
Nowa --> Kompletacja : przypiszZamowienie()
Nowa --> Anulowana : błąd / BOK
Kompletacja --> GotowaPudelka : wszystkie Pudełka OK
Kompletacja --> Problem : zglosUszkodzenie()\n/ brak zgodności
Problem --> Kompletacja : rozwiazano
Problem --> Anulowana : nie do skompletowania
GotowaPudelka --> Oznakowana : generujEtykieteWysylkowa()
Oznakowana --> GotowaZaladunek : gotowaDoZaladunku()
GotowaZaladunek --> Zaladowana : Manifest.dodajTorbe()
GotowaZaladunek --> Anulowana : wycofanie zamowienia
Zaladowana --> [*] : Manifest.zatwierdzWydanie()
Anulowana --> [*]

' --- Notatki umieszczone obok stanow (automatycznie polaczone linia) ---
note right of Kompletacja
  **Akcje:**
  - Pobranie listy Pudełek
  - Dla każdego: weryfikujZgodnosc()
  - Rejestracja ZuzyciaOpakowan
end note

note left of GotowaPudelka
  **Stan przejściowy:**
  Wszystkie Pudełka w torbie,
  zgodne z dietą zamówienia.
end note

note right of Oznakowana
  Wygenerowano EtykieteWysylkowa
  z kodem QR, trasą, stopem,
  oknem czasowym dostawy.
end note

note left of Zaladowana
  **Stan końcowy dla Magazynu:**
  Torba jest w aucie.
  Dalsze statusy (np. "Dostarczono")
  aktualizowane już w Module 4.
end note

@enduml