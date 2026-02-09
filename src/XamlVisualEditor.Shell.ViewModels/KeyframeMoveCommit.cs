namespace XamlVisualEditor.Shell.ViewModels;

public sealed class KeyframeMoveCommit
{
    public KeyframeMoveCommit(AnimationKeyframeViewModel keyframe, double oldTime, double newTime)
    {
        Keyframe = keyframe;
        OldTime = oldTime;
        NewTime = newTime;
    }

    public AnimationKeyframeViewModel Keyframe { get; }

    public double OldTime { get; }

    public double NewTime { get; }
}
