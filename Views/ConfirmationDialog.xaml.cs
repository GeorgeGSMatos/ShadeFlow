using System.Windows;
using ShadeFlow.Services;

namespace ShadeFlow.Views;

public partial class ConfirmationDialog : Window
{
    private ConfirmationDialog(string title, string message, string confirmLabel, bool destructive,
        bool informational, InterfaceTone tone)
    {
        InitializeComponent();
        Title = title;
        HeadingText.Text = title;
        MessageText.Text = message;
        InterfacePresentation.Apply(HeadingIcon, destructive ? InterfaceTone.Danger : tone);
        HeadingIcon.Text = InterfacePresentation.Glyph(destructive ? InterfaceTone.Danger : tone);
        ConfirmButton.Content = confirmLabel;
        ConfirmButton.IsDefault = !destructive;
        if (destructive) ConfirmButton.SetResourceReference(StyleProperty, "DangerButton");
        CancelButton.Visibility = informational ? Visibility.Collapsed : Visibility.Visible;
        CancelButton.IsDefault = destructive;
        Loaded += (_, _) => { if (destructive) CancelButton.Focus(); else ConfirmButton.Focus(); };
    }

    public static bool Confirm(Window owner, string title, string message, string action, bool destructive = false) =>
        new ConfirmationDialog(title, message, action, destructive, false, InterfaceTone.Attention) { Owner = owner }.ShowDialog() == true;

    public static void Inform(Window owner, string title, string message, InterfaceTone tone = InterfaceTone.Attention) =>
        new ConfirmationDialog(title, message, "Fechar", false, true, tone) { Owner = owner }.ShowDialog();

    private void ConfirmClick(object sender, RoutedEventArgs args) => DialogResult = true;
}
