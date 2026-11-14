# AI Agent Store — testowy sklep z agentami AI

## Cel
Zbuduj kompletną, uruchamialną aplikację webową sklepu internetowego „AgentHub”,
w którym klient kupuje cyfrowych agentów AI. To demonstracyjne MVP do testowania
Software Factory, a nie produkcyjny system płatności ani usługa wykonująca zadania AI.
Nie kończ na projekcie architektury, mockupach ani samym API: dostarcz działający UI,
backend, trwałe dane, testy i instrukcję uruchomienia.

## Technologia i układ plików
- .NET 11, ASP.NET Core Razor Pages, C#, HTML/CSS oraz niewielka ilość JavaScript.
- Jeden serwer aplikacji. Bez Reacta, Node.js jako zależności aplikacji, Dockera,
  zewnętrznego SQL Servera, CDN-ów i wymaganych kluczy API.
- SQLite z Entity Framework Core; baza inicjalizowana i seedowana automatycznie
  przy pierwszym uruchomieniu. Dane zamówień pozostają po restarcie aplikacji.
- Dobierz istniejące, zgodne wersje pakietów NuGet i sprawdź ich restore/build.
- Plik rozwiązania: `AiAgentStore.slnx`.
- Projekt webowy: `src/AiAgentStore.Web/AiAgentStore.Web.csproj`.
- Projekt testowy xUnit: `tests/AiAgentStore.Tests/AiAgentStore.Tests.csproj`.
- `README.md` z komendami restore/build/test/run oraz ścieżką i sposobem resetu bazy.
- Wszystkie pliki twórz wyłącznie w bieżącym workspace. Nie zmieniaj kodu Software Factory.
- Nie wykonuj git push, wdrożeń, zakupów, wysyłania maili ani operacji na usługach produkcyjnych.

## Interfejs
- Język polski, waluta PLN, formatowanie kwot pl-PL; obliczenia pieniężne używają decimal.
- Responsywny, dopracowany wizualnie sklep: jasne tło, ciemny nagłówek, fioletowy akcent,
  karty produktów, czytelne ceny, badge kategorii i ikony bez zewnętrznych zasobów.
- Nawigacja: logo AgentHub, Katalog, Jak to działa, Koszyk z licznikiem produktów.
- Widoczny komunikat „Sklep demonstracyjny — płatności i odpowiedzi AI są symulowane”.
- Formularze z etykietami, walidacją i komunikatami błędów; poprawny widok mobilny.
- Koszyk oraz zamówienia oddzielone dla różnych klientów/sesji. Bez logowania w MVP.
- Bez panelu administracyjnego, marketplace sprzedawców i prawdziwych subskrypcji.

## Katalog — co najmniej 6 produktów startowych
Ceny są jednorazowymi cenami demonstracyjnej licencji, bez automatycznych odnowień.
Każdy produkt ma stabilny slug, nazwę, krótki i pełny opis, kategorię, cenę,
listę możliwości oraz przykładowe zastosowanie. Seed nie duplikuje produktów po restarcie.

| Nazwa | Slug | Kategoria | Cena |
|---|---|---|---|
| Support Bot | support-bot | Obsługa klienta | 49,00 PLN |
| Email Writer | email-writer | Komunikacja | 39,00 PLN |
| Meeting Analyst | meeting-analyst | Produktywność | 79,00 PLN |
| Content Creator | content-creator | Marketing | 59,00 PLN |
| Research Assistant | research-assistant | Analiza | 89,00 PLN |
| Sales Assistant | sales-assistant | Sprzedaż | 69,00 PLN |

- Strona główna z hero, trzema wyróżnionymi agentami i przyciskiem „Przeglądaj agentów”.
- Katalog: wyszukiwanie po nazwie/opisie, filtr kategorii, sortowanie po cenie i nazwie,
  przycisk czyszczenia filtrów oraz przyjazny stan „brak wyników”.
- Szczegóły produktu: opis, cena, możliwości, przykład użycia i „Dodaj do koszyka”.
- Nieistniejący slug produktu zwraca HTTP 404.

