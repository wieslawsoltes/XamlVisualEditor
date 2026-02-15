using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace XamlVisualEditor.DebugSettingsExtension.Views;

public sealed partial class DebugSettingsPanelView : UserControl
{
    public DebugSettingsPanelView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
