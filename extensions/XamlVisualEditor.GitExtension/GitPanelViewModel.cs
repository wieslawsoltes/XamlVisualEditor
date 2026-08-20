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
using ReactiveUI.SourceGenerators;
using XamlVisualEditor.Core;
using CoreGitDiffLineKind = XamlVisualEditor.Core.GitDiffLineKind;
using XamlVisualEditor.Core.Interfaces;
using XamlVisualEditor.Extensions;

namespace XamlVisualEditor.GitExtension;

/// <summary>
/// ViewModel entry representing a git file change for the panel.
/// </summary>
public sealed partial class GitChangeEntryViewModel : ReactiveObject
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
public sealed partial class GitDiffLineViewModel
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

public sealed partial class GitPanelViewModel : ReactiveObject, IDisposable
{
    private readonly IGitService? _gitService;
    private readonly IWorkspaceInfo? _workspaceInfo;
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
    public partial string? RepositoryRoot { get; private set; }

    [Reactive]
    public partial string BranchName { get; private set; } = string.Empty;

    [Reactive]
    public partial string? UpstreamName { get; private set; }

    [Reactive]
    public partial int AheadBy { get; private set; }

    [Reactive]
    public partial int BehindBy { get; private set; }

    [Reactive]
    public partial bool IsRepository { get; private set; }

    [Reactive]
    public partial string? ErrorMessage { get; private set; }

    [Reactive]
    public partial bool IsBusy { get; private set; }

    [Reactive]
    public partial string? SearchText { get; set; }

    [Reactive]
    public partial string? CommitMessage { get; set; }

    [Reactive]
    public partial string DiffText { get; private set; } = string.Empty;

    [Reactive]
    public partial GitChangeEntryViewModel? SelectedStagedChange { get; set; }

    [Reactive]
    public partial GitChangeEntryViewModel? SelectedUnstagedChange { get; set; }

    public IReadOnlyList<GitChangeEntryViewModel> StagedItems => _stagedItems;

    public IReadOnlyList<GitChangeEntryViewModel> UnstagedItems => _unstagedItems;

    public DataGridCollectionView StagedView => _stagedView;

