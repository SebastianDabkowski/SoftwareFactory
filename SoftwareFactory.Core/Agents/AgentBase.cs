using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SoftwareFactory.Core.Agents
{
    public class AgentBase
    {
        private Process _process = null!;
        private readonly string _workspace;

        public string AgentName { get; set; } = string.Empty;
        public string Instruction { get; set; } = string.Empty;
        public int Id { get; internal set; }
        public CodexModelConfig Config { get; internal set; } =
            new(CodexModels.Models.Gpt61Sol, CodexModels.Reasoning.Medium);

        public AgentBase(string projectPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);
            _workspace = Path.GetFullPath(projectPath);
        }

        public async Task StartAgent(string prompt)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
            if (Config is null || string.IsNullOrWhiteSpace(Config.model))
            {
                throw new InvalidOperationException("Agent configuration is not set.");
            }

            // The factory sets the agent configuration after construction.
            StartProcess(_workspace);
            try
            {
                await RunAgentAsync(prompt);
            }
            finally
            {
                StopAgent();
            }
        }

        private async Task RunAgentAsync(string prompt)
        {
            // 1. Initialize
            var clientInfo = new
            {
                name = "software_factory",
                title = "Software Factory",
                version = "1.0.0"
            };

            await RequestAsync(0, "initialize", new { clientInfo });

            // 2. Initialization notification
            await SendAsync(new { method = "initialized", @params = new { } });

            // 3. Create thread
            var parameters = new { model = Config.model, cwd = _workspace, developerInstructions = Instruction };
            var threadResponse = await RequestAsync(1, "thread/start", parameters);

            var threadId = threadResponse["result"]?["thread"]?["id"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(threadId))
            {
                throw new Exception("Missing thread ID.");
            }

            Console.WriteLine($"Thread: {threadId}");



            // 4. Start agent task
            string text = $"{Instruction}\n {prompt}";
            var input = new[] { new { type = "text", text = text } };
            var turnParameters = new { threadId, input, effort = Config.reasoningEffort };
            await RequestAsync(2, "turn/start", turnParameters);

            // 5. Stream events
            Console.WriteLine("Agent started.");

            while (true)
            {
                var message = await ReadMessageAsync();
                var method = message["method"]?.ToString();
                bool isExit = false;
                switch (method)
                {
                    case "item/commandExecution/requestApproval":
                        await HandleCommandApprovalAsync(message);
                        break;
                    case "item/fileChange/requestApproval":
                        await HandleFileApprovalAsync(message);
                        break;
                    case "item/tool/requestUserInput":
                        await HandleUserInputAsync(message);
                        break;

                    case "item/agentMessage/delta":
                        AgentMessageDelta(message);
                        break;
                    case "item/commandExecution/outputDelta":
                        AgentMessageDelta(message);
                        break;

                    case "item/completed":
                        Console.WriteLine("\n[Item completed]");
                        break;
                    case "turn/completed":
                        AgentTurnCompleted(message);
                        isExit = true;
                        break;
                    default:
                        break;
                }

                if (isExit)
                {
                    break;
                }
            }

            Console.WriteLine("Finished.");
        }

        public async Task Message(string prompt)
        {
            var input = new[] { new { type = "text", text = prompt } };
            var parameters = new { input };
            await RequestAsync(3, "turn/start", parameters);
        }

        public async Task SetModel(CodexModelConfig config)
        {
            if (!IsModelExisting(config.model))
            {
                throw new ArgumentException($"Model '{config.model}' does not exist.");
            }

            var input = new[] { new { model = config.model, effort = config.reasoningEffort, cwd = "/projects/crm/backend", developerInstructions = Instruction } };
            var parameters = new { input };
            await RequestAsync(9, "thread/start", parameters);
        }

        private bool IsModelExisting(string model)
        {
            if (model == CodexModels.Models.Gpt61Sol ||
                model == CodexModels.Models.Gpt6Sol ||
                model == CodexModels.Models.Gpt6Luna ||
                model == CodexModels.Models.Gpt6Astra ||
                model == CodexModels.Models.Gpt56 ||
                model == CodexModels.Models.Gpt56Terra ||
                model == CodexModels.Models.Gpt56Luna)
            {
                return true;
            }

            return false;
        }

        public void StopAgent()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit();
            }
        }

        private async Task HandleUserInputAsync(JsonObject message)
        {
            var requestId = message["id"]!.GetValue<int>();
            var questions = message["params"]?["questions"]?.AsArray();
            var answers = new Dictionary<string, object>();

            if (questions is null)
            {
                return;
            }

            foreach (var question in questions)
            {
                var questionId = question?["id"]?.ToString();
                var questionText = question?["question"]?.ToString();

                if (questionId is null)
                    continue;

                Console.WriteLine();
                Console.WriteLine(questionText);

                var options = question?["options"]?.AsArray();

                if (options is { Count: > 0 })
                {
                    foreach (var option in options)
                    {
                        Console.WriteLine($"  {option?["label"]}");
                    }
                }

                Console.Write("Odpowiedź: ");

                var answer = Console.ReadLine() ?? "";

                answers[questionId] = new
                {
                    answers = options is { Count: > 0 }
                        ? new[] { answer }
                        : new[] { $"user_note: {answer}" }
                };
            }

            await SendAsync(new
            {
                id = requestId,
                result = new { answers }
            });
        }

        private async Task HandleFileApprovalAsync(JsonObject message)
        {
            var requestId = message["id"]!.GetValue<int>();
            Console.WriteLine("Codex chce zmodyfikować pliki.");
            Console.Write("Zatwierdzić? (y/n): ");

            var answer = Console.ReadLine();

            await SendAsync(new
            {
                id = requestId,
                result = new
                {
                    decision = answer?.Trim()
                        .Equals("y", StringComparison.OrdinalIgnoreCase)
                            == true ? "accept" : "decline"
                }
            });
        }

        private async Task HandleCommandApprovalAsync(JsonObject message)
        {
            var requestId = message["id"]?.GetValue<int>() ?? throw new Exception("Missing request ID");
            var command = message["params"]?["command"]?.ToString() ?? "Unknown command";

            Console.WriteLine();
            Console.WriteLine($"Codex chce wykonać: {command}");
            Console.Write("Zatwierdzić? (y/n): ");

            var answer = Console.ReadLine();
            var decision = answer?.Trim().ToLowerInvariant()
                == "y" ? "accept" : "decline";

            await SendAsync(new
            {
                id = requestId,
                result = new { decision }
            });
        }

        private void AgentTurnCompleted(JsonObject message)
        {
            var status = message["params"]?["turn"]?["status"]?.ToString();
            Console.WriteLine($"\nTurn completed: {status}");

            if (status != "completed")
            {
                throw new Exception($"Codex turn failed: {status}");
            }
        }

        /* Agnet response handling methods */
        private void AgentMessageDelta(JsonObject message)
        {
            var delta = message["params"]?["delta"]?.ToString();
            Console.Write(delta);
        }

        private void StartProcess(string workspace)
        {
            CheckWorkspace(workspace);

            _process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "codex",
                    Arguments = "app-server",
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            _process.Start();

            _ = Task.Run(async () =>
            {
                while (await _process.StandardError.ReadLineAsync() is { } line)
                {
                    Console.Error.WriteLine($"[stderr] {line}");
                }
            });
        }

        private void CheckWorkspace(string workspace)
        {
            if (!Directory.Exists(workspace))
            {
                Directory.CreateDirectory(workspace);
            }
        }

        private async Task SendAsync(object message)
        {
            var json = JsonSerializer.Serialize(message);

            await _process.StandardInput.WriteLineAsync(json);
            await _process.StandardInput.FlushAsync();
        }

        private async Task<JsonObject> ReadMessageAsync()
        {
            var line = await _process.StandardOutput.ReadLineAsync();

            if (line is null)
            {
                throw new Exception("Codex connection closed.");
            }

            return JsonNode.Parse(line)!.AsObject();
        }

        // Wysyła żądanie i czeka na odpowiedź o określonym ID.
        // W tym uproszczonym przykładzie obsługujemy je sekwencyjnie.
        private async Task<JsonObject> RequestAsync(int id, string method, object parameters)
        {
            await SendAsync(new { id, method, @params = parameters });

            while (true)
            {
                var message = await ReadMessageAsync();

                // Zdarzenia mogą przychodzić także między odpowiedziami.
                if (message["id"]?.ToString() != id.ToString())
                {
                    continue;
                }

                if (message["error"] is not null)
                {
                    throw new Exception(message["error"]!.ToJsonString());
                }

                return message;
            }
        }
    }
}