using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using XamlVisualEditor.Animation;
using XamlVisualEditor.Core;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Xaml.Ast;
using XamlVisualEditor.Sync;

namespace XamlVisualEditor.Shell.ViewModels;

public sealed partial class AnimationEditorViewModel : ReactiveObject, IDisposable, IAnimationEditorPanelModel
{
    private const double DefaultDurationSeconds = 1.0;
    private const double DefaultFrameRate = 60.0;
    private const double BasePixelsPerSecond = 120.0;

    private readonly CompositeDisposable _disposables = new();
    private readonly SerialDisposable _selectionSubscription = new();
    private readonly MainWindowViewModel _mainVm;
    private readonly IAnimationPreviewService? _previewService;
    private readonly AnimationUndoRedoService _undoRedo = new();
    private readonly SerialDisposable _keyframeSubscription = new();
    private readonly SerialDisposable _keyframeValidationSubscription = new();
    private bool _isApplyingUndoRedo;
    private int _timelineIndex = 1;
    private AnimationKeyframeViewModel? _selectionAnchor;
    private List<KeyframeClipboardEntry> _keyframeClipboard = new();

    [Reactive]
    public partial DesignerDocumentViewModel? ActiveDocument { get; private set; }

    public ObservableCollection<AnimationTimelineViewModel> Timelines { get; } = new();

    public ObservableCollection<AnimationKeyframeViewModel> SelectedKeyframes { get; } = new();

    public ObservableCollection<TimelineTickViewModel> RulerTicks { get; } = new();

    public ObservableCollection<AnimationResourceEntryViewModel> AvailableResources { get; } = new();

    [Reactive]
    public partial AnimationResourceEntryViewModel? SelectedResource { get; set; }

    [Reactive]
    public partial AnimationTimelineViewModel? SelectedTimeline { get; set; }

    [Reactive]
    public partial AnimationTrackViewModel? SelectedTrack { get; set; }

    [Reactive]
    public partial AnimationKeyframeViewModel? SelectedKeyframe { get; set; }

    [Reactive]
    public partial bool IsKeyframeTimeValid { get; private set; } = true;

    [Reactive]
    public partial bool IsKeyframeValueValid { get; private set; } = true;

    [Reactive]
    public partial bool IsKeyframeEasingValid { get; private set; } = true;

    [Reactive]
    public partial string KeyframeValidationMessage { get; private set; } = string.Empty;

    [Reactive]
    public partial double CurrentTimeSeconds { get; set; }

    [Reactive]
    public partial double Zoom { get; set; } = 1.0;

    public double PixelsPerSecond => Math.Max(12.0, BasePixelsPerSecond * Zoom);

    public double TimelineWidth => Math.Max(1.0, (SelectedTimeline?.DurationSeconds ?? DefaultDurationSeconds) * PixelsPerSecond);

    public double SnapIntervalSeconds => SelectedTimeline is null || SelectedTimeline.FrameRate <= 0
        ? 0.0
        : 1.0 / SelectedTimeline.FrameRate;

    [Reactive]
    public partial string NewTrackPropertyName { get; set; } = string.Empty;

    [Reactive]
    public partial string StatusMessage { get; private set; } = "Ready";

    [Reactive]
    public partial Guid? TargetNodeId { get; private set; }

    [Reactive]
    public partial string TargetDisplayName { get; private set; } = "No selection";

    public ObservableCollection<AnimationResourceScopeEntry> ResourceScopes { get; } = new()
    {
        new AnimationResourceScopeEntry(AnimationResourceScope.DocumentResources, "Document Resources"),
        new AnimationResourceScopeEntry(AnimationResourceScope.SelectedElementResources, "Selected Element Resources"),
        new AnimationResourceScopeEntry(AnimationResourceScope.StylesResources, "Styles Resources")
    };

    [Reactive]
    public partial AnimationResourceScopeEntry SelectedScope { get; set; }

    public ReactiveCommand<Unit, Unit> AddTimelineCommand { get; }
    public ReactiveCommand<Unit, Unit> AddTrackCommand { get; }
    public ReactiveCommand<Unit, Unit> AddKeyframeCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteSelectionCommand { get; }
    public ReactiveCommand<Unit, Unit> CreateResourceCommand { get; }
    public ReactiveCommand<double, Unit> SetCurrentTimeCommand { get; }
    public ReactiveCommand<AnimationTrackViewModel, Unit> SelectTrackCommand { get; }
    public ReactiveCommand<KeyframeSelectionRequest, Unit> SelectKeyframeCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyKeyframesCommand { get; }
    public ReactiveCommand<Unit, Unit> PasteKeyframesCommand { get; }
    public ReactiveCommand<Unit, Unit> PlayPreviewCommand { get; }
    public ReactiveCommand<Unit, Unit> StopPreviewCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshResourcesCommand { get; }
    public ReactiveCommand<AnimationResourceEntryViewModel?, Unit> LoadResourceCommand { get; }
    public ReactiveCommand<Unit, Unit> UpdateResourceCommand { get; }
    public ReactiveCommand<Unit, Unit> DeleteResourceCommand { get; }
    public ReactiveCommand<Unit, Unit> UndoCommand { get; }
    public ReactiveCommand<Unit, Unit> RedoCommand { get; }
    public ReactiveCommand<KeyframeMoveCommit, Unit> CommitMoveCommand { get; }

