namespace XamlVisualEditor.Extensions;

/// <summary>Provides access to the animation editor view model.</summary>
public interface IAnimationEditorHost
{
    /// <summary>Gets the animation editor view model.</summary>
    object? ViewModel { get; }
}
