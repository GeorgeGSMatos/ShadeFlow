using System.Globalization;
using System.Windows;
using ShadeFlow.Models;

namespace ShadeFlow.Views;

public partial class ScheduleEntryDialog : Window
{
    public ScheduleEntryDialog(ScheduleEntry? entry = null)
    {
        InitializeComponent();
        TimeInput.Text = entry?.TimeText ?? DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
        BrightnessInput.Text = (entry?.Brightness ?? 100).ToString(CultureInfo.InvariantCulture);
        TimeInput.SelectAll();
        TimeInput.Focus();
    }

    public ScheduleEntry? Entry { get; private set; }

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (!TimeOnly.TryParseExact(TimeInput.Text.Trim(), "HH:mm", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var time))
        {
            ValidationMessage.Text = "Informe um horário válido entre 00:00 e 23:59.";
            return;
        }

        if (!int.TryParse(BrightnessInput.Text.Trim(), NumberStyles.None,
                CultureInfo.InvariantCulture, out var brightness) || brightness is < 0 or > 100)
        {
            ValidationMessage.Text = "Informe um brilho inteiro entre 0 e 100.";
            return;
        }

        Entry = new ScheduleEntry(time, brightness);
        DialogResult = true;
    }
}
