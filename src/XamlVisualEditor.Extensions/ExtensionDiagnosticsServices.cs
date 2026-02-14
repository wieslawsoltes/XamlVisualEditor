using System;
using XamlVisualEditor.Core;

namespace XamlVisualEditor.Extensions;

/// <summary>Provides access to language diagnostics.</summary>
public interface IDiagnosticsService
{
    /// <summary>Gets diagnostics for a file or the active workspace.</summary>
    Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(string? filePath, CancellationToken ct);

    /// <summary>Gets diagnostics for a query.</summary>
    Task<IReadOnlyList<LanguageDiagnostic>> GetDiagnosticsAsync(DiagnosticsQuery query, CancellationToken ct);

    /// <summary>Gets diagnostics snapshots for a query.</summary>
    Task<IReadOnlyList<DiagnosticsDocumentSnapshot>> GetDiagnosticsSnapshotAsync(DiagnosticsQuery query, CancellationToken ct);

    /// <summary>Gets available diagnostic channels.</summary>
    Task<IReadOnlyList<DiagnosticsChannelInfo>> GetChannelsAsync(CancellationToken ct);

    /// <summary>Raised when diagnostic channels change.</summary>
    event EventHandler<DiagnosticsChannelsChangedEventArgs>? ChannelsChanged;

    /// <summary>Raised when diagnostics are published for a channel.</summary>
    event EventHandler<DiagnosticsChannelPublishedEventArgs>? DiagnosticsChannelPublished;

    /// <summary>Raised when diagnostics snapshots are published.</summary>
    event EventHandler<DiagnosticsSnapshotPublishedEventArgs>? DiagnosticsSnapshotPublished;

    /// <summary>Raised when diagnostics are published for a file or workspace.</summary>
    event EventHandler<DiagnosticsPublishedEventArgs>? DiagnosticsPublished;

    /// <summary>Raised when diagnostics change.</summary>
    event EventHandler<DiagnosticsChangedEventArgs>? DiagnosticsChanged;
}

/// <summary>Describes a diagnostics query.</summary>
public sealed record DiagnosticsQuery(string? FilePath, string? ChannelId);

/// <summary>Represents a diagnostics snapshot for a document.</summary>
public sealed record DiagnosticsDocumentSnapshot(string FilePath, IReadOnlyList<LanguageDiagnostic> Diagnostics);

/// <summary>Describes a diagnostics channel.</summary>
public sealed record DiagnosticsChannelInfo(string Id, string DisplayName);

/// <summary>Diagnostics channel change notification.</summary>
public sealed class DiagnosticsChannelsChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public DiagnosticsChannelsChangedEventArgs(IReadOnlyList<DiagnosticsChannelInfo> channels)
    {
        Channels = channels ?? Array.Empty<DiagnosticsChannelInfo>();
    }

    /// <summary>Gets the channel list snapshot.</summary>
    public IReadOnlyList<DiagnosticsChannelInfo> Channels { get; }
}

/// <summary>Diagnostics channel publication notification.</summary>
public sealed class DiagnosticsChannelPublishedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public DiagnosticsChannelPublishedEventArgs(
        string channelId,
        string? filePath,
        IReadOnlyList<LanguageDiagnostic> diagnostics)
    {
        ChannelId = channelId;
        FilePath = filePath;
        Diagnostics = diagnostics ?? Array.Empty<LanguageDiagnostic>();
    }

    /// <summary>Gets the channel identifier.</summary>
    public string ChannelId { get; }

    /// <summary>Gets the file path that changed (null for global).</summary>
    public string? FilePath { get; }

    /// <summary>Gets the diagnostics snapshot.</summary>
    public IReadOnlyList<LanguageDiagnostic> Diagnostics { get; }
}

/// <summary>Diagnostics snapshot publication notification.</summary>
public sealed class DiagnosticsSnapshotPublishedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public DiagnosticsSnapshotPublishedEventArgs(IReadOnlyList<DiagnosticsDocumentSnapshot> snapshots)
    {
        Snapshots = snapshots ?? Array.Empty<DiagnosticsDocumentSnapshot>();
    }

    /// <summary>Gets the diagnostics snapshots.</summary>
    public IReadOnlyList<DiagnosticsDocumentSnapshot> Snapshots { get; }
}

/// <summary>Diagnostic publication notification.</summary>
public sealed class DiagnosticsPublishedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public DiagnosticsPublishedEventArgs(string? filePath, IReadOnlyList<LanguageDiagnostic> diagnostics)
    {
        FilePath = filePath;
        Diagnostics = diagnostics ?? Array.Empty<LanguageDiagnostic>();
    }

    /// <summary>Gets the file path that changed (null for global).</summary>
    public string? FilePath { get; }

    /// <summary>Gets the diagnostics snapshot.</summary>
    public IReadOnlyList<LanguageDiagnostic> Diagnostics { get; }
}

/// <summary>Diagnostic change notification.</summary>
public sealed class DiagnosticsChangedEventArgs : EventArgs
{
    /// <summary>Creates event args.</summary>
    public DiagnosticsChangedEventArgs(string? filePath)
    {
        FilePath = filePath;
    }

    /// <summary>Gets the file path that changed (null for global).</summary>
    public string? FilePath { get; }
}
