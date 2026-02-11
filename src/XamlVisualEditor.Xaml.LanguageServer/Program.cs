using XamlVisualEditor.Xaml.LanguageServer;

using CancellationTokenSource cts = new();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cts.Cancel();
};

XamlLanguageServer server = new();
await server.RunAsync(
    Console.OpenStandardInput(),
    Console.OpenStandardOutput(),
    cts.Token);
