using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using XamlVisualEditor.Extensions.Hosting;
using Xunit;

namespace XamlVisualEditor.Tests.Unit.Extensions;

public sealed class ExtensionHostSupervisorTests
{
    [Fact]
    public async Task RestartPolicyStopsAfterLimit()
    {
        var factory = new FakeProcessFactory();
        var reporter = new InMemoryExtensionCrashReporter();
        var policy = new ExtensionRestartPolicy(1, TimeSpan.FromMinutes(5));
        var supervisor = new ExtensionHostSupervisor(factory, reporter, policy);

        ExtensionCrashInfo? crashInfo = null;
        supervisor.ExtensionCrashed += (_, info) => crashInfo = info;

        await supervisor.StartAsync("ext", CancellationToken.None);
        FakeExtensionProcess first = factory.LastCreated!;
        first.Crash(42, "boom");

        FakeExtensionProcess second = factory.LastCreated!;
        second.Crash(43, "boom2");

        Assert.NotNull(crashInfo);
        Assert.Equal("ext", crashInfo!.ExtensionId);
        Assert.Contains(reporter.Items, item => item.ExitCode == 42);
        Assert.Contains(reporter.Items, item => item.ExitCode == 43);
    }

    [Fact]
    public async Task DispatcherCapturesFailures()
    {
        var dispatcher = new ExtensionCallDispatcher();
        ExtensionCallResult result = await dispatcher.ExecuteAsync(
            () => throw new InvalidOperationException("fail"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(result.Canceled);
        Assert.Equal("fail", result.Error);
    }

    private sealed class FakeProcessFactory : IExtensionProcessFactory
    {
        public FakeExtensionProcess? LastCreated { get; private set; }

        public IExtensionProcess Create(string extensionId)
        {
            LastCreated = new FakeExtensionProcess(extensionId);
            return LastCreated;
        }
    }

    private sealed class FakeExtensionProcess : IExtensionProcess
    {
        public FakeExtensionProcess(string extensionId)
        {
            ExtensionId = extensionId;
        }

        public string ExtensionId { get; }

        public bool IsRunning { get; private set; }

        public event EventHandler<ExtensionProcessExitedEventArgs>? Exited;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public void Crash(int exitCode, string errorTail)
        {
            IsRunning = false;
            Exited?.Invoke(this, new ExtensionProcessExitedEventArgs(exitCode, true, errorTail));
        }
    }
}
