using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XamlVisualEditor.App.Views;

public sealed partial class ExtensionTreeView : UserControl
{
    public ExtensionTreeView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
