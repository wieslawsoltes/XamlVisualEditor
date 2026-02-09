using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.Animation;

public static class AnimationResourceReader
{
    public static IReadOnlyList<AnimationResourceSnapshot> LoadAnimations(MutableAstDocument document)
    {
        List<AnimationResourceSnapshot> results = new();
        if (document.Root is null)
        {
            return results;
        }

        Traverse(document.Root, owner: document.Root, scope: AnimationResourceScope.DocumentResources, results);
        return results;
    }

    private static void Traverse(
        MutableAstObjectNode node,
        MutableAstObjectNode owner,
        AnimationResourceScope scope,
        List<AnimationResourceSnapshot> results)
    {
        foreach (MutableAstPropertyNode prop in node.Properties)
        {
            if (string.Equals(prop.PropertyName, "Resources", StringComparison.Ordinal) && prop.Value is MutableAstObjectNode resourcesNode)
            {
                foreach (MutableAstNode child in resourcesNode.Children)
                {
                    if (child is MutableAstObjectNode childObj)
                    {
                        ReadAnimationNode(childObj, owner, scope, results);
                        Traverse(childObj, owner, scope, results);
                    }
                }
            }

            if (string.Equals(prop.PropertyName, "Styles", StringComparison.Ordinal) && prop.Value is MutableAstObjectNode stylesNode)
            {
                Traverse(stylesNode, node, AnimationResourceScope.StylesResources, results);
            }
        }

        foreach (MutableAstNode child in node.Children)
        {
            if (child is MutableAstObjectNode childObj)
            {
                Traverse(childObj, childObj, AnimationResourceScope.SelectedElementResources, results);
            }
        }
    }

    private static void ReadAnimationNode(
        MutableAstObjectNode node,
        MutableAstObjectNode owner,
        AnimationResourceScope scope,
        List<AnimationResourceSnapshot> results)
    {
        if (!string.Equals(node.TypeName, "Animation", StringComparison.Ordinal))
        {
            return;
        }

        string? key = node.GetPropertyValue("x:Key") ?? node.GetPropertyValue("Key");
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        double durationSeconds = 0.0;
        string? durationText = node.GetPropertyValue("Duration");
        if (TimeSpan.TryParse(durationText, CultureInfo.InvariantCulture, out TimeSpan duration))
        {
            durationSeconds = duration.TotalSeconds;
        }

        AnimationTimelineModel timeline = new()
        {
            Name = key,
            DurationSeconds = durationSeconds
        };

        Dictionary<string, AnimationTrackModel> tracks = new(StringComparer.Ordinal);
        foreach (MutableAstNode child in node.Children)
        {
            if (child is not MutableAstObjectNode keyFrame || !string.Equals(keyFrame.TypeName, "KeyFrame", StringComparison.Ordinal))
            {
                continue;
            }

            double timeSeconds = ResolveTimeFromCue(keyFrame.GetPropertyValue("Cue"), durationSeconds);
            string? easing = keyFrame.GetPropertyValue("Easing");

            foreach (MutableAstNode setterNode in keyFrame.Children)
            {
                if (setterNode is not MutableAstObjectNode setter || !string.Equals(setter.TypeName, "Setter", StringComparison.Ordinal))
                {
                    continue;
                }

                string? propertyName = setter.GetPropertyValue("Property");
                string? valueText = setter.GetPropertyValue("Value");
                if (string.IsNullOrWhiteSpace(propertyName))
                {
                    continue;
                }

                if (!tracks.TryGetValue(propertyName, out AnimationTrackModel? track))
                {
                    track = new AnimationTrackModel { PropertyName = propertyName };
                    tracks[propertyName] = track;
                }

                track.Keyframes.Add(new AnimationKeyframeModel
                {
                    TimeSeconds = timeSeconds,
                    Value = valueText ?? string.Empty,
                    Easing = easing
                });
            }
        }

        foreach (AnimationTrackModel track in tracks.Values)
        {
            timeline.Tracks.Add(track);
        }

        results.Add(new AnimationResourceSnapshot(key, timeline, scope, owner.Id));
    }

    private static double ResolveTimeFromCue(string? cueText, double durationSeconds)
    {
        if (string.IsNullOrWhiteSpace(cueText) || durationSeconds <= 0.0)
        {
            return 0.0;
        }

        string trimmed = cueText.Trim();
        if (trimmed.EndsWith("%", StringComparison.Ordinal))
        {
            string percentText = trimmed.TrimEnd('%');
            if (double.TryParse(percentText, NumberStyles.Float, CultureInfo.InvariantCulture, out double percent))
            {
                return Math.Clamp(percent / 100.0, 0.0, 1.0) * durationSeconds;
            }
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double cue))
        {
            return Math.Clamp(cue, 0.0, 1.0) * durationSeconds;
        }

        return 0.0;
    }
}

public sealed class AnimationResourceSnapshot
{
    public AnimationResourceSnapshot(string key, AnimationTimelineModel timeline, AnimationResourceScope scope, Guid ownerId)
    {
        Key = key;
        Timeline = timeline;
        Scope = scope;
        OwnerId = ownerId;
    }

    public string Key { get; }

    public AnimationTimelineModel Timeline { get; }

    public AnimationResourceScope Scope { get; }

    public Guid OwnerId { get; }
}
