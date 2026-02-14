using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting.IdeBridge;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class IdeBridgePermissionServiceTests
{
    [Fact]
    public async Task AuthorizeAsync_AllowsFullAccessWhenApproved()
    {
        FakeSettings settings = new();
        FakeWindow window = new()
        {
            NextPick = new QuickPickItem("Allow full access", "", null)
        };

        IdeBridgePermissionService service = new(settings, window);
        IdeBridgeWorkspacePermissionState? state = await service.AuthorizeAsync("ws1", null, CancellationToken.None);

        Assert.NotNull(state);
        Assert.True(state!.Capabilities.Write);
        Assert.True(state.Capabilities.Terminal);
    }

    [Fact]
    public async Task AuthorizeAsync_DeniesWhenTokenDoesNotMatch()
    {
        FakeSettings settings = new();
        FakeWindow window = new()
        {
            NextPick = new QuickPickItem("Allow read-only", "", null)
        };

        IdeBridgePermissionService service = new(settings, window);
        IdeBridgeWorkspacePermissionState? state = await service.AuthorizeAsync("ws2", null, CancellationToken.None);
        Assert.NotNull(state);

        IdeBridgeWorkspacePermissionState? denied = await service.AuthorizeAsync("ws2", "wrong-token", CancellationToken.None);
        Assert.Null(denied);
    }

    private sealed class FakeWindow : IWindow
    {
        public QuickPickItem? NextPick { get; set; }

        public Task ShowInformationMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ShowWarningMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ShowErrorMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string?> ShowInputBoxAsync(InputBoxOptions options, CancellationToken cancellationToken) => Task.FromResult<string?>(null);

        public Task<QuickPickItem?> ShowQuickPickAsync(IReadOnlyList<QuickPickItem> items, QuickPickOptions options, CancellationToken cancellationToken)
            => Task.FromResult(NextPick);

        public IOutputChannel CreateOutputChannel(string name) => new NullOutputChannel(name);

        public Task<IReadOnlyList<OutputChannelInfo>> GetOutputChannelsAsync(CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<OutputChannelInfo>>(Array.Empty<OutputChannelInfo>());

#pragma warning disable CS0067
        public event EventHandler<OutputChannelEventArgs>? OutputChannelCreated;

        public event EventHandler<OutputChannelEventArgs>? OutputChannelRemoved;

        public event EventHandler<OutputChannelMessageEventArgs>? OutputChannelMessage;

        public event EventHandler<OutputChannelClearedEventArgs>? OutputChannelCleared;
#pragma warning restore CS0067

        public IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority) => new NullStatusBarItem();

        private sealed class NullOutputChannel : IOutputChannel
        {
            public NullOutputChannel(string name) => Name = name;

            public string Name { get; }

            public void Append(string value)
            {
            }

            public void AppendLine(string value)
            {
            }

            public void Show()
            {
            }

            public void Hide()
            {
            }

            public void Clear()
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class NullStatusBarItem : IStatusBarItem
        {
            public string Text { get; set; } = string.Empty;
            public string? Tooltip { get; set; }
            public string? CommandId { get; set; }

            public void Show()
            {
            }

            public void Hide()
            {
            }

            public void Dispose()
            {
            }
        }
    }

    private sealed class FakeSettings : ISettings
    {
        private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

        public T? Get<T>(string section, T? defaultValue = default)
        {
            if (_values.TryGetValue(section, out object? value) && value is T typed)
            {
                return typed;
            }

            return defaultValue;
        }

        public Task UpdateAsync(string section, object? value, SettingsTarget target, CancellationToken cancellationToken)
        {
            _values[section] = value;
            return Task.CompletedTask;
        }
    }
}
