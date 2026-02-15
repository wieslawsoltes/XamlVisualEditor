namespace XamlVisualEditor.Extensions;

/// <summary>Provides access to the animation editor view model.</summary>
public interface IAnimationEditorHost
{
    /// <summary>Gets the animation editor view model.</summary>
    object? ViewModel { get; }

    /// <summary>Begins an animation edit transaction.</summary>
    IDisposable BeginTransaction(string name);

    /// <summary>Triggers preview refresh for current animation selection.</summary>
    Task RefreshPreviewAsync(CancellationToken cancellationToken);
}
