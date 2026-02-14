namespace XamlVisualEditor.Extensions;

/// <summary>Provides access to the debug settings view model.</summary>
public interface IDebugSettingsHost
{
    /// <summary>Gets the debug settings view model.</summary>
    object? ViewModel { get; }
}

/// <summary>Provides access to the LSP settings view model.</summary>
public interface ILspSettingsHost
{
    /// <summary>Gets the LSP settings view model.</summary>
    object? ViewModel { get; }
}
