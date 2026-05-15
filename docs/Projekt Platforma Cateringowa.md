# **Karta Projektu: Platforma Cateringowa "Kuchnia u Cygana"**

## **1\. Informacje ogólne i skład zespołu**

**Nazwa projektu:** Kuchnia u Cygana – System e-commerce i ERP dla cateringu dietetycznego

**Skład zespołu (5 osób):**

1. **\[Dawid Janczyło\]** – Moduł E-commerce i Zamówień  
2. **\[Gabriel Ostaszewski\]** – Moduł Katalogu Diet i Receptur  
3. **\[Maciej Cyuńczyk\]** – Moduł Produkcji i Magazynu  
4. **\[Tomasz Golonko\]** – Moduł Logistyki i Dostaw  
5. **\[Paweł Trochimczyk\]** – Moduł Administracji, HR i Komunikacji

---

## **2\. Opis projektu i cele biznesowe**

Projekt "Kuchnia u Cygana" to kompleksowa platforma webowa stworzona do obsługi rosnącego rynku cateringu dietetycznego (tzw. "diet pudełkowych"). Aplikacja stanowi pomost pomiędzy nowoczesnym portalem e-commerce dla klientów detalicznych (B2C), a zaawansowanym systemem zarządzania zasobami przedsiębiorstwa (ERP) przeznaczonym dla pracowników kuchni, zaopatrzenia i logistyki. 

Głównym celem systemu jest pełna cyfryzacja i automatyzacja procesów w firmie gastronomicznej, co pozwala wyeliminować błędy ludzkie, zoptymalizować koszty i znacząco przyspieszyć codzienną pracę.

**Perspektywa Klienta (B2C)**  
Od strony klienta platforma oferuje intuicyjny interfejs do przeglądania dostępnych diet, wyboru odpowiedniej kaloryczności oraz składania i opłacania zamówień. Kluczowym elementem jest elastyczny kalendarz dostaw, który pozwala użytkownikom na swobodne zarządzanie swoimi posiłkami – na przykład wstrzymywanie dostaw na czas wyjazdu i przenoszenie ich na inne dni. Zjawisko "samoobsługi" drastycznie odciąża Biuro Obsługi Klienta (BOK) i znacząco poprawia User Experience (UX), budując długotrwałą lojalność.

**Perspektywa Zaplecza i Produkcji (ERP)**  
Z perspektywy zaplecza firmy system wykonuje najcięższą pracę analityczną. Zamiast ręcznego liczenia porcji, aplikacja automatycznie agreguje wszystkie aktywne zamówienia na dany dzień i na podstawie zdefiniowanych wcześniej receptur wylicza dokładne zapotrzebowanie na surowce (tzw. *Food Cost*).  
System generuje gotowe plany produkcyjne dla kucharzy, automatycznie aktualizuje stany w wirtualnym magazynie (wykorzystując logikę kontroli dat ważności FEFO \- *First Expired, First Out*) i pomaga w generowaniu list zakupowych dla dostawców. Gwarantuje to drastyczne zmniejszenie strat żywności (*Zero Waste*) oraz ustandaryzowanie jakości i smaku potraw dzięki scentralizowanej bazie receptur. Dodatkowo moduł inwentaryzacji i śledzenia partii produkcyjnych wspiera zgodność z rygorystycznymi normami sanitarnymi (HACCP).

**Logistyka i Zarządzanie**  
Proces zamyka zintegrowany moduł logistyczny oraz administracyjny. Aplikacja wspiera dyspozytorów w grupowaniu adresów, wyznaczaniu optymalnych tras dla floty pojazdów oraz generowaniu etykiet przewozowych w formacie PDF, co przynosi wymierne oszczędności czasu i paliwa.  
Nad całością operacji czuwa zaawansowany system ról i uprawnień (kucharz, kierowca, administrator, klient), zapewniający ścisłe bezpieczeństwo danych. Platforma wspiera również utrzymanie posprzedażowe poprzez wbudowany system zgłoszeń reklamacyjnych (z możliwością załączania zdjęć posiłków), co ułatwia sprawną mitygację problemów z klientami.

