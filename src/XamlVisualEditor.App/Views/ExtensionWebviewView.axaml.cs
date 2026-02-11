using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XamlVisualEditor.App.Views;

public sealed partial class ExtensionWebviewView : UserControl
{
    public ExtensionWebviewView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
