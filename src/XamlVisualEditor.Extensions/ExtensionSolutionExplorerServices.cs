namespace XamlVisualEditor.Extensions;

/// <summary>Provides access to the solution explorer panel view model.</summary>
public interface ISolutionExplorerPanelHost
{
    /// <summary>Gets the host-owned solution explorer view model.</summary>
    object? ViewModel { get; }
}