---

## **3\. Kluczowe Korzyści Biznesowe**

Wdrożenie platformy niesie za sobą szereg wymiernych korzyści dla przedsiębiorstwa cateringowego:

* **Zwiększenie marż i minimalizacja strat (Zero Waste):** Automatyczne, precyzyjne generowanie planów produkcyjnych i list zakupowych ściśle zintegrowanych z aktualnymi zamówieniami eliminuje zjawisko nadprodukcji i marnowania żywności.  
* **Usprawnienie rotacji magazynowej:** Zastosowanie wirtualnego magazynu z algorytmem FEFO (First Expired, First Out) gwarantuje zużywanie surowców o najkrótszym terminie przydatności w pierwszej kolejności.  
* **Redukcja kosztów operacyjnych:** Pełna automatyzacja procesów – od agregacji zamówień po wyznaczanie optymalnych tras i generowanie etykiet przewozowych – drastycznie skraca czas manualnej, podatnej na błędy pracy personelu (kucharzy, dyspozytorów, BOK).  
* **Wzrost zadowolenia i lojalności klientów:** Oddanie w ręce klienta potężnego narzędzia "samoobsługi" (elastyczny kalendarz dostaw, zgłoszenia) poprawia User Experience (UX), jednocześnie zmniejszając liczbę zapytań telefonicznych.  
* **Zgodność z normami i standaryzacja:** Śledzenie partii produkcyjnych (traceability) oraz scentralizowana cyfrowa baza receptur ułatwiają zachowanie wysokiej, powtarzalnej jakości posiłków oraz bezproblemowe spełnianie wymogów sanitarnych (HACCP).

---

## **4\. Architektura, mechanizmy i technologie**

Aplikacja zostanie zrealizowana zgodnie z najlepszymi praktykami wydajnego programowania w .NET:

* **Architektura:** Warstwowa (N-Tier / Clean Architecture) z podziałem na API/Web, Warstwę Logiki Biznesowej (BLL) oraz Warstwę Dostępu do Danych (DAL).  
* **Wzorce projektowe:** Wzorzec **Repository** (do komunikacji z bazą), **Dependency Injection** (DI) oraz **Data Transfer Object (DTO)** (do mapowania danych wyjściowych).  
* **Baza danych:** Relacyjna baza danych obsługiwana przez **Entity Framework Core** (ORM).  
* **Operacje CRUD:** Zaimplementowane we wszystkich modułach (np. zarządzanie dietami, składnikami, pojazdami).  
* **Uwierzytelnianie:** Zaimplementowane role (Klient, Pracownik, Admin) oparte na ClaimsIdentity.  
* **Obsługa plików:** Wgrywanie zdjęć posiłków do menu oraz zdjęć do formularza reklamacji (HTML file).  
* **Generowanie dokumentów:** Generowanie raportów produkcyjnych i etykiet przewozowych do formatu **PDF**.  
* **Zewnętrzne API:** Wykorzystanie API płatności (np. Stripe), API mapowego (geokodowanie adresów) oraz OpenAI (generowanie treści).  
* **Wdrożenie:** Aplikacja zostanie wdrożona na środowisko produkcyjne (np. Azure / SmarterASP).

---

## **5\. Zakres Projektu (Scope) i lista funkcjonalności**

Aby zrealizować powyższe cele biznesowe, system został podzielony na 5 głównych modułów w ramach aplikacji webowej:

