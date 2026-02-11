using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XamlVisualEditor.App.Views;

public sealed partial class ExtensionManagerView : UserControl
{
    public ExtensionManagerView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
