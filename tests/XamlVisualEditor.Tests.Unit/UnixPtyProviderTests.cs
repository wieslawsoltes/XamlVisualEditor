using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using XamlVisualEditor.Terminal;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class UnixPtyProviderTests
{
    [Fact]
    public async Task ResizeUpdatesKernelWindowSize()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        TerminalSessionOptions options = new()
        {
            Columns = 80,
            Rows = 24,
            Command = "/bin/sh",
            Arguments = new[] { "-lc", "stty size; trap 'stty size' WINCH; while :; do sleep 1; done" }
        };

        using TerminalSession session = new(options, new UnixPtyProvider(), new ManagedTerminalEmulatorFactory());
        session.Start();

        bool initial = await WaitForTextAsync(session.Emulator, "24 80", TimeSpan.FromSeconds(3));
        Assert.True(initial, GetVisibleText(session.Emulator));

        session.Resize(100, 40);

        bool resized = await WaitForTextAsync(session.Emulator, "40 100", TimeSpan.FromSeconds(3));
        Assert.True(resized, GetVisibleText(session.Emulator));
    }

    private static async Task<bool> WaitForTextAsync(ITerminalEmulator emulator, string expected, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (GetVisibleText(emulator).Contains(expected, StringComparison.Ordinal))
            {
                return true;
            }

            await Task.Delay(50).ConfigureAwait(false);
        }

        return GetVisibleText(emulator).Contains(expected, StringComparison.Ordinal);
    }

    private static string GetVisibleText(ITerminalEmulator emulator)
    {
        StringBuilder builder = new();
        emulator.Read((buffer, _) =>
        {
            for (int row = 0; row < buffer.Rows; row++)
            {
                TerminalLine line = buffer.GetLine(row);
                for (int col = 0; col < buffer.Columns; col++)
                {
                    TerminalCell cell = line.Cells[col];
                    if (cell.Width == 0)
                    {
                        continue;
                    }

                    builder.Append(cell.Rune.ToString());
                }

                builder.Append('\n');
            }
        });

        return builder.ToString();
    }
}
