using Avalonia.Controls;
using XamlVisualEditor.Xaml.Ast;

namespace XamlVisualEditor.Designer.Rendering;

/// <summary>
/// Extension point for the design surface control instantiation. Hooks let external
/// extensions take over or refine the preview of control libraries whose behavior the
/// generic factory cannot know (custom layout engines, runtime-coupled containers).
/// </summary>
public interface IDesignPreviewHook
{
    /// <summary>
    /// Creates the control for an AST node, or returns null to let the default factory
    /// handle it. <paramref name="createChildTree"/> builds nested nodes through the
    /// factory so hook-created containers participate in the normal pipeline.
    /// </summary>
    Control? TryCreateControl(MutableAstObjectNode node, Func<MutableAstObjectNode, Control?> createChildTree);

    /// <summary>
    /// Called after the factory finished a node: the control exists, its properties are
    /// applied, and its children are attached. Hooks may adjust the result in place.
    /// </summary>
    void PostProcess(Control control, MutableAstObjectNode node);
}

/// <summary>
/// Process-wide registry for design preview hooks. A registry (instead of constructor
/// injection) because the design surface builds control factories per document while
/// hooks arrive from independently activated extensions.
/// </summary>
public static class DesignPreviewHookRegistry
{
    private static readonly object Gate = new();
    private static IDesignPreviewHook[] _hooks = Array.Empty<IDesignPreviewHook>();

    /// <summary>Gets the currently registered hooks.</summary>
    public static IReadOnlyList<IDesignPreviewHook> Hooks => _hooks;

    /// <summary>Registers a hook; disposing the result removes it again.</summary>
    public static IDisposable Register(IDesignPreviewHook hook)
    {
        if (hook is null)
        {
            throw new ArgumentNullException(nameof(hook));
        }

        lock (Gate)
        {
            IDesignPreviewHook[] next = new IDesignPreviewHook[_hooks.Length + 1];
            _hooks.CopyTo(next, 0);
            next[^1] = hook;
            _hooks = next;
        }

        return new Registration(hook);
    }

    private static void Remove(IDesignPreviewHook hook)
    {
        lock (Gate)
        {
            _hooks = _hooks.Where(existing => !ReferenceEquals(existing, hook)).ToArray();
        }
    }

    private sealed class Registration : IDisposable
    {
        private IDesignPreviewHook? _hook;

        public Registration(IDesignPreviewHook hook)
        {
            _hook = hook;
        }

        public void Dispose()
        {
            IDesignPreviewHook? hook = _hook;
            _hook = null;
            if (hook is not null)
            {
                Remove(hook);
            }
        }
    }
}
