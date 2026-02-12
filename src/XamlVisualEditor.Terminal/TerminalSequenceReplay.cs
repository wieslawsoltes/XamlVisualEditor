using System;
using System.Collections.Generic;
using System.IO;

namespace XamlVisualEditor.Terminal;

public static class TerminalSequenceReplay
{
    public sealed class Entry
    {
        public string Direction { get; }
        public byte[] Data { get; }

        public Entry(string direction, byte[] data)
        {
            Direction = direction;
            Data = data;
        }
    }

    public static IReadOnlyList<Entry> Load(string filePath)
    {
        List<Entry> entries = new();
        foreach (string line in File.ReadLines(filePath))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            int space = trimmed.IndexOf(' ');
            if (space <= 0 || space == trimmed.Length - 1)
            {
                continue;
            }

            string direction = trimmed[..space];
            string payload = trimmed[(space + 1)..];
            byte[] data = Convert.FromBase64String(payload);
            entries.Add(new Entry(direction, data));
        }

        return entries;
    }

    public static void ReplayOutput(ITerminalEmulator emulator, IReadOnlyList<Entry> entries)
    {
        foreach (Entry entry in entries)
        {
            if (!string.Equals(entry.Direction, "OUT", StringComparison.Ordinal))
            {
                continue;
            }

            emulator.ProcessInput(entry.Data);
        }
    }
}
