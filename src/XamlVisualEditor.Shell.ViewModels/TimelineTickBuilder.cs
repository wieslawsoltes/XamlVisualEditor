using System;
using System.Collections.Generic;
using System.Globalization;

namespace XamlVisualEditor.Shell.ViewModels;

public static class TimelineTickBuilder
{
    private static readonly double[] s_majorSteps =
    {
        0.1,
        0.25,
        0.5,
        1.0,
        2.0,
        5.0,
        10.0
    };

    public static IReadOnlyList<TimelineTickViewModel> BuildTicks(
        double durationSeconds,
        double pixelsPerSecond)
    {
        List<TimelineTickViewModel> ticks = new();
        double duration = Math.Max(0.0, durationSeconds);
        double pps = Math.Max(1.0, pixelsPerSecond);

        double majorStep = ResolveMajorStep(pps);
        double minorStep = ResolveMinorStep(majorStep, pps);

        double time = 0.0;
        while (time <= duration + 0.0001)
        {
            bool isMajor = IsMajorTick(time, majorStep);
            double height = isMajor ? 18.0 : 10.0;
            double opacity = isMajor ? 0.6 : 0.2;
            string label = isMajor ? FormatTime(time) : string.Empty;

            ticks.Add(new TimelineTickViewModel(
                timeSeconds: time,
                positionPixels: time * pps,
                label: label,
                height: height,
                opacity: opacity,
                isMajor: isMajor));

            time += minorStep;
        }

        return ticks;
    }

    private static double ResolveMajorStep(double pixelsPerSecond)
    {
        double targetSeconds = 80.0 / pixelsPerSecond;
        foreach (double step in s_majorSteps)
        {
            if (step >= targetSeconds)
            {
                return step;
            }
        }

        return s_majorSteps[^1];
    }

    private static double ResolveMinorStep(double majorStep, double pixelsPerSecond)
    {
        double minor = majorStep / 5.0;
        if (minor * pixelsPerSecond < 8.0)
        {
            minor = majorStep / 2.0;
        }

        if (minor * pixelsPerSecond < 8.0)
        {
            minor = majorStep;
        }

        return minor;
    }

    private static bool IsMajorTick(double timeSeconds, double majorStep)
    {
        if (majorStep <= 0.0)
        {
            return true;
        }

        double remainder = timeSeconds % majorStep;
        return remainder < 0.0001 || Math.Abs(remainder - majorStep) < 0.0001;
    }

    private static string FormatTime(double timeSeconds)
    {
        if (timeSeconds < 1.0)
        {
            return timeSeconds.ToString("0.0", CultureInfo.InvariantCulture) + "s";
        }

        return timeSeconds.ToString("0.#", CultureInfo.InvariantCulture) + "s";
    }
}
