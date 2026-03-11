namespace XamlVisualEditor.Extensions;

/// <summary>Represents the solution explorer panel model exposed to extensions.</summary>
public interface ISolutionExplorerPanelModel
{
}

/// <summary>Provides access to the solution explorer panel model.</summary>
public interface ISolutionExplorerPanelHost
{
    /// <summary>Gets the host-owned solution explorer panel model.</summary>
    ISolutionExplorerPanelModel? PanelModel { get; }
}
