using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionPermissionServiceTests
{
    [Fact]
    public async Task RequestAsync_AlwaysAllow_IsRememberedAndReused()
    {
        FakeSettings settings = new();
        FakeWindow window = new();
        window.EnqueuePick(new QuickPickItem("Always allow", "remember", null));

        ExtensionPermissionService service = new("sample.extension", settings, window);
        service.Declare(
        [
            new ExtensionCapabilityDeclaration("workspace.write", "Write files", "Allow writing workspace files.", true)
        ]);

        ExtensionPermissionDecision first = await service.RequestAsync("workspace.write", CancellationToken.None);
        ExtensionPermissionDecision second = await service.RequestAsync("workspace.write", CancellationToken.None);
        IReadOnlyList<ExtensionPermissionEntry> remembered = await service.GetRememberedAsync(CancellationToken.None);

        Assert.True(first.IsAllowed);
        Assert.True(first.IsRemembered);
        Assert.Equal(ExtensionPermissionDecisionSource.Prompt, first.Source);
        Assert.True(second.IsAllowed);
        Assert.True(second.IsRemembered);
        Assert.Equal(ExtensionPermissionDecisionSource.Remembered, second.Source);
        Assert.Single(remembered);
        Assert.Equal("workspace.write", remembered[0].CapabilityId);
        Assert.Equal(1, window.QuickPickCalls);
    }

    [Fact]
    public async Task RequestAsync_AllowOnce_DoesNotPersistDecision()
    {
        FakeSettings settings = new();
        FakeWindow window = new();
        window.EnqueuePick(new QuickPickItem("Allow once", null, null));
        window.EnqueuePick(new QuickPickItem("Deny once", null, null));

        ExtensionPermissionService service = new("sample.extension", settings, window);
        service.Declare(
        [
            new ExtensionCapabilityDeclaration("terminal.run", "Run terminal", "Allow running terminal commands.")
        ]);

        ExtensionPermissionDecision first = await service.RequestAsync("terminal.run", CancellationToken.None);
        ExtensionPermissionDecision second = await service.RequestAsync("terminal.run", CancellationToken.None);
        IReadOnlyList<ExtensionPermissionEntry> remembered = await service.GetRememberedAsync(CancellationToken.None);

        Assert.True(first.IsAllowed);
        Assert.False(first.IsRemembered);
        Assert.False(second.IsAllowed);
        Assert.False(second.IsRemembered);
        Assert.Empty(remembered);
        Assert.Equal(2, window.QuickPickCalls);
    }

    [Fact]
    public async Task RequestAsync_UndeclaredCapability_IsDeniedAndAudited()
    {
        FakeSettings settings = new();
        FakeWindow window = new();
        ExtensionPermissionService service = new("sample.extension", settings, window);

        ExtensionPermissionAuditEventArgs? lastAudit = null;
        service.AccessAudited += (_, args) => lastAudit = args;

        ExtensionPermissionDecision decision = await service.RequestAsync("unknown.capability", CancellationToken.None);

        Assert.False(decision.IsAllowed);
        Assert.Equal(ExtensionPermissionDecisionSource.Undeclared, decision.Source);
        Assert.NotNull(lastAudit);
        Assert.Equal("unknown.capability", lastAudit!.CapabilityId);
        Assert.False(lastAudit.IsAllowed);
        Assert.Equal(ExtensionPermissionDecisionSource.Undeclared, lastAudit.Source);
    }

    [Fact]
    public async Task ClearRememberedAsync_RemovesStoredCapabilityDecision()
    {
        FakeSettings settings = new();
        FakeWindow window = new();
        window.EnqueuePick(new QuickPickItem("Always allow", null, null));

        ExtensionPermissionService service = new("sample.extension", settings, window);
        service.Declare(
        [
            new ExtensionCapabilityDeclaration("diagnostics.read", "Read diagnostics", "Access diagnostics streams.")
        ]);

        await service.RequestAsync("diagnostics.read", CancellationToken.None);
        await service.ClearRememberedAsync("diagnostics.read", CancellationToken.None);
        IReadOnlyList<ExtensionPermissionEntry> remembered = await service.GetRememberedAsync(CancellationToken.None);

        Assert.Empty(remembered);
    }

    private sealed class FakeWindow : IWindow
    {
        private readonly Queue<QuickPickItem?> _picks = new();

        public int QuickPickCalls { get; private set; }

        public void EnqueuePick(QuickPickItem? item)
        {
            _picks.Enqueue(item);
        }

        public Task ShowInformationMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ShowWarningMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ShowErrorMessageAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<string?> ShowInputBoxAsync(InputBoxOptions options, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);

        public Task<QuickPickItem?> ShowQuickPickAsync(
            IReadOnlyList<QuickPickItem> items,
            QuickPickOptions options,
            CancellationToken cancellationToken)
        {
            QuickPickCalls++;
            if (_picks.Count > 0)
            {
                return Task.FromResult(_picks.Dequeue());
            }

            return Task.FromResult<QuickPickItem?>(null);
        }

        public IOutputChannel CreateOutputChannel(string name) => new NullOutputChannel(name);

        public Task<IReadOnlyList<OutputChannelInfo>> GetOutputChannelsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OutputChannelInfo>>(Array.Empty<OutputChannelInfo>());

#pragma warning disable CS0067
        public event EventHandler<OutputChannelEventArgs>? OutputChannelCreated;
        public event EventHandler<OutputChannelEventArgs>? OutputChannelRemoved;
        public event EventHandler<OutputChannelMessageEventArgs>? OutputChannelMessage;
        public event EventHandler<OutputChannelClearedEventArgs>? OutputChannelCleared;
#pragma warning restore CS0067

        public IStatusBarItem CreateStatusBarItem(StatusBarAlignment alignment, int priority) => new NullStatusBarItem();

        private sealed class NullOutputChannel : IOutputChannel
        {
            public NullOutputChannel(string name)
            {
                Name = name;
            }

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
        private readonly Dictionary<string, SettingsSectionSchema> _schemas = new(StringComparer.OrdinalIgnoreCase);

        public event EventHandler<SettingsSectionChangedEventArgs>? SectionChanged;

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
            SectionChanged?.Invoke(this, new SettingsSectionChangedEventArgs(section, target, value));
            return Task.CompletedTask;
        }

        public IDisposable RegisterSchema(SettingsSectionSchema schema)
        {
            _schemas[schema.Section] = schema;
            return new Registration(() => _schemas.Remove(schema.Section));
        }

        public IReadOnlyList<SettingsSectionSchema> GetSchemas()
        {
            return _schemas.Values.ToList();
        }

        public bool TryGetSchema(string section, out SettingsSectionSchema schema)
        {
            return _schemas.TryGetValue(section, out schema!);
        }

        public IReadOnlyList<SettingsValidationIssue> Validate(string section, object? value)
        {
            if (!_schemas.TryGetValue(section, out SettingsSectionSchema? schema) || schema.Validator is null)
            {
                return Array.Empty<SettingsValidationIssue>();
            }

            return schema.Validator(value) ?? Array.Empty<SettingsValidationIssue>();
        }

        public IDisposable SubscribeSection<T>(string section, Action<SettingsSectionChangedEventArgs<T>> handler)
        {
            EventHandler<SettingsSectionChangedEventArgs> wrapped = (_, args) =>
            {
                if (!string.Equals(args.Section, section, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                if (args.Value is null)
                {
                    handler(new SettingsSectionChangedEventArgs<T>(args.Section, args.Target, default));
                    return;
                }

                if (args.Value is T typed)
                {
                    handler(new SettingsSectionChangedEventArgs<T>(args.Section, args.Target, typed));
                }
            };

            SectionChanged += wrapped;
            return new Registration(() => SectionChanged -= wrapped);
        }

        private sealed class Registration : IDisposable
        {
            private readonly Action _unsubscribe;
            private bool _disposed;

            public Registration(Action unsubscribe)
            {
                _unsubscribe = unsubscribe;
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _unsubscribe();
            }
        }
    }
}
