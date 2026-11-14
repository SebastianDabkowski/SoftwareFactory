# Software Factory — agenci na pi.dev

Aplikacja .NET 11 uruchamia dla każdego agenta osobny proces **Pi w trybie RPC**.
Nie korzysta z Codex app-server ani z protokołu JSON-RPC Codex.

## Pełny proces wytwarzania — wersja 0.1

Definicja procesu obejmuje **Biznes → Architekturę → Kodowanie → Test → Stage**,
rozmowy Architect ↔ BusinessAnalyst, ADR, backlog, pracę backend/frontend, QA z
Playwright, PR/review/merge, pętle poprawek, quality gates i monitoring.

- **Opis i plan przygotowania factory do serwera:** [docs/process/software-factory-v0.1.md](docs/process/software-factory-v0.1.md).
- **Definicja JSON do aktualizacji:** `SoftwareFactory.Core/Workflow/Definitions/software-factory.v0.1.json`.
- **Przykład kontraktu ticketów:** [docs/process/ticket.example.json](docs/process/ticket.example.json).

```sh
# Podgląd definicji, bez uruchamiania agentów.
dotnet run --project SoftwareFactory -- --show-process

# Przygotowanie projektu na podstawie dowolnego celu.
dotnet run --project SoftwareFactory -- --prepare-project --goal "Zbuduj system rezerwacji wizyt" --workspace ./workspaces/rezerwacje

# Cel z pliku UTF-8.
dotnet run --project SoftwareFactory -- --prepare-project --goal-file ./cel.md --workspace ./workspaces/moj-projekt

# Przygotowanie istniejącego scenariusza sklepu pod nowy proces.
dotnet run --project SoftwareFactory -- --prepare-project --scenario ai-agent-store --workspace ./workspaces/sklep-proces-01
```

Przygotowanie zapisuje cel, wersjonowany snapshot procesu, stan `Prepared`, zdarzenie
przygotowania i pusty backlog. Nie wykonuje agentów, Git, PR ani wdrożeń. Katalog
musi być pusty lub nie istnieć. `--dry-run` pozwala obejrzeć cel bez zapisywania plików.

**Pełny executor grafu i integracje serwerowe nie są jeszcze zaimplementowane.**
Dotychczasowe komendy bez `--prepare-project` uruchamiają jedynie wcześniejsze demo
Architect → Developer → opcjonalnie QA. Nie wznawiają przygotowanego procesu 0.1.
Limity pętli, checkpointy i approval są określone w kontrakcie; egzekwowanie w czasie
wykonania będzie zadaniem przyszłego executora. Nie ma automatycznego deploymentu
produkcyjnego.

## Wymagania i uruchomienie