## Dema trzech agentów — bez rzeczywistych wywołań AI
Dema działają deterministycznie na serwerze, bez API LLM i bez zewnętrznych połączeń.
Wynik jest wyraźnie oznaczony jako symulacja, a nie realna odpowiedź modelu.
1. Support Bot: klient wpisuje pytanie o sklep; proste reguły zwracają odpowiedź
   dotyczącą zakupu, licencji lub kontaktu. Dla innych pytań pokaż bezpieczny fallback.
2. Email Writer: formularz odbiorcy, celu wiadomości, tonu (formalny/swobodny) i punktów;
   wygeneruj temat oraz treść maila z szablonu. Niczego faktycznie nie wysyłaj.
3. Meeting Analyst: formularz tekstu transkrypcji spotkania; pokaż demonstracyjne
   podsumowanie, decyzje i zadania z właścicielami, jeżeli występują w tekście.
   Nie udawaj, że ustalenia nieobecne w tekście są faktami. Nie implementuj nagrywania
   ani transkrypcji audio. Dodaj gotowy przykładowy tekst do wczytania.
- Puste oraz zbyt długie dane wejściowe mają czytelny błąd walidacji.
- Nie loguj pełnej treści maili, pytań ani transkrypcji. Renderuj dane z escapingiem HTML.

## Koszyk i demonstracyjny zakup
- Dodawanie/usuwanie agentów; cyfrowy produkt występuje w koszyku tylko raz.
- Podsumowanie pozycji i łącznej ceny; ceny zawsze odczytywane po stronie serwera.
  Nie ufaj kwotom przesłanym przez przeglądarkę.
- Checkout: imię, poprawny adres e-mail i akceptacja demonstracyjnego regulaminu.
- Brak formularza kart, integracji Stripe/PayPal i rzeczywistych obciążeń.
- Pusty koszyk nie pozwala złożyć zamówienia. Zabezpiecz formularze przed CSRF.
- Zamówienie oraz jego pozycje zapisuj atomowo w SQLite; snapshot nazw i cen zakupu.
- Dla każdej kupionej pozycji zapisz demonstracyjny identyfikator licencji.
- Po zakupie wyczyść koszyk i pokaż numer zamówienia, kupionych agentów oraz licencje.
- Potwierdzenie zakupu nie wysyła e-maila. Zamówienia klienta nie mogą być widoczne
  dla innej sesji ani przez samo odgadnięcie kolejnego identyfikatora.

## Weryfikacja i kryteria odbioru
- Minimum 10 sensownych testów, bez testów-pustych zaślepek.
- Pokryj: seed i brak duplikatów, filtrowanie, sortowanie, dodawanie i usuwanie z koszyka,
  brak duplikatów w koszyku, sumowanie decimal, pusty koszyk, walidację checkoutu,
  zapis zamówienia i licencji, izolację koszyków/zamówień oraz dema trzech agentów.
- Dodaj testy HTTP dla strony głównej, katalogu, 404 produktu i `/health`.
- Testy nie zależą od sieci, LLM ani produkcyjnej bazy; każdy test ma izolowane dane.
- `GET /health` zwraca HTTP 200 po poprawnym starcie.
- W bieżącym workspace wykonaj i popraw do skutku:
  ```sh
  dotnet restore AiAgentStore.slnx
  dotnet build AiAgentStore.slnx --no-restore
  dotnet test AiAgentStore.slnx --no-build
  ```
- Uruchom aplikację lokalnie, sprawdź `/health` i stronę główną, a następnie zamknij
  uruchomiony przez siebie proces. Nie zostawiaj serwerów w tle.
- Docelowa komenda dla użytkownika:
  ```sh
  dotnet run --project src/AiAgentStore.Web --urls http://localhost:5080
  ```
- QA zapisuje `QA_REPORT.md`: rzeczywiste komendy i wyniki, liczbę testów, wyniki
  smoke testu HTTP, znane ograniczenia, instrukcję ręcznego przejścia zakupowego
  i końcowy status PASS/FAIL. Nie deklaruj sukcesu, jeżeli komenda nie była wykonana
  lub zakończyła się błędem; brak dostępu do narzędzia/pakietu zgłoś jako blokadę.
