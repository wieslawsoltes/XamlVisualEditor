using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.Json;
using ReactiveUI;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.ToolboxExtension;

public sealed class ToolboxPanelItem
{
    public ToolboxPanelItem(string displayName, string commandId, IReadOnlyList<string> commandArgs)
    {
        DisplayName = displayName;
        CommandId = commandId;
        CommandArguments = commandArgs;
    }

    public string DisplayName { get; }

    public string CommandId { get; }

    public IReadOnlyList<string> CommandArguments { get; }
}

public sealed class ToolboxCatalogEntry
{
    public string? DisplayName { get; set; }

    public string? TypeName { get; set; }

    public string? XmlNamespace { get; set; }

    public string? ParentNodeId { get; set; }

    public string? CommandId { get; set; }

    public List<string>? CommandArguments { get; set; }
}

public sealed class ToolboxPanelViewModel : ReactiveObject
{
    private readonly ICommands _commands;
    private readonly ISettings _settings;
    private ToolboxPanelItem? _selectedItem;
    private const string CatalogSettingsKey = "toolbox.catalog";
    private const string DefaultInsertCommandId = "toolbox.insertSelected";

    public ToolboxPanelViewModel(ICommands commands, ISettings settings)
    {
        _commands = commands;
        _settings = settings;

        LoadCatalog();

        IObservable<bool> canInsert = this.WhenAnyValue(x => x.SelectedItem).Select(item => item is not null);
        InsertSelectedCommand = ReactiveCommand.CreateFromTask(InsertSelectedAsync, canInsert);
    }

    public ObservableCollection<ToolboxPanelItem> Items { get; } = new();

    public ToolboxPanelItem? SelectedItem
    {
        get => _selectedItem;
        set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    public ReactiveCommand<Unit, Unit> InsertSelectedCommand { get; }

    private async Task InsertSelectedAsync()
    {
        if (SelectedItem is null)
        {
            return;
        }

        object?[] commandArgs = SelectedItem.CommandArguments.Cast<object?>().ToArray();
        await _commands.ExecuteAsync(SelectedItem.CommandId, commandArgs, CancellationToken.None);
    }

    private void LoadCatalog()
    {
        List<ToolboxPanelItem> items = TryLoadCatalogFromSettings();
        if (items.Count == 0)
        {
            items = GetDefaultItems();
        }

        Items.Clear();
        foreach (ToolboxPanelItem item in items)
        {
            Items.Add(item);
        }

        SelectedItem = Items.FirstOrDefault();
    }

    private List<ToolboxPanelItem> TryLoadCatalogFromSettings()
    {
        List<ToolboxCatalogEntry>? typedCatalog = _settings.Get<List<ToolboxCatalogEntry>>(CatalogSettingsKey);
        if (typedCatalog is not null)
        {
            return MapEntries(typedCatalog);
        }

        string? jsonCatalog = _settings.Get<string>(CatalogSettingsKey);
        if (string.IsNullOrWhiteSpace(jsonCatalog))
        {
            return new List<ToolboxPanelItem>();
        }

        try
        {
            List<ToolboxCatalogEntry>? parsed = JsonSerializer.Deserialize<List<ToolboxCatalogEntry>>(jsonCatalog);
            if (parsed is null)
            {
                return new List<ToolboxPanelItem>();
            }

            return MapEntries(parsed);
        }
        catch
        {
            return new List<ToolboxPanelItem>();
        }
    }

    private static List<ToolboxPanelItem> MapEntries(IEnumerable<ToolboxCatalogEntry> entries)
    {
        List<ToolboxPanelItem> result = new();
        foreach (ToolboxCatalogEntry entry in entries)
        {
            if (entry.CommandArguments is { Count: > 0 })
            {
                string commandId = string.IsNullOrWhiteSpace(entry.CommandId)
                    ? DefaultInsertCommandId
                    : entry.CommandId;
                string displayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                    ? commandId
                    : entry.DisplayName;

                result.Add(new ToolboxPanelItem(displayName, commandId, entry.CommandArguments));
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.TypeName) || string.IsNullOrWhiteSpace(entry.XmlNamespace))
            {
                continue;
            }

            List<string> args = new() { entry.TypeName, entry.XmlNamespace };
            if (!string.IsNullOrWhiteSpace(entry.ParentNodeId))
            {
                args.Add(entry.ParentNodeId);
            }

            string derivedCommandId = string.IsNullOrWhiteSpace(entry.CommandId)
                ? DefaultInsertCommandId
                : entry.CommandId;
            string derivedDisplayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.TypeName
                : entry.DisplayName;

            result.Add(new ToolboxPanelItem(derivedDisplayName, derivedCommandId, args));
        }

        return result;
    }

