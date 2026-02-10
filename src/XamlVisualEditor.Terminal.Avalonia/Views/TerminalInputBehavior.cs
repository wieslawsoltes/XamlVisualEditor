using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using XamlVisualEditor.Shell.ViewModels;
using XamlVisualEditor.Terminal;

namespace XamlVisualEditor.Terminal.Avalonia.Views;

public sealed class TerminalInputBehavior
{
    private static readonly ConditionalWeakTable<Control, SpaceInputState> s_spaceState = new();
    private static readonly ConditionalWeakTable<Control, ClipboardSubscription> s_clipboardSubscriptions = new();

    public static readonly AttachedProperty<TerminalViewModel?> ViewModelProperty =
        AvaloniaProperty.RegisterAttached<TerminalInputBehavior, Control, TerminalViewModel?>("ViewModel");

    static TerminalInputBehavior()
    {
        ViewModelProperty.Changed.AddClassHandler<Control>(OnViewModelChanged);
    }

    public static TerminalViewModel? GetViewModel(Control control)
    {
        return control.GetValue(ViewModelProperty);
    }

    public static void SetViewModel(Control control, TerminalViewModel? value)
    {
        control.SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.NewValue is TerminalViewModel && e.OldValue is null)
        {
            control.Focusable = true;
            control.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
            control.AddHandler(InputElement.TextInputEvent, OnTextInput, RoutingStrategies.Bubble);
            control.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            control.AddHandler(InputElement.PointerMovedEvent, OnPointerMoved, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            control.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            control.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheel, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            control.Focus();
            AttachClipboard(control, (TerminalViewModel)e.NewValue);
        }
        else if (e.NewValue is null && e.OldValue is TerminalViewModel)
        {
            control.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            control.RemoveHandler(InputElement.TextInputEvent, OnTextInput);
            control.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            control.RemoveHandler(InputElement.PointerMovedEvent, OnPointerMoved);
            control.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
            control.RemoveHandler(InputElement.PointerWheelChangedEvent, OnPointerWheel);
            DetachClipboard(control);
        }
    }

    private static void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        if (e.Text == " " && s_spaceState.TryGetValue(control, out SpaceInputState? state) && state.SuppressNextTextInput)
        {
            state.SuppressNextTextInput = false;
            e.Handled = true;
            return;
        }

        TerminalViewModel? vm = GetViewModel(control);
        if (vm is null)
        {
            return;
        }

        vm.ResetScrollback();

