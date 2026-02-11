using XamlVisualEditor.Extensions;
using XamlVisualEditor.Extensions.Hosting.VscodeCompat;

namespace XamlVisualEditor.VscodeCompatExtension;

/// <summary>Manages VS Code compatibility host lifecycle.</summary>
public sealed class VscodeCompatRuntimeController : IAsyncDisposable
{
    private const string SettingsSection = "vscodeCompat";
    private readonly ISettings _settings;
    private readonly IWorkspace _workspace;
    private readonly VscodeCompatHost _host;
    private VscodeCompatSettings _currentSettings = new();
    private bool _suppressReload;

    public VscodeCompatRuntimeController(
        ISettings settings,
        IWorkspace workspace,
        VscodeCompatHost host)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _host = host ?? throw new ArgumentNullException(nameof(host));
    }

    /// <summary>Gets the current settings.</summary>
    public VscodeCompatSettings CurrentSettings => _currentSettings;

    /// <summary>Gets whether the host is running.</summary>
    public bool IsRunning => _host.IsRunning;

    /// <summary>Loads settings and starts the host if enabled.</summary>
    public async Task InitializeAsync(CancellationToken ct)
    {
        _currentSettings = LoadSettings();
        _workspace.ConfigurationChanged += OnConfigurationChanged;
        if (_currentSettings.Enabled)
        {
            await _host.StartAsync(_currentSettings, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Applies settings and restarts the host.</summary>
    public async Task ApplySettingsAsync(VscodeCompatSettings settings, CancellationToken ct)
    {
        _currentSettings = settings;
        _suppressReload = true;
        try
        {
            await _settings.UpdateAsync(SettingsSection, settings, SettingsTarget.User, ct).ConfigureAwait(false);
        }
        finally
        {
            _suppressReload = false;
        }

        await RestartAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Restarts the host using current settings.</summary>
    public async Task RestartAsync(CancellationToken ct)
    {
        await _host.StopAsync().ConfigureAwait(false);
        if (_currentSettings.Enabled)
        {
            await _host.StartAsync(_currentSettings, ct).ConfigureAwait(false);
        }
    }

    private VscodeCompatSettings LoadSettings()
    {
        VscodeCompatSettings? settings = _settings.Get<VscodeCompatSettings>(SettingsSection);
        return settings ?? new VscodeCompatSettings();
    }

    private void OnConfigurationChanged(object? sender, ConfigurationChangedEventArgs args)
    {
        if (_suppressReload)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(args.Section)
            && !args.Section.StartsWith(SettingsSection, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        VscodeCompatSettings latest = LoadSettings();
        if (latest == _currentSettings)
        {
            return;
        }

        _currentSettings = latest;
        _ = RestartAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        _workspace.ConfigurationChanged -= OnConfigurationChanged;
        return new ValueTask(_host.StopAsync());
    }
}
