using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Collections;
using Avalonia.Controls.DataGridHierarchical;
using ReactiveUI;
using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.OutputExtension;

public sealed class DiagnosticsChannelViewModel
{
    public DiagnosticsChannelViewModel(string id, string displayName)
    {
        Id = id;
        DisplayName = displayName;
    }

    public string Id { get; }
    public string DisplayName { get; }
}

public sealed class DiagnosticEntryViewModel
{
    public DiagnosticEntryViewModel(LanguageDiagnostic diagnostic)
    {
        Severity = diagnostic.Severity;
        Message = diagnostic.Message;
        FilePath = diagnostic.FilePath;
        Line = diagnostic.Range.Start.Line;
        Column = diagnostic.Range.Start.Column;
        Code = diagnostic.Code;
        Source = diagnostic.Source;
        Range = diagnostic.Range;
    }

    public DiagnosticSeverity Severity { get; }
    public string Message { get; }
    public string FilePath { get; }
    public int Line { get; }
    public int Column { get; }
    public string? Code { get; }
    public string? Source { get; }
    public LanguageTextRange Range { get; }
}

public sealed class ProblemsGroupViewModel : ReactiveObject
{
    private bool _isExpanded = true;

    public ProblemsGroupViewModel(string key, string title)
    {
        Key = key;
        Title = title;
    }

    public string Key { get; }
    public string Title { get; }
    public ObservableCollection<DiagnosticEntryViewModel> Items { get; } = new();

    public int Count => Items.Count;

    public bool IsExpanded
    {
        get => _isExpanded;
        set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }
}

public sealed class ProblemsPanelViewModel : ReactiveObject, IDisposable
{
    private const string AllChannelsId = "all";
    private readonly IDiagnosticsService _diagnostics;
    private readonly IEditorServices _editor;
    private readonly CompositeDisposable _disposables = new();
    private readonly CompositeDisposable _groupDisposables = new();
    private readonly ObservableCollection<DiagnosticsChannelViewModel> _channels = new();
    private readonly ObservableCollection<DiagnosticEntryViewModel> _entries = new();
    private readonly ObservableCollection<object> _rootItems = new();
    private readonly Dictionary<string, IReadOnlyList<LanguageDiagnostic>> _diagnosticsByChannel = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _expandedGroups = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<DiagnosticsDocumentSnapshot> _latestSnapshots = Array.Empty<DiagnosticsDocumentSnapshot>();
    private DiagnosticsChannelViewModel? _selectedChannel;
    private DiagnosticEntryViewModel? _selectedDiagnostic;
    private HierarchicalNode? _selectedRow;
    private object? _selectedItem;
    private string? _searchText;
    private string _selectedSeverity = "All";
    private string _selectedGroupBy = "File";
    private CancellationTokenSource? _loadCts;

    public ProblemsPanelViewModel(IDiagnosticsService diagnostics, IEditorServices editor)
    {
        _diagnostics = diagnostics;
        _editor = editor;

        ChannelsView = new DataGridCollectionView(_channels);

        var options = new HierarchicalOptions
        {
            ChildrenSelector = item => item is ProblemsGroupViewModel group ? group.Items : null,
            IsLeafSelector = item => item is DiagnosticEntryViewModel,
            IsExpandedSelector = item => item is ProblemsGroupViewModel group && group.IsExpanded,
            IsExpandedSetter = (item, value) =>
            {
                if (item is ProblemsGroupViewModel group)
                {
                    group.IsExpanded = value;
                }
            },
            VirtualizeChildren = true
        };

        Model = new HierarchicalModel(options);
        Model.SetRoots(_rootItems);

        _disposables.Add(this.WhenAnyValue(x => x.SearchText)
            .Subscribe(_ => RebuildGroups()));
        _disposables.Add(this.WhenAnyValue(x => x.SelectedSeverity)
            .Skip(1)
            .Subscribe(_ => RebuildGroups()));
        _disposables.Add(this.WhenAnyValue(x => x.SelectedGroupBy)
            .Skip(1)
            .Subscribe(_ => RebuildGroups()));

        _disposables.Add(this.WhenAnyValue(x => x.SelectedChannel)
            .Subscribe(channel =>
            {
                if (channel is null)
                {
                    ClearDiagnostics();
                    return;
                }

                _ = LoadChannelAsync(channel);
            }));

        _disposables.Add(this.WhenAnyValue(x => x.SelectedRow)
            .Subscribe(row =>
            {
                SelectedItem = row?.Item;
                SelectedDiagnostic = row?.Item as DiagnosticEntryViewModel;
            }));

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);

