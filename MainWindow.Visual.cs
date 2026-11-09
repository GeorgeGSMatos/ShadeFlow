using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ShadeFlow.Services;
using RadioButton = System.Windows.Controls.RadioButton;

namespace ShadeFlow;

public partial class MainWindow
{
    public static readonly DependencyProperty DisplayCardWidthProperty = DependencyProperty.Register(
        nameof(DisplayCardWidth), typeof(double), typeof(MainWindow), new PropertyMetadata(340d));

    public double DisplayCardWidth
    {
        get => (double)GetValue(DisplayCardWidthProperty);
        private set => SetValue(DisplayCardWidthProperty, value);
    }

    private static void ShowMessage(TextBlock target, string message, InterfaceTone tone)
    {
        target.Text = message;
        InterfacePresentation.Apply(target, tone);
    }

    private bool _visualInitialized;

    private void VisualLoaded(object sender, RoutedEventArgs e)
    {
        if (!_visualInitialized)
        {
            _visualInitialized = true;
            DisplayList.SizeChanged += (_, _) => UpdateDisplayCardWidth();
            AboutVersion.Text = "Versão " + typeof(MainWindow).Assembly.GetName().Version?.ToString(3);
            DisplaysNavigation.IsChecked = true;
        }
        RefreshVisualTheme();
        UpdateDisplayCardWidth();
    }

    private void UpdateDisplayCardWidth()
    {
        if (DisplayList.ActualWidth <= 0) return;
        var columns = Math.Max(1, Math.Min(3, (int)(DisplayList.ActualWidth / 330)));
        DisplayCardWidth = Math.Floor(DisplayList.ActualWidth / columns);
    }

    private void NavigationChanged(object sender, RoutedEventArgs e)
    {
        if (!_visualInitialized || sender is not RadioButton { Tag: string page }) return;
        DisplaysPage.Visibility = page == "Displays" ? Visibility.Visible : Visibility.Collapsed;
        AutomationPage.Visibility = page == "Automation" ? Visibility.Visible : Visibility.Collapsed;
        PreferencesPage.Visibility = page == "Preferences" ? Visibility.Visible : Visibility.Collapsed;
        PresetsPage.Visibility = page == "Presets" ? Visibility.Visible : Visibility.Collapsed;
        PageScroller.ScrollToTop();
        if (page == "Preferences") SettingsExpanded(sender, e);
        UiMotion.Enter(PageContent);
    }

    private void ManageAutomationClick(object sender, RoutedEventArgs e) => AutomationNavigation.IsChecked = true;
    private void TimelineSizeChanged(object sender, SizeChangedEventArgs e) => DrawScheduleTimeline();

    private void DrawScheduleTimeline()
    {
        if (ScheduleTimeline is null) return;
        ScheduleTimeline.Children.Clear();
        ScheduleTimeline.Visibility = _scheduleEntries.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        var width = ScheduleTimeline.ActualWidth;
        if (width <= 0 || _scheduleEntries.Count == 0) return;
        var line = new Line { X1 = 0, X2 = width, Y1 = 12, Y2 = 12, StrokeThickness = 2 };
        line.SetResourceReference(Shape.StrokeProperty, "ShadeTrackBrush");
        ScheduleTimeline.Children.Add(line);
        var next = _scheduler?.Snapshot.NextOccurrence?.Entry;
        // Map actual clock times, including uneven gaps, onto the compact day overview.
        var first = _scheduleEntries[0].Time.ToTimeSpan().TotalMinutes;
        var last = _scheduleEntries[^1].Time.ToTimeSpan().TotalMinutes;
        var previousLabel = double.NegativeInfinity;
        foreach (var entry in _scheduleEntries)
        {
            var position = last == first ? width / 2 : (entry.Time.ToTimeSpan().TotalMinutes - first) / (last - first) * width;
            var dot = new Ellipse { Width = 8, Height = 8 };
            dot.SetResourceReference(Shape.FillProperty, entry == next ? "ShadeAccentBrush" : "ShadeMutedTextBrush");
            Canvas.SetLeft(dot, position - 4); Canvas.SetTop(dot, 8);
            ScheduleTimeline.Children.Add(dot);
            // Dense schedules keep their dots; skip overlapping labels instead of hiding entries.
            if (position - previousLabel < 48) continue;
            var label = new TextBlock { Text = entry.TimeText, FontSize = 10, Width = 44, TextAlignment = TextAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, "ShadeMutedTextBrush");
            Canvas.SetLeft(label, position - 22); Canvas.SetTop(label, 26);
            ScheduleTimeline.Children.Add(label);
            previousLabel = position;
        }
    }
}
