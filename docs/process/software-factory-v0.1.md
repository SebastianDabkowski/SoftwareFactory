# Proces wytwarzania Software Factory — wersja 0.1.0

Status: **uzgodniona baza robocza do dalszych aktualizacji, nie gotowy executor produkcyjny**.

Cel: na podstawie celu projektu przejść przez analizę biznesową, architekturę,
implementację kolejnych ticketów, testy oraz wdrożenie na **stage**. Wdrożenie
produkcyjne nie jest częścią tej wersji. Samo Software Factory ma docelowo działać
jako usługa na serwerze, niezależnie od wdrażanych aplikacji.

## 0. Co jest teraz w kodzie

- Wersjonowana, maszynowo czytelna definicja:
  `SoftwareFactory.Core/Workflow/Definitions/software-factory.v0.1.json`.
- Modele etapów, kroków, przejść i limitów; walidacja definicji/grafu.
- Publiczny model `Ticket`, pytania, komentarze między rolami, zależności i PR.
- Rola `Monitoring` oraz przygotowane role pozostałych agentów.
- Przygotowanie katalogu projektu: cel, snapshot definicji, początkowy stan,
  dziennik zdarzeń i pusty backlog; **bez wywoływania modeli**.
- Dotychczasowy `SoftwareSystem.Start()` nadal wykonuje wyłącznie wcześniejsze demo
  Architect → Developer → opcjonalnie QA. **Nie wykonuje tego dokumentu/grafu.**

Jeszcze niezaimplementowane: wykonawca pełnego grafu, wznawianie kroków, faktyczne
liczenie limitów pętli, walidacja wyników agentów, współdzielony magazyn stanu,
API/worker serwerowy, obsługa zatwierdzeń, integracje Git/PR/CI/CD i monitoring.
Wymagania ich działania opisujemy poniżej; nie traktujemy ich jako istniejącej funkcji.

## 1. Wejście, wersjonowanie i odpowiedzialność

Wejściem jest `BRIEF.md`: cel, odbiorcy, problem, zakres, ograniczenia, kryteria
sukcesu oraz znane decyzje. Nie trzeba wybierać technologii z góry — ma wynikać
z celu i decyzji architektonicznych, chyba że właściciel narzuca ograniczenia.

Każdy projekt otrzymuje `projectId`, każdy przebieg `runId`. Przy przygotowaniu
zapisujemy kopię definicji i jej wersję oraz SHA-256 definicji i celu. Edycja
szablonu factory nie może zmienić procesu już uruchomionego projektu. Nowe wersje
będą dodawane jako osobne pliki. Migracja istniejącego przebiegu wymaga jawnej
operacji, uzasadnienia i zdarzenia audytowego — nie podmieniamy snapshotu w tle.

**Orkiestrator decyduje o przejściu kroku; model dostarcza wynik i dowody.**
Samo „zrobione” w odpowiedzi agenta nigdy nie zastępuje pliku, wyniku polecenia,
aktualnego CI, potwierdzenia API Git czy dowodu wdrożenia.

## 2. Etap 1 — Biznes

**BusinessAnalyst Agent**:
1. Czyta cel projektu.
2. Robi research problemu, użytkowników, alternatyw/konkurencji, wartości,
   ograniczeń, ryzyk i mierników sukcesu.
3. Zapisuje `docs/business/research.md` ze źródłami i datą dostępu.
4. Oddziela fakty, hipotezy, założenia i kwestie do właściciela projektu.

Brak sieci nie może prowadzić do wymyślania źródeł. Agent opisuje ograniczenie.
Brak informacji niezbędnych do rozpoczęcia oznacza `Blocked` i pytanie do człowieka.

## 3. Etap 2 — Architektura i przygotowanie implementacji

