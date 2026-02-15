using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XamlVisualEditor.LspSettingsExtension.Views;

public sealed partial class LspSettingsPanelView : UserControl
{
    public LspSettingsPanelView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