1. **Portal B2C i E-commerce:** Sklep internetowy, kreator zamówień, kalendarz dostaw, płatności online (Stripe), profile klientów.  
2. **Katalog Menu i Receptury:** Zarządzanie bazą posiłków, składników, alergenów, kreator receptur oraz integracja z AI (OpenAI) do generowania opisów.  
3. **Produkcja i Magazyn (ERP):** Agregator zamówień, automatyczny plan produkcji, rozchodowanie surowców z magazynu (Smart Inventory, FEFO), generowanie kart produkcji i kompletacji.  
4. **Logistyka i Dostawy:** Planowanie tras, przypisywanie kierowców, geokodowanie (OpenStreetMap), generowanie etykiet przewozowych, panel mobilny dla kierowcy.  
5. **Administracja i HR:** System ról i uprawnień, helpdesk/ticketing dla klientów, zarządzanie grafikami personelu, dashboard z KPI dla zarządu.

Poniżej znajduje się szczegółowe omówienie funkcjonalności każdego z nich:

### **Moduł 1: Portal Klienta i E-commerce (B2C)**

Ten moduł to "wizytówka" firmy. Służy do pozyskiwania klientów i obsługi ich zamówień.

* **Rejestracja i uwierzytelnianie:** Klienci mogą zakładać konta, logować się i zarządzać swoim profilem (zmiana hasła, edycja danych kontaktowych). System wykorzystuje ClaimsIdentity.  
* **Przeglądanie oferty (Katalog):** Użytkownik widzi dostępne rodzaje diet (np. Keto, Vege, Sport) wraz z ich wariantami kalorycznymi (np. 1500 kcal, 2500 kcal) oraz przykładowym menu na dany tydzień.  
* **Kreator zamówienia:** Klient wybiera dietę, kaloryczność, datę rozpoczęcia oraz długość trwania zamówienia (np. 20 dni roboczych).  
* **Zarządzanie kalendarzem dostaw:** Klient w swoim panelu widzi kalendarz aktywnych diet. Posiada kluczową funkcjonalność "zawieszenia" dostawy (np. z powodu wyjazdu na urlop) i przeniesienia jej na inny dzień, co automatycznie aktualizuje harmonogram produkcji.  
* **Książka adresowa:** Możliwość zdefiniowania wielu adresów dostaw (np. "Dom", "Praca") i przypisania ich do konkretnych dni w kalendarzu.  
* **Integracja z płatnościami:** Po złożeniu zamówienia klient jest przekierowywany do bramki płatności (wykorzystanie zewnętrznego API, np. Stripe/PayU w trybie testowym).

### **Moduł 2: Zarządzanie Menu i Recepturami (Core Biznesowy)**

Moduł dla dietetyków i managerów, służący do projektowania tego, co firma sprzedaje.

* **CRUD dla Diet i Posiłków:** Tworzenie nowych diet, dodawanie posiłków do bazy (np. "Owsianka z malinami", "Kurczak z ryżem").  
* **Obsługa plików (Zdjęcia):** Podczas dodawania posiłku, pracownik może wgrać jego apetyczne zdjęcie (wykorzystanie formularza HTML file upload), które będzie wyświetlane klientom.  
* **Zarządzanie Składnikami i Alergenami:** Prowadzenie słownika wszystkich surowców (mąka, jajka, mleko) oraz przypisywanie do nich potencjalnych alergenów (np. gluten, laktoza).  
* **Kreator Receptur (Relacje ManyToMany):** Najbardziej zaawansowana część modułu. Pracownik łączy Posiłek ze Składnikami, określając dokładną gramaturę (np. Posiłek X składa się z 200g ryżu i 150g kurczaka). Pozwala to na późniejsze wyliczanie kosztów i zapotrzebowania.  
* **Integracja z AI (OpenAI API):** Funkcjonalność automatycznego generowania chwytliwych, marketingowych opisów posiłków na podstawie wprowadzonych składników (np. po kliknięciu "Generuj opis", AI zwraca tekst zachęcający do zakupu).

### **Moduł 3: System Produkcyjny i Magazyn (ERP)**

Moduł przeznaczony dla szefa kuchni i zaopatrzeniowców. Tłumaczy zamówienia klientów na realne zadania w kuchni.