| Krok | Rola | Działanie i wynik |
|---|---|---|
| 2.1 `architecture.scope` | Architect | Funkcjonalności, aktorzy, przypadki użycia, zdarzenia i reguły → `docs/architecture/system-model.md`. |
| 2.2 `architecture.business-questions` | Architect | Pytania biznesowe z kontekstem, ID i informacją o blokowaniu → `docs/business/questions.json`. |
| 2.3 `architecture.business-answers` | BusinessAnalyst | Odpowiedzi, źródła, założenia i eskalacje → `docs/business/answers.md`; aktualizacja pytań. |
| 2.4 `architecture.domain` | Architect | Core domain, odpowiedzialność aplikacji, słownik, granice i integracje → `architecture.md`. |
| 2.5 `architecture.quality-questions` | Architect | Pytania o bezpieczeństwo, prywatność, wydajność, dostępność, koszty, skalę, utrzymywalność i serwer → `docs/architecture/quality-questions.json`. |
| 2.6 `architecture.quality-answers` | BusinessAnalyst | Priorytety biznesowe i mierzalne charakterystyki → `docs/architecture/quality-requirements.md`. |
| 2.7 `architecture.decisions` | Architect | Przegląd i zapis ADR → `docs/adr/NNNN-title.md`. |
| 2.8 `architecture.code-structure` | Architect | Układ kodu, projekty/moduły, zależności, konfiguracja, testy, migracje i wdrażanie → `docs/architecture/code-structure.md`; aktualizacja `architecture.md`. |
| 2.9 `architecture.backlog` | Architect | Lista zadań zgodna z `Ticket.cs` → `tickets/tickets.json`. Pierwsze zadanie: struktura kodu i bazowy build/test. |
| 2.10 `architecture.backlog-answers` | BusinessAnalyst | Odpowiedzi na pytania ticketów, uzupełnienie kryteriów akceptacji. |
| 2.11 `architecture.backlog-review` | Architect | Przegląd wszystkich zadań, zależności i kolejności; braki wracają do BA lub planowania. |
| 2.12 `architecture.visual-style` | Architect | Wygląd aplikacji dla Frontend Agenta → `docs/design/ui-style.md`; ekrany powiązane z ticketami. |

### Reguły architektury

- ADR powstaje **przy każdej istotnej decyzji w całym cyklu**, nie tylko w kroku 2.7.
  Zawiera kontekst, alternatywy, decyzję, konsekwencje, status i odnośniki.
- Kanoniczna nazwa w nowym procesie: **`architecture.md`**. Wcześniejszy runner demo
  nadal używa `ARCHITECTURE.md`; przy migracji nie tworzymy dwóch rozbieżnych źródeł.
- Pytanie blokujące bez odpowiedzi zatrzymuje zależną pracę. BA nie jest substytutem
  właściciela produktu: nie może „potwierdzać” nieznanej ceny, regulacji czy budżetu.
- Powrót do backlogu nie usuwa historii ani ukończonych ticketów. Identyfikatory
  są stabilne; nowe wymagania nie mogą po cichu nadpisać istniejących zadań.
- Style guide opisuje layout, kolory, typografię, komponenty, błędy, puste stany,
  ładowanie, mobile i dostępność. Dla systemu bez UI dopuszczalne jest uzasadnione N/A.

### Definition of Ready ticketa

- Stabilne ID, projekt, opis, zakres, kolejność i właściciel roli.
- Testowalne kryteria akceptacji i odniesienia do architektury/ADR.
- Zakończone zależności, brak blokujących pytań, określony zakres frontend/Playwright.
- Status `Ready`/`Todo`, albo `ChangesRequested` po wcześniejszej weryfikacji.
- `Ticket.CanStart(...)` sprawdza podstawową gotowość pojedynczego zadania. Nie jest
  pełnym walidatorem backlogu, wykrywaniem cykli ani schedulerem — te elementy
  należą do przyszłego wykonawcy.

## 4. Etap 3 — Kodowanie, QA i PR dla każdego zadania

```text
Wybierz gotowy ticket → branch z develop
    → Developer/backend + unit testy + komentarz dla Frontend
    → Frontend na tym samym branchu
    → QA: scenariusze → Playwright/testy API
        ├─ błąd → komentarz/reprodukcja → Developer → Frontend → QA
        └─ PASS → Developer: PR → Architect: review
                     ├─ uwagi → komentarz → Developer → Frontend → QA → PR/review
                     └─ zatwierdzenie + zielone CI → merge do develop → kolejny ticket
```

1. **Developer** wybiera zadanie z zakończonymi zależnościami. Poprawki
   `ChangesRequested` mają pierwszeństwo, dalej `order`, `priority`, `id`.
   Nie wolno uznać backlogu za pusty, jeżeli pozostały zablokowane zadania zakresu.
