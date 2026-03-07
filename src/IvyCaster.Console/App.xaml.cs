using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace IvyCaster.Console;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ApplicationThemeManager.Apply(ApplicationTheme.Light);
        ApplicationAccentColorManager.Apply(
            (Color)ColorConverter.ConvertFromString("#A9FFAA"));
    }
}

