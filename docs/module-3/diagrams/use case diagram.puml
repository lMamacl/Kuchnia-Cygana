@startuml
left to right direction
skinparam packageStyle rectangle
skinparam usecase {
  BackgroundColor LightBlue
  BorderColor DarkBlue
}

actor "Szef Kuchni" as Chef
actor "Magazynier" as Mag
actor "⚙️ System (Zadanie w tle)" as Sys

' --- ZEWNĘTRZNE MODUŁY (Punkty styku) ---
usecase "Pobierz Aktywne Zamówienia\n(Moduł 1)" as M1
usecase "Pobierz 7-dniowy Plan Diet\ni Posiłków (Moduł 2)" as M2
usecase "Pobierz Trasy, Okna Czasowe i Stop-y\n(Z Modułu 4)" as M4_Trasy
usecase "Przekaż Potwierdzenie Załadunku\ni Kody QR Paczek (Do Modułu 4)" as M4_Wydanie

package "Moduł 3: Produkcja, Kompletacja i Magazyn (WMS/ERP)" {
  
  ' --- SEKCJA 1: PRODUKCJA (KUCHNIA) ---
  usecase "Wygeneruj Dzienny Plan Produkcji" as UC1
  usecase "Oblicz Zapotrzebowanie (Food Cost)" as UC2
  usecase "Wygeneruj Raport o Brakach" as UC_Braki
  usecase "Pobierz Zbiorczą Kartę Gotowania" as UC3
  usecase "Zatwierdź Ugotowanie Posiłków" as UC4
  usecase "Wyprodukuj Półprodukty\n(np. Buliony, Sosy)" as UC_Polprodukty
  
  ' NOWE: Zabezpieczenie na poziomie kuchni
  usecase "Porcjuj i naklej Etykiety Produktowe\n(Na zgrzaną folię)" as UC_Porcjowanie
  usecase "Generuj Etykiety Produktowe\n(Tylko Info: Danie, Kcal, Alergeny)" as UC_EtykietaPudelko
  
  ' --- SEKCJA 2: KOMPLETACJA I ZAŁADUNEK (MAGAZYN) ---
  usecase "Kompletuj Pudełka i Pakuj do Torby\n(Priorytetyzacja wg Tras)" as UC_Pakowanie
  usecase "Weryfikuj zgodność pudełka z dietą\n(Zabezpieczenie przed pomyłką posiłku)" as UC_Weryfikacja
  
  usecase "Przypisz torbę do zamówienia\n(Złota nić danych)" as UC_Przypisanie
  usecase "Generuj Etykietę Wysyłkową na Torbę\n(Trasa, Stop, Klient)" as UC_EtykietyWysylkowe
  
  usecase "Zgłoś Uszkodzenie / Brak Pudełka\n(Podczas kompletacji)" as UC_ZgloszenieBraku

  usecase "Skanuj i Załaduj Torby do Auta\n(Przez Magazyniera wg LIFO)" as UC_Zaladunek
  usecase "Utwórz Manifest Załadunkowy\n(Zestawienie per Auto)" as UC_Lista
  usecase "Zatwierdź Wydanie Towaru i Manifestu\n(Kurier przejmuje zapakowane auto)" as UC_Wydanie
  usecase "Rozlicz Opakowania\n(Pudełka, Torby, Wieczka)" as UC_Opakowania

  ' --- SEKCJA 3: SMART INVENTORY ---
  usecase "Automatycznie Zdejmij Składniki (FEFO)" as UC5
  usecase "Analizuj Daty Ważności i Lead-Time\n(Czas oczekiwania na dostawę)" as UC_SmartAnaliza
  usecase "Generuj Prewencyjny Alert o Uzupełnieniu" as UC_SmartAlert
  usecase "Wprowadź Dostawę\n(z Datą Ważności i Partią)" as UC8
  usecase "Utwórz Zamówienie u Dostawcy" as UC9

  ' --- SEKCJA 4: NADZÓR I UTRZYMANIE ---
  usecase "Przeprowadź Inwentaryzację (Korekta)" as UC_Inwentaryzacja
  usecase "Zarejestruj Straty / Przeceny" as UC_Straty
  usecase "Kontrola Temperatur i HACCP\n(Sanepid)" as UC_HACCP
  usecase "Śledzenie Partii (Traceability)" as UC_Sanepid
}

' ==========================================
' RELACJE I PRZEPŁYWY (FLOW)
' ==========================================

' --- Kucharz i Produkcja ---
Sys --> UC1 : O 22:00
Chef --> UC1 : Ręczne wymuszenie
Chef --> UC3
Chef --> UC4
Chef --> UC_Polprodukty
Chef --> UC_Porcjowanie

UC1 ..> M1 : <<include>>
UC1 ..> M2 : <<include>>
UC1 ..> UC2 : <<include>>
UC4 ..> UC5 : <<include>>
UC2 ..> UC_Braki : <<include>>

' Etykiety produktowe na kuchni
UC_Porcjowanie ..> UC_EtykietaPudelko : <<include>>

' --- Magazynier: Kompletacja, Etykiety, Załadunek ---
Mag --> UC_ZgloszenieBraku
Mag --> UC_Pakowanie

' Pakowanie wymaga sprawdzenia czy pudełko pasuje do tej konkretnej torby (Ochrona przed błędem)
UC_Pakowanie ..> UC_Weryfikacja : <<include>>
UC_Pakowanie ..> UC_Przypisanie : <<include>>
UC_Pakowanie ..> UC_Opakowania : <<include>>

' Generowanie i drukowanie etykiety zbiorczej na Torbę (wymaga danych z tras)
UC_Przypisanie ..> UC_EtykietyWysylkowe : <<include>>
UC_EtykietyWysylkowe ..> M4_Trasy : <<include>> (Pobiera przypisanie do Tras)


' Załadunek (Skanowanie przez magazyniera) i Logika Manifestu
Mag --> UC_Zaladunek
' Łańcuch zatwierdzania wydania (Załadunek -> Manifest -> Wydanie)
UC_Zaladunek ..> UC_Lista : <<include>>
UC_Lista ..> UC_Wydanie : <<include>>
UC_Wydanie ..> M4_Wydanie : <<include>> (Zasila aplikację kierowcy kompletem danych)


' --- Magazynier: Inteligentny Magazyn ---
Mag --> UC8
Mag --> UC9
Mag --> UC_Inwentaryzacja
Mag --> UC_Straty

UC8 ..> UC_HACCP : <<include>> (Przyjęcie towaru)
UC5 ..> UC_Sanepid : <<extend>> [Loguj nr Partii i Odbiorcę]

' --- System: Smart Inventory ---
Sys --> UC_SmartAnaliza : Codzienny audyt
UC_SmartAnaliza <.. UC_SmartAlert : <<extend>> \n[Gdy krótka data lub \nczas dostawy > zapasy]
UC5 <.. UC_SmartAlert : <<extend>> \n[Gdy zapasy < minimum]

' ==========================================
' WYMUSZENIE UKŁADU GRAFICZNEGO (Niewidoczne linie)
' ==========================================
' Ten blok upewnia się, że sekcje są ułożone chronologicznie od góry do dołu
UC1 -[hidden]down-> UC_Pakowanie
UC_Pakowanie -[hidden]down-> UC8
UC8 -[hidden]down-> UC_Inwentaryzacja

M1 -[hidden]down-> M4_Trasy
Sys -[hidden]down-> Chef
Chef -[hidden]down-> Mag

@enduml