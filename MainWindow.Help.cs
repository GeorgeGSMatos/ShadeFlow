using System.Windows;
using ShadeFlow.Services;
using ShadeFlow.Views;

namespace ShadeFlow;

public partial class MainWindow
{
    private void HelpClick(object sender, RoutedEventArgs args)
    {
        var section = PreferencesPage.Visibility == Visibility.Visible ? HelpSection.Settings
            : AutomationPage.Visibility == Visibility.Visible ? HelpSection.Automation
            : PresetsPage.Visibility == Visibility.Visible ? HelpSection.Presets
            : HelpSection.Displays;
        new HelpWindow(section, _development) { Owner = this }.ShowDialog();
    }
}
