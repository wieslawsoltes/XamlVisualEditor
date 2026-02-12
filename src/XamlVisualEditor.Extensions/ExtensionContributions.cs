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

public sealed record ExtensionMenuContribution(
    string CommandId,
    string Title,
    string? Location,
    string? Group,
    int Priority = 0);

public sealed record ExtensionToolbarContribution(
    string CommandId,
    string Title,
    string? Tooltip,
    string? Location,
    string? Group,
    int Priority = 0);

public sealed record ExtensionCommandPaletteContribution(string CommandId, string Title, string? Category);

public sealed record ExtensionViewContribution(
    string ViewId,
    string Title,
    ExtensionViewType Type,
    ExtensionViewLocation Location,
    int Priority);

/// <summary>Known menu locations for extensions.</summary>
public static class ExtensionMenuLocations
{
    public const string File = "menu.file";
    public const string FileNew = "menu.file.new";
    public const string Tools = "menu.tools";
    public const string ToolsWorkspace = "menu.tools.workspace";
    public const string Extensions = "menu.extensions";
}

/// <summary>Known toolbar locations for extensions.</summary>
public static class ExtensionToolbarLocations
{
    public const string Main = "toolbar.main";
    public const string Extensions = "toolbar.extensions";
}

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
