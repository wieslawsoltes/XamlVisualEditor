using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Styling;
using XamlVisualEditor.Animation;
using XamlVisualEditor.Designer.Core;
using XamlVisualEditor.Shell.ViewModels;

namespace XamlVisualEditor.App.Services;

public sealed class AnimationPreviewService : IAnimationPreviewService
{
    private CancellationTokenSource? _previewCts;
    private Control? _activeControl;

    public bool TryPlayPreview(
        DesignSurfaceViewModel designSurface,
        Guid? targetNodeId,
        AnimationTimelineModel timeline,
        out string? error)
    {
        error = null;

        if (designSurface is null)
        {
            error = "Design surface is not available.";
            return false;
        }

        if (targetNodeId is null)
        {
            error = "Select a target element to preview.";
            return false;
        }

        if (timeline.Tracks.Count == 0)
        {
            error = "Add at least one track to preview.";
            return false;
        }

        if (!TryResolveControl(designSurface, targetNodeId.Value, out Control control))
        {
            error = "Unable to locate the target element for preview.";
            return false;
        }

        Avalonia.Animation.Animation animation = BuildAnimation(control, timeline, out error);
        if (error is not null)
        {
            return false;
        }

        StopPreview();
        _activeControl = control;
        _previewCts = new CancellationTokenSource();

        _ = animation.RunAsync(control, _previewCts.Token);
        return true;
    }

    public void StopPreview()
    {
        if (_previewCts is not null)
        {
            _previewCts.Cancel();
            _previewCts.Dispose();
            _previewCts = null;
        }

        _activeControl = null;
    }

    private static bool TryResolveControl(DesignSurfaceViewModel surface, Guid nodeId, out Control control)
    {
        control = null!;
        if (!surface.ItemMap.TryGetValue(nodeId, out DesignItem? item) || item?.VisualElement is null)
        {
            return false;
        }

        control = item.VisualElement;
        return true;
    }

    private static Avalonia.Animation.Animation BuildAnimation(Control control, AnimationTimelineModel timeline, out string? error)
    {
        error = null;
        Avalonia.Animation.Animation animation = new()
        {
            Duration = TimeSpan.FromSeconds(Math.Max(0.0, timeline.DurationSeconds))
        };

        Dictionary<double, List<(string Property, AnimationKeyframeModel Keyframe)>> cueGroups = new();
        foreach (AnimationTrackModel track in timeline.Tracks)
        {
            if (string.IsNullOrWhiteSpace(track.PropertyName))
            {
                continue;
            }

            foreach (AnimationKeyframeModel keyframe in track.Keyframes)
            {
                double cue = ResolveCue(timeline.DurationSeconds, keyframe.TimeSeconds);
                if (!cueGroups.TryGetValue(cue, out List<(string, AnimationKeyframeModel)>? entries))
                {
                    entries = new List<(string, AnimationKeyframeModel)>();
                    cueGroups[cue] = entries;
                }

                entries.Add((track.PropertyName, keyframe));
            }
        }

        foreach (KeyValuePair<double, List<(string Property, AnimationKeyframeModel Keyframe)>> group in cueGroups)
        {
            KeyFrame keyFrame = new()
            {
                Cue = new Cue(group.Key)
            };

            string? easing = group.Value.Select(entry => entry.Keyframe.Easing)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
            if (TryResolveKeySpline(easing, out KeySpline? keySpline))
            {
                keyFrame.KeySpline = keySpline;
            }

            foreach ((string propertyName, AnimationKeyframeModel keyframe) in group.Value)
            {
                if (!TryResolveProperty(control, propertyName, out AvaloniaProperty property))
                {
                    continue;
                }

                if (!AnimationValueParser.TryParse(keyframe.Value, property.PropertyType, out object? value))
                {
                    if (!AnimationValueParser.TryGetFallbackValue(keyframe.Value, control, property, out value))
                    {
                        continue;
                    }
                }

                keyFrame.Setters.Add(new Setter(property, value));
            }

            if (keyFrame.Setters.Count > 0)
            {
                animation.Children.Add(keyFrame);
            }
        }

        if (animation.Children.Count == 0)
        {
            error = "No valid keyframes could be created for preview.";
        }

        return animation;
    }