    public AnimationEditorViewModel(MainWindowViewModel mainVm, IAnimationPreviewService? previewService)
    {
        _mainVm = mainVm;
        _previewService = previewService;
        SelectedScope = ResourceScopes[0];

        AddTimelineCommand = ReactiveCommand.Create(AddTimeline);
        AddTrackCommand = ReactiveCommand.Create(AddTrack);
        AddKeyframeCommand = ReactiveCommand.Create(AddKeyframe);
        DeleteSelectionCommand = ReactiveCommand.Create(DeleteSelection);
        CreateResourceCommand = ReactiveCommand.Create(CreateResource);
        SetCurrentTimeCommand = ReactiveCommand.Create<double>(SetCurrentTime);
        SelectTrackCommand = ReactiveCommand.Create<AnimationTrackViewModel>(SelectTrack);
        SelectKeyframeCommand = ReactiveCommand.Create<KeyframeSelectionRequest>(SelectKeyframe);
        CopyKeyframesCommand = ReactiveCommand.Create(CopyKeyframes);
        PasteKeyframesCommand = ReactiveCommand.Create(PasteKeyframes);
        PlayPreviewCommand = ReactiveCommand.Create(PlayPreview);
        StopPreviewCommand = ReactiveCommand.Create(StopPreview);
        RefreshResourcesCommand = ReactiveCommand.Create(RefreshResources);
        LoadResourceCommand = ReactiveCommand.Create<AnimationResourceEntryViewModel?>(LoadResource);
        UpdateResourceCommand = ReactiveCommand.Create(UpdateResource);
        DeleteResourceCommand = ReactiveCommand.Create(DeleteResource);
        UndoCommand = ReactiveCommand.Create(Undo);
        RedoCommand = ReactiveCommand.Create(Redo);
        CommitMoveCommand = ReactiveCommand.Create<KeyframeMoveCommit>(CommitMove);

        IDisposable activeDocSubscription = _mainVm.WhenAnyValue(x => x.ActiveDesignerDocument)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(UpdateActiveDocument);
        _disposables.Add(activeDocSubscription);
        _disposables.Add(_keyframeSubscription);
        _disposables.Add(_keyframeValidationSubscription);
        _disposables.Add(_selectionSubscription);

        IDisposable zoomSubscription = this.WhenAnyValue(x => x.Zoom)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(PixelsPerSecond));
                this.RaisePropertyChanged(nameof(TimelineWidth));
                RebuildRulerTicks();
            });
        _disposables.Add(zoomSubscription);

        IDisposable timelineSubscription = this.WhenAnyValue(x => x.SelectedTimeline)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(TimelineWidth));
                RebuildRulerTicks();
            });
        _disposables.Add(timelineSubscription);

        IDisposable durationSubscription = this.WhenAnyValue(x => x.SelectedTimeline)
            .Select(timeline => timeline is null
                ? Observable.Return(0.0)
                : timeline.WhenAnyValue(t => t.DurationSeconds))
            .Switch()
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(TimelineWidth));
                this.RaisePropertyChanged(nameof(SnapIntervalSeconds));
                RebuildRulerTicks();
            });
        _disposables.Add(durationSubscription);

        IDisposable frameRateSubscription = this.WhenAnyValue(x => x.SelectedTimeline)
            .Select(timeline => timeline is null
                ? Observable.Return(0.0)
                : timeline.WhenAnyValue(t => t.FrameRate))
            .Switch()
            .Subscribe(_ => this.RaisePropertyChanged(nameof(SnapIntervalSeconds)));
        _disposables.Add(frameRateSubscription);

        IDisposable selectedKeyframeSubscription = this.WhenAnyValue(x => x.SelectedKeyframe)
            .Subscribe(SubscribeKeyframeChanges);
        _disposables.Add(selectedKeyframeSubscription);

        IDisposable selectedTimelineValidationSubscription = this.WhenAnyValue(x => x.SelectedTimeline)
            .Select(timeline => timeline is null
                ? Observable.Return(0.0)
                : timeline.WhenAnyValue(t => t.DurationSeconds))
            .Switch()
            .Subscribe(_ => UpdateKeyframeValidation());
        _disposables.Add(selectedTimelineValidationSubscription);

        if (Timelines.Count == 0)
        {
            AddTimeline();
        }
    }

    private void UpdateActiveDocument(DesignerDocumentViewModel? document)
    {
        ActiveDocument = document;
        TargetNodeId = null;
        TargetDisplayName = "No selection";
        AvailableResources.Clear();

        if (document is null)
        {
            return;
        }

        IDisposable selectionSubscription = document.WhenAnyValue(x => x.SelectedNodeId)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(id => UpdateTargetFromSelection(document, id));
        _selectionSubscription.Disposable = selectionSubscription;

        RefreshResources();
    }

    private void UpdateTargetFromSelection(DesignerDocumentViewModel document, Guid? nodeId)
    {
        TargetNodeId = nodeId;

        if (nodeId is null)
        {
            TargetDisplayName = "No selection";
            return;
        }

        MutableAstNode? node = document.NodeMap.FindById(nodeId.Value);
        if (node is not MutableAstObjectNode objNode)
        {
            TargetDisplayName = "No selection";
            return;
        }

        string? name = objNode.GetPropertyValue("x:Name") ?? objNode.GetPropertyValue("Name");
        TargetDisplayName = string.IsNullOrWhiteSpace(name)
            ? objNode.TypeName
            : $"{objNode.TypeName} ({name})";
    }

    private void AddTimeline()
    {
        string name = $"Animation {_timelineIndex++}";
        AnimationTimelineViewModel timeline = new()
        {
            Name = name,
            ResourceKey = name.Replace(" ", string.Empty, StringComparison.Ordinal),
            DurationSeconds = DefaultDurationSeconds,
            FrameRate = DefaultFrameRate
        };

        Timelines.Add(timeline);
        SelectedTimeline = timeline;
        RebuildRulerTicks();
        StatusMessage = $"Created timeline {name}";
    }

    private void AddTrack()
    {
        if (SelectedTimeline is null)
        {
            StatusMessage = "Select a timeline to add tracks.";
            return;
        }

        string property = string.IsNullOrWhiteSpace(NewTrackPropertyName)
            ? "Opacity"
            : NewTrackPropertyName.Trim();

        AnimationTrackViewModel track = new() { PropertyName = property };
        SelectedTimeline.Tracks.Add(track);
        SelectedTrack = track;
        NewTrackPropertyName = string.Empty;
        StatusMessage = $"Added track {property}.";
    }

    private void AddKeyframe()
    {
        if (SelectedTrack is null)
        {
            StatusMessage = "Select a track to add keyframes.";
            return;
        }

        AnimationKeyframeViewModel keyframe = new()
        {
            TimeSeconds = Math.Clamp(CurrentTimeSeconds, 0.0, SelectedTimeline?.DurationSeconds ?? DefaultDurationSeconds),
            Value = ResolveKeyframeValue(),
            Owner = SelectedTrack
        };

        SelectedTrack.Keyframes.Add(keyframe);
        SelectSingleKeyframe(keyframe);
        RecordEdit(new AnimationEdit(
            description: "Add keyframe",
            apply: () => SelectedTrack.Keyframes.Add(keyframe),
            undo: () => SelectedTrack.Keyframes.Remove(keyframe)));
        StatusMessage = "Added keyframe.";
    }

    private string ResolveKeyframeValue()
    {
        if (ActiveDocument?.SyncEngine.CurrentDocument is not MutableAstDocument document)
        {
            return string.Empty;
        }

        if (TargetNodeId is null || SelectedTrack is null)
        {
            return string.Empty;
        }

        MutableAstNode? node = ActiveDocument.NodeMap.FindById(TargetNodeId.Value);
        if (node is not MutableAstObjectNode objNode)
        {
            return string.Empty;
        }

        string propertyName = SelectedTrack.PropertyName;
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return string.Empty;
        }

        return objNode.GetPropertyValue(propertyName) ?? string.Empty;
    }

    private void DeleteSelection()
    {
        if (SelectedKeyframes.Count > 0)
        {
            List<KeyframeRemoval> removals = CaptureRemovals(SelectedKeyframes);
            RemoveKeyframes(removals);
            RecordEdit(new AnimationEdit(
                description: "Remove keyframes",
                apply: () => RemoveKeyframes(removals),
                undo: () => RestoreKeyframes(removals)));
            ClearKeyframeSelection();
            StatusMessage = "Removed keyframes.";
            return;
        }

        if (SelectedKeyframe is not null && SelectedTrack is not null)
        {
            AnimationKeyframeViewModel removed = SelectedKeyframe;
            int index = SelectedTrack.Keyframes.IndexOf(removed);
            SelectedTrack.Keyframes.Remove(SelectedKeyframe);
            SelectedKeyframe.IsSelected = false;
            SelectedKeyframe = null;
            RecordEdit(new AnimationEdit(
                description: "Remove keyframe",
                apply: () => SelectedTrack.Keyframes.Remove(removed),
                undo: () => InsertKeyframe(SelectedTrack, removed, index)));
            StatusMessage = "Removed keyframe.";
            return;
        }

        if (SelectedTrack is not null && SelectedTimeline is not null)
        {
            AnimationTrackViewModel removed = SelectedTrack;
            int index = SelectedTimeline.Tracks.IndexOf(removed);
            SelectedTimeline.Tracks.Remove(SelectedTrack);
            SelectedTrack = null;
            RecordEdit(new AnimationEdit(
                description: "Remove track",
                apply: () => SelectedTimeline.Tracks.Remove(removed),
                undo: () => InsertTrack(SelectedTimeline, removed, index)));
            StatusMessage = "Removed track.";
        }
    }

    private void CreateResource()
    {
        if (ActiveDocument?.SyncEngine.CurrentDocument is not MutableAstDocument document)
        {
            StatusMessage = "No active document.";
            return;
        }

        if (SelectedTimeline is null)
        {
            StatusMessage = "Select a timeline to create a resource.";
            return;
        }

        List<AnimationTrackModel> tracks = new();
        foreach (AnimationTrackViewModel track in SelectedTimeline.Tracks)
        {
            if (string.IsNullOrWhiteSpace(track.PropertyName))
            {
                continue;
            }

            AnimationTrackModel trackModel = new()
            {
                PropertyName = track.PropertyName
            };

            foreach (AnimationKeyframeViewModel keyframe in track.Keyframes)
            {
                trackModel.Keyframes.Add(new AnimationKeyframeModel
                {
                    TimeSeconds = keyframe.TimeSeconds,
                    Value = keyframe.Value,
                    Easing = keyframe.Easing
                });
            }

            if (trackModel.Keyframes.Count > 0)
            {
                tracks.Add(trackModel);
            }
        }

        if (tracks.Count == 0)
        {
            StatusMessage = "Add at least one keyframe before creating a resource.";
            return;
        }

        MutableAstObjectNode? owner = null;
        if (SelectedScope.Scope == AnimationResourceScope.SelectedElementResources && TargetNodeId is not null)
        {
            owner = ActiveDocument.NodeMap.FindById(TargetNodeId.Value) as MutableAstObjectNode;
        }

        AnimationResourceDefinition definition = new(
            key: SelectedTimeline.ResourceKey,
            durationSeconds: SelectedTimeline.DurationSeconds,
            tracks: tracks,
            scope: SelectedScope.Scope,
            scopeOwner: owner);

        bool added = AnimationResourceWriter.TryAddAnimationResource(document, definition, out string? error);
        if (!added)
        {
            StatusMessage = error ?? "Failed to create animation resource.";
            return;
        }

        ActiveDocument.SyncEngine.NotifyAstChanged(document, SyncSource.DesignSurface);
        RefreshResources();
        StatusMessage = $"Added animation {SelectedTimeline.ResourceKey}.";
    }

    private void PlayPreview()
    {
        if (_previewService is null)
        {
            StatusMessage = "Preview service unavailable.";
            return;
        }

        if (ActiveDocument is null || SelectedTimeline is null)
        {
            StatusMessage = "Select a timeline and target to preview.";
            return;
        }

        AnimationTimelineModel timeline = BuildTimelineModel(SelectedTimeline);
        bool started = _previewService.TryPlayPreview(
            ActiveDocument.DesignSurface,
            TargetNodeId,
            timeline,
            out string? error);

        if (!started)
        {
            StatusMessage = error ?? "Preview failed.";
            return;
        }

        StatusMessage = "Previewing animation.";
    }

    private void StopPreview()
    {
        _previewService?.StopPreview();
        StatusMessage = "Preview stopped.";
    }

    private void RefreshResources()
    {
        AvailableResources.Clear();
        SelectedResource = null;

        if (ActiveDocument?.SyncEngine.CurrentDocument is not MutableAstDocument document)
        {
            return;
        }

        IReadOnlyList<AnimationResourceSnapshot> snapshots = AnimationResourceReader.LoadAnimations(document);
        foreach (AnimationResourceSnapshot snapshot in snapshots)
        {
            AvailableResources.Add(new AnimationResourceEntryViewModel(snapshot));
        }
    }

    private void LoadResource(AnimationResourceEntryViewModel? entry)
    {
        if (entry is null)
        {
            StatusMessage = "Select a resource to load.";
            return;
        }

        AnimationTimelineViewModel timeline = BuildTimelineViewModel(entry.Snapshot);
        Timelines.Add(timeline);
        SelectedTimeline = timeline;
        StatusMessage = $"Loaded {entry.Snapshot.Key}.";
    }

    private void UpdateResource()
    {
        if (ActiveDocument?.SyncEngine.CurrentDocument is not MutableAstDocument document)
        {
            StatusMessage = "No active document.";
            return;
        }

        if (SelectedTimeline is null || SelectedResource is null)
        {
            StatusMessage = "Select a timeline and resource to update.";
            return;
        }

        List<AnimationTrackModel> tracks = new();
        foreach (AnimationTrackViewModel track in SelectedTimeline.Tracks)
        {
            if (string.IsNullOrWhiteSpace(track.PropertyName))
            {
                continue;
            }

            AnimationTrackModel trackModel = new() { PropertyName = track.PropertyName };
            foreach (AnimationKeyframeViewModel keyframe in track.Keyframes)
            {
                trackModel.Keyframes.Add(new AnimationKeyframeModel
                {
                    TimeSeconds = keyframe.TimeSeconds,
                    Value = keyframe.Value,
                    Easing = keyframe.Easing
                });
            }

            if (trackModel.Keyframes.Count > 0)
            {
                tracks.Add(trackModel);
            }
        }

        if (tracks.Count == 0)
        {
            StatusMessage = "Add at least one keyframe before updating.";
            return;
        }

        if (string.IsNullOrWhiteSpace(SelectedTimeline.ResourceKey))
        {
            StatusMessage = "Resource key is required.";
            return;
        }

        MutableAstObjectNode? owner = null;
        AnimationResourceSnapshot snapshot = SelectedResource.Snapshot;
        if (snapshot.Scope is AnimationResourceScope.SelectedElementResources or AnimationResourceScope.StylesResources)
        {
            owner = ActiveDocument.NodeMap.FindById(snapshot.OwnerId) as MutableAstObjectNode;
        }

        AnimationResourceDefinition definition = new(
            key: SelectedTimeline.ResourceKey,
            durationSeconds: SelectedTimeline.DurationSeconds,
            tracks: tracks,
            scope: snapshot.Scope,
            scopeOwner: owner);

        bool updated = AnimationResourceWriter.TryUpdateAnimationResource(document, definition, snapshot, out string? error);
        if (!updated)
        {
            StatusMessage = error ?? "Failed to update resource.";
            return;
        }

        ActiveDocument.SyncEngine.NotifyAstChanged(document, SyncSource.DesignSurface);
        RefreshResources();
        SelectedResource = AvailableResources.FirstOrDefault(r => string.Equals(r.Snapshot.Key, definition.Key, StringComparison.Ordinal));
        StatusMessage = $"Updated {definition.Key}.";
    }

    private void DeleteResource()
    {
        if (ActiveDocument?.SyncEngine.CurrentDocument is not MutableAstDocument document)
        {
            StatusMessage = "No active document.";
            return;
        }

        if (SelectedResource is null)
        {
            StatusMessage = "Select a resource to delete.";
            return;
        }

        bool deleted = AnimationResourceWriter.TryDeleteAnimationResource(document, SelectedResource.Snapshot, out string? error);
        if (!deleted)
        {
            StatusMessage = error ?? "Failed to delete resource.";
            return;
        }

        ActiveDocument.SyncEngine.NotifyAstChanged(document, SyncSource.DesignSurface);
        RefreshResources();
        SelectedResource = null;
        StatusMessage = "Deleted resource.";
    }

    private void SetCurrentTime(double timeSeconds)
    {
        if (SelectedTimeline is not null)
        {
            CurrentTimeSeconds = Math.Clamp(timeSeconds, 0.0, SelectedTimeline.DurationSeconds);
            return;
        }

        CurrentTimeSeconds = Math.Max(0.0, timeSeconds);
    }

    private void RebuildRulerTicks()
    {
        RulerTicks.Clear();

        if (SelectedTimeline is null)
        {
            return;
        }

        IReadOnlyList<TimelineTickViewModel> ticks = TimelineTickBuilder.BuildTicks(
            SelectedTimeline.DurationSeconds,
            PixelsPerSecond);

        foreach (TimelineTickViewModel tick in ticks)
        {
            RulerTicks.Add(tick);
        }
    }

    private void SelectTrack(AnimationTrackViewModel track)
    {
        SelectedTrack = track;
        ClearKeyframeSelection();
    }

    private void SelectKeyframe(KeyframeSelectionRequest request)
    {
        AnimationKeyframeViewModel keyframe = request.Keyframe;
        if (request.Mode == KeyframeSelectionMode.Range)
        {
            SelectKeyframeRange(keyframe, request.Additive);
            return;
        }

        if (!request.Additive)
        {
            ClearKeyframeSelection();
        }

        if (SelectedKeyframes.Contains(keyframe))
        {
            if (request.Additive)
            {
                DeselectKeyframe(keyframe);
            }
        }
        else
        {
            SelectAdditionalKeyframe(keyframe);
        }

        SelectedKeyframe = SelectedKeyframes.LastOrDefault();
        SelectedTrack = keyframe.Owner ?? SelectedTrack;
        _selectionAnchor = keyframe;
    }

    private void SelectSingleKeyframe(AnimationKeyframeViewModel keyframe)
    {
        ClearKeyframeSelection();
        SelectAdditionalKeyframe(keyframe);
        SelectedKeyframe = keyframe;
        SelectedTrack = keyframe.Owner ?? SelectedTrack;
        _selectionAnchor = keyframe;
    }

    private void SelectKeyframeRange(AnimationKeyframeViewModel keyframe, bool additive)
    {
        AnimationTrackViewModel? track = keyframe.Owner;
        if (track is null)
        {
            SelectSingleKeyframe(keyframe);
            return;
        }

        AnimationKeyframeViewModel? anchor = _selectionAnchor ?? SelectedKeyframe;
        if (anchor?.Owner != track)
        {
            SelectSingleKeyframe(keyframe);
            return;
        }

        int start = track.Keyframes.IndexOf(anchor);
        int end = track.Keyframes.IndexOf(keyframe);
        if (start < 0 || end < 0)
        {
            SelectSingleKeyframe(keyframe);
            return;
        }

        if (!additive)
        {
            ClearKeyframeSelection();
        }

        if (start > end)
        {
            (start, end) = (end, start);
        }

        for (int i = start; i <= end; i++)
        {
            SelectAdditionalKeyframe(track.Keyframes[i]);
        }

        SelectedKeyframe = keyframe;
        SelectedTrack = track;
        _selectionAnchor = anchor;
    }

    private void SelectAdditionalKeyframe(AnimationKeyframeViewModel keyframe)
    {
        if (SelectedKeyframes.Contains(keyframe))
        {
            return;
        }

        keyframe.IsSelected = true;
        SelectedKeyframes.Add(keyframe);
    }

    private void DeselectKeyframe(AnimationKeyframeViewModel keyframe)
    {
        if (!SelectedKeyframes.Remove(keyframe))
        {
            return;
        }

        keyframe.IsSelected = false;
        if (SelectedKeyframe == keyframe)
        {
            SelectedKeyframe = SelectedKeyframes.LastOrDefault();
        }
    }

    private void ClearKeyframeSelection()
    {
        foreach (AnimationKeyframeViewModel keyframe in SelectedKeyframes)
        {
            keyframe.IsSelected = false;
        }

        SelectedKeyframes.Clear();
        SelectedKeyframe = null;
    }

    private void SubscribeKeyframeChanges(AnimationKeyframeViewModel? keyframe)
    {
        if (keyframe is null)
        {
            _keyframeSubscription.Disposable = null;
            _keyframeValidationSubscription.Disposable = null;
            UpdateKeyframeValidation();
            return;
        }

        IDisposable subscription = Observable.Merge(
                PairChanges(keyframe.WhenAnyValue(k => k.TimeSeconds))
                    .Select(pair => new KeyframeEditChange(keyframe, AnimationKeyframeEditKind.Time, pair.OldValue, pair.NewValue)),
                PairChanges(keyframe.WhenAnyValue(k => k.Value))
                    .Select(pair => new KeyframeEditChange(keyframe, AnimationKeyframeEditKind.Value, pair.OldValue, pair.NewValue)),
                PairChanges(keyframe.WhenAnyValue(k => k.Easing))
                    .Select(pair => new KeyframeEditChange(keyframe, AnimationKeyframeEditKind.Easing, pair.OldValue, pair.NewValue)))
            .Where(_ => !_isApplyingUndoRedo)
            .Subscribe(change => RecordEdit(ToEdit(change)));

        _keyframeSubscription.Disposable = subscription;

        IDisposable validationSubscription = Observable.Merge(
                keyframe.WhenAnyValue(k => k.TimeSeconds).Select(_ => Unit.Default),
                keyframe.WhenAnyValue(k => k.Value).Select(_ => Unit.Default),
                keyframe.WhenAnyValue(k => k.Easing).Select(_ => Unit.Default))
            .Subscribe(_ => UpdateKeyframeValidation());

        _keyframeValidationSubscription.Disposable = validationSubscription;
        UpdateKeyframeValidation();
    }

    private void UpdateKeyframeValidation()
    {
        if (SelectedKeyframe is null)
        {
            IsKeyframeTimeValid = true;
            IsKeyframeValueValid = true;
            IsKeyframeEasingValid = true;
            KeyframeValidationMessage = string.Empty;
            return;
        }

        double maxTime = SelectedTimeline?.DurationSeconds ?? double.MaxValue;
        IsKeyframeTimeValid = SelectedKeyframe.TimeSeconds >= 0.0
            && SelectedKeyframe.TimeSeconds <= maxTime;
        IsKeyframeValueValid = !string.IsNullOrWhiteSpace(SelectedKeyframe.Value);
        IsKeyframeEasingValid = IsValidEasing(SelectedKeyframe.Easing);

        if (!IsKeyframeTimeValid)
        {
            KeyframeValidationMessage = "Time must be within the timeline duration.";
        }
        else if (!IsKeyframeValueValid)
        {
            KeyframeValidationMessage = "Value cannot be empty.";
        }
        else if (!IsKeyframeEasingValid)
        {
            KeyframeValidationMessage = "Easing is invalid. Use a keyword or cubic-bezier.";
        }
        else
        {
            KeyframeValidationMessage = string.Empty;
        }
    }

    private static bool IsValidEasing(string? easing)
    {
        if (string.IsNullOrWhiteSpace(easing))
        {
            return true;
        }

        string token = easing.Trim();
        string keyword = token.ToLowerInvariant();
        if (keyword is "linear" or "ease" or "easein" or "easeout" or "easeinout")
        {
            return true;
        }

        if (token.StartsWith("cubic-bezier(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(")", StringComparison.Ordinal))
        {
            string args = token.Substring("cubic-bezier(".Length, token.Length - "cubic-bezier(".Length - 1);
            return TryParseBezierArgs(args);
        }

        return TryParseBezierArgs(token);
    }

    private static bool TryParseBezierArgs(string args)
    {
        string[] parts = args.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
        {
            return false;
        }

        return double.TryParse(parts[0], out _)
            && double.TryParse(parts[1], out _)
            && double.TryParse(parts[2], out _)
            && double.TryParse(parts[3], out _);
    }

    private void CommitMove(KeyframeMoveCommit commit)
    {
        if (Math.Abs(commit.OldTime - commit.NewTime) < 0.0001)
        {
            return;
        }

        RecordEdit(new AnimationEdit(
            description: "Move keyframe",
            apply: () => commit.Keyframe.TimeSeconds = commit.NewTime,
            undo: () => commit.Keyframe.TimeSeconds = commit.OldTime));
    }

    private AnimationEdit ToEdit(KeyframeEditChange change)
    {
        return change.Kind switch
        {
            AnimationKeyframeEditKind.Time => new AnimationEdit(
                "Edit keyframe time",
                apply: () => change.Keyframe.TimeSeconds = Convert.ToDouble(change.NewValue ?? 0.0),
                undo: () => change.Keyframe.TimeSeconds = Convert.ToDouble(change.OldValue ?? 0.0)),
            AnimationKeyframeEditKind.Value => new AnimationEdit(
                "Edit keyframe value",
                apply: () => change.Keyframe.Value = Convert.ToString(change.NewValue) ?? string.Empty,
                undo: () => change.Keyframe.Value = Convert.ToString(change.OldValue) ?? string.Empty),
            AnimationKeyframeEditKind.Easing => new AnimationEdit(
                "Edit keyframe easing",
                apply: () => change.Keyframe.Easing = Convert.ToString(change.NewValue),
                undo: () => change.Keyframe.Easing = Convert.ToString(change.OldValue)),
            _ => new AnimationEdit("Edit keyframe", () => { }, () => { })
        };
    }

    private static IObservable<ValuePair<T>> PairChanges<T>(IObservable<T> source)
    {
        return source
            .Buffer(2, 1)
            .Where(values => values.Count == 2)
            .Select(values => new ValuePair<T>(values[0], values[1]));
    }

    private void RecordEdit(AnimationEdit edit)
    {
        if (_isApplyingUndoRedo)
        {
            return;
        }

        _undoRedo.Record(edit);
    }

    private void Undo()
    {
        if (!_undoRedo.CanUndo)
        {
            return;
        }

        _isApplyingUndoRedo = true;
        try
        {
            _undoRedo.Undo();
        }
        finally
        {
            _isApplyingUndoRedo = false;
        }
    }

    private void Redo()
    {
        if (!_undoRedo.CanRedo)
        {
            return;
        }

        _isApplyingUndoRedo = true;
        try
        {
            _undoRedo.Redo();
        }
        finally
        {
            _isApplyingUndoRedo = false;
        }
    }

    private static void InsertKeyframe(AnimationTrackViewModel track, AnimationKeyframeViewModel keyframe, int index)
    {
        int target = index < 0 ? track.Keyframes.Count : Math.Min(index, track.Keyframes.Count);
        track.Keyframes.Insert(target, keyframe);
    }

    private static void InsertTrack(AnimationTimelineViewModel timeline, AnimationTrackViewModel track, int index)
    {
        int target = index < 0 ? timeline.Tracks.Count : Math.Min(index, timeline.Tracks.Count);
        timeline.Tracks.Insert(target, track);
    }

    private static AnimationTimelineModel BuildTimelineModel(AnimationTimelineViewModel timeline)
    {
        AnimationTimelineModel model = new()
        {
            Name = timeline.Name,
            DurationSeconds = timeline.DurationSeconds,
            FrameRate = timeline.FrameRate
        };

        foreach (AnimationTrackViewModel track in timeline.Tracks)
        {
            if (string.IsNullOrWhiteSpace(track.PropertyName))
            {
                continue;
            }

            AnimationTrackModel trackModel = new() { PropertyName = track.PropertyName };
            foreach (AnimationKeyframeViewModel keyframe in track.Keyframes)
            {
                trackModel.Keyframes.Add(new AnimationKeyframeModel
                {
                    TimeSeconds = keyframe.TimeSeconds,
                    Value = keyframe.Value,
                    Easing = keyframe.Easing
                });
            }

            if (trackModel.Keyframes.Count > 0)
            {
                model.Tracks.Add(trackModel);
            }
        }

        return model;
    }

    private static AnimationTimelineViewModel BuildTimelineViewModel(AnimationResourceSnapshot snapshot)
    {
        AnimationTimelineViewModel timeline = new()
        {
            Name = snapshot.Key,
            ResourceKey = snapshot.Key,
            DurationSeconds = snapshot.Timeline.DurationSeconds,
            FrameRate = snapshot.Timeline.FrameRate
        };

        foreach (AnimationTrackModel track in snapshot.Timeline.Tracks)
        {
            AnimationTrackViewModel trackVm = new() { PropertyName = track.PropertyName };
            foreach (AnimationKeyframeModel keyframe in track.Keyframes)
            {
                trackVm.Keyframes.Add(new AnimationKeyframeViewModel
                {
                    TimeSeconds = keyframe.TimeSeconds,
                    Value = keyframe.Value,
                    Easing = keyframe.Easing,
                    Owner = trackVm
                });
            }

            timeline.Tracks.Add(trackVm);
        }

        return timeline;
    }

    public void Dispose()
    {
        _disposables.Dispose();
        _undoRedo.Dispose();
        _previewService?.StopPreview();
    }

    private void CopyKeyframes()
    {
        List<AnimationKeyframeViewModel> source = SelectedKeyframes.Count > 0
            ? SelectedKeyframes.ToList()
            : SelectedKeyframe is null
                ? new List<AnimationKeyframeViewModel>()
                : new List<AnimationKeyframeViewModel> { SelectedKeyframe };

        if (source.Count == 0)
        {
            StatusMessage = "Select keyframes to copy.";
            return;
        }

        double baseTime = source.Min(k => k.TimeSeconds);
        _keyframeClipboard = source
            .OrderBy(k => k.TimeSeconds)
            .Select(k => new KeyframeClipboardEntry(k.TimeSeconds - baseTime, k.Value, k.Easing))
            .ToList();
        StatusMessage = $"Copied {source.Count} keyframe(s).";
    }

    private void PasteKeyframes()
    {
        if (SelectedTrack is null)
        {
            StatusMessage = "Select a track to paste into.";
            return;
        }

        if (_keyframeClipboard.Count == 0)
        {
            StatusMessage = "Clipboard is empty.";
            return;
        }

        double duration = SelectedTimeline?.DurationSeconds ?? DefaultDurationSeconds;
        List<AnimationKeyframeViewModel> added = new();
        foreach (KeyframeClipboardEntry entry in _keyframeClipboard)
        {
            double time = Math.Clamp(CurrentTimeSeconds + entry.OffsetSeconds, 0.0, duration);
            AnimationKeyframeViewModel keyframe = new()
            {
                TimeSeconds = time,
                Value = entry.Value,
                Easing = entry.Easing,
                Owner = SelectedTrack
            };
            SelectedTrack.Keyframes.Add(keyframe);
            added.Add(keyframe);
        }

        if (added.Count == 0)
        {
            return;
        }

        RecordEdit(new AnimationEdit(
            description: "Paste keyframes",
            apply: () => added.ForEach(k => SelectedTrack.Keyframes.Add(k)),
            undo: () => added.ForEach(k => SelectedTrack.Keyframes.Remove(k))));

        ClearKeyframeSelection();
        foreach (AnimationKeyframeViewModel keyframe in added)
        {
            SelectAdditionalKeyframe(keyframe);
        }
        SelectedKeyframe = added.LastOrDefault();
        StatusMessage = $"Pasted {added.Count} keyframe(s).";
    }

    private static List<KeyframeRemoval> CaptureRemovals(IEnumerable<AnimationKeyframeViewModel> keyframes)
    {
        List<KeyframeRemoval> removals = new();
        foreach (AnimationKeyframeViewModel keyframe in keyframes)
        {
            AnimationTrackViewModel? track = keyframe.Owner;
            if (track is null)
            {
                continue;
            }

            int index = track.Keyframes.IndexOf(keyframe);
            removals.Add(new KeyframeRemoval(track, keyframe, index));
        }

        return removals;
    }

    private static void RemoveKeyframes(IEnumerable<KeyframeRemoval> removals)
    {
        foreach (KeyframeRemoval removal in removals)
        {
            removal.Track.Keyframes.Remove(removal.Keyframe);
        }
    }

    private static void RestoreKeyframes(IEnumerable<KeyframeRemoval> removals)
    {
        foreach (KeyframeRemoval removal in removals.OrderBy(r => r.Index))
        {
            InsertKeyframe(removal.Track, removal.Keyframe, removal.Index);
        }
    }
}

