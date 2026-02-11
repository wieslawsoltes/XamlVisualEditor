using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using XamlVisualEditor.Core;
using XamlVisualEditor.Core.Interfaces;

namespace XamlVisualEditor.Shell.ViewModels;

/// <summary>
/// ViewModel entry representing a git file change for the panel.
/// </summary>
public sealed class GitChangeEntryViewModel : ReactiveObject
{
    public GitChangeEntryViewModel(
        string path,
        string? oldPath,
        GitChangeKind changeKind,
        bool isStaged,
        bool isUntracked)
    {
        Path = path;
        OldPath = oldPath;
        ChangeKind = changeKind;
        IsStaged = isStaged;
        IsUntracked = isUntracked;
        DisplayPath = BuildDisplayPath(path, oldPath);
        StatusLabel = FormatChangeKind(changeKind, isStaged, isUntracked);
    }

    public string Path { get; }

    public string? OldPath { get; }

    public string DisplayPath { get; }

    public GitChangeKind ChangeKind { get; }

    public bool IsStaged { get; }

    public bool IsUntracked { get; }

    public string StatusLabel { get; }

    private static string BuildDisplayPath(string path, string? oldPath)
    {
        return string.IsNullOrWhiteSpace(oldPath)
            ? path
            : oldPath + " -> " + path;
    }

    private static string FormatChangeKind(GitChangeKind kind, bool isStaged, bool isUntracked)
    {
        if (isUntracked)
        {
            return "Untracked";
        }

        string scope = isStaged ? "Staged" : "Unstaged";
        return kind switch
        {
            GitChangeKind.Added => scope + " Added",
            GitChangeKind.Modified => scope + " Modified",
            GitChangeKind.Deleted => scope + " Deleted",
            GitChangeKind.Renamed => scope + " Renamed",
            GitChangeKind.Copied => scope + " Copied",
            GitChangeKind.TypeChanged => scope + " Type Changed",
            GitChangeKind.Unmerged => scope + " Unmerged",
            _ => scope + " Changed"
        };
    }
}

/// <summary>
/// ViewModel for the git panel UI.
/// </summary>
public enum GitDiffLineKind
{
    FileHeader,
    HunkHeader,
    Added,
    Removed,
    Context,
    NoNewline
}

/// <summary>
/// ViewModel entry representing a diff line for the preview.
/// </summary>
public sealed class GitDiffLineViewModel
{
    public GitDiffLineViewModel(
        GitDiffLineKind kind,
        string text,
        string marker,
        int? oldLine,
        int? newLine)
    {
        Kind = kind;
        Text = text;
        Marker = marker;
        OldLine = oldLine;
        NewLine = newLine;
    }

    public GitDiffLineKind Kind { get; }

    public string Text { get; }

    public string Marker { get; }

    public int? OldLine { get; }

    public int? NewLine { get; }
}

public sealed class GitPanelViewModel : ReactiveObject, IDisposable
{
    private readonly IGitService? _gitService;
    private readonly Func<string?>? _workspacePathProvider;
    private readonly CompositeDisposable _disposables = new();
    private readonly ObservableCollection<GitChangeEntryViewModel> _stagedItems = new();
    private readonly ObservableCollection<GitChangeEntryViewModel> _unstagedItems = new();
    private readonly DataGridCollectionView _stagedView;
    private readonly DataGridCollectionView _unstagedView;
    private FileSystemWatcher? _watcher;
    private readonly Subject<Unit> _refreshRequests = new();
    private readonly ObservableAsPropertyHelper<int> _stagedCount;
    private readonly ObservableAsPropertyHelper<int> _unstagedCount;
    private readonly ObservableAsPropertyHelper<int> _diffLineCount;

    [Reactive]
    public string? RepositoryRoot { get; private set; }

    [Reactive]
    public string BranchName { get; private set; } = string.Empty;

    [Reactive]
    public string? UpstreamName { get; private set; }

    [Reactive]
    public int AheadBy { get; private set; }

    [Reactive]
    public int BehindBy { get; private set; }

    [Reactive]
    public bool IsRepository { get; private set; }

    [Reactive]
    public string? ErrorMessage { get; private set; }

    [Reactive]
    public bool IsBusy { get; private set; }

    [Reactive]
    public string? SearchText { get; set; }

    [Reactive]
    public string? CommitMessage { get; set; }

    [Reactive]
    public string DiffText { get; private set; } = string.Empty;

    [Reactive]
    public GitChangeEntryViewModel? SelectedStagedChange { get; set; }

    [Reactive]
    public GitChangeEntryViewModel? SelectedUnstagedChange { get; set; }

