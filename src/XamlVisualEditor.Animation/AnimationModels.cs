using System;
using System.Collections.Generic;

namespace XamlVisualEditor.Animation;

public sealed class AnimationTimelineModel
{
    public string Name { get; set; } = "Timeline";

    public double DurationSeconds { get; set; } = 1.0;

    public double FrameRate { get; set; } = 60.0;

    public List<AnimationTrackModel> Tracks { get; } = new();
}

public sealed class AnimationTrackModel
{
    public string PropertyName { get; set; } = string.Empty;

    public List<AnimationKeyframeModel> Keyframes { get; } = new();
}

public sealed class AnimationKeyframeModel
{
    public double TimeSeconds { get; set; }

    public string Value { get; set; } = string.Empty;

    public string? Easing { get; set; }
}

public enum AnimationResourceScope
{
    DocumentResources,
    SelectedElementResources,
    StylesResources
}

public sealed class AnimationResourceDefinition
{
    public AnimationResourceDefinition(
        string key,
        double durationSeconds,
        IReadOnlyList<AnimationTrackModel> tracks,
        AnimationResourceScope scope,
        object? scopeOwner = null)
    {
        Key = key;
        DurationSeconds = durationSeconds;
        Tracks = tracks;
        Scope = scope;
        ScopeOwner = scopeOwner;
    }

    public string Key { get; }

    public double DurationSeconds { get; }

    public IReadOnlyList<AnimationTrackModel> Tracks { get; }

    public AnimationResourceScope Scope { get; }

    public object? ScopeOwner { get; }
}