    public DataGridCollectionView UnstagedView => _unstagedView;

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
        IWorkspaceInfo? workspaceInfo = null)
    {
        _gitService = gitService;
        _workspaceInfo = workspaceInfo;

        _stagedView = new DataGridCollectionView(_stagedItems);
        _unstagedView = new DataGridCollectionView(_unstagedItems);

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
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => ApplyFilter());
        _disposables.Add(searchSubscription);

        IDisposable refreshSubscription = _refreshRequests
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(_ => RefreshCommand.Execute().Subscribe());
        _disposables.Add(refreshSubscription);

        if (_workspaceInfo is not null)
        {
            EventHandler<WorkspaceChangedEventArgs> handler = (_, _) => _refreshRequests.OnNext(Unit.Default);
            _workspaceInfo.WorkspaceChanged += handler;
            _disposables.Add(Disposable.Create(() => _workspaceInfo.WorkspaceChanged -= handler));
        }
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

        string? workspacePath = _workspaceInfo?.WorkspacePath;
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
        string? selectedPath = SelectedStagedChange?.Path ?? SelectedUnstagedChange?.Path;
        bool wasStaged = SelectedStagedChange is not null;

        BranchName = status.BranchName;
        UpstreamName = status.UpstreamName;
        AheadBy = status.AheadBy;
        BehindBy = status.BehindBy;
        IsRepository = status.IsRepository;
        ErrorMessage = status.ErrorMessage;

        ReplaceItems(_stagedItems, status, isStaged: true);
        ReplaceItems(_unstagedItems, status, isStaged: false);
        ApplyFilter();
        RestoreSelection(selectedPath, wasStaged);
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
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(ApplyFilterCore, DispatcherPriority.Background);
            return;
        }

        ApplyFilterCore();
    }

    private void ApplyFilterCore()
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

        if (_unstagedItems.Count == 0)
        {
            return;
        }

        string[] paths = new string[_unstagedItems.Count];
        for (int i = 0; i < _unstagedItems.Count; i++)
        {
            paths[i] = _unstagedItems[i].Path;
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

        if (_stagedItems.Count == 0)
        {
            return;
        }

        string[] paths = new string[_stagedItems.Count];
        for (int i = 0; i < _stagedItems.Count; i++)
        {
            paths[i] = _stagedItems[i].Path;
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

        string? message = CommitMessage?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        await _gitService.CommitAsync(RepositoryRoot, message);
        CommitMessage = string.Empty;
        await RefreshAsync();
    }

    private void ConfigureWatcher(string? repoRoot)
    {
        if (string.IsNullOrWhiteSpace(repoRoot))
        {
            _watcher?.Dispose();
            _watcher = null;
            return;
        }

        if (_watcher is not null && string.Equals(_watcher.Path, repoRoot, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(repoRoot)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };

        FileSystemEventHandler handler = (_, args) =>
        {
            if (IsGitInternalPath(args.FullPath))
            {
                return;
            }

            _refreshRequests.OnNext(Unit.Default);
        };
        RenamedEventHandler renamedHandler = (_, args) =>
        {
            if (IsGitInternalPath(args.FullPath))
            {
                return;
            }

            _refreshRequests.OnNext(Unit.Default);
        };
        ErrorEventHandler errorHandler = (_, _) => _refreshRequests.OnNext(Unit.Default);

        _watcher.Changed += handler;
        _watcher.Created += handler;
        _watcher.Deleted += handler;
        _watcher.Renamed += renamedHandler;
        _watcher.Error += errorHandler;

        _disposables.Add(Disposable.Create(() =>
        {
            if (_watcher is not null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= handler;
                _watcher.Created -= handler;
                _watcher.Deleted -= handler;
                _watcher.Renamed -= renamedHandler;
                _watcher.Error -= errorHandler;
            }
        }));
    }


    private static void ReplaceItems(
        ObservableCollection<GitChangeEntryViewModel> target,
        GitRepositoryStatus status,
        bool isStaged)
    {
        target.Clear();

        foreach (GitFileChange change in status.Changes)
        {
            bool staged = isStaged
                ? change.IndexStatus != GitChangeKind.None
                : change.WorkTreeStatus != GitChangeKind.None;
            if (!staged)
            {
                continue;
            }

            GitChangeKind kind = isStaged ? change.IndexStatus : change.WorkTreeStatus;
            bool isUntracked = change.IndexStatus == GitChangeKind.Untracked || change.WorkTreeStatus == GitChangeKind.Untracked;
            target.Add(new GitChangeEntryViewModel(
                change.Path,
                change.OldPath,
                kind,
                isStaged,
                isUntracked));
        }
    }

    private void RestoreSelection(string? selectedPath, bool wasStaged)
    {
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        GitChangeEntryViewModel? match = wasStaged
            ? FindMatching(_stagedItems, selectedPath)
            : FindMatching(_unstagedItems, selectedPath);

        if (match is null)
        {
            return;
        }

        if (wasStaged)
        {
            SelectedStagedChange = match;
            _stagedView.MoveCurrentTo(match);
        }
        else
        {
            SelectedUnstagedChange = match;
            _unstagedView.MoveCurrentTo(match);
        }
    }

    private static GitChangeEntryViewModel? FindMatching(
        IEnumerable<GitChangeEntryViewModel> items,
        string selectedPath)
    {
        foreach (GitChangeEntryViewModel item in items)
        {
            if (PathsEqual(item.Path, selectedPath) || (item.OldPath is not null && PathsEqual(item.OldPath, selectedPath)))
            {
                return item;
            }
        }

        return null;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGitInternalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        char separator = Path.DirectorySeparatorChar;
        string normalized = path.Replace(Path.AltDirectorySeparatorChar, separator);
        string token = separator + ".git" + separator;

        if (normalized.Contains(token, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return normalized.EndsWith(separator + ".git", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<GitDiffLineViewModel> BuildDiffLines(GitDiff diff)
    {
        if (diff.Files.Count == 0)
        {
            return Array.Empty<GitDiffLineViewModel>();
        }

        List<GitDiffLineViewModel> results = new();
        foreach (GitDiffFile file in diff.Files)
        {
            foreach (string header in file.HeaderLines)
            {
                results.Add(new GitDiffLineViewModel(GitDiffLineKind.FileHeader, header, string.Empty, null, null));
            }

            foreach (GitDiffHunk hunk in file.Hunks)
            {
                results.Add(new GitDiffLineViewModel(GitDiffLineKind.HunkHeader, hunk.Header, string.Empty, null, null));

                foreach (GitDiffLine line in hunk.Lines)
                {
                    GitDiffLineKind kind = MapLineKind(line.Kind);
                    string marker = GetMarker(kind);
                    results.Add(new GitDiffLineViewModel(kind, line.Text, marker, line.OldLine, line.NewLine));
                }
            }
        }

        return results.Count == 0 ? Array.Empty<GitDiffLineViewModel>() : results;
    }

    private static GitDiffLineKind MapLineKind(CoreGitDiffLineKind kind)
    {
        return kind switch
        {
            CoreGitDiffLineKind.Added => GitDiffLineKind.Added,
            CoreGitDiffLineKind.Removed => GitDiffLineKind.Removed,
            CoreGitDiffLineKind.NoNewline => GitDiffLineKind.NoNewline,
            _ => GitDiffLineKind.Context
        };
    }

    private static string GetMarker(GitDiffLineKind kind)
    {
        return kind switch
        {
            GitDiffLineKind.Added => "+",
            GitDiffLineKind.Removed => "-",
            GitDiffLineKind.Context => " ",
            GitDiffLineKind.NoNewline => "\\",
            _ => string.Empty
        };
    }
}