    public IReadOnlyList<GitChangeEntryViewModel> StagedItems => _stagedItems;

    public IReadOnlyList<GitChangeEntryViewModel> UnstagedItems => _unstagedItems;

    public DataGridCollectionView StagedView => _stagedView;

    public DataGridCollectionView UnstagedView => _unstagedView;

    public ObservableCollection<DataGridColumnDefinition> ChangeColumns { get; }

    public ObservableCollection<GitDiffLineViewModel> DiffLines { get; } = new();

    public int StagedCount => _stagedCount.Value;

    public int UnstagedCount => _unstagedCount.Value;

    public int DiffLineCount => _diffLineCount.Value;

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public ReactiveCommand<Unit, Unit> StageCommand { get; }

    public ReactiveCommand<Unit, Unit> UnstageCommand { get; }

    public ReactiveCommand<Unit, Unit> DiscardCommand { get; }

    public ReactiveCommand<Unit, Unit> StageAllCommand { get; }

    public ReactiveCommand<Unit, Unit> UnstageAllCommand { get; }

    public ReactiveCommand<Unit, Unit> CommitCommand { get; }

    public GitPanelViewModel(
        IGitService? gitService = null,
        Func<string?>? workspacePathProvider = null)
    {
        _gitService = gitService;
        _workspacePathProvider = workspacePathProvider;

        _stagedView = new DataGridCollectionView(_stagedItems);
        _unstagedView = new DataGridCollectionView(_unstagedItems);
        ChangeColumns = BuildChangeColumns();

        EventHandler stagedCurrentChanged = (_, _) =>
        {
            SelectedStagedChange = _stagedView.CurrentItem as GitChangeEntryViewModel;
        };
        _stagedView.CurrentChanged += stagedCurrentChanged;
        _disposables.Add(Disposable.Create(() => _stagedView.CurrentChanged -= stagedCurrentChanged));

        EventHandler unstagedCurrentChanged = (_, _) =>
        {
            SelectedUnstagedChange = _unstagedView.CurrentItem as GitChangeEntryViewModel;
        };
        _unstagedView.CurrentChanged += unstagedCurrentChanged;
        _disposables.Add(Disposable.Create(() => _unstagedView.CurrentChanged -= unstagedCurrentChanged));

        IObservable<int> stagedCountChanged = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                handler => _stagedItems.CollectionChanged += handler,
                handler => _stagedItems.CollectionChanged -= handler)
            .Select(_ => _stagedItems.Count)
            .StartWith(_stagedItems.Count);
        _stagedCount = stagedCountChanged.ToProperty(this, x => x.StagedCount);
        _disposables.Add(_stagedCount);

