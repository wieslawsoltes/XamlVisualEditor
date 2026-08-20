using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Xunit;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Terminal;
using XamlVisualEditor.Terminal.Avalonia.Controls;

namespace XamlVisualEditor.Tests.UI;

public sealed class TerminalGoldenRenderTests
{
    private const string GoldenHashPath = "TestData/terminal/basic.sha256";
    private const string SequencePath = "TestData/terminal/basic.seq";

    [AvaloniaFact]
    public async Task BasicSequence_RenderHash_MatchesGolden()
    {
        TestTerminalSession session = new();
        TerminalViewModel vm = new(session);
        TerminalControl control = new()
        {
            Width = 640,
            Height = 320,
            TerminalViewModel = vm
        };

        Window window = await ShowInWindowAsync(control);
        try
        {
            string sequenceFile = Path.Combine(AppContext.BaseDirectory, SequencePath);
            string goldenFile = Path.Combine(AppContext.BaseDirectory, GoldenHashPath);

            IReadOnlyList<TerminalSequenceReplay.Entry> entries = TerminalSequenceReplay.Load(sequenceFile);
            TerminalSequenceReplay.ReplayOutput(session.Emulator, entries);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            string hash = await RenderHashAsync(control);
            bool update = Environment.GetEnvironmentVariable("TERMINAL_GOLDEN_UPDATE") == "1";
            if (!File.Exists(goldenFile))
            {
                if (update)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(goldenFile) ?? ".");
                    File.WriteAllText(goldenFile, hash);
                    return;
                }

                Assert.Fail($"Golden hash missing at {goldenFile}. Set TERMINAL_GOLDEN_UPDATE=1 to generate.");
            }

            string expected = File.ReadAllText(goldenFile).Trim();
            if (string.Equals(expected, "__PLACEHOLDER__", StringComparison.Ordinal))
            {
                if (update)
                {
                    File.WriteAllText(goldenFile, hash);
                    return;
                }

                return;
            }

            Assert.Equal(expected, hash);
        }
        finally
        {
            window.Close();
        }
    }

    private static async Task<string> RenderHashAsync(Control control)
    {
        RenderTargetBitmap rtb = new(new PixelSize((int)control.Bounds.Width, (int)control.Bounds.Height));
        await Dispatcher.UIThread.InvokeAsync(() => rtb.Render(control));
        using Stream stream = new MemoryStream();
#pragma warning disable CS0618
        rtb.Save(stream);
#pragma warning restore CS0618
        stream.Position = 0;
        using SHA256 sha = SHA256.Create();
        byte[] hash = sha.ComputeHash(stream);
        StringBuilder builder = new();
        foreach (byte b in hash)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }

    private static async Task<Window> ShowInWindowAsync(Control control)
    {
        Window window = new()
        {
            Content = control,
            Width = control.Width,
            Height = control.Height
        };

        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { });
        return window;
    }

    private sealed class TestTerminalSession : ITerminalSession
    {
        public ITerminalEmulator Emulator { get; }

#pragma warning disable CS0067
        public event Action? ScreenUpdated;
        public event Action<string>? TitleChanged;
        public event Action<ReadOnlyMemory<byte>>? OutputReceived;
        public event Action<int?>? Exited;
#pragma warning restore CS0067

        public TestTerminalSession(int columns = 120, int rows = 40)
        {
            Emulator = new TerminalEmulator(columns, rows);
            Emulator.ScreenUpdated += () => ScreenUpdated?.Invoke();
            Emulator.TitleChanged += title => TitleChanged?.Invoke(title);
        }

        public void Start()
        {
        }

        public void Write(ReadOnlySpan<byte> data)
        {
        }

        public void Resize(int columns, int rows, int pixelWidth = 0, int pixelHeight = 0)
        {
            Emulator.Resize(columns, rows);
        }

        public IReadOnlyList<TerminalCellPosition> ResizeWithMapping(int columns, int rows, IReadOnlyList<TerminalCellPosition> positions, int pixelWidth = 0, int pixelHeight = 0)
        {
            return Emulator.ResizeWithMapping(columns, rows, positions);
        }

        public IReadOnlyList<TerminalCellPosition> ResizeWithMappingGlobal(int columns, int rows, IReadOnlyList<TerminalCellPosition> positions, int pixelWidth = 0, int pixelHeight = 0)
        {
            return Emulator.ResizeWithMappingGlobal(columns, rows, positions);
        }

        public void Dispose()
        {
        }
    }
}
