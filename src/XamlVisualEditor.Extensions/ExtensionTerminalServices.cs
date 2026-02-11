namespace XamlVisualEditor.Extensions;

/// <summary>Provides terminal access for external tooling.</summary>
public interface ITerminalBridge
{
    /// <summary>Creates a terminal session.</summary>
    Task<TerminalInfo> CreateAsync(TerminalCreateRequest request, CancellationToken ct);

    /// <summary>Sends text to a terminal session.</summary>
    Task SendTextAsync(Guid terminalId, string text, CancellationToken ct);
}

/// <summary>Terminal creation request.</summary>
public sealed record TerminalCreateRequest(
    string? Title,
    string? WorkingDirectory,
    string? ShellPath,
    IReadOnlyList<string>? Arguments);

/// <summary>Terminal descriptor.</summary>
public sealed record TerminalInfo(Guid Id, string Title);
