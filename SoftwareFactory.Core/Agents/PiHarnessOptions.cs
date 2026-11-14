namespace SoftwareFactory.Core.Agents;

public sealed record PiHarnessOptions
{
    /// <summary>Pi executable on PATH, or an absolute path to pi/pi.exe/pi.cmd.</summary>
    public string ExecutablePath { get; init; } = "pi";

    /// <summary>Optional Pi JavaScript entry point, launched directly with Node.js.</summary>
    public string? CliPath { get; init; }
    public string NodeExecutablePath { get; init; } = "node";
    public bool PersistSessions { get; init; } = true;
    public bool TrustProjectConfiguration { get; init; }
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan RunTimeout { get; init; } = TimeSpan.FromMinutes(30);
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(5);

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ExecutablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(NodeExecutablePath);
        if (CliPath is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(CliPath);
        if (RequestTimeout <= TimeSpan.Zero || RunTimeout <= TimeSpan.Zero || ShutdownTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout), "Pi timeouts must be positive.");
    }
}