* **Agregator Zamówień (Plan Produkcji):** System codziennie o określonej godzinie (lub na żądanie) zbiera wszystkie aktywne zamówienia na następny dzień i agreguje je. Zamiast pokazywać 100 osobnych zamówień, system mówi kucharzowi: "Na jutro musisz ugotować łącznie 45 porcji owsianki i 55 porcji kurczaka".  
* **Kalkulator Food Cost:** Na podstawie zagregowanych posiłków i ich receptur (z Modułu 2), system wylicza łączne zapotrzebowanie na surowce (np. "Potrzebujemy 10 kg ryżu i 8 kg kurczaka").  
* **Generowanie Karty Produkcyjnej (PDF):** Szef kuchni może wygenerować i pobrać plik PDF z czytelną tabelą produkcyjną na dany dzień, którą drukuje i wiesza w kuchni.  
* **Zarządzanie Magazynem (CRUD):** Śledzenie aktualnych stanów magazynowych. Wprowadzanie dostaw od hurtowników (zwiększanie stanów).  
* **Automatyczne rozchodowanie:** Po zatwierdzeniu zakończenia produkcji na dany dzień, system automatycznie odejmuje zużyte składniki z wirtualnego magazynu.

### **Moduł 4: Logistyka i Dostawy**

Moduł dla dyspozytorów i kierowców, odpowiedzialny za to, by jedzenie trafiło pod właściwy adres.

* **Zarządzanie Flotą i Kierowcami:** Ewidencja pojazdów firmowych (numery rejestracyjne, ładowność) oraz przypisywanie do nich kierowców.  
* **Planowanie Tras (Routing):** Dyspozytor widzi listę wszystkich adresów dostaw na dany dzień. Może grupować je w trasy (np. "Trasa Północ", "Trasa Południe") i przypisywać do konkretnego kierowcy.  
* **Integracja z API Mapowym:** Wykorzystanie zewnętrznego API (np. Google Maps / OpenStreetMap) do geokodowania adresów klientów (zamiana tekstu na współrzędne) w celu ułatwienia planowania.  
* **Generowanie Etykiet Przewozowych (PDF):** System generuje plik PDF z etykietami do naklejenia na torby cateringowe. Etykieta zawiera adres, imię klienta, rodzaj diety oraz kod QR/kreskowy.  
* **Panel Kierowcy:** Zalogowany kierowca (rola: Moderator/Kierowca) widzi na telefonie tylko swoją trasę na dziś. Może odznaczać statusy paczek (np. "Dostarczono", "Brak dostępu do klatki").

### **Moduł 5: Administracja, HR i Obsługa Klienta**

Moduł spinający całą aplikację, służący do zarządzania personelem i rozwiązywania problemów.

* **Zarządzanie Użytkownikami i Rolami:** Administrator może nadawać uprawnienia (np. awansować zwykłego użytkownika na pracownika kuchni lub kierowcę). System ról ściśle kontroluje dostęp do poszczególnych kontrolerów i widoków.  
* **Grafiki Pracownicze (HR):** Prosty system do planowania zmian dla pracowników kuchni i kierowców (kto pracuje w jaki dzień).  
* **System Zgłoszeń (Helpdesk / Tickety):** Klienci mogą zgłaszać problemy z zamówieniem (np. "Paczka była uszkodzona", "Brakowało jednego posiłku").  
* **Obsługa załączników w zgłoszeniach:** Klient podczas tworzenia zgłoszenia reklamacyjnego może wgrać plik (np. zdjęcie zniszczonego pudełka zrobione telefonem). Pracownik obsługi klienta widzi to zdjęcie w panelu administracyjnym i może odpowiedzieć na zgłoszenie.  
* **Dashboard Analityczny:** Ekran startowy dla Administratora wyświetlający podstawowe statystyki (np. liczba aktywnych diet w tym tygodniu, kończące się zapasy w magazynie).