2. Branch ma nazwę `ticket/{ticketId}-{slug}`, pochodzi z aktualnego `develop`.
   W poprawkach używamy tego samego brancha i PR; nie tworzymy ich ponownie.
3. **Developer** realizuje backend i unit testy. W komentarzu do Frontend przekazuje
   brakujące UI, kontrakty API, ograniczenia i dowody sprawdzenia backendu.
4. **Frontend** kontynuuje ten sam ticket/branch zgodnie z design briefem.
   Dla backend-only zapisuje N/A z uzasadnieniem.
5. **QA** tworzy scenariusze oparte na kryteriach, następnie automatyzuje je w
   Playwright i uruchamia. Dla zadania bez UI wykonuje właściwe testy API/integracyjne;
   brak zakresu UI nie zwalnia z weryfikacji.
6. Przy błędzie QA dodaje komentarz do ticketa: kroki reprodukcji, oczekiwane/
   rzeczywiste zachowanie, wersja/SHA, log, trace/screenshot i adresat Developer.
   Status `ChangesRequested`, powrót do implementacji backendu (3.2).
7. **Developer** tworzy/aktualizuje PR do `develop` dopiero po przejściu testów.
8. **Architect** sprawdza diff, architekturę, ADR, kryteria, bezpieczeństwo i CI.
   Uwagi w review i tickecie kierują pracę z powrotem do 3.2.
9. **Architect** zatwierdza merge, ale operację wykonuje kontrolowana integracja Git.
   Wymagane są review oraz zielone CI dla **aktualnego SHA**; zmiana SHA unieważnia
   wcześniejsze zatwierdzenie. Po potwierdzonym merge zapisujemy commit i `Done`.
10. Wracamy po następny ticket. Pusty uzgodniony backlog prowadzi do etapu testowego.

Pytanie biznesowe odkryte w kodowaniu zachowuje `currentTicketId`, branch i historię.
Ticket zostaje zablokowany, trafia do BA i przeglądu architekta. Po wyjaśnieniu
wraca jako poprawka do tego samego zadania. Nie rozpoczynamy implementacji innego
zakresu pod tym samym ID.

### Statusy ticketów

```text
New → Ready/Todo → InProgress → ReadyForFrontend → ReadyForTesting
    → ReadyForReview → ReadyForDeployment → Done (potwierdzony merge)
                  ↘ ChangesRequested → InProgress
Dowolny krok wymagający danych/uprawnień → Blocked
Jawnie wycofane zadanie → Cancelled
```

`ReadyForDeployment` w cyklu ticketa oznacza dopuszczenie do integracji po review,
a nie dowód wdrożenia aplikacji. Wersję wdrożoną opisuje osobny manifest release.
Znaczenia istniejących numerycznych wartości enum nie zostały zmienione.
JSON używa nazw camelCase, nie numerów.

Komentarze w `Ticket.cs` są od teraz strukturalne: autor, adresat, krok procesu,
wiadomość, czas UTC i ścieżki dowodów. Model był wcześniej wewnętrzny i nieużywany
przez runner; jeżeli istnieją zewnętrzne pliki ze starym `comments: ["tekst"]`,
będą wymagały jawnej migracji do nowego kontraktu.

## 5. Etap 4 — Release na test i raport jakości

1. **DevOps** buduje wersjonowany artefakt z `develop`, po zatwierdzeniu wdraża go na
   **test**, wykonuje migracje i health check oraz zapisuje `releases/test/manifest.json`
   i raport wdrożenia. Manifest zawiera release ID, commit SHA, digest artefaktu,
   środowisko, URL, pipeline, migracje i wynik. Błąd wdrożenia trafia do triage.
2. **QA** wykonuje całą regresję na wdrożonej wersji. Znalezione defekty tworzą
   tickety `Bug` z reprodukcją, release ID i dowodami. Istniejące błędy deduplikujemy.
3. **QualityEngineer** sprawdza charakterystyki systemu, analizę statyczną,
   zależności, bezpieczeństwo, dostępność i wyniki pomiarów. Zapisuje raport
   jakości i tickety dla niespełnionych kryteriów.