        IObservable<int> unstagedCountChanged = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                handler => _unstagedItems.CollectionChanged += handler,
                handler => _unstagedItems.CollectionChanged -= handler)
            .Select(_ => _unstagedItems.Count)
            .StartWith(_unstagedItems.Count);
        _unstagedCount = unstagedCountChanged.ToProperty(this, x => x.UnstagedCount);
        _disposables.Add(_unstagedCount);

        IObservable<int> diffLineCountChanged = Observable.FromEventPattern<NotifyCollectionChangedEventHandler, NotifyCollectionChangedEventArgs>(
                handler => DiffLines.CollectionChanged += handler,
                handler => DiffLines.CollectionChanged -= handler)
            .Select(_ => DiffLines.Count)
            .StartWith(DiffLines.Count);
        _diffLineCount = diffLineCountChanged.ToProperty(this, x => x.DiffLineCount);
        _disposables.Add(_diffLineCount);

        RefreshCommand = ReactiveCommand.CreateFromTask(RefreshAsync);

        IObservable<bool> canStage = this.WhenAnyValue(x => x.SelectedUnstagedChange)
            .Select(change => change is not null);
        IObservable<bool> canUnstage = this.WhenAnyValue(x => x.SelectedStagedChange)
            .Select(change => change is not null);
        IObservable<bool> canDiscard = this.WhenAnyValue(x => x.SelectedUnstagedChange)
            .Select(change => change is not null);

        StageCommand = ReactiveCommand.CreateFromTask(StageSelectedAsync, canStage);
        UnstageCommand = ReactiveCommand.CreateFromTask(UnstageSelectedAsync, canUnstage);
        DiscardCommand = ReactiveCommand.CreateFromTask(DiscardSelectedAsync, canDiscard);

        StageAllCommand = ReactiveCommand.CreateFromTask(StageAllAsync, this.WhenAnyValue(x => x.UnstagedCount)
            .Select(count => count > 0));
        UnstageAllCommand = ReactiveCommand.CreateFromTask(UnstageAllAsync, this.WhenAnyValue(x => x.StagedCount)
            .Select(count => count > 0));

        IObservable<bool> canCommit = this.WhenAnyValue(x => x.CommitMessage, x => x.StagedCount, x => x.IsRepository,
                (message, count, isRepo) => isRepo && count > 0 && !string.IsNullOrWhiteSpace(message))
            .DistinctUntilChanged();
        CommitCommand = ReactiveCommand.CreateFromTask(CommitAsync, canCommit);

        IDisposable stagedSelection = this.WhenAnyValue(x => x.SelectedStagedChange)
            .Where(change => change is not null)
            .Subscribe(change =>
            {
                SelectedUnstagedChange = null;
                _ = LoadDiffAsync(change!);
            });
        _disposables.Add(stagedSelection);

        IDisposable unstagedSelection = this.WhenAnyValue(x => x.SelectedUnstagedChange)
            .Where(change => change is not null)
            .Subscribe(change =>
            {
                SelectedStagedChange = null;
                _ = LoadDiffAsync(change!);
            });
        _disposables.Add(unstagedSelection);

        IDisposable selectionClear = this.WhenAnyValue(x => x.SelectedStagedChange, x => x.SelectedUnstagedChange)
            .Where(selection => selection.Item1 is null && selection.Item2 is null)
            .Subscribe(_ =>
            {
                DiffText = string.Empty;
                DiffLines.Clear();
            });
        _disposables.Add(selectionClear);

        IDisposable searchSubscription = this.WhenAnyValue(x => x.SearchText)
            .Throttle(TimeSpan.FromMilliseconds(200))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => ApplyFilter());
        _disposables.Add(searchSubscription);

        IDisposable refreshSubscription = _refreshRequests
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxApp.MainThreadScheduler)
            .Subscribe(_ => RefreshCommand.Execute().Subscribe());
        _disposables.Add(refreshSubscription);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _watcher = null;
        _refreshRequests.Dispose();
        _disposables.Dispose();
    }

    public async System.Threading.Tasks.Task RefreshAsync()
    {
        if (_gitService is null)
        {
                Dispatcher.UIThread.Post(() =>
                {
                    ErrorMessage = "Git service unavailable";
                    IsRepository = false;
                    ClearChanges();
                });
            return;
        }

        string? workspacePath = _workspacePathProvider?.Invoke();
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
                Dispatcher.UIThread.Post(() =>
                {
                    RepositoryRoot = null;
                    ErrorMessage = "No workspace loaded";
                    IsRepository = false;
                    ClearChanges();
                });
            return;
        }

        IsBusy = true;
        try
        {
            string? repoRoot = await _gitService.GetRepositoryRootAsync(workspacePath);
            if (string.IsNullOrWhiteSpace(repoRoot))
            {
                    Dispatcher.UIThread.Post(() =>
                    {
                        RepositoryRoot = null;
                        ErrorMessage = "Workspace is not a git repository";
                        IsRepository = false;
                        ClearChanges();
                    });
                return;
            }

            GitRepositoryStatus status = await _gitService.GetStatusAsync(repoRoot);
                Dispatcher.UIThread.Post(() =>
                {
                    RepositoryRoot = repoRoot;
                    ApplyStatus(status);
                });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyStatus(GitRepositoryStatus status)
    {
        BranchName = status.BranchName;
        UpstreamName = status.UpstreamName;
        AheadBy = status.AheadBy;
        BehindBy = status.BehindBy;
        IsRepository = status.IsRepository;
        ErrorMessage = status.ErrorMessage;

        ReplaceItems(_stagedItems, status, isStaged: true);
        ReplaceItems(_unstagedItems, status, isStaged: false);
        ApplyFilter();
        ConfigureWatcher(status.RepositoryRoot);
    }

    private void ClearChanges()
    {
        _stagedItems.Clear();
        _unstagedItems.Clear();
        DiffLines.Clear();
        DiffText = string.Empty;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string filter = SearchText?.Trim() ?? string.Empty;
        StringComparison comparison = StringComparison.OrdinalIgnoreCase;

        bool FilterItem(object? item)
        {
            if (item is not GitChangeEntryViewModel entry)
            {
                return false;
            }

            return string.IsNullOrEmpty(filter)
                || entry.DisplayPath.Contains(filter, comparison)
                || entry.StatusLabel.Contains(filter, comparison);
        }

        _stagedView.Filter = FilterItem;
        _unstagedView.Filter = FilterItem;
        _stagedView.Refresh();
        _unstagedView.Refresh();
    }

    private async System.Threading.Tasks.Task LoadDiffAsync(GitChangeEntryViewModel change)
    {
        if (_gitService is null || string.IsNullOrWhiteSpace(RepositoryRoot))
        {
            DiffText = string.Empty;
            return;
        }

        GitDiffRequest request = new()
        {
            Staged = change.IsStaged,
            Path = change.Path,
            IsUntracked = change.IsUntracked
        };

        GitDiff diff = await _gitService.GetDiffAsync(RepositoryRoot, request);
        IReadOnlyList<GitDiffLineViewModel> lines = BuildDiffLines(diff);
        Dispatcher.UIThread.Post(() =>
        {
            DiffText = diff.RawText ?? string.Empty;
            DiffLines.Clear();
            foreach (GitDiffLineViewModel line in lines)
            {
                DiffLines.Add(line);
            }
        });
    }

    private async System.Threading.Tasks.Task StageSelectedAsync()
    {
        if (_gitService is null || string.IsNullOrWhiteSpace(RepositoryRoot))
        {
            return;
        }

        GitChangeEntryViewModel? selected = SelectedUnstagedChange;
        if (selected is null)
        {
            return;
        }

        await _gitService.StageAsync(RepositoryRoot, new[] { selected.Path });
        await RefreshAsync();
    }

    private async System.Threading.Tasks.Task UnstageSelectedAsync()
    {
        if (_gitService is null || string.IsNullOrWhiteSpace(RepositoryRoot))
        {
            return;
        }

        GitChangeEntryViewModel? selected = SelectedStagedChange;
        if (selected is null)
        {
            return;
        }

        await _gitService.UnstageAsync(RepositoryRoot, new[] { selected.Path });
        await RefreshAsync();
    }

    private async System.Threading.Tasks.Task DiscardSelectedAsync()
    {
        if (_gitService is null || string.IsNullOrWhiteSpace(RepositoryRoot))
        {
            return;
        }

        GitChangeEntryViewModel? selected = SelectedUnstagedChange;
        if (selected is null)
        {
            return;
        }

        if (selected.IsUntracked)
        {
            await _gitService.RemoveUntrackedAsync(RepositoryRoot, new[] { selected.Path });
        }
        else
        {
            await _gitService.DiscardAsync(RepositoryRoot, new[] { selected.Path });
        }

        await RefreshAsync();
    }

    private async System.Threading.Tasks.Task StageAllAsync()
    {
        if (_gitService is null || string.IsNullOrWhiteSpace(RepositoryRoot))
        {
            return;
        }

        List<string> paths = new(_unstagedItems.Count);
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (GitChangeEntryViewModel entry in _unstagedItems)
        {
            if (unique.Add(entry.Path))
            {
                paths.Add(entry.Path);
            }
        }

        await _gitService.StageAsync(RepositoryRoot, paths);
        await RefreshAsync();
    }

    private async System.Threading.Tasks.Task UnstageAllAsync()
    {
        if (_gitService is null || string.IsNullOrWhiteSpace(RepositoryRoot))
        {
            return;
        }

        List<string> paths = new(_stagedItems.Count);
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (GitChangeEntryViewModel entry in _stagedItems)
        {
            if (unique.Add(entry.Path))
            {
                paths.Add(entry.Path);
            }
        }

        await _gitService.UnstageAsync(RepositoryRoot, paths);
        await RefreshAsync();
    }

    private async System.Threading.Tasks.Task CommitAsync()
    {
        if (_gitService is null || string.IsNullOrWhiteSpace(RepositoryRoot))
        {
            return;
        }

        string? message = CommitMessage;
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        await _gitService.CommitAsync(RepositoryRoot, message);
        CommitMessage = string.Empty;
        await RefreshAsync();
    }

    private static ObservableCollection<DataGridColumnDefinition> BuildChangeColumns()
    {
        return new ObservableCollection<DataGridColumnDefinition>
        {
            new DataGridTextColumnDefinition
            {
                Header = "Path",
                Binding = DataGridBindingDefinition.Create<GitChangeEntryViewModel, string>(
                    item => item.DisplayPath,
                    (_, _) => { }),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star),
                SortMemberPath = nameof(GitChangeEntryViewModel.DisplayPath)
            },
            new DataGridTextColumnDefinition
            {
                Header = "Status",
                Binding = DataGridBindingDefinition.Create<GitChangeEntryViewModel, string>(
                    item => item.StatusLabel,
                    (_, _) => { }),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                SortMemberPath = nameof(GitChangeEntryViewModel.StatusLabel)
            }
        };
    }

    private static void ReplaceItems(
        ObservableCollection<GitChangeEntryViewModel> target,
        GitRepositoryStatus status,
        bool isStaged)
    {
        target.Clear();

        foreach (GitFileChange change in status.Changes)
        {
            if (change.IsIgnored)
            {
                continue;
            }

            if (!isStaged && (change.IsUntracked || change.WorkTreeStatus == GitChangeKind.Untracked))
            {
                target.Add(new GitChangeEntryViewModel(
                    change.Path,
                    change.OldPath,
                    GitChangeKind.Untracked,
                    isStaged: false,
                    isUntracked: true));
                continue;
            }

            if (isStaged)
            {
                if (change.IndexStatus != GitChangeKind.None
                    && change.IndexStatus != GitChangeKind.Ignored
                    && change.IndexStatus != GitChangeKind.Untracked)
                {
                    target.Add(new GitChangeEntryViewModel(
                        change.Path,
                        change.OldPath,
                        change.IndexStatus,
                        isStaged: true,
                        isUntracked: false));
                }
            }
            else
            {
                if (change.WorkTreeStatus != GitChangeKind.None
                    && change.WorkTreeStatus != GitChangeKind.Ignored
                    && change.WorkTreeStatus != GitChangeKind.Untracked)
                {
                    target.Add(new GitChangeEntryViewModel(
                        change.Path,
                        change.OldPath,
                        change.WorkTreeStatus,
                        isStaged: false,
                        isUntracked: false));
                }
            }
        }
    }

    private static IReadOnlyList<GitDiffLineViewModel> BuildDiffLines(GitDiff diff)
    {
        List<GitDiffLineViewModel> lines = new();

        if (diff.Files.Count == 0 && !string.IsNullOrWhiteSpace(diff.RawText))
        {
            string normalized = diff.RawText.Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace("\r", "\n", StringComparison.Ordinal);
            string[] rawLines = normalized.Split('\n');
            for (int i = 0; i < rawLines.Length; i++)
            {
                if (rawLines[i].Length == 0 && i == rawLines.Length - 1)
                {
                    break;
                }

                lines.Add(new GitDiffLineViewModel(
                    GitDiffLineKind.Context,
                    rawLines[i],
                    string.Empty,
                    null,
                    null));
            }

            return lines;
        }

        foreach (GitDiffFile file in diff.Files)
        {
            foreach (string header in file.HeaderLines)
            {
                lines.Add(new GitDiffLineViewModel(
                    GitDiffLineKind.FileHeader,
                    header,
                    string.Empty,
                    null,
                    null));
            }

            foreach (GitDiffHunk hunk in file.Hunks)
            {
                lines.Add(new GitDiffLineViewModel(
                    GitDiffLineKind.HunkHeader,
                    hunk.Header,
                    "@@",
                    null,
                    null));

                foreach (GitDiffLine line in hunk.Lines)
                {
                    GitDiffLineKind kind = line.Kind switch
                    {
                        XamlVisualEditor.Core.GitDiffLineKind.Added => GitDiffLineKind.Added,
                        XamlVisualEditor.Core.GitDiffLineKind.Removed => GitDiffLineKind.Removed,
                        XamlVisualEditor.Core.GitDiffLineKind.NoNewline => GitDiffLineKind.NoNewline,
                        _ => GitDiffLineKind.Context
                    };

                    string marker = line.Kind switch
                    {
                        XamlVisualEditor.Core.GitDiffLineKind.Added => "+",
                        XamlVisualEditor.Core.GitDiffLineKind.Removed => "-",
                        XamlVisualEditor.Core.GitDiffLineKind.NoNewline => "!",
                        _ => " "
                    };

                    lines.Add(new GitDiffLineViewModel(
                        kind,
                        line.Text,
                        marker,
                        line.OldLine,
                        line.NewLine));
                }
            }
        }

        return lines;
    }

    private void ConfigureWatcher(string repoRoot)
    {
        if (_watcher is not null && string.Equals(_watcher.Path, repoRoot, StringComparison.Ordinal))
        {
            return;
        }

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(repoRoot)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName
        };

        _watcher.Changed += OnWatcherChanged;
        _watcher.Created += OnWatcherChanged;
        _watcher.Deleted += OnWatcherChanged;
        _watcher.Renamed += OnWatcherChanged;
        _watcher.EnableRaisingEvents = true;
    }

    private void OnWatcherChanged(object? sender, FileSystemEventArgs args)
    {
        if (args.FullPath.Contains(Path.Combine(RepositoryRoot ?? string.Empty, ".git"), StringComparison.Ordinal))
        {
            return;
        }

        _refreshRequests.OnNext(Unit.Default);
    }
}
