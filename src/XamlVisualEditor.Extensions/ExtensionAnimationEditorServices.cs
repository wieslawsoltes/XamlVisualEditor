namespace XamlVisualEditor.Extensions;

/// <summary>Represents the animation editor panel model exposed to extensions.</summary>
public interface IAnimationEditorPanelModel
{
}

/// <summary>Provides access to the animation editor panel model.</summary>
public interface IAnimationEditorHost
{
    /// <summary>Gets the animation editor panel model.</summary>
    IAnimationEditorPanelModel? PanelModel { get; }

    /// <summary>Begins an animation edit transaction.</summary>
    IDisposable BeginTransaction(string name);

    /// <summary>Triggers preview refresh for current animation selection.</summary>
    Task RefreshPreviewAsync(CancellationToken cancellationToken);
}