4. **Architect** analizuje wszystkie błędy, doprecyzowuje priorytety i kryteria.
   Gotowe błędy wracają do etapu 3. Po poprawkach powstaje **nowy release test**
   i ponownie wykonujemy regresję oraz ocenę jakości.
5. Dopiero pozytywny wynik bez otwartych blokujących defektów dopuszcza stage.

## 6. Etap 5 — Stage, monitoring i feedback

1. **DevOps** po zatwierdzeniu promuje **ten sam sprawdzony digest artefaktu** na stage;
   nie buduje innej wersji. Wykonuje health check/smoke test, a przy błędzie rollback
   i triage. Raport dotyczy konkretnej wersji.
2. **Monitoring Agent** przez skonfigurowane okno obserwacji sprawdza logi, metryki,
   trace i progi/SLO. Ma uprawnienia read-only. Błędy tworzą tickety; brak telemetrii
   oznacza blokadę, nie „brak błędów”. Nie restartuje ani nie wdraża samodzielnie.
3. **BusinessAnalyst** porównuje działający system z celem i miernikami sukcesu.
   Zapisuje uwagi, rozróżniając defekty od nowych pomysłów/zmian zakresu.
4. **Architect** przegląda backlog i braki. Zatwierdzone zadania wracają do etapu 3,
   a następnie przechodzą ponownie przez test i stage.
5. Koniec: spełniony uzgodniony zakres, brak blokujących defektów, dowody dla
   aktualnej wersji, zaakceptowane ograniczenia i `artifacts/final-report.md`.

Nowe pomysły są propozycjami `Change` do decyzji właściciela. Nie pozwalamy agentom
rozszerzać bez końca zakresu tylko po to, by ciągle tworzyć następne tickety.

## 7. Pętle, limity i zatwierdzenia

Definicja 0.1 zawiera domyślne limity do egzekwowania przez przyszły executor:
- maksymalnie 3 rundy wyjaśnień dla danej grupy pytań;
- maksymalnie 3 powroty implementacyjne QA/review dla jednego ticketa;
- maksymalnie 3 cykle poprawek release w przebiegu.

Przekroczenie limitu: `Blocked`, zapis dowodów i powodu, eskalacja do właściciela.
Nie oznacza PASS ani porzucenia błędu. Przyszłe retry awarii technicznej musi być
odróżniane od nowej iteracji biznesowej i od retry wewnętrznego harnessu Pi.

Krok z `requiresApproval: true` ma przed wykonaniem przejść do `AwaitingApproval`.
Zatwierdzenie wiążemy z projektem, krokiem, wersją/SHA/digestem, środowiskiem,
konkretną operacją i osobą zatwierdzającą. Odmowa nie uruchamia poleceń wdrożenia.
W 0.1 takie bramki dotyczą **test** i **stage**. Brak konfiguracji środowiska,
repozytorium lub uprawnień jest blokadą — nie zastępujemy integracji fikcyjnym URL.

Tokeny, sekrety i dostęp do serwera nie należą do celu, ticketów, promptów ani logów.
Obietnica modelu o read-only nie ogranicza systemowych uprawnień: potrzebne są
oddzielne tożsamości, sandbox/kontener i egzekwowanie uprawnień przez hosta.

## 8. Układ przygotowanego projektu

```text
project/
  BRIEF.md
  project.json
  README.factory.md
  process/
    definition.json        # snapshot wersji dla projektu
    state.json             # Prepared, business.research, run/project IDs, hashe
    events.jsonl           # na początku wyłącznie zdarzenie przygotowania
  tickets/
    tickets.json           # na początku []
  docs/
    business/
    architecture/
    adr/
    design/
  artifacts/
    tickets/
    releases/
  releases/
    test/
    stage/
```

Dokumenty biznesowe, `architecture.md`, ADR, kod aplikacji, testy, raporty i manifesty
release tworzą dopiero właściwe kroki. Nie zapisujemy fikcyjnych wyników ani
pustych dokumentów udających zakończenie prac. `{ticketId}`, `{releaseId}` i
`NNNN-title` w definicji to szablony nazw, nie gotowe pliki.

## 9. Docelowe uruchamianie Software Factory na serwerze

To projekt architektury przyszłej implementacji, nie już wdrożona usługa:

