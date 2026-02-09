using System;
using XamlVisualEditor.Animation;
using XamlVisualEditor.Designer.Core;

namespace XamlVisualEditor.Shell.ViewModels;

public interface IAnimationPreviewService
{
    bool TryPlayPreview(
        DesignSurfaceViewModel designSurface,
        Guid? targetNodeId,
        AnimationTimelineModel timeline,
        out string? error);

    void StopPreview();
}
