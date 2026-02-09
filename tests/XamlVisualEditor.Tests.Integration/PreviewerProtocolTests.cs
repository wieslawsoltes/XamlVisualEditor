using System;
using System.Net;
using System.Threading.Tasks;
using Avalonia.Remote.Protocol;
using Avalonia.Remote.Protocol.Designer;
using Avalonia.Remote.Protocol.Viewport;
using Xunit;

namespace XamlVisualEditor.Tests.Integration;

public sealed class PreviewerProtocolTests
{
    [Fact]
    public async Task PreviewerTcpSession_SendsPendingUpdateAfterConnect()
    {
        object session = CreateSession("/tmp/Test.axaml");
        try
        {
            string xaml = "<UserControl xmlns=\"https://github.com/avaloniaui\" />";
            await InvokeSendUpdateAsync(session, xaml, "/tmp/Test.dll", "/Test.axaml", 640, 480);

            TaskCompletionSource<UpdateXamlMessage> updateTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<ClientViewportAllocatedMessage> viewportTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            int port = GetPort(session);
            IAvaloniaRemoteTransportConnection connection =
                await new BsonTcpTransport().Connect(IPAddress.Loopback, port);

            connection.OnMessage += (_, message) =>
            {
                if (message is ClientViewportAllocatedMessage viewport &&
                    Math.Abs(viewport.Width - 640) < 0.1 &&
                    Math.Abs(viewport.Height - 480) < 0.1)
                {
                    viewportTcs.TrySetResult(viewport);
                }

                if (message is UpdateXamlMessage update)
                {
                    updateTcs.TrySetResult(update);
                }
            };

            connection.Start();

            Task allMessages = Task.WhenAll(updateTcs.Task, viewportTcs.Task);
            Task timeout = Task.Delay(TimeSpan.FromSeconds(5));
            Task completed = await Task.WhenAny(allMessages, timeout);

            Assert.NotSame(timeout, completed);

            UpdateXamlMessage updateMessage = await updateTcs.Task;
            Assert.Equal(xaml, updateMessage.Xaml);
            Assert.Equal("/tmp/Test.dll", updateMessage.AssemblyPath);
            Assert.Equal("/Test.axaml", updateMessage.XamlFileProjectPath);

            connection.Dispose();
        }
        finally
        {
            InvokeDispose(session);
        }
    }

    private static object CreateSession(string xamlFilePath)
    {
        Type? type = typeof(XamlVisualEditor.Shell.ViewModels.MainWindowViewModel).Assembly
            .GetType("XamlVisualEditor.Shell.ViewModels.PreviewerTcpSession");
        Assert.NotNull(type);
        return Activator.CreateInstance(type!, new object?[] { xamlFilePath, null })
            ?? throw new InvalidOperationException("Failed to create PreviewerTcpSession.");
    }

    private static async Task InvokeSendUpdateAsync(
        object session,
        string xaml,
        string assemblyPath,
        string projectPath,
        double width,
        double height)
    {
        System.Reflection.MethodInfo? method = session.GetType().GetMethod("SendUpdateXamlAsync");
        Assert.NotNull(method);
        object? task = method!.Invoke(session, new object?[] { xaml, assemblyPath, projectPath, width, height });
        Assert.NotNull(task);
        await (Task)task!;
    }

    private static int GetPort(object session)
    {
        System.Reflection.PropertyInfo? property = session.GetType().GetProperty("Port");
        Assert.NotNull(property);
        object? value = property!.GetValue(session);
        Assert.NotNull(value);
        return (int)value!;
    }

    private static void InvokeDispose(object session)
    {
        System.Reflection.MethodInfo? method = session.GetType().GetMethod("Dispose");
        method?.Invoke(session, null);
    }
}
