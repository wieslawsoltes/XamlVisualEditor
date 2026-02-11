using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace IdeBridgeCli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        CliOptions options = CliOptions.Parse(args);
        if (options.ShowHelp)
        {
            PrintHelp();
            return 0;
        }

        if (string.IsNullOrWhiteSpace(options.Command))
        {
            Console.Error.WriteLine("Missing command.");
            PrintHelp();
            return 1;
        }

        await using Transport transport = await Transport.ConnectAsync(options, CancellationToken.None).ConfigureAwait(false);
        await using JsonRpcClient client = new(transport.Input, transport.Output);
        client.Start();

        JsonElement initResult = await client.SendRequestAsync(
            "bridge.initialize",
            new
            {
                sessionToken = options.SessionToken,
                workspaceId = options.WorkspaceId,
                clientName = "IdeBridgeCli",
                clientVersion = "0.1"
            },
            CancellationToken.None).ConfigureAwait(false);

        if (options.Command == "init")
        {
            PrintJson(initResult);
            return 0;
        }

        if (options.Command == "commands")
        {
            JsonElement result = await client.SendRequestAsync("commands.list", null, CancellationToken.None).ConfigureAwait(false);
            PrintJson(result);
            return 0;
        }

        if (options.Command == "watch")
        {
            client.NotificationReceived += (method, payload) =>
            {
                Console.WriteLine($"notification: {method}");
                if (payload.HasValue)
                {
                    PrintJson(payload.Value);
                }
            };

            using CancellationTokenSource cts = new();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            try
            {
                await Task.Delay(Timeout.Infinite, cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            return 0;
        }

        Console.Error.WriteLine($"Unknown command: {options.Command}");
        PrintHelp();
        return 1;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("idebridge-cli [--stdio] [--tcp host:port] [--unix path] [--token token] [--workspace id] <command>");
        Console.WriteLine("Commands:");
        Console.WriteLine("  init       Initialize and print session info");
        Console.WriteLine("  commands   List registered commands");
        Console.WriteLine("  watch      Listen for notifications");
    }

    private static void PrintJson(JsonElement element)
    {
        string json = JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }
}

internal sealed class CliOptions
{
    public string? Command { get; set; }
    public bool UseStdio { get; set; }
    public string? TcpEndpoint { get; set; }
    public string? UnixPath { get; set; }
    public string? SessionToken { get; set; }
    public string? WorkspaceId { get; set; }
    public bool ShowHelp { get; set; }

    public static CliOptions Parse(string[] args)
    {
        CliOptions options = new();
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--help" || arg == "-h")
            {
                options.ShowHelp = true;
                return options;
            }

            if (arg == "--stdio")
            {
                options.UseStdio = true;
                continue;
            }

            if (arg == "--tcp" && i + 1 < args.Length)
            {
                options.TcpEndpoint = args[++i];
                continue;
            }

            if (arg == "--unix" && i + 1 < args.Length)
            {
                options.UnixPath = args[++i];
                continue;
            }

            if (arg == "--token" && i + 1 < args.Length)
            {
                options.SessionToken = args[++i];
                continue;
            }

            if (arg == "--workspace" && i + 1 < args.Length)
            {
                options.WorkspaceId = args[++i];
                continue;
            }

            if (!arg.StartsWith('-'))
            {
                options.Command = arg;
                break;
            }
        }

        return options;
    }
}

internal sealed class Transport : IAsyncDisposable
{
    private readonly TcpClient? _tcpClient;
    private readonly Socket? _unixSocket;

    public Transport(Stream input, Stream output, TcpClient? tcpClient = null, Socket? unixSocket = null)
    {
        Input = input;
        Output = output;
        _tcpClient = tcpClient;
        _unixSocket = unixSocket;
    }

    public Stream Input { get; }
    public Stream Output { get; }

    public static async Task<Transport> ConnectAsync(CliOptions options, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(options.UnixPath))
        {
            Socket socket = new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(options.UnixPath), ct).ConfigureAwait(false);
            NetworkStream stream = new(socket, ownsSocket: true);
            return new Transport(stream, stream, unixSocket: socket);
        }

        if (!string.IsNullOrWhiteSpace(options.TcpEndpoint))
        {
            (string host, int port) = ParseTcpEndpoint(options.TcpEndpoint);
            TcpClient client = new();
            await client.ConnectAsync(host, port, ct).ConfigureAwait(false);
            NetworkStream stream = client.GetStream();
            return new Transport(stream, stream, tcpClient: client);
        }

        Stream input = Console.OpenStandardInput();
        Stream output = Console.OpenStandardOutput();
        return new Transport(input, output);
    }

    private static (string Host, int Port) ParseTcpEndpoint(string endpoint)
    {
        string host = "127.0.0.1";
        string portText = endpoint;

        int separator = endpoint.IndexOf(':');
        if (separator >= 0)
        {
            host = endpoint.Substring(0, separator);
            portText = endpoint[(separator + 1)..];
        }

        if (!int.TryParse(portText, out int port))
        {
            throw new FormatException("Invalid TCP endpoint.");
        }

        return (string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host, port);
    }

    public ValueTask DisposeAsync()
    {
        _tcpClient?.Dispose();
        _unixSocket?.Dispose();
        return ValueTask.CompletedTask;
    }
}
