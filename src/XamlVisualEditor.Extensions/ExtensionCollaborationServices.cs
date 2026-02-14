namespace XamlVisualEditor.Extensions;

/// <summary>Provides access to the collaboration panel view model.</summary>
public interface ICollaborationPanelHost
{
    /// <summary>Gets the collaboration panel view model.</summary>
    object? ViewModel { get; }
}
