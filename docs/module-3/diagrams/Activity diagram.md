@startuml
skinparam activityBackgroundColor #FEFEFE
skinparam activityBorderColor #3465A4
skinparam defaultFontSize 10

title Diagram czynności – Przebieg torby i etykiet (UC_Pakowanie)

|Magazynier|
start
: Pobierz listę zamówień\ndo skompletowania;

|System|
: Pobierz aktywne Zamowienia;

|Magazynier|
while (dla każdego Zamowienia) is (kolejne)
  : Weź pustą torbę\n<<create>> Torba (Nowa);

  |System|
  : przypiszZamowienie()\nTorba → stan "W kompletacji";
  note left: Powiązanie ze złotą nicią\n(Zamowienie, Klient)

  |Magazynier|
  while (dla każdego wymaganego Pudełka) is (3-6)
    : Wybierz Pudełko\nz kuchni;
    |System|
    : weryfikujZgodnosc(torba);
    if (Zgodność OK?) then (tak)
      |Magazynier|
      : Dodaj Pudełko do Torby;
      |System|
      : dodajPudelko()\n(Torba zawiera pudełko);
    else (nie)
      |Magazynier|
      : zglosUszkodzenie()\n(problem fizyczny lub błędny posiłek);
      note left: Torba → stan\n"Oczekuje na rozpatrzenie"
      if (Czy da się rozwiązać?) then (tak)
        : Wymień Pudełko\nna poprawne;
        |Magazynier|
        : Dodaj poprawione Pudełko\ndo Torby;
        |System|
        : dodajPudelko()\n(Torba zawiera pudełko);
      else (nie)
        |System|
        : Anuluj Torbę\nTorba → stan "Anulowana";
        stop
      endif
    endif
  endwhile (koniec pudełek)

  |System|
  : Oznacz Torbę jako "Skompletowana";
  : Generuj Etykietę Wysyłkową\n(dane z Trasy, Stopu, okna czasowego);
  : przypiszEtykiete()\ndo Torby;
  note right: Torba → stan "Oznakowana"

  : Rozlicz zużycie opakowań\n(ZuzycieOpakowan);
  : gotowaDoZaladunku()\nTorba → stan "Gotowa do załadunku";

  |Magazynier|
  : Przenieś Torbę do\nstrefy załadunku;

  |System|
  : dodajTorbe() do Manifestu\n(sprawdzLIFO, Auto);
  : Załaduj na Auto\n(Manifest.zatwierdzWydanie);
  note right: Torba → stan "Załadowana"

endwhile (koniec zamówień)

|System|
: Proces kompletacji\nzakończony;

|Magazynier|
stop
@enduml