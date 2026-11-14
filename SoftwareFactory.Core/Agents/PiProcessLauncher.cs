using System.Diagnostics;
using System.Text;

namespace SoftwareFactory.Core.Agents;

internal static class PiProcessLauncher
{
    internal static ProcessStartInfo Create(
        string workspace, string agentName, string instruction, PiModelConfig config, PiHarnessOptions options)
    {
        options.Validate();
        config.Validate();

        string executable = options.ExecutablePath;
        string? cliPath = options.CliPath is null ? null : Path.GetFullPath(options.CliPath);
        if (cliPath is null && OperatingSystem.IsWindows())
        {
            executable = ResolveWindowsExecutable(executable);
            if (!Path.GetExtension(executable).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                // CreateProcess cannot run npm/installer .cmd shims directly. Avoid cmd /c:
                // arbitrary instructions must never become shell syntax.
                string directory = Path.GetDirectoryName(executable)!;
                string[] entryPoints =
                [
                    Path.Combine(directory, "pi-launcher.js"),
                    Path.Combine(directory, "node_modules", "@earendil-works", "pi-coding-agent", "dist", "cli.js"),
                    Path.Combine(directory, "node_modules", "@badlogic", "pi-coding-agent", "dist", "cli.js")
                ];
                cliPath = entryPoints.FirstOrDefault(File.Exists)
                    ?? throw new FileNotFoundException(
                        "Cannot find the Pi JavaScript entry point next to its Windows shim. Set PiHarnessOptions.CliPath.");
            }
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = cliPath is null ? executable : options.NodeExecutablePath,
            WorkingDirectory = workspace,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (cliPath is not null)
            startInfo.ArgumentList.Add(cliPath);
        foreach (string argument in new[]
        {
            "--mode", "rpc", "--provider", config.Provider, "--model", config.Model,
            "--thinking", config.ThinkingLevel,
            options.TrustProjectConfiguration ? "--approve" : "--no-approve"
        })
            startInfo.ArgumentList.Add(argument);
        if (!options.PersistSessions)
            startInfo.ArgumentList.Add("--no-session");
        if (!string.IsNullOrWhiteSpace(agentName))
        {
            startInfo.ArgumentList.Add("--name");
            startInfo.ArgumentList.Add(agentName);
        }
        if (!string.IsNullOrWhiteSpace(instruction))
        {
            startInfo.ArgumentList.Add("--append-system-prompt");
            startInfo.ArgumentList.Add(instruction);
        }

        // Do not expose stale metadata from a parent Pi session to the child harness.
        foreach (string variable in new[] { "PI_SESSION_ID", "PI_SESSION_FILE", "PI_PROVIDER", "PI_MODEL", "PI_REASONING_LEVEL" })
            startInfo.Environment.Remove(variable);
        return startInfo;
    }

    private static string ResolveWindowsExecutable(string executable)
    {
        string[] extensions = Path.HasExtension(executable) ? [""] : [".exe", ".cmd", ".bat", ""];
        IEnumerable<string> directories = Path.IsPathRooted(executable) || executable.Contains(Path.DirectorySeparatorChar)
            || executable.Contains(Path.AltDirectorySeparatorChar)
            ? [Directory.GetCurrentDirectory()]
            : (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
        foreach (string directory in directories)
        {
            foreach (string extension in extensions)
            {
                string candidate = Path.GetFullPath(Path.Combine(directory.Trim('"'), executable + extension));
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        throw new FileNotFoundException("Pi CLI was not found. Install Pi from https://pi.dev or set PiHarnessOptions.CliPath.", executable);
    }
}
