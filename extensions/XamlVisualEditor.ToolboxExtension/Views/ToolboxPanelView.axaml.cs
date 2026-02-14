using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XamlVisualEditor.ToolboxExtension.Views;

public sealed partial class ToolboxPanelView : UserControl
{
    public ToolboxPanelView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