public sealed partial class AnimationTimelineViewModel : ReactiveObject
{
    [Reactive]
    public partial string Name { get; set; } = string.Empty;

    [Reactive]
    public partial string ResourceKey { get; set; } = string.Empty;

    [Reactive]
    public partial double DurationSeconds { get; set; } = 1.0;

    [Reactive]
    public partial double FrameRate { get; set; } = 60.0;

    public ObservableCollection<AnimationTrackViewModel> Tracks { get; } = new();
}

public sealed partial class AnimationTrackViewModel : ReactiveObject
{
    [Reactive]
    public partial string PropertyName { get; set; } = string.Empty;

    public ObservableCollection<AnimationKeyframeViewModel> Keyframes { get; } = new();
}

public sealed partial class AnimationKeyframeViewModel : ReactiveObject
{
    public AnimationTrackViewModel? Owner { get; init; }

    [Reactive]
    public partial double TimeSeconds { get; set; }

    [Reactive]
    public partial string Value { get; set; } = string.Empty;

    [Reactive]
    public partial string? Easing { get; set; }

    [Reactive]
    public partial bool IsSelected { get; set; }
}

public enum KeyframeSelectionMode
{
    Replace,
    Add,
    Range
}

public sealed partial class KeyframeSelectionRequest
{
    public KeyframeSelectionRequest(AnimationKeyframeViewModel keyframe, KeyframeSelectionMode mode, bool additive)
    {
        Keyframe = keyframe;
        Mode = mode;
        Additive = additive;
    }

