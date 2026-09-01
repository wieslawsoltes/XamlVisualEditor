using Avalonia.Controls;
using XamlVisualEditor.Designer.Rendering;
using XamlVisualEditor.Xaml.Ast;
using Xunit;

namespace XamlVisualEditor.Tests.Unit;

public sealed class DesignPreviewHookTests
{
    [Fact]
    public void Registry_RegistersAndRemovesHooks()
    {
        StubHook hook = new();

        IDisposable registration = DesignPreviewHookRegistry.Register(hook);
        Assert.Contains(hook, DesignPreviewHookRegistry.Hooks);

        registration.Dispose();
        Assert.DoesNotContain(hook, DesignPreviewHookRegistry.Hooks);
    }

    [Fact]
    public void CreateControlTree_UsesHookCreationAndPostProcess()
    {
        StubHook hook = new();
        using IDisposable registration = DesignPreviewHookRegistry.Register(hook);
        MutableAstObjectNode node = new()
        {
            TypeName = "HookedSurface",
            XmlNamespace = "clr-namespace:XamlVisualEditor.Tests.Unit"
        };
        ControlFactory factory = new();

        Control? control = factory.CreateControlTree(node);

        Border border = Assert.IsType<Border>(control);
        Assert.Equal("post-processed", border.Tag);
    }

    [Fact]
    public void CreateControlTree_PostProcessesDefaultCreatedControls()
    {
        StubHook hook = new();
        using IDisposable registration = DesignPreviewHookRegistry.Register(hook);
        MutableAstObjectNode node = new()
        {
            TypeName = "Button",
            XmlNamespace = "https://github.com/avaloniaui"
        };
        ControlFactory factory = new();

        Control? control = factory.CreateControlTree(node);

        Button button = Assert.IsType<Button>(control);
        Assert.Equal("post-processed", button.Tag);
    }

    private sealed class StubHook : IDesignPreviewHook
    {
        public Control? TryCreateControl(MutableAstObjectNode node, Func<MutableAstObjectNode, Control?> createChildTree)
        {
            return node.TypeName == "HookedSurface" ? new Border() : null;
        }

        public void PostProcess(Control control, MutableAstObjectNode node)
        {
            control.Tag = "post-processed";
        }
    }
}
