using System.Runtime.InteropServices;
using System.Windows;

namespace ShadeFlow.Views;

public partial class AboutWindow : Window
{
    private readonly string _diagnostics;

    public AboutWindow(string version, string settingsPath, string logsPath, string diagnostics)
    {
        InitializeComponent();
        var license = "Licença proprietária · George GS Matos · Todos os direitos reservados.";
        ReportText.Text = string.Join(Environment.NewLine,
            "ShadeFlow · Versão " + version,
            license,
            "Windows: " + RuntimeInformation.OSDescription,
            "Ambiente: " + RuntimeInformation.FrameworkDescription,
            "Arquitetura: " + RuntimeInformation.ProcessArchitecture,
            "Configuração: " + settingsPath,
            "Logs: " + logsPath);
        _diagnostics = license + Environment.NewLine + diagnostics;
    }

    private void CopyDiagnosticsClick(object sender, RoutedEventArgs args)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        try { System.Windows.Clipboard.SetText(_diagnostics); }
        catch (ExternalException error)
        {
            ErrorText.Text = "Não foi possível copiar o diagnóstico: " + error.Message;
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