    private static bool TryResolveProperty(Control control, string propertyName, out AvaloniaProperty property)
    {
        property = null!;
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        IReadOnlyList<AvaloniaProperty> properties = AvaloniaPropertyRegistry.Instance.GetRegistered(control.GetType());
        AvaloniaProperty? resolved = properties.FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.Ordinal));
        if (resolved is null)
        {
            return false;
        }

        property = resolved;
        return true;
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
    private static bool TryResolveKeySpline(string? easing, out KeySpline? keySpline)
    {
        keySpline = null;
        if (string.IsNullOrWhiteSpace(easing))
        {
            return false;
        }

        string token = easing.Trim();
        if (token.StartsWith("cubic-bezier(", StringComparison.OrdinalIgnoreCase) && token.EndsWith(")", StringComparison.Ordinal))
        {
            string args = token.Substring("cubic-bezier(".Length, token.Length - "cubic-bezier(".Length - 1);
            return TryParseKeySpline(args, out keySpline);
        }

        string keyword = token.ToLowerInvariant();
        return keyword switch
        {
            "linear" => false,
            "ease" => TryParseKeySpline("0.25,0.1,0.25,1", out keySpline),
            "easein" => TryParseKeySpline("0.42,0,1,1", out keySpline),
            "easeout" => TryParseKeySpline("0,0,0.58,1", out keySpline),
            "easeinout" => TryParseKeySpline("0.42,0,0.58,1", out keySpline),
            _ => TryParseKeySpline(token, out keySpline)
        };
    }

    private static bool TryParseKeySpline(string text, out KeySpline? keySpline)
    {
        keySpline = null;
        try
        {
            KeySpline parsed = KeySpline.Parse(text, CultureInfo.InvariantCulture);
            if (parsed.IsValid())
            {
                keySpline = parsed;
                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}

internal static class AnimationValueParser
{
    public static bool TryParse(string? text, Type targetType, out object? value)
    {
        value = null;
        if (targetType == typeof(string))
        {
            value = text ?? string.Empty;
            return true;
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (text.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return TryParseMarkupExtension(text, out value);
        }

        Type effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (effectiveType != targetType)
        {
            return TryParse(text, effectiveType, out value);
        }

        if (targetType == typeof(double))
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                value = result;
                return true;
            }
        }

        if (targetType == typeof(float))
        {
            if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            {
                value = result;
                return true;
            }
        }

        if (targetType == typeof(int))
        {
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            {
                value = result;
                return true;
            }
        }

        if (targetType == typeof(bool))
        {
            if (bool.TryParse(text, out bool result))
            {
                value = result;
                return true;
            }
        }

        if (targetType == typeof(TimeSpan))
        {
            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan result))
            {
                value = result;
                return true;
            }
        }

        if (targetType.IsEnum)
        {
            if (Enum.TryParse(targetType, text, true, out object? result))
            {
                value = result;
                return true;
            }
        }

        if (targetType == typeof(Color))
        {
            if (Color.TryParse(text, out Color color))
            {
                value = color;
                return true;
            }
        }

        if (typeof(IBrush).IsAssignableFrom(targetType))
        {
            if (Color.TryParse(text, out Color brushColor))
            {
                value = new SolidColorBrush(brushColor);
                return true;
            }
        }

        if (targetType == typeof(Thickness))
        {
            if (TryParseThickness(text, out Thickness thickness))
            {
                value = thickness;
                return true;
            }
        }

        if (targetType == typeof(GridLength))
        {
            if (TryParseGridLength(text, out GridLength gridLength))
            {
                value = gridLength;
                return true;
            }
        }

        if (targetType == typeof(CornerRadius))
        {
            if (TryParseCornerRadius(text, out CornerRadius cornerRadius))
            {
                value = cornerRadius;
                return true;
            }
        }

        if (targetType == typeof(Point))
        {
            if (TryParseVector2(text, out double x, out double y))
            {
                value = new Point(x, y);
                return true;
            }
        }

        if (targetType == typeof(Size))
        {
            if (TryParseVector2(text, out double width, out double height))
            {
                value = new Size(width, height);
                return true;
            }
        }

        if (targetType == typeof(Rect))
        {
            if (TryParseVector4(text, out double x, out double y, out double w, out double h))
            {
                value = new Rect(x, y, w, h);
                return true;
            }
        }

        return false;
    }

    public static bool TryGetFallbackValue(string? text, Control control, AvaloniaProperty property, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (!text.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return false;
        }

        object? current = control.GetValue(property);
        if (current is null || current == AvaloniaProperty.UnsetValue)
        {
            return false;
        }

        value = current;
        return true;
    }

    private static bool TryParseMarkupExtension(string text, out object? value)
    {
        value = null;
        string trimmed = text.Trim();
        if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
        {
            return false;
        }

        string inner = trimmed.Substring(1, trimmed.Length - 2).Trim();
        if (inner.StartsWith("Binding", StringComparison.OrdinalIgnoreCase))
        {
            string args = inner.Substring("Binding".Length).Trim();
            string path = ParseBindingPath(args);
            value = new Binding { Path = path };
            return true;
        }

        return false;
    }

    private static string ParseBindingPath(string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return string.Empty;
        }

        string trimmed = args.TrimStart();
        if (trimmed.StartsWith("Path=", StringComparison.OrdinalIgnoreCase))
        {
            string path = trimmed.Substring("Path=".Length).Trim();
            int commaIndex = path.IndexOf(',', StringComparison.Ordinal);
            if (commaIndex >= 0)
            {
                path = path.Substring(0, commaIndex);
            }

            return path.Trim();
        }

        int splitIndex = trimmed.IndexOf(',', StringComparison.Ordinal);
        return splitIndex >= 0 ? trimmed.Substring(0, splitIndex).Trim() : trimmed;
    }

    private static bool TryParseThickness(string? text, out Thickness thickness)
    {
        thickness = default;
        if (!TryParseVector4(text, out double left, out double top, out double right, out double bottom))
        {
            if (!TryParseVector2(text, out double uniform, out double uniformY))
            {
                return false;
            }

            if (Math.Abs(uniform - uniformY) < 0.0001)
            {
                thickness = new Thickness(uniform);
                return true;
            }

            thickness = new Thickness(uniform, uniformY, uniform, uniformY);
            return true;
        }

        thickness = new Thickness(left, top, right, bottom);
        return true;
    }

    private static bool TryParseCornerRadius(string? text, out CornerRadius radius)
    {
        radius = default;
        if (!TryParseVector4(text, out double tl, out double tr, out double br, out double bl))
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double uniform))
            {
                radius = new CornerRadius(uniform);
                return true;
            }

            return false;
        }

        radius = new CornerRadius(tl, tr, br, bl);
        return true;
    }

    private static bool TryParseVector2(string? text, out double x, out double y)
    {
        x = 0.0;
        y = 0.0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            if (double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double uniform))
            {
                x = uniform;
                y = uniform;
                return true;
            }
            return false;
        }

        if (parts.Length >= 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double px) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double py))
        {
            x = px;
            y = py;
            return true;
        }

        return false;
    }

    private static bool TryParseVector4(string? text, out double a, out double b, out double c, out double d)
    {
        a = 0.0;
        b = 0.0;
        c = 0.0;
        d = 0.0;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 4 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double p1) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double p2) &&
            double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double p3) &&
            double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double p4))
        {
            a = p1;
            b = p2;
            c = p3;
            d = p4;
            return true;
        }

        return false;
    }

    private static bool TryParseGridLength(string? text, out GridLength length)
    {
        length = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            length = GridLength.Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
