using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;
using XamlVisualEditor.App;
using XamlVisualEditor.App.Views;
using XamlVisualEditor.Shell;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Terminal;
using XamlVisualEditor.Terminal.Avalonia.Controls;
using XamlVisualEditor.Terminal.Avalonia.Views;

namespace XamlVisualEditor.Tests.UI;

public sealed class TerminalControlTests
{
    [AvaloniaFact]
    public async Task TerminalControl_TextInput_SendsBytes()
    {
        TestTerminalSession session = new();
        TerminalViewModel vm = new(session);
        TerminalView view = new() { DataContext = vm };

        Window window = await ShowInWindowAsync(view);
        try
        {
            TerminalControl control = GetTerminalControl(view);
            TextInputEventArgs textInput = new()
            {
                RoutedEvent = InputElement.TextInputEvent,
                Source = control,
                Text = "abc"
            };

            control.RaiseEvent(textInput);
            await Dispatcher.UIThread.InvokeAsync(() => { });

            Assert.Equal("abc", session.GetWrittenText());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task TerminalControl_KeyDown_Enter_SendsCarriageReturn()
    {
        TestTerminalSession session = new();
        TerminalViewModel vm = new(session);
        TerminalView view = new() { DataContext = vm };

        Window window = await ShowInWindowAsync(view);
        try
        {
            TerminalControl control = GetTerminalControl(view);
            control.Focus();
            await Dispatcher.UIThread.InvokeAsync(() => { });

            PressKey(window, Key.Enter);
            await Dispatcher.UIThread.InvokeAsync(() => { });

            Assert.Contains("\r", session.GetWrittenText());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ViewLocator_TerminalTool_DataContextAsTool_StillProcessesInput()
    {
        TestTerminalSession session = new();
        TerminalViewModel vm = new(session);
        TerminalTool tool = new(vm);
        ViewLocator locator = new();
        Control view = locator.Build(tool);

        // Simulate docking host assigning the dockable as DataContext.
        view.DataContext = tool;

        Window window = await ShowInWindowAsync(view);
        try
        {
            TerminalControl control = GetTerminalControl(view);
            TextInputEventArgs textInput = new()
            {
                RoutedEvent = InputElement.TextInputEvent,
                Source = control,
                Text = "abc"
            };

            control.RaiseEvent(textInput);
            await Dispatcher.UIThread.InvokeAsync(() => { });

            Assert.Equal("abc", session.GetWrittenText());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task TerminalControl_MouseFocus_KeyDown_Enter_SendsCarriageReturn()
    {
        TestTerminalSession session = new();
        TerminalViewModel vm = new(session);
        TerminalView view = new() { DataContext = vm };

        Window window = await ShowInWindowAsync(view);
        try
        {
            TerminalControl control = GetTerminalControl(view);
            Point center = new(control.Bounds.Width / 2, control.Bounds.Height / 2);
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.LeftMouseButton);
            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            await Dispatcher.UIThread.InvokeAsync(() => { });

            PressKey(window, Key.Enter);
            await Dispatcher.UIThread.InvokeAsync(() => { });

            Assert.Contains("\r", session.GetWrittenText());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task TerminalToolView_DataContextSwap_RebindsResizeAndInputToNewTerminal()
    {
        TestTerminalSession session1 = new();
        TerminalViewModel vm1 = new(session1);
        TerminalTool tool1 = new(vm1);

        TestTerminalSession session2 = new();
        TerminalViewModel vm2 = new(session2);
        TerminalTool tool2 = new(vm2);

        TerminalToolView view = new() { DataContext = tool1 };
        Window window = await ShowInWindowAsync(view, width: 800, height: 600);
        try
        {
            TerminalControl control = GetTerminalControl(view);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            Assert.NotEmpty(session1.ResizeCalls);

            TextInputEventArgs textInputFirst = new()
            {
                RoutedEvent = InputElement.TextInputEvent,
                Source = control,
                Text = "one"
            };
            control.RaiseEvent(textInputFirst);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            Assert.Equal("one", session1.GetWrittenText());

            view.DataContext = tool2;
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            Assert.NotEmpty(session2.ResizeCalls);

            TextInputEventArgs textInputSecond = new()
            {
                RoutedEvent = InputElement.TextInputEvent,
                Source = control,
                Text = "two"
            };
            control.RaiseEvent(textInputSecond);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            Assert.Equal("one", session1.GetWrittenText());
            Assert.Equal("two", session2.GetWrittenText());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task TerminalControl_Resize_UpdatesSession()
    {
        TestTerminalSession session = new();
        TerminalViewModel vm = new(session);
        TerminalView view = new() { DataContext = vm };

        Window window = await ShowInWindowAsync(view, width: 640, height: 400);
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() => { });

            Assert.NotEmpty(session.ResizeCalls);
            (int Columns, int Rows) last = session.ResizeCalls[^1];
            Assert.True(last.Columns > 0);
            Assert.True(last.Rows > 0);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task TerminalControl_LogicalScrollable_ExtentReflectsScrollback()
    {
        TestTerminalSession session = new();
        TerminalViewModel vm = new(session);
        TerminalView view = new() { DataContext = vm };

        Window window = await ShowInWindowAsync(view, width: 640, height: 320);
        try
        {
            AppendLines(session.Emulator, 300);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            TerminalControl control = GetTerminalControl(view);
            IScrollable scrollable = control;

            Assert.True(scrollable.Extent.Height > scrollable.Viewport.Height);
            double maxOffset = scrollable.Extent.Height - scrollable.Viewport.Height;
            Assert.True(Math.Abs(scrollable.Offset.Y - maxOffset) < 0.001);
            Assert.Equal(0, vm.ScrollOffset);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task TerminalControl_LogicalScrollable_OffsetSetterUpdatesViewModel()
    {
        TestTerminalSession session = new();
        TerminalViewModel vm = new(session);
        TerminalView view = new() { DataContext = vm };

        Window window = await ShowInWindowAsync(view, width: 640, height: 320);
        try
        {
            AppendLines(session.Emulator, 300);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);

            TerminalControl control = GetTerminalControl(view);
            IScrollable scrollable = control;
            double maxOffset = scrollable.Extent.Height - scrollable.Viewport.Height;

            scrollable.Offset = new Vector(0, 0);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            Assert.Equal((int)Math.Round(maxOffset), vm.ScrollOffset);

            scrollable.Offset = new Vector(0, maxOffset);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            Assert.Equal(0, vm.ScrollOffset);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void TerminalViewModel_Selection_TriggersInvalidation()
    {
        TestTerminalSession session = new();
        TerminalViewModel vm = new(session);

        int invalidations = 0;
        vm.FrameInvalidated += () => invalidations++;

        vm.StartSelection(0, 0);
        vm.UpdateSelection(0, 2);
        vm.ClearSelection();

        Assert.Equal(3, invalidations);
    }

    private static TerminalControl GetTerminalControl(Control root)
    {
        TerminalControl? control = root.GetVisualDescendants().OfType<TerminalControl>().FirstOrDefault();
        Assert.NotNull(control);
        return control!;
    }

    private static async Task<Window> ShowInWindowAsync(Control control, double width = 800, double height = 600)
    {
        Window window = new()
        {
            Content = control,
            Width = width,
            Height = height
        };
        window.Show();
        await Dispatcher.UIThread.InvokeAsync(() => { });
        control.Focus();
        await Dispatcher.UIThread.InvokeAsync(() => { });
        return window;
    }

    private static void AppendLines(ITerminalEmulator emulator, int lineCount)
    {
        StringBuilder builder = new();
        for (int i = 0; i < lineCount; i++)
        {
            builder.Append("line ").Append(i).Append('\n');
        }

        byte[] data = Encoding.UTF8.GetBytes(builder.ToString());
        emulator.ProcessInput(data);
    }

    private static void PressKey(TopLevel window, Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPress(key, modifiers, MapPhysicalKey(key), string.Empty);
    }

    private static PhysicalKey MapPhysicalKey(Key key)
    {
        return key switch
        {
            Key.Enter => PhysicalKey.Enter,
            Key.Escape => PhysicalKey.Escape,
            Key.Tab => PhysicalKey.Tab,
            Key.Back => PhysicalKey.Backspace,
            _ => PhysicalKey.None
        };
    }

    private sealed class TestTerminalSession : ITerminalSession
    {
        public ITerminalEmulator Emulator { get; }
        public List<(int Columns, int Rows)> ResizeCalls { get; } = new();
        private readonly List<byte[]> _writes = new();

        public event Action? ScreenUpdated;
        public event Action<string>? TitleChanged;

        public TestTerminalSession(int columns = 80, int rows = 24)
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
            _writes.Add(data.ToArray());
        }

        public void Resize(int columns, int rows, int pixelWidth = 0, int pixelHeight = 0)
        {
            ResizeCalls.Add((columns, rows));
            Emulator.Resize(columns, rows);
        }

        public IReadOnlyList<TerminalCellPosition> ResizeWithMapping(int columns, int rows, IReadOnlyList<TerminalCellPosition> positions, int pixelWidth = 0, int pixelHeight = 0)
        {
            ResizeCalls.Add((columns, rows));
            return Emulator.ResizeWithMapping(columns, rows, positions);
        }

        public IReadOnlyList<TerminalCellPosition> ResizeWithMappingGlobal(int columns, int rows, IReadOnlyList<TerminalCellPosition> positions, int pixelWidth = 0, int pixelHeight = 0)
        {
            ResizeCalls.Add((columns, rows));
            return Emulator.ResizeWithMappingGlobal(columns, rows, positions);
        }

        public string GetWrittenText()
        {
            if (_writes.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new();
            foreach (byte[] chunk in _writes)
            {
                builder.Append(Encoding.UTF8.GetString(chunk));
            }
            return builder.ToString();
        }

        public void Dispose()
        {
        }
    }
}