    private static List<ToolboxPanelItem> GetDefaultItems()
    {
        return new List<ToolboxPanelItem>
        {
            new("Grid", DefaultInsertCommandId, new[] { "Grid", "https://github.com/avaloniaui" }),
            new("DockPanel", DefaultInsertCommandId, new[] { "DockPanel", "https://github.com/avaloniaui" }),
            new("StackPanel", DefaultInsertCommandId, new[] { "StackPanel", "https://github.com/avaloniaui" }),
            new("WrapPanel", DefaultInsertCommandId, new[] { "WrapPanel", "https://github.com/avaloniaui" }),
            new("Canvas", DefaultInsertCommandId, new[] { "Canvas", "https://github.com/avaloniaui" }),
            new("Border", DefaultInsertCommandId, new[] { "Border", "https://github.com/avaloniaui" }),
            new("ScrollViewer", DefaultInsertCommandId, new[] { "ScrollViewer", "https://github.com/avaloniaui" }),
            new("Viewbox", DefaultInsertCommandId, new[] { "Viewbox", "https://github.com/avaloniaui" }),
            new("UserControl", DefaultInsertCommandId, new[] { "UserControl", "https://github.com/avaloniaui" }),
            new("ContentControl", DefaultInsertCommandId, new[] { "ContentControl", "https://github.com/avaloniaui" }),
            new("ItemsControl", DefaultInsertCommandId, new[] { "ItemsControl", "https://github.com/avaloniaui" }),
            new("TabControl", DefaultInsertCommandId, new[] { "TabControl", "https://github.com/avaloniaui" }),
            new("Expander", DefaultInsertCommandId, new[] { "Expander", "https://github.com/avaloniaui" }),
            new("GroupBox", DefaultInsertCommandId, new[] { "GroupBox", "https://github.com/avaloniaui" }),
            new("Button", DefaultInsertCommandId, new[] { "Button", "https://github.com/avaloniaui" }),
            new("ToggleButton", DefaultInsertCommandId, new[] { "ToggleButton", "https://github.com/avaloniaui" }),
            new("CheckBox", DefaultInsertCommandId, new[] { "CheckBox", "https://github.com/avaloniaui" }),
            new("RadioButton", DefaultInsertCommandId, new[] { "RadioButton", "https://github.com/avaloniaui" }),
            new("ToggleSwitch", DefaultInsertCommandId, new[] { "ToggleSwitch", "https://github.com/avaloniaui" }),
            new("TextBlock", DefaultInsertCommandId, new[] { "TextBlock", "https://github.com/avaloniaui" }),
            new("TextBox", DefaultInsertCommandId, new[] { "TextBox", "https://github.com/avaloniaui" }),
            new("ComboBox", DefaultInsertCommandId, new[] { "ComboBox", "https://github.com/avaloniaui" }),
            new("ListBox", DefaultInsertCommandId, new[] { "ListBox", "https://github.com/avaloniaui" }),
            new("ListView", DefaultInsertCommandId, new[] { "ListView", "https://github.com/avaloniaui" }),
            new("TreeView", DefaultInsertCommandId, new[] { "TreeView", "https://github.com/avaloniaui" }),
            new("DataGrid", DefaultInsertCommandId, new[] { "DataGrid", "https://github.com/avaloniaui" }),
            new("Slider", DefaultInsertCommandId, new[] { "Slider", "https://github.com/avaloniaui" }),
            new("ProgressBar", DefaultInsertCommandId, new[] { "ProgressBar", "https://github.com/avaloniaui" }),
            new("Image", DefaultInsertCommandId, new[] { "Image", "https://github.com/avaloniaui" }),
            new("Rectangle", DefaultInsertCommandId, new[] { "Rectangle", "https://github.com/avaloniaui" }),
            new("Ellipse", DefaultInsertCommandId, new[] { "Ellipse", "https://github.com/avaloniaui" }),
            new("Path", DefaultInsertCommandId, new[] { "Path", "https://github.com/avaloniaui" }),
            new("Menu", DefaultInsertCommandId, new[] { "Menu", "https://github.com/avaloniaui" }),
            new("MenuItem", DefaultInsertCommandId, new[] { "MenuItem", "https://github.com/avaloniaui" }),
            new("ToolBar", DefaultInsertCommandId, new[] { "ToolBar", "https://github.com/avaloniaui" }),
            new("StatusBar", DefaultInsertCommandId, new[] { "StatusBar", "https://github.com/avaloniaui" }),
            new("SplitView", DefaultInsertCommandId, new[] { "SplitView", "https://github.com/avaloniaui" }),
            new("GridSplitter", DefaultInsertCommandId, new[] { "GridSplitter", "https://github.com/avaloniaui" })
        };
    }
}