        if (!string.IsNullOrEmpty(e.Text))
        {
            vm.SendText(e.Text);
            e.Handled = true;
        }
    }

    private static async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        TerminalViewModel? vm = GetViewModel(control);
        if (vm is null)
        {
            return;
        }

        vm.ResetScrollback();

        if (IsClipboardCopy(e))
        {
            string text = vm.GetSelectedText();
            if (!string.IsNullOrEmpty(text))
            {
                await SetClipboardAsync(control, text);
            }
            e.Handled = true;
            return;
        }

        if (IsClipboardPaste(e))
        {
            string? text = await GetClipboardAsync(control);
            if (!string.IsNullOrEmpty(text))
            {
                await vm.SendPasteAsync(text).ConfigureAwait(false);
            }
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Space && e.KeyModifiers == KeyModifiers.None)
        {
            TerminalViewModel? vmSpace = GetViewModel(control);
            if (vmSpace is not null)
            {
                SpaceInputState state = s_spaceState.GetOrCreateValue(control);
                state.SuppressNextTextInput = true;
                vmSpace.SendText(" ");
                e.Handled = true;
                return;
            }
        }

        TerminalKeyInfo keyInfo = MapKey(e.Key, e.KeyModifiers);
        if (keyInfo.Key != TerminalKey.Unknown)
        {
            vm.SendKey(keyInfo);
            e.Handled = true;
            return;
        }

        if (TryGetControlChar(e, out char printable))
        {
            vm.SendText(printable.ToString());
            e.Handled = true;
        }
    }

    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        control.Focus();
        TerminalViewModel? vm = GetViewModel(control);
        if (vm is null)
        {
            return;
        }

        if (e.GetCurrentPoint(control).Properties.IsRightButtonPressed)
        {
            string text = vm.GetSelectedText();
            if (!string.IsNullOrEmpty(text))
            {
                _ = SetClipboardAsync(control, text);
                e.Handled = true;
            }
            return;
        }

        Point pos = e.GetPosition(control);
        if (e.GetCurrentPoint(control).Properties.IsLeftButtonPressed
            && vm.TryGetCellFromPoint(pos.X, pos.Y, out int row, out int col))
        {
            vm.StartSelection(row, col);
            bool sendMouseReport = vm.Emulator.State.MouseMode != TerminalMouseMode.None && !vm.HasSelection;
            if (sendMouseReport && e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            {
                vm.SendMouseReport(row, col, TerminalMouseButton.Left, TerminalMouseAction.Press);
            }
        }
    }

    private static void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        TerminalViewModel? vm = GetViewModel(control);
        if (vm is null)
        {
            return;
        }

        Point pos = e.GetPosition(control);
        if (vm.TryGetCellFromPoint(pos.X, pos.Y, out int row, out int col))
        {
            if (e.GetCurrentPoint(control).Properties.IsLeftButtonPressed)
            {
                vm.UpdateSelection(row, col);
                if (!vm.HasSelection && vm.Emulator.State.MouseMode != TerminalMouseMode.None)
                {
                    vm.SendMouseReport(row, col, TerminalMouseButton.Left, TerminalMouseAction.Drag);
                }
            }
            else if (!vm.HasSelection && vm.Emulator.State.MouseMode != TerminalMouseMode.None)
            {
                vm.SendMouseReport(row, col, TerminalMouseButton.None, TerminalMouseAction.Move);
            }
        }
    }

    private static void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        TerminalViewModel? vm = GetViewModel(control);
        if (vm is null)
        {
            return;
        }

        Point pos = e.GetPosition(control);
        if (e.InitialPressMouseButton == MouseButton.Left
            && vm.TryGetCellFromPoint(pos.X, pos.Y, out int row, out int col))
        {
            vm.UpdateSelection(row, col);
            if (e.InitialPressMouseButton == MouseButton.Left && !vm.HasSelection && vm.Emulator.State.MouseMode != TerminalMouseMode.None)
            {
                vm.SendMouseReport(row, col, TerminalMouseButton.Left, TerminalMouseAction.Release);
            }
        }
    }

    private static void OnPointerWheel(object? sender, PointerWheelEventArgs e)
    {
        if (sender is not Control control)
        {
            return;
        }

        TerminalViewModel? vm = GetViewModel(control);
        if (vm is null)
        {
            return;
        }

        if (vm.Emulator.State.MouseMode != TerminalMouseMode.None)
        {
            Point pos = e.GetPosition(control);
            if (vm.TryGetCellFromPoint(pos.X, pos.Y, out int row, out int col))
            {
                TerminalMouseButton button = e.Delta.Y < 0 ? TerminalMouseButton.WheelDown : TerminalMouseButton.WheelUp;
                vm.SendMouseReport(row, col, button, TerminalMouseAction.Press);
                vm.SendMouseReport(row, col, TerminalMouseButton.None, TerminalMouseAction.Release);
                e.Handled = true;
            }
        }

        int delta = e.Delta.Y > 0 ? 3 : -3;
        vm.ScrollByLines(delta);
        e.Handled = true;
    }

    private static TerminalKeyInfo MapKey(Key key, KeyModifiers modifiers)
    {
        bool ctrl = modifiers.HasFlag(KeyModifiers.Control);
        bool alt = modifiers.HasFlag(KeyModifiers.Alt);
        bool shift = modifiers.HasFlag(KeyModifiers.Shift);

        TerminalKey terminalKey = key switch
        {
            Key.Enter => TerminalKey.Enter,
            Key.Escape => TerminalKey.Escape,
            Key.Tab => TerminalKey.Tab,
            Key.Back => TerminalKey.Backspace,
            Key.Up => TerminalKey.Up,
            Key.Down => TerminalKey.Down,
            Key.Left => TerminalKey.Left,
            Key.Right => TerminalKey.Right,
            Key.Home => TerminalKey.Home,
            Key.End => TerminalKey.End,
            Key.PageUp => TerminalKey.PageUp,
            Key.PageDown => TerminalKey.PageDown,
            Key.Insert => TerminalKey.Insert,
            Key.Delete => TerminalKey.Delete,
            Key.F1 => TerminalKey.F1,
            Key.F2 => TerminalKey.F2,
            Key.F3 => TerminalKey.F3,
            Key.F4 => TerminalKey.F4,
            Key.F5 => TerminalKey.F5,
            Key.F6 => TerminalKey.F6,
            Key.F7 => TerminalKey.F7,
            Key.F8 => TerminalKey.F8,
            Key.F9 => TerminalKey.F9,
            Key.F10 => TerminalKey.F10,
            Key.F11 => TerminalKey.F11,
            Key.F12 => TerminalKey.F12,
            Key.NumPad0 => TerminalKey.Keypad0,
            Key.NumPad1 => TerminalKey.Keypad1,
            Key.NumPad2 => TerminalKey.Keypad2,
            Key.NumPad3 => TerminalKey.Keypad3,
            Key.NumPad4 => TerminalKey.Keypad4,
            Key.NumPad5 => TerminalKey.Keypad5,
            Key.NumPad6 => TerminalKey.Keypad6,
            Key.NumPad7 => TerminalKey.Keypad7,
            Key.NumPad8 => TerminalKey.Keypad8,
            Key.NumPad9 => TerminalKey.Keypad9,
            Key.Decimal => TerminalKey.KeypadDecimal,
            Key.Add => TerminalKey.KeypadAdd,
            Key.Subtract => TerminalKey.KeypadSubtract,
            Key.Multiply => TerminalKey.KeypadMultiply,
            Key.Divide => TerminalKey.KeypadDivide,
            _ => TerminalKey.Unknown
        };

        return new TerminalKeyInfo(terminalKey, ctrl, alt, shift);
    }

    private static bool IsClipboardCopy(KeyEventArgs e)
    {
        bool ctrlOrCmd = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        return e.Key == Key.C && ctrlOrCmd && !e.KeyModifiers.HasFlag(KeyModifiers.Alt);
    }

    private static bool IsClipboardPaste(KeyEventArgs e)
    {
        bool ctrlOrCmd = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        return e.Key == Key.V && ctrlOrCmd && !e.KeyModifiers.HasFlag(KeyModifiers.Alt);
    }

    private static bool TryGetControlChar(KeyEventArgs e, out char value)
    {
        value = '\0';
        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (ctrl)
        {
            if (e.Key >= Key.A && e.Key <= Key.Z)
            {
                int letterIndex = (int)e.Key - (int)Key.A;
                value = (char)(letterIndex + 1);
                return true;
            }
        }

        return false;
    }

    private static async Task SetClipboardAsync(Control control, string text)
    {
        if (control.GetVisualRoot() is TopLevel topLevel && topLevel.Clipboard is not null)
        {
            await topLevel.Clipboard.SetTextAsync(text);
        }
    }

    private static void AttachClipboard(Control control, TerminalViewModel viewModel)
    {
        ClipboardSubscription subscription = s_clipboardSubscriptions.GetOrCreateValue(control);
        subscription.Attach(viewModel, text => _ = SetClipboardAsync(control, text));
    }

    private static void DetachClipboard(Control control)
    {
        if (s_clipboardSubscriptions.TryGetValue(control, out ClipboardSubscription? subscription))
        {
            subscription.Detach();
            s_clipboardSubscriptions.Remove(control);
        }
    }

    private static async Task<string?> GetClipboardAsync(Control control)
    {
        if (control.GetVisualRoot() is TopLevel topLevel && topLevel.Clipboard is not null)
        {
            return await topLevel.Clipboard.TryGetTextAsync();
        }

        return null;
    }

    private sealed class SpaceInputState
    {
        public bool SuppressNextTextInput { get; set; }
    }

    private sealed class ClipboardSubscription
    {
        private TerminalViewModel? _viewModel;
        private Action<string>? _handler;

        public void Attach(TerminalViewModel viewModel, Action<string> handler)
        {
            Detach();
            _viewModel = viewModel;
            _handler = handler;
            _viewModel.ClipboardCopyRequested += OnClipboardCopyRequested;
        }

        public void Detach()
        {
            if (_viewModel is not null)
            {
                _viewModel.ClipboardCopyRequested -= OnClipboardCopyRequested;
            }

            _viewModel = null;
            _handler = null;
        }

        private void OnClipboardCopyRequested(string text)
        {
            _handler?.Invoke(text);
        }
    }
}
