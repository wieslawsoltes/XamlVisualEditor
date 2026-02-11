using System.Text.Json;
using XamlVisualEditor.Extensions.Hosting.IdeBridge;

namespace XamlVisualEditor.Extensions.Hosting.VscodeCompat;

/// <summary>Handles a VS Code compatibility JSON-RPC session.</summary>
public sealed class VscodeCompatSession : IAsyncDisposable
{
    private readonly IdeBridgeJsonRpcConnection _connection;
    private readonly ICommands _commands;
    private readonly IWindow _window;
    private readonly ISettings _settings;
    private readonly IExtensionLogger _logger;
    private readonly List<IDisposable> _commandRegistrations = new();

    public VscodeCompatSession(
        IdeBridgeJsonRpcConnection connection,
        ICommands commands,
        IWindow window,
        ISettings settings,
        IExtensionLogger logger)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _connection.RegisterRequestHandler("vscode.commands.register", HandleRegisterCommandAsync);
        _connection.RegisterRequestHandler("vscode.commands.execute", HandleExecuteCommandAsync);
        _connection.RegisterRequestHandler("vscode.commands.get", HandleGetCommandsAsync);
        _connection.RegisterRequestHandler("vscode.workspace.getConfiguration", HandleGetConfigurationAsync);
        _connection.RegisterRequestHandler("vscode.window.showInformationMessage", HandleShowInformationMessageAsync);
        _connection.RegisterRequestHandler("vscode.window.showWarningMessage", HandleShowWarningMessageAsync);
        _connection.RegisterRequestHandler("vscode.window.showErrorMessage", HandleShowErrorMessageAsync);

        _connection.Disconnected += OnDisconnected;
    }

    /// <summary>Starts the session.</summary>
    public void Start(CancellationToken ct)
    {
        _connection.Start(ct);
    }

    private async Task<object?> HandleRegisterCommandAsync(JsonElement? parameters, CancellationToken ct)
    {
        string? commandId = GetString(parameters, "id");
        if (string.IsNullOrWhiteSpace(commandId))
        {
            throw new IdeBridgeJsonRpcException(-32602, "Command id is required.");
        }

        IDisposable registration = _commands.Register(commandId, async context =>
        {
            await _connection.SendNotificationAsync(
                    "vscode.commands.invoke",
                    new { id = commandId, args = context.Arguments },
                    CancellationToken.None)
                .ConfigureAwait(false);
        });

        _commandRegistrations.Add(registration);
        _logger.Info("Registered VS Code command: " + commandId);

        return new { ok = true };
    }

    private async Task<object?> HandleExecuteCommandAsync(JsonElement? parameters, CancellationToken ct)
    {
        string? commandId = GetString(parameters, "id");
        if (string.IsNullOrWhiteSpace(commandId))
        {
            throw new IdeBridgeJsonRpcException(-32602, "Command id is required.");
        }

        IReadOnlyList<object?>? args = GetArgs(parameters);
        await _commands.ExecuteAsync(commandId, args, ct).ConfigureAwait(false);

        return new { ok = true };
    }

    private async Task<object?> HandleGetCommandsAsync(JsonElement? parameters, CancellationToken ct)
    {
        IReadOnlyList<string> commands = await _commands.GetCommandsAsync(ct).ConfigureAwait(false);
        return new { commands };
    }

    private Task<object?> HandleGetConfigurationAsync(JsonElement? parameters, CancellationToken ct)
    {
        string section = GetString(parameters, "section") ?? string.Empty;
        Dictionary<string, object?>? values = _settings.Get<Dictionary<string, object?>>(section);

        return Task.FromResult<object?>(new { section, values = values ?? new Dictionary<string, object?>() });
    }

    private async Task<object?> HandleShowInformationMessageAsync(JsonElement? parameters, CancellationToken ct)
    {
        string? message = GetString(parameters, "text");
        if (!string.IsNullOrWhiteSpace(message))
        {
            await _window.ShowInformationMessageAsync(message, ct).ConfigureAwait(false);
        }

        return new { ok = true };
    }

    private async Task<object?> HandleShowWarningMessageAsync(JsonElement? parameters, CancellationToken ct)
    {
        string? message = GetString(parameters, "text");
        if (!string.IsNullOrWhiteSpace(message))
        {
            await _window.ShowWarningMessageAsync(message, ct).ConfigureAwait(false);
        }

        return new { ok = true };
    }

    private async Task<object?> HandleShowErrorMessageAsync(JsonElement? parameters, CancellationToken ct)
    {
        string? message = GetString(parameters, "text");
        if (!string.IsNullOrWhiteSpace(message))
        {
            await _window.ShowErrorMessageAsync(message, ct).ConfigureAwait(false);
        }

        return new { ok = true };
    }

    private void OnDisconnected(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.Error("VS Code compatibility session disconnected.", exception);
        }

        ClearCommands();
    }

    private void ClearCommands()
    {
        foreach (IDisposable registration in _commandRegistrations)
        {
            registration.Dispose();
        }

        _commandRegistrations.Clear();
    }

    private static string? GetString(JsonElement? parameters, string name)
    {
        if (parameters is null)
        {
            return null;
        }

        JsonElement element = parameters.Value;
        if (element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static IReadOnlyList<object?>? GetArgs(JsonElement? parameters)
    {
        if (parameters is null)
        {
            return null;
        }

        JsonElement element = parameters.Value;
        if (!element.TryGetProperty("args", out JsonElement argsElement) || argsElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<object?> args = new();
        foreach (JsonElement item in argsElement.EnumerateArray())
        {
            object? value = JsonSerializer.Deserialize<object?>(item.GetRawText(), IdeBridgeMessageFraming.SerializerOptions);
            args.Add(value);
        }

        return args;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        ClearCommands();
        _connection.Disconnected -= OnDisconnected;
        await _connection.DisposeAsync().ConfigureAwait(false);
    }
}
