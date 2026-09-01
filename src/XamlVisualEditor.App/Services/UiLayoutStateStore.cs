using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Serilog;

namespace XamlVisualEditor.App.Services;

internal sealed class UiLayoutState
{
    public double WindowWidth { get; set; } = 1400;

    public double WindowHeight { get; set; } = 900;

    public int? WindowX { get; set; }

    public int? WindowY { get; set; }

    public bool WindowMaximized { get; set; }

    public Dictionary<string, SplitterLayoutState> Splitters { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class SplitterLayoutState
{
    public double ColumnRatio { get; set; } = 0.5;

    public double RowRatio { get; set; } = 0.5;
}

internal static class UiLayoutStateStore
{
    private const double MinimumWindowWidth = 800;
    private const double MinimumWindowHeight = 600;
    private const double MinimumSplitRatio = 0.1;
    private const double MaximumSplitRatio = 0.9;
    public static UiLayoutState Load()
    {
        return LoadCore(GetPath());
    }

    public static void Update(Action<UiLayoutState> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        string path = GetPath();
        UiLayoutState state = LoadCore(path);
        update(state);
        Normalize(state);

        try
        {
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, Serialize(state));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to save UI layout state");
        }
    }

    internal static string Serialize(UiLayoutState state)
    {
        Normalize(state);
        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }

    internal static UiLayoutState Deserialize(string json)
    {
        UiLayoutState state = JsonSerializer.Deserialize<UiLayoutState>(json) ?? new UiLayoutState();
        state.Splitters ??= new Dictionary<string, SplitterLayoutState>(StringComparer.Ordinal);
        Normalize(state);
        return state;
    }

    internal static double NormalizeRatio(double ratio)
    {
        if (!double.IsFinite(ratio))
        {
            return 0.5;
        }

        return Math.Clamp(ratio, MinimumSplitRatio, MaximumSplitRatio);
    }

    private static UiLayoutState LoadCore(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new UiLayoutState();
            }

            return Deserialize(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load UI layout state");
            return new UiLayoutState();
        }
    }

    private static void Normalize(UiLayoutState state)
    {
        if (!double.IsFinite(state.WindowWidth) || state.WindowWidth < MinimumWindowWidth)
        {
            state.WindowWidth = 1400;
        }

        if (!double.IsFinite(state.WindowHeight) || state.WindowHeight < MinimumWindowHeight)
        {
            state.WindowHeight = 900;
        }

        // A stored position is only usable as a complete pair.
        if (state.WindowX is null || state.WindowY is null)
        {
            state.WindowX = null;
            state.WindowY = null;
        }

        foreach (SplitterLayoutState splitter in state.Splitters.Values)
        {
            splitter.ColumnRatio = NormalizeRatio(splitter.ColumnRatio);
            splitter.RowRatio = NormalizeRatio(splitter.RowRatio);
        }
    }

    private static string GetPath()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Combine(appData, "XamlVisualEditor", "ui-layout.json");
    }
}
