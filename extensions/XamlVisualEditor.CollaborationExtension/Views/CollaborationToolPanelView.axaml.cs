using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XamlVisualEditor.CollaborationExtension.Views;

public sealed partial class CollaborationToolPanelView : UserControl
{
    public CollaborationToolPanelView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
