using System.Windows;
using ShadeFlow.Services;

namespace ShadeFlow.Views;

public partial class HelpWindow : Window
{
    public HelpWindow(HelpSection section, bool development)
    {
        InitializeComponent();
        Title = HeadingText.Text = "Ajuda · " + ContextHelp.Title(section);
        Topics.ItemsSource = ContextHelp.Topics(section);
        if (development) Introduction.Text = "Modo de desenvolvimento: brilho em prévia e perfil separado; atalhos globais não são registrados.";
    }
    private void CloseClick(object sender, RoutedEventArgs args) => DialogResult = true;
    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs args)
    {
        if (args.Key == System.Windows.Input.Key.Escape) { args.Handled = true; Close(); return; }
        base.OnPreviewKeyDown(args);
    }
}