- **API/control plane**: projekty i cele, start/pause/resume/cancel, backlog,
  pytania do właściciela, zatwierdzenia i podgląd postępu; uwierzytelnianie i RBAC.
- **Orchestrator + kolejka trwała**: interpretuje snapshot procesu, sprawdza
  warunki przejścia, zależności, limity, zatwierdzenia i wyniki integracji.
- **Worker pool**: uruchamia izolowane harnessy Pi per rola/krok, z limitem czasu,
  uprawnień, kosztów i równoległości; prywatny workspace/worktree na aktywny ticket.
- **Trwały store** (np. PostgreSQL, do decyzji ADR): przebiegi, tickety, pytania,
  zdarzenia, próby, approval i lease; pliki workspace nie wystarczą jako baza
  rozproszonej orkiestracji.
- **Artifact storage**: dokumenty, raporty Playwright, logi i artefakty release.
  Duże/binarne pliki nie muszą być w bazie ani w repozytorium aplikacji.
- **Adaptery**: Git provider/PR/CI, deployment test/stage, secret store i telemetria.
  Konfiguracja jawnie wiąże projekt z dozwolonym repozytorium i środowiskami.

### Odporność na restart i wielokrotne dostarczenie zadania

- Przed/po kroku zapisujemy checkpoint i zdarzenia `started/completed/failed/blocked`.
  Stan oraz event outbox muszą być transakcyjne w przyszłym store.
- Lease/heartbeat zapobiega jednoczesnej pracy dwóch workerów nad tym samym ticketem.
- Klucz idempotencji obejmuje run, step, ticket/release i numer próby.
- Po awarii najpierw sprawdzamy stan zewnętrzny. Nie tworzymy ponownie już istniejącego
  brancha, PR, merge ani deploymentu, jeżeli operacja zakończyła się przed zapisem stanu.
- Prompt i wynik agenta nie są autoryzacją operacji. Wynik ma zawierać rozpoznawany
  `outcome`, artefakty, wersję, wyniki poleceń oraz dowody; host weryfikuje ich istnienie
  i zgodność przed przejściem według definicji.
- Resume wykonuje bieżący bezpieczny krok, nie uruchamia całego projektu od początku.
- Aktualizacje procesu/agentów/modeli wymagają audytu; zapisywane są użyte wersje.

### Kolejność następnych implementacji

1. Executor i trwały stan dla etapów biznes/architektura, pytania i resume; testy na FakePi.
2. Repozytorium ticketów, scheduler zależności, izolacja branchy/worktree i pętla implementacji.
3. QA/Playwright oraz hostowa weryfikacja dowodów i limitów.
4. Adapter PR/CI, review i kontrolowany merge.
5. Wersjonowane release, konfiguracja środowisk, approval i rollback.
6. Monitoring, quality gates i feedback BA.
7. API + durable queue/store, autoryzacja, kontenery i deployment samego factory na serwer.

## 10. Przygotowanie projektu z celu

Z katalogu rozwiązania:

```sh
# Podgląd definicji JSON; nie startuje Pi.
dotnet run --project SoftwareFactory -- --show-process

# Własny cel; przygotowanie plików bez wywoływania modeli.
dotnet run --project SoftwareFactory -- --prepare-project --goal "Zbuduj system rezerwacji wizyt" --workspace ./workspaces/rezerwacje

# Cel z pliku UTF-8.
dotnet run --project SoftwareFactory -- --prepare-project --goal-file ./moj-cel.md --workspace ./workspaces/moj-projekt

# Istniejący scenariusz sklepu jako cel nowego procesu.
dotnet run --project SoftwareFactory -- --prepare-project --scenario ai-agent-store --workspace ./workspaces/sklep-proces-01

# Tylko podgląd celu i planowanego przygotowania, bez plików.
dotnet run --project SoftwareFactory -- --prepare-project --scenario ai-agent-store --dry-run
```

Nie podawaj jednocześnie `--goal`, `--goal-file` i `--scenario`. Przygotowanie
wymaga pustego/nieistniejącego katalogu; nie nadpisuje istniejącego projektu.
Komenda nie uruchamia pełnego procesu. `Prepared` nie oznacza `Running` ani `Completed`.
