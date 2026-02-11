using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XamlVisualEditor.App.Views;

public sealed partial class ExtensionToolView : UserControl
{
    public ExtensionToolView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
