using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.Animation;

public static class AnimationResourceWriter
{
    private const string AvaloniaNamespace = "https://github.com/avaloniaui";
    private const string XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static bool TryAddAnimationResource(
        MutableAstDocument document,
        AnimationResourceDefinition definition,
        out string? error)
    {
        error = null;

        if (document is null)
        {
            error = "Missing document.";
            return false;
        }

        if (document.Root is null)
        {
            error = "Document root is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(definition.Key))
        {
            error = "Animation key is required.";
            return false;
        }

        if (definition.Tracks is null || definition.Tracks.Count == 0)
        {
            error = "At least one track is required.";
            return false;
        }

        MutableAstObjectNode owner = ResolveScopeOwner(document, definition, out error);
        if (error is not null)
        {
            return false;
        }

        EnsureXNamespace(document);

        MutableAstObjectNode resourcesRoot = EnsureResourcesContainer(owner);
        MutableAstObjectNode animationNode = BuildAnimationNode(definition);
        resourcesRoot.Children.Add(animationNode);
        return true;
    }

    public static bool TryUpdateAnimationResource(
        MutableAstDocument document,
        AnimationResourceDefinition definition,
        AnimationResourceSnapshot snapshot,
        out string? error)
    {
        error = null;

        if (document is null)
        {
            error = "Missing document.";
            return false;
        }

        if (definition.Tracks is null || definition.Tracks.Count == 0)
        {
            error = "At least one track is required.";
            return false;
        }

        if (!TryFindResourceNode(document, snapshot, out MutableAstObjectNode resourcesRoot, out MutableAstObjectNode? existing, out int index, out error))
        {
            return false;
        }

        EnsureXNamespace(document);
        MutableAstObjectNode updated = BuildAnimationNode(definition);
        if (index < 0 || index >= resourcesRoot.Children.Count)
        {
            resourcesRoot.Children.Add(updated);
        }
        else
        {
            resourcesRoot.Children[index] = updated;
        }

        return true;
    }

    public static bool TryRenameAnimationResource(
        MutableAstDocument document,
        AnimationResourceSnapshot snapshot,
        string newKey,
        out string? error)
    {
        error = null;
        if (document is null)
        {
            error = "Missing document.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(newKey))
        {
            error = "New key is required.";
            return false;
        }

        if (!TryFindResourceNode(document, snapshot, out _, out MutableAstObjectNode? existing, out _, out error))
        {
            return false;
        }

        if (existing is null)
        {
            error = "Animation resource not found.";
            return false;
        }

        SetProperty(existing, "x:Key", newKey);
        return true;
    }

    public static bool TryDeleteAnimationResource(
        MutableAstDocument document,
        AnimationResourceSnapshot snapshot,
        out string? error)
    {
        error = null;
        if (document is null)
        {
            error = "Missing document.";
            return false;
        }

        if (!TryFindResourceNode(document, snapshot, out MutableAstObjectNode resourcesRoot, out MutableAstObjectNode? existing, out int index, out error))
        {
            return false;
        }

        if (existing is null)
        {
            error = "Animation resource not found.";
            return false;
        }

        if (index >= 0 && index < resourcesRoot.Children.Count)
        {
            resourcesRoot.Children.RemoveAt(index);
        }
        else
        {
            resourcesRoot.Children.Remove(existing);
        }

        return true;
    }

    private static MutableAstObjectNode ResolveScopeOwner(
        MutableAstDocument document,
        AnimationResourceDefinition definition,
        out string? error)
    {
        error = null;
        switch (definition.Scope)
        {
            case AnimationResourceScope.DocumentResources:
                return document.Root!;
            case AnimationResourceScope.SelectedElementResources:
                if (definition.ScopeOwner is MutableAstObjectNode selected)
                {
                    return selected;
                }

                error = "No selected element is available for resources.";
                return document.Root!;
            case AnimationResourceScope.StylesResources:
                return ResolveStylesOwner(document, definition, out error);
            default:
                error = "Unknown resource scope.";
                return document.Root!;
        }
    }

    private static MutableAstObjectNode ResolveStylesOwner(
        MutableAstDocument document,
        AnimationResourceDefinition definition,
        out string? error)
    {
        error = null;
        if (definition.ScopeOwner is MutableAstObjectNode selected)
        {
            if (IsStylesNode(selected))
            {
                return selected;
            }

            MutableAstObjectNode stylesNode = EnsureStylesNode(selected);
            return stylesNode;
        }

        if (document.Root is not null && IsStylesNode(document.Root))
        {
            return document.Root;
        }

        error = "No styles scope available for resources.";
        return document.Root!;
    }

    private static bool TryFindResourceNode(
        MutableAstDocument document,
        AnimationResourceSnapshot snapshot,
        out MutableAstObjectNode resourcesRoot,
        out MutableAstObjectNode? resourceNode,
        out int index,
        out string? error)
    {
        error = null;
        resourcesRoot = null!;
        resourceNode = null;
        index = -1;

        if (document.Root is null)
        {
            error = "Document root is missing.";
            return false;
        }

        MutableAstObjectNode? owner = snapshot.Scope switch
        {
            AnimationResourceScope.DocumentResources => document.Root,
            AnimationResourceScope.SelectedElementResources => FindNodeById(document.Root, snapshot.OwnerId),
            AnimationResourceScope.StylesResources => FindNodeById(document.Root, snapshot.OwnerId),
            _ => null
        };

        if (owner is null)
        {
            error = "Resource owner not found.";
            return false;
        }

        MutableAstObjectNode resourcesOwner = owner;
        if (snapshot.Scope == AnimationResourceScope.StylesResources)
        {
            MutableAstObjectNode? stylesNode = TryGetStylesNode(owner);
            if (stylesNode is null)
            {
                error = "Styles scope not found.";
                return false;
            }

            resourcesOwner = stylesNode;
        }

        MutableAstObjectNode? resourcesContainer = TryGetResourcesContainer(resourcesOwner);
        if (resourcesContainer is null)
        {
            error = "Resources container not found.";
            return false;
        }

        resourcesRoot = resourcesContainer;
        for (int i = 0; i < resourcesRoot.Children.Count; i++)
        {
            if (resourcesRoot.Children[i] is not MutableAstObjectNode child)
            {
                continue;
            }

            if (!string.Equals(child.TypeName, "Animation", StringComparison.Ordinal))
            {
                continue;
            }

            string? key = child.GetPropertyValue("x:Key") ?? child.GetPropertyValue("Key");
            if (!string.Equals(key, snapshot.Key, StringComparison.Ordinal))
            {
                continue;
            }

            resourceNode = child;
            index = i;
            return true;
        }

        error = "Animation resource not found.";
        return false;
    }

    private static MutableAstObjectNode? FindNodeById(MutableAstObjectNode node, Guid id)
    {
        if (node.Id == id)
        {
            return node;
        }

        foreach (MutableAstPropertyNode prop in node.Properties)
        {
            if (prop.Value is MutableAstObjectNode childObj)
            {
                MutableAstObjectNode? found = FindNodeById(childObj, id);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        foreach (MutableAstNode child in node.Children)
        {
            if (child is MutableAstObjectNode childObj)
            {
                MutableAstObjectNode? found = FindNodeById(childObj, id);
                if (found is not null)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static MutableAstObjectNode? TryGetStylesNode(MutableAstObjectNode owner)
    {
        MutableAstPropertyNode? stylesProp = owner.Properties.FirstOrDefault(p => p.PropertyName == "Styles");
        return stylesProp?.Value as MutableAstObjectNode;
    }

    private static MutableAstObjectNode? TryGetResourcesContainer(MutableAstObjectNode owner)
    {
        MutableAstPropertyNode? resourcesProp = owner.Properties.FirstOrDefault(p => p.PropertyName == "Resources");
        return resourcesProp?.Value as MutableAstObjectNode;
    }

    private static void SetProperty(MutableAstObjectNode node, string propertyName, string value)
    {
        MutableAstPropertyNode? prop = node.Properties.FirstOrDefault(p => p.PropertyName == propertyName);
        if (prop is null)
        {
            node.Properties.Add(new MutableAstPropertyNode
            {
                PropertyName = propertyName,
                Value = new MutableAstTextNode { Text = value }
            });
            return;
        }

        prop.Value = new MutableAstTextNode { Text = value };
    }

    private static void EnsureXNamespace(MutableAstDocument document)
    {
        if (!document.NamespaceAliases.ContainsKey("x"))
        {
            document.NamespaceAliases["x"] = XamlNamespace;
        }
    }

    private static MutableAstObjectNode EnsureStylesNode(MutableAstObjectNode owner)
    {
        MutableAstPropertyNode? stylesProp = owner.Properties.FirstOrDefault(p => p.PropertyName == "Styles");
        if (stylesProp?.Value is MutableAstObjectNode stylesNode)
        {
            return stylesNode;
        }

        MutableAstObjectNode created = new()
        {
            TypeName = "Styles",
            XmlNamespace = AvaloniaNamespace
        };

        if (stylesProp is null)
        {
            stylesProp = new MutableAstPropertyNode { PropertyName = "Styles", Value = created };
            owner.Properties.Add(stylesProp);
        }
        else
        {
            stylesProp.Value = created;
        }

        return created;
    }

    private static bool IsStylesNode(MutableAstObjectNode node)
    {
        return string.Equals(node.TypeName, "Styles", StringComparison.Ordinal);
    }

    private static MutableAstObjectNode EnsureResourcesContainer(MutableAstObjectNode owner)
    {
        MutableAstPropertyNode? resourcesProp = owner.Properties.FirstOrDefault(p => p.PropertyName == "Resources");
        if (resourcesProp?.Value is MutableAstObjectNode existing)
        {
            return existing;
        }

        MutableAstObjectNode resourceDictionary = new()
        {
            TypeName = "ResourceDictionary",
            XmlNamespace = AvaloniaNamespace
        };

        if (resourcesProp is null)
        {
            resourcesProp = new MutableAstPropertyNode { PropertyName = "Resources", Value = resourceDictionary };
            owner.Properties.Add(resourcesProp);
        }
        else
        {
            resourcesProp.Value = resourceDictionary;
        }

        return resourceDictionary;
    }

    private static MutableAstObjectNode BuildAnimationNode(AnimationResourceDefinition definition)
    {
        MutableAstObjectNode animation = new()
        {
            TypeName = "Animation",
            XmlNamespace = AvaloniaNamespace
        };

        animation.Properties.Add(new MutableAstPropertyNode
        {
            PropertyName = "x:Key",
            Value = new MutableAstTextNode { Text = definition.Key }
        });

        string durationText = TimeSpan.FromSeconds(Math.Max(0.0, definition.DurationSeconds))
            .ToString("c", CultureInfo.InvariantCulture);

        animation.Properties.Add(new MutableAstPropertyNode
        {
            PropertyName = "Duration",
            Value = new MutableAstTextNode { Text = durationText }
        });

        var cueGroups = new SortedDictionary<double, List<(string Property, AnimationKeyframeModel Keyframe)>>();
        foreach (AnimationTrackModel track in definition.Tracks)
        {
            if (string.IsNullOrWhiteSpace(track.PropertyName))
            {
                continue;
            }

            foreach (AnimationKeyframeModel keyframe in track.Keyframes)
            {
                double cue = ResolveCue(definition.DurationSeconds, keyframe.TimeSeconds);
                if (!cueGroups.TryGetValue(cue, out List<(string, AnimationKeyframeModel)>? entries))
                {
                    entries = new List<(string, AnimationKeyframeModel)>();
                    cueGroups[cue] = entries;
                }

                entries.Add((track.PropertyName, keyframe));
            }
        }

        foreach ((double cue, List<(string Property, AnimationKeyframeModel Keyframe)> entries) in cueGroups)
        {
            animation.Children.Add(BuildKeyFrameNode(cue, entries));
        }

        return animation;
    }

    private static MutableAstObjectNode BuildKeyFrameNode(
        double cue,
        List<(string Property, AnimationKeyframeModel Keyframe)> entries)
    {
        MutableAstObjectNode keyFrame = new()
        {
            TypeName = "KeyFrame",
            XmlNamespace = AvaloniaNamespace
        };

        string cueText = FormatCue(cue);
        keyFrame.Properties.Add(new MutableAstPropertyNode
        {
            PropertyName = "Cue",
            Value = new MutableAstTextNode { Text = cueText }
        });

        string? easing = entries.Select(entry => entry.Keyframe.Easing)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(easing))
        {
            keyFrame.Properties.Add(new MutableAstPropertyNode
            {
                PropertyName = "Easing",
                Value = new MutableAstTextNode { Text = easing! }
            });
        }

        foreach ((string property, AnimationKeyframeModel keyframe) in entries)
        {
            keyFrame.Children.Add(BuildSetterNode(property, keyframe.Value));
        }

        return keyFrame;
    }

    private static MutableAstObjectNode BuildSetterNode(string propertyName, string? value)
    {
        MutableAstObjectNode setter = new()
        {
            TypeName = "Setter",
            XmlNamespace = AvaloniaNamespace
        };

        setter.Properties.Add(new MutableAstPropertyNode
        {
            PropertyName = "Property",
            Value = new MutableAstTextNode { Text = propertyName }
        });

        setter.Properties.Add(new MutableAstPropertyNode
        {
            PropertyName = "Value",
            Value = new MutableAstTextNode { Text = value ?? string.Empty }
        });

        return setter;
    }

    private static double ResolveCue(double durationSeconds, double timeSeconds)
    {
        if (durationSeconds <= 0.0)
        {
            return 0.0;
        }

        double cue = timeSeconds / durationSeconds;
        return Math.Clamp(cue, 0.0, 1.0);
    }

    private static string FormatCue(double cue)
    {
        double percent = Math.Clamp(cue, 0.0, 1.0) * 100.0;
        return percent.ToString("0.###", CultureInfo.InvariantCulture) + "%";
    }
}