- .NET SDK 11.
- Node.js 22.19 lub nowszy.
- [Pi CLI](https://pi.dev) dostępne jako `pi` w `PATH`.
- Dostęp do wybranego modelu: zaloguj się przez `pi` → `/login` lub ustaw zmienną
  środowiskową providera, np. `OPENAI_API_KEY`. Nie zapisuj kluczy w kodzie.

Komendy wykonuj w katalogu zawierającym `SoftwareFactory.slnx`:

```sh
dotnet build SoftwareFactory.slnx -warnaserror
dotnet test SoftwareFactory.slnx

# Sprawdzenie instalacji: start Pi + get_state + zamknięcie, bez promptu do modelu.
dotnet run --project SoftwareFactory -- --check-harness

# Pełny przepływ Architect → Developer; może zużywać płatne tokeny.
dotnet run --project SoftwareFactory
```

Bez `--scenario` i `--prepare-project` cel i katalog runnera demo są ustawione w `SoftwareFactory/Program.cs`.
Opcja `--workspace` pozwala zmienić katalog wynikowy. `--help` pokazuje dostępne tryby.
Architect zapisuje `ARCHITECTURE.md`, a Developer korzysta z niego przy implementacji.
Agenci wymieniają wyniki przez wspólny workspace; bezpośrednia komunikacja między
modelami przez `AgentMessageBus` nie jest jeszcze podłączona.
`Ctrl+C` anuluje pracę i zamyka procesy.

## Runner demonstracyjny: sklep z agentami AI

Gotowy scenariusz **`ai-agent-store`** zleca budowę aplikacji **AgentHub**:

- polski, responsywny interfejs sklepu, katalog, wyszukiwarka i filtry;
- sześciu agentów, w tym **Support Bot**, **Email Writer** i **Meeting Analyst**;
- koszyk, checkout bez prawdziwych płatności, zamówienia i demonstracyjne licencje;
- trzy działające dema: odpowiedź bota, pisanie maila i analiza tekstu spotkania;
- ASP.NET Core Razor Pages, .NET 11, SQLite, dane startowe i testy xUnit;
- symulowane odpowiedzi AI — wygenerowany sklep nie wymaga kluczy LLM.

Specyfikacja znajduje się w
`SoftwareFactory.Core/Scenarios/ai-agent-store.md` i jest osadzona w aplikacji.
Możesz zmienić ją przed kolejną kompilacją factory.

### 1. Obejrzyj zlecenie — bez kosztów modeli

```sh
dotnet run --project SoftwareFactory -- --scenario ai-agent-store --dry-run
```

Ten tryb nie uruchamia Pi i nie tworzy workspace.

### 2. Zleć agentom zbudowanie sklepu

```sh
dotnet run --project SoftwareFactory -- --scenario ai-agent-store
```

Na Windows możesz zamiast tego uruchomić:

```bat
run-ai-agent-store.cmd --dry-run
run-ai-agent-store.cmd
```

**Pełne uruchomienie wywołuje modele przez Pi i może zużywać płatne tokeny.**
Przygotowanie scenariusza lub dry-run nie oznacza, że sklep został już wygenerowany.

Wyniki trafią do `workspaces/ai-agent-store` względem bieżącego katalogu; skrypt CMD
ustawia katalog roboczy na katalog repozytorium. Workspace musi być pusty lub nie
istnieć. Kolejne uruchomienie wykonaj w nowym katalogu, np.:

```sh
dotnet run --project SoftwareFactory -- --scenario ai-agent-store --workspace ./workspaces/ai-agent-store-02
```

Factory zapisze `BRIEF.md`, a potem uruchomi kolejno **Architect → Developer → QA**.
QA dostaje zadanie rzeczywistego wykonania restore/build/test, smoke testu HTTP,
naprawienia wykrytych błędów i zapisania `QA_REPORT.md`. Zakończenie procesu agenta
nie jest samo w sobie dowodem przejścia testów — przeczytaj raport i sprawdź wyniki.

### 3. Sprawdź i uruchom wygenerowany sklep

Po zakończeniu agentów, jeżeli raport nie wskazuje blokad i pliki zostały utworzone:

```sh
cd workspaces/ai-agent-store
dotnet restore AiAgentStore.slnx
dotnet build AiAgentStore.slnx --no-restore
dotnet test AiAgentStore.slnx --no-build
dotnet run --project src/AiAgentStore.Web --urls http://localhost:5080
```

Otwórz **http://localhost:5080**. Ręczny test: wyszukaj Email Writer, dodaj do
koszyka, dodaj Meeting Analyst, przejdź przez demonstracyjny zakup, sprawdź
potwierdzenie i licencje. Następnie przetestuj trzy formularze demo na stronach
produktów. Nikt nie jest rzeczywiście obciążany, a e-maile nie są wysyłane.

Oczekiwane pliki: `BRIEF.md`, `ARCHITECTURE.md`, `AiAgentStore.slnx`,
`src/AiAgentStore.Web/`, `tests/AiAgentStore.Tests/`, `README.md`, `QA_REPORT.md`.
Katalog `workspaces/` jest ignorowany przez Git, aby wygenerowana aplikacja nie
mieszała się z kodem factory.

## AgentBase

```csharp
using SoftwareFactory.Core.Agents;

await using var agent = AgentFactory.CreateAgent(
    AgentRole.Developer,
    "./workspace",
    new PiHarnessOptions { PersistSessions = true },
    new PiModelConfig(
        Model: "gpt-6.1-sol",
        ThinkingLevel: "medium",
        Provider: "openai"));

await agent.StartAgent("Przeczytaj ARCHITECTURE.md i zaimplementuj API.");
Console.WriteLine(agent.LastResponse);

// Ten sam proces, sesja i kontekst rozmowy.
await agent.Message("Uruchom testy i popraw znalezione błędy.");

// Możliwa również zmiana modelu przed startem lub w działającej sesji.
await agent.SetModel(new PiModelConfig("gpt-6.1-sol", "high", "openai"));
```

Nazwy providerów i modeli są przekazywane do Pi bez lokalnej whitelisty — można
korzystać również z własnych modeli skonfigurowanych w Pi. Domyślna konfiguracja
zachowuje dotychczasowy model `openai/gpt-6.1-sol`; model musi być dostępny w Twojej
instalacji Pi. Poziom myślenia jest dobierany do roli, chyba że podasz `modelConfig`.

- `StartHarnessAsync()` uruchamia sam harness i sprawdza gotowość przez `get_state`.
  Następnie użyj `Message()`, aby wysłać pierwszy prompt.
- `StartAgent()` uruchamia harness i wykonuje pierwszy prompt.
- `Message()` czeka na zakończenie promptu w tej samej sesji. Równoległe wywołania są
  wykonywane kolejno, nie jako steering.
- `SessionId`, `SessionFile` i `LastResponse` udostępniają wyniki działania.
- `SetModel()` używa poleceń `set_model` i `set_thinking_level` w działającej sesji.
- Metody asynchroniczne przyjmują `CancellationToken`. Anulowanie/błąd zamyka harness;
  można następnie uruchomić go ponownie z nową sesją.
- `StopAgentAsync()` i `DisposeAsync()` wysyłają `abort`, zamykają stdin i czekają na
  wyjście. Po przekroczeniu limitu zabijane jest całe drzewo procesu.
- `StartAgent()` i `Message()` czekają na `agent_settled`, nie tylko na akceptację
  promptu czy `agent_end`, dzięki czemu nie przerywają automatycznych retry.

Sesje domyślnie zapisuje Pi w swoim katalogu użytkownika. Nie wznawiamy automatycznie
poprzedniej sesji po restarcie procesu. `PersistSessions = false` dodaje `--no-session`.
Domyślne timeouty: polecenie RPC 30 s, prompt 30 min, zamknięcie 5 s.

## Windows i niestandardowa instalacja

Na Windows obsługiwany jest instalator pi.dev (`pi-launcher.js`) i standardowy shim
npm (`node_modules/.../dist/cli.js`). JavaScript uruchamiany jest bezpośrednio przez
Node.js, bez `cmd /c`, aby instrukcje agenta nie stały się składnią powłoki.

Dla innego sposobu instalacji podaj ścieżkę do entry pointu Pi:

```csharp
var options = new PiHarnessOptions
{
    CliPath = @"C:\tools\pi\dist\cli.js",
    NodeExecutablePath = "node",
    RequestTimeout = TimeSpan.FromSeconds(60),
    RunTimeout = TimeSpan.FromMinutes(45)
};

var factory = new SoftwareFactory.Core.SoftwareSystem(harnessOptions: options);
await factory.Start("./workspace", "Zbuduj sklep internetowy.");
```

Można też ustawić `ExecutablePath` wskazujący `pi`/`pi.exe`/`pi.cmd`.

## Uprawnienia i interakcje

**Pi nie ma domyślnych pytań o zgodę na każde narzędzie ani sandboxa.** Narzędzia
mogą czytać i modyfikować pliki oraz wykonywać polecenia z uprawnieniami użytkownika.
Workspace nie ogranicza dostępu do innych katalogów. Do pracy z niezaufanym kodem
użyj oddzielnego konta, kontenera lub maszyny wirtualnej.

Domyślnie przekazujemy `--no-approve`: konfiguracja lokalna projektu nie jest
zaufana. `TrustProjectConfiguration = true` dodaje `--approve` i pozwala ładować
lokalne rozszerzenia/ustawienia — włączaj to tylko dla sprawdzonych projektów.
Zasoby globalne użytkownika nadal są ładowane zgodnie z konfiguracją Pi.

Tryb factory jest bezobsługowy: dialogi rozszerzeń (`confirm`, `select`, `input`,
`editor`) dostają odpowiedź `cancelled: true`, nigdy automatyczną zgodę. Nie jest to
mechanizm zatwierdzania zwykłych wywołań narzędzi Pi.

Testy protokołu używają lokalnego procesu `FakePi.mjs` i wymagają Node.js, ale nie
wykonują połączeń sieciowych, nie potrzebują kluczy i nie zużywają tokenów.