    public AnimationKeyframeViewModel Keyframe { get; }

    public KeyframeSelectionMode Mode { get; }

    public bool Additive { get; }
}

public readonly struct KeyframeClipboardEntry
{
    public KeyframeClipboardEntry(double offsetSeconds, string value, string? easing)
    {
        OffsetSeconds = offsetSeconds;
        Value = value;
        Easing = easing;
    }

    public double OffsetSeconds { get; }

    public string Value { get; }

    public string? Easing { get; }
}

public readonly struct KeyframeRemoval
{
    public KeyframeRemoval(AnimationTrackViewModel track, AnimationKeyframeViewModel keyframe, int index)
    {
        Track = track;
        Keyframe = keyframe;
        Index = index;
    }

    public AnimationTrackViewModel Track { get; }

    public AnimationKeyframeViewModel Keyframe { get; }

    public int Index { get; }
}

public sealed partial class AnimationResourceScopeEntry
{
    public AnimationResourceScopeEntry(AnimationResourceScope scope, string displayName)
    {
        Scope = scope;
        DisplayName = displayName;
    }

    public AnimationResourceScope Scope { get; }

    public string DisplayName { get; }
}

public sealed partial class TimelineTickViewModel
{
    public TimelineTickViewModel(double timeSeconds, double positionPixels, string label, double height, double opacity, bool isMajor)
    {
        TimeSeconds = timeSeconds;
        PositionPixels = positionPixels;
        Label = label;
        Height = height;
        Opacity = opacity;
        IsMajor = isMajor;
    }

    public double TimeSeconds { get; }

    public double PositionPixels { get; }

    public string Label { get; }

    public double Height { get; }

    public double Opacity { get; }

    public bool IsMajor { get; }
}

public sealed partial class AnimationResourceEntryViewModel
{
    public AnimationResourceEntryViewModel(AnimationResourceSnapshot snapshot)
    {
        Snapshot = snapshot;
        DisplayName = snapshot.Key;
    }

    public AnimationResourceSnapshot Snapshot { get; }

    public string DisplayName { get; }
}

public readonly struct ValuePair<T>
{
    public ValuePair(T oldValue, T newValue)
    {
        OldValue = oldValue;
        NewValue = newValue;
    }

    public T OldValue { get; }

    public T NewValue { get; }
}
