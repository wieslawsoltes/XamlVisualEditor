using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Extensions;

public enum ExtensionViewType
{
    Tree,
    Webview,
    Custom
}

public enum ExtensionViewLocation
{
    Left,
    Right,
    Bottom
}

public sealed record ExtensionMenuContribution(string CommandId, string Title, string? Group);

public sealed record ExtensionToolbarContribution(string CommandId, string Title, string? Tooltip, string? Group);

public sealed record ExtensionCommandPaletteContribution(string CommandId, string Title, string? Category);

public sealed record ExtensionViewContribution(
    string ViewId,
    string Title,
    ExtensionViewType Type,
    ExtensionViewLocation Location,
    int Priority);

public interface IExtensionContributionRegistry
{
    event EventHandler? Changed;

    IReadOnlyList<ExtensionMenuContribution> MenuItems { get; }

    IReadOnlyList<ExtensionToolbarContribution> ToolbarItems { get; }

    IReadOnlyList<ExtensionCommandPaletteContribution> CommandPaletteItems { get; }

    IReadOnlyList<ExtensionViewContribution> ViewContributions { get; }

    IDisposable RegisterMenuItems(string extensionId, IReadOnlyList<ExtensionMenuContribution> items);

    IDisposable RegisterToolbarItems(string extensionId, IReadOnlyList<ExtensionToolbarContribution> items);

    IDisposable RegisterCommandPaletteItems(string extensionId, IReadOnlyList<ExtensionCommandPaletteContribution> items);

    IDisposable RegisterViews(string extensionId, IReadOnlyList<ExtensionViewContribution> views);
}
