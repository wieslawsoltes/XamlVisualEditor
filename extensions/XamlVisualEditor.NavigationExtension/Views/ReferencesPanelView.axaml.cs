using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XamlVisualEditor.NavigationExtension.Views;

public sealed partial class ReferencesPanelView : UserControl
{
    public ReferencesPanelView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
