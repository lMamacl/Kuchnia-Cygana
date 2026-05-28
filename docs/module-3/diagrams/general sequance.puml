@startuml
skinparam activityBackgroundColor #FEFEFE
skinparam activityBorderColor #3465A4
skinparam defaultFontSize 10
skinparam arrowColor #3465A4

|System (Harmonogram)|
    |Szef Kuchni|
        |Magazynier|
            |Kierowca|

|System|
    start
    :<<async>> Uruchom codziennie o 22:00\nlub na żądanie;
    :Pobierz aktywne Zamówienia\n(<<external>> Moduł 1);
    :Pobierz DietVariant i Meal\n(<<external>> Moduł 2);
    :<<create>> Utwórz PlanProdukcji\n(agregacja zamówień);
    :Wygeneruj PozycjePlanu\n(dla każdego Meal i DietVariant);
    :Oblicz zapotrzebowanie\nna składniki (Food Cost);
    :Sprawdź dostępność składników\nw magazynie (Skladnik);
    if (Brakuje składników?) then (tak)
        :Wygeneruj Raport o brakach\noraz alerty do zaopatrzenia;
        stop
    else (nie)
    endif
    :Udostępnij PlanProdukcji\nw panelu Szefa Kuchni;
    |Szef Kuchni|
        :Pobierz PlanProdukcji\nna dany dzień;
        :Rozpocznij produkcję\n(wg PozycjiPlanu);
        :Przygotuj półprodukty\n(<<create>> SubRecipe);
        :Ugotuj posiłki;
        :<<create>> Porcjuj i zapakuj\nw Pudełka (zafoliowane);
        :Dla każdego Pudelka\n<<create>> EtykietaProduktowa\n(kod QR, alergeny, kcal);
        :Zarejestruj zużycie składników\n(Skladnik.zdejmijZeStanuFEFO)\noraz powiąż Pudelko z Partią\n(<<async>> HACCP);
        :Zgłoś zakończenie produkcji\n(PlanProdukcji.zatwierdzUgotowanie);
    |Magazynier|
        :Pobierz listę zamówień do\nskompletowania (Zamowienie);
        :Pobierz Trasy i Stopy\n(<<external>> Moduł 4);
        :Dla każdego Zamowienia\n<<create>> Torba;
        :Zweryfikuj zgodność Pudełek\nz dietą (Torba.weryfikujZgodnosc);
        :Umieść 3-6 Pudełek w Torbie;
        :<<create>> EtykietaWysylkowa\ndla Torby (dane z Trasy/Stopu);
        :Rozlicz zużycie opakowań\n(ZuzycieOpakowan);
        :Oznacz Torbę jako gotową\ndo załadunku;
        :<<create>> ManifestZaladunkowy\ndla Trasy;
        :Dodaj Torby do Manifestu\n(Manifest.dodajTorbe);
        :Przypisz Auto do Manifestu\n(<<external>> Moduł 4);
        :Skanuj i załaduj Torby\ndo Auta (LIFO);
        :Zatwierdź wydanie towaru\n(Manifest.zatwierdzWydanie);
        :Przekaż Manifest Kierowcy\n(wydruk lub cyfrowo);
    |Kierowca|
        :Odbierz Manifest\nz listą Stopów i Toreb;
        :Realizuj dostawy\n(aktualizuj statusy w Module 4);
        stop
|System|
    :(w tle) Aktualizuj stany\nmagazynowe i alerty;
@enduml