        IObservable<bool> hasSelection = this.WhenAnyValue(x => x.SelectedDiagnostic)
            .Select(entry => entry is not null);
        OpenLocationCommand = ReactiveCommand.CreateFromTask(OpenSelectedAsync, hasSelection);

        ClearCommand = ReactiveCommand.Create(ClearDiagnostics);
        CopySelectedCommand = ReactiveCommand.CreateFromTask(CopySelectedAsync, hasSelection);

        IObservable<bool> hasEntries = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                h => _entries.CollectionChanged += h,
                h => _entries.CollectionChanged -= h)
            .Select(_ => _entries.Count > 0)
            .StartWith(_entries.Count > 0);
        CopyAllCommand = ReactiveCommand.CreateFromTask(CopyAllAsync, hasEntries);

        ClearFiltersCommand = ReactiveCommand.Create(() =>
        {
            SearchText = null;
            SelectedSeverity = "All";
            SelectedGroupBy = "File";
        });
    }

    public DataGridCollectionView ChannelsView { get; }

    public HierarchicalModel Model { get; }

    public Interaction<string, Unit> CopyToClipboardInteraction { get; } = new();

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public ReactiveCommand<Unit, Unit> OpenLocationCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearCommand { get; }

    public ReactiveCommand<Unit, Unit> CopySelectedCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyAllCommand { get; }

    public ReactiveCommand<Unit, Unit> ClearFiltersCommand { get; }

    public IReadOnlyList<string> SeverityOptions { get; } = new[] { "All", "Error", "Warning", "Info" };

    public IReadOnlyList<string> GroupByOptions { get; } = new[] { "None", "File", "Channel" };

    public DiagnosticsChannelViewModel? SelectedChannel
    {
        get => _selectedChannel;
        set => this.RaiseAndSetIfChanged(ref _selectedChannel, value);
    }

    public HierarchicalNode? SelectedRow
    {
        get => _selectedRow;
        set => this.RaiseAndSetIfChanged(ref _selectedRow, value);
    }

    public object? SelectedItem
    {
        get => _selectedItem;
        private set => this.RaiseAndSetIfChanged(ref _selectedItem, value);
    }

    public DiagnosticEntryViewModel? SelectedDiagnostic
    {
        get => _selectedDiagnostic;
        private set => this.RaiseAndSetIfChanged(ref _selectedDiagnostic, value);
    }

    public string SelectedSeverity
    {
        get => _selectedSeverity;
        set => this.RaiseAndSetIfChanged(ref _selectedSeverity, value);
    }

    public string SelectedGroupBy
    {
        get => _selectedGroupBy;
        set => this.RaiseAndSetIfChanged(ref _selectedGroupBy, value);
    }

    public string? SearchText
    {
        get => _searchText;
        set => this.RaiseAndSetIfChanged(ref _searchText, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await RefreshChannelsAsync(cancellationToken);
    }

    public void HandleChannelsChanged(IReadOnlyList<DiagnosticsChannelInfo> channels)
    {
        string? selectedId = SelectedChannel?.Id;
        _channels.Clear();

        _channels.Add(new DiagnosticsChannelViewModel(AllChannelsId, "All"));
        foreach (DiagnosticsChannelInfo channel in channels)
        {
            _channels.Add(new DiagnosticsChannelViewModel(channel.Id, channel.DisplayName));
        }

        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            SelectedChannel = FindChannel(selectedId) ?? _channels[0];
        }
        else if (_channels.Count > 0)
        {
            SelectedChannel = _channels[0];
        }
        else
        {
            SelectedChannel = null;
            ClearDiagnostics();
        }
    }

    public void HandleDiagnosticsPublished(string channelId, IReadOnlyList<LanguageDiagnostic> diagnostics)
    {
        _diagnosticsByChannel[channelId] = diagnostics;
        RebuildGroups();
    }

    public void HandleSnapshotsPublished(IReadOnlyList<DiagnosticsDocumentSnapshot> snapshots)
    {
        _latestSnapshots = snapshots;
        UpdateEntriesFromSnapshots();
    }

    public async Task<bool> NavigateToRelativeAsync(int delta, CancellationToken cancellationToken)
    {
        if (delta == 0)
        {
            return false;
        }

        List<DiagnosticEntryViewModel> filtered = GetFilteredEntries();
        if (filtered.Count == 0)
        {
            return false;
        }

        int direction = delta > 0 ? 1 : -1;
        int currentIndex = SelectedDiagnostic is null
            ? (direction > 0 ? -1 : 0)
            : filtered.IndexOf(SelectedDiagnostic);
        if (currentIndex < 0)
        {
            currentIndex = direction > 0 ? -1 : 0;
        }

        int nextIndex = currentIndex;
        int steps = Math.Abs(delta);
        for (int i = 0; i < steps; i++)
        {
            nextIndex = direction > 0
                ? (nextIndex + 1) % filtered.Count
                : (nextIndex - 1 + filtered.Count) % filtered.Count;
        }

        DiagnosticEntryViewModel next = filtered[nextIndex];
        SelectedDiagnostic = next;
        SelectedRow = Model.FindNode(next);

        LanguageLocation location = new()
        {
            FilePath = next.FilePath,
            Range = next.Range
        };

        await _editor.OpenLocationAsync(location, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _disposables.Dispose();
        _groupDisposables.Dispose();
    }

    private async Task RefreshAsync()
    {
        await RefreshChannelsAsync(CancellationToken.None);
        if (SelectedChannel is not null)
        {
            await LoadChannelAsync(SelectedChannel);
        }
    }

    private async Task RefreshChannelsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DiagnosticsChannelInfo> channels = await _diagnostics.GetChannelsAsync(cancellationToken);
        HandleChannelsChanged(channels);
    }

    private async Task LoadChannelAsync(DiagnosticsChannelViewModel channel)
    {
        CancellationTokenSource? previous = _loadCts;
        _loadCts = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();

        CancellationToken token = _loadCts.Token;
        string? channelId = string.Equals(channel.Id, AllChannelsId, StringComparison.OrdinalIgnoreCase)
            ? null
            : channel.Id;

        IReadOnlyList<DiagnosticsDocumentSnapshot> snapshots = await _diagnostics.GetDiagnosticsSnapshotAsync(
            new DiagnosticsQuery(null, channelId),
            token);
        _latestSnapshots = snapshots;
        UpdateEntriesFromSnapshots();
    }

    private void UpdateEntriesFromSnapshots()
    {
        _entries.Clear();
        foreach (DiagnosticsDocumentSnapshot snapshot in _latestSnapshots)
        {
            foreach (LanguageDiagnostic diagnostic in snapshot.Diagnostics)
            {
                _entries.Add(new DiagnosticEntryViewModel(diagnostic));
            }
        }

        RebuildGroups();
    }

    private void ClearDiagnostics()
    {
        _entries.Clear();
        _rootItems.Clear();
        SelectedDiagnostic = null;
        SelectedRow = null;
    }

    private void RebuildGroups()
    {
        string? selectedKey = SelectedDiagnostic is null ? null : BuildDiagnosticKey(SelectedDiagnostic);
        _groupDisposables.Clear();
        _rootItems.Clear();

        List<DiagnosticEntryViewModel> filtered = GetFilteredEntries();
        if (filtered.Count == 0)
        {
            SelectedDiagnostic = null;
            SelectedRow = null;
            return;
        }

        switch (SelectedGroupBy)
        {
            case "None":
                foreach (DiagnosticEntryViewModel entry in filtered)
                {
                    _rootItems.Add(entry);
                }
                break;
            case "Channel":
                BuildChannelGroups(filtered);
                break;
            default:
                BuildFileGroups(filtered);
                break;
        }

        Model.Refresh();

        if (!string.IsNullOrWhiteSpace(selectedKey))
        {
            DiagnosticEntryViewModel? found = FindEntryByKey(_rootItems, selectedKey);
            if (found is not null)
            {
                SelectedRow = Model.FindNode(found);
            }
        }
    }

    private void BuildFileGroups(List<DiagnosticEntryViewModel> filtered)
    {
        Dictionary<string, ProblemsGroupViewModel> groups = new(StringComparer.OrdinalIgnoreCase);
        foreach (DiagnosticEntryViewModel entry in filtered)
        {
            string key = $"file:{entry.FilePath}";
            if (!groups.TryGetValue(key, out ProblemsGroupViewModel? group))
            {
                string title = Path.GetFileName(entry.FilePath);
                group = new ProblemsGroupViewModel(key, title)
                {
                    IsExpanded = _expandedGroups.Contains(key)
                };
                TrackGroupExpansion(group);
                groups[key] = group;
                _rootItems.Add(group);
            }

            group.Items.Add(entry);
        }
    }

    private void BuildChannelGroups(List<DiagnosticEntryViewModel> filtered)
    {
        Dictionary<string, ProblemsGroupViewModel> groups = new(StringComparer.OrdinalIgnoreCase);
        foreach (DiagnosticEntryViewModel entry in filtered)
        {
            string channelId = GetChannelId(entry.Source);
            string key = $"channel:{channelId}";
            if (!groups.TryGetValue(key, out ProblemsGroupViewModel? group))
            {
                group = new ProblemsGroupViewModel(key, channelId)
                {
                    IsExpanded = _expandedGroups.Contains(key)
                };
                TrackGroupExpansion(group);
                groups[key] = group;
                _rootItems.Add(group);
            }

            group.Items.Add(entry);
        }
    }

    private void TrackGroupExpansion(ProblemsGroupViewModel group)
    {
        IDisposable subscription = group.WhenAnyValue(x => x.IsExpanded)
            .Subscribe(isExpanded => UpdateExpanded(group.Key, isExpanded));
        _groupDisposables.Add(subscription);
    }

    private void UpdateExpanded(string key, bool isExpanded)
    {
        if (isExpanded)
        {
            _expandedGroups.Add(key);
        }
        else
        {
            _expandedGroups.Remove(key);
        }
    }

    private List<DiagnosticEntryViewModel> GetFilteredEntries()
    {
        List<DiagnosticEntryViewModel> filtered = new();
        string? search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim();
        string? channelId = SelectedChannel?.Id;
        bool filterChannel = !string.IsNullOrWhiteSpace(channelId)
            && !string.Equals(channelId, AllChannelsId, StringComparison.OrdinalIgnoreCase);

        foreach (DiagnosticEntryViewModel entry in _entries)
        {
            if (filterChannel && !string.Equals(GetChannelId(entry.Source), channelId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!IsSeverityMatch(entry.Severity, SelectedSeverity))
            {
                continue;
            }

            if (search is not null && !MatchesSearch(entry, search))
            {
                continue;
            }

            filtered.Add(entry);
        }

        return filtered;
    }

    private async Task OpenSelectedAsync()
    {
        if (SelectedDiagnostic is null)
        {
            return;
        }

        LanguageLocation location = new()
        {
            FilePath = SelectedDiagnostic.FilePath,
            Range = SelectedDiagnostic.Range
        };

        await _editor.OpenLocationAsync(location, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task CopySelectedAsync()
    {
        if (SelectedDiagnostic is null)
        {
            return;
        }

        await CopyToClipboardInteraction.Handle(FormatEntry(SelectedDiagnostic)).ToTask().ConfigureAwait(false);
    }

    private async Task CopyAllAsync()
    {
        List<DiagnosticEntryViewModel> filtered = GetFilteredEntries();
        if (filtered.Count == 0)
        {
            return;
        }

        StringBuilder builder = new();
        foreach (DiagnosticEntryViewModel entry in filtered)
        {
            builder.AppendLine(FormatEntry(entry));
        }

        await CopyToClipboardInteraction.Handle(builder.ToString()).ToTask().ConfigureAwait(false);
    }

    private static bool IsSeverityMatch(DiagnosticSeverity severity, string filter)
    {
        return filter switch
        {
            "Error" => severity == DiagnosticSeverity.Error,
            "Warning" => severity == DiagnosticSeverity.Warning,
            "Info" => severity == DiagnosticSeverity.Info,
            _ => true
        };
    }

    private static bool MatchesSearch(DiagnosticEntryViewModel entry, string search)
    {
        return entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.FilePath.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (entry.Code?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
            || (entry.Source?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static string GetChannelId(string? source)
    {
        return string.IsNullOrWhiteSpace(source) ? "default" : source;
    }

    private static string BuildDiagnosticKey(DiagnosticEntryViewModel entry)
    {
        return $"{entry.FilePath}:{entry.Line}:{entry.Column}:{entry.Message}";
    }

    private static DiagnosticEntryViewModel? FindEntryByKey(IEnumerable<object> roots, string key)
    {
        foreach (object root in roots)
        {
            if (root is DiagnosticEntryViewModel entry && BuildDiagnosticKey(entry) == key)
            {
                return entry;
            }

            if (root is ProblemsGroupViewModel group)
            {
                foreach (DiagnosticEntryViewModel child in group.Items)
                {
                    if (BuildDiagnosticKey(child) == key)
                    {
                        return child;
                    }
                }
            }
        }

        return null;
    }

    private static string FormatEntry(DiagnosticEntryViewModel entry)
    {
        return $"[{entry.Severity}] {entry.Message} ({entry.FilePath}:{entry.Line}:{entry.Column})";
    }

    private DiagnosticsChannelViewModel? FindChannel(string id)
    {
        foreach (DiagnosticsChannelViewModel channel in _channels)
        {
            if (string.Equals(channel.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return channel;
            }
        }

        return null;
    }
}
