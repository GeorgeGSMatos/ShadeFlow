using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShadeFlow;
using ShadeFlow.Models;
using ShadeFlow.Services;
using ShadeFlow.Views;
using Application = System.Windows.Application;
using RadioButton = System.Windows.Controls.RadioButton;
using ComboBox = System.Windows.Controls.ComboBox;
using Panel = System.Windows.Controls.Panel;
using Brush = System.Windows.Media.Brush;

// Native WPF integration checks. The subclass suppresses MainWindow's service startup:
// no tray, hotkeys, monitor discovery, overlays, scheduler, or writable settings.
internal sealed class PreviewWindow : MainWindow
{
    public PreviewWindow() : base(LaunchOptions.Parse(["--safe-mode"]), Path.GetTempPath()) { }
    protected override void OnSourceInitialized(EventArgs e) { }
}

// Dispatcher pumping also raises Application.Startup. Suppress the production
// App.OnStartup path, not only the preview window's native service initialization.
internal sealed class PreviewApp : App
{
    protected override void OnStartup(StartupEventArgs e) { }
}

internal static class Program
{
    private static int _checks;
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var output = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "ShadeFlow-visual-checks");
            Directory.CreateDirectory(output);
            var app = new PreviewApp();
            app.InitializeComponent();
            new ThemeService().Apply(app, darkMode: true);
            var main = new PreviewWindow { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
            var displays = new ObservableCollection<DisplayState>
            {
                new(new DisplayInfo("DISPLAY1", "preview-1", "DISPLAY1", "Monitor externo", null, new DisplayBounds(0, 0, 2560, 1440), new(2560,1440), true, true), 70) { Alias = "Monitor externo" },
                new(new DisplayInfo("DISPLAY2", "preview-2", "DISPLAY2", "Internal display", null, new DisplayBounds(2560, 0, 1920, 1080), new(1920,1080), false, true), 45) { Alias = "Monitor interno" }
            };
            var list = (ItemsControl)main.FindName("DisplayList");
            list.ItemsSource = displays;
            list.IsEnabled = true;
            var presets = new ObservableCollection<BrightnessPreset>
            {
                new() { Name = "Jogo", Icon = "game", HoldUntilResume = true, Displays = new() { ["preview-1"] = 60, ["preview-2"] = 40 } },
                new() { Name = "Noite", Icon = "night", Displays = new() { ["preview-1"] = 30, ["preview-2"] = 45 } }
            };
            ((ItemsControl)main.FindName("PresetList")).ItemsSource = presets;
            var entries = (ObservableCollection<ScheduleEntry>)((ItemsControl)main.FindName("ScheduleList")).ItemsSource;
            foreach (var entry in new[] { new ScheduleEntry(new(7,0),70), new(new(10,0),100), new(new(16,30),75), new(new(18,30),50), new(new(20,0),30), new(new(21,30),20) }) entries.Add(entry);
            main.Show();
            Flush(main);
            Check(((FrameworkElement)main.FindName("DisplaysPage")).IsVisible, "Displays is the initial page");
            Check(main.FontFamily.Source.Contains("Segoe"), "Window uses shared Segoe typography");
            Check(UiMotion.GetScope(main), "Main window opts into UI-only motion");
            Check(UiMotion.IsEnabled == (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast), "Motion follows Windows accessibility preferences");

            foreach (var (page, tag) in new[] { ("DisplaysPage", "Displays"), ("AutomationPage", "Automation"), ("PresetsPage", "Presets"), ("PreferencesPage", "Preferences") })
            {
                var navigation = Descendants<RadioButton>(main).Single(r => Equals(r.Tag, tag));
                navigation.IsChecked = true;
                Flush(main);
                Check(((FrameworkElement)main.FindName(page)).IsVisible, "Navigation opens " + tag);
                Check(new[] { "DisplaysPage", "AutomationPage", "PresetsPage", "PreferencesPage" }.Count(p => ((FrameworkElement)main.FindName(p)).IsVisible) == 1, "Only one page is visible: " + tag);
                Save(main, Path.Combine(output, tag + ".png"));
            }

            Check(Descendants<System.Windows.Controls.Button>(main).Any(button => System.Windows.Automation.AutomationProperties.GetName(button) == "Ajuda"), "Contextual Help stays available in the navigation footer");
            Check(Descendants<ShadeFlow.Controls.HotkeyCaptureButton>(main).Count() == 4, "Settings exposes four shortcut capture controls");
            var help = new HelpWindow(HelpSection.Settings, development: true) { Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, ShowActivated = false };
            help.Show(); Flush(help);
            Check(help.Title == "Ajuda · Configurações", "Help identifies the current settings page");
            Save(help, Path.Combine(output, "Help.png")); help.Close();
            var about = new AboutWindow("1.2.1", "Configuração de teste", "Logs de teste", "Diagnóstico de teste") { Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, ShowActivated = false };
            about.Show(); Flush(about);
            Check(about.Title == "Informações e diagnóstico", "Diagnostics uses the shared product terminology");
            Save(about, Path.Combine(output, "Diagnostics.png")); about.Close();

            var transition = (ComboBox)main.FindName("TransitionChoice");
            Check(transition.Items.Count == 6, "All six transition durations retained");
            Check(transition.Template.FindName("PART_Popup", transition) is Popup, "ComboBox popup template loads");
            Check(transition.Template.FindName("PART_EditableTextBox", transition) is null, "Noneditable preference dropdown");

            ((RadioButton)main.FindName("DisplaysNavigation")).IsChecked = true;
            Flush(main);
            var sliders = Descendants<Slider>(list).ToArray();
            Check(sliders.Length == 2, "Each monitor exposes a slider");
            var track = (Track)sliders[0].Template.FindName("PART_Track", sliders[0]);
            Check(track.Thumb is not null && track.DecreaseRepeatButton.Command == Slider.DecreaseLarge, "Slider thumb and click commands wired");
            Slider.IncreaseLarge.Execute(null, sliders[0]);
            Flush(main);
            Check(displays[0].Brightness == 75, "Track command changes brightness through the binding");
            var details = Descendants<Expander>(list).First();
            details.IsExpanded = true;
            Flush(main);
            Check(Descendants<System.Windows.Controls.TextBox>(details).Any(t => t.IsVisible), "Expanded details expose alias editor");
            details.IsExpanded = false;
            sliders[0].Value = 65;
            Flush(main);
            Check(displays[0].Brightness == 65 && displays[1].Brightness == 45, "Slider binding preserves independent values");
            displays[1].Brightness = 30;
            Flush(main);
            Check(sliders[1].Value == 30, "Programmatic brightness updates slider");
            main.Width = 760;
            Flush(main);
            Check(main.DisplayCardWidth > 400, "Narrow window switches to one card per row");
            Save(main, Path.Combine(output, "Displays-compact.png"));
            main.Width = 1000;
            Flush(main);
            Check(main.DisplayCardWidth < 400, "Wide window uses two cards per row");
            Save(main, Path.Combine(output, "Displays-dark.png"));
            new ThemeService().Apply(app, darkMode: false);
            Flush(main);
            Save(main, Path.Combine(output, "Displays-light.png"));
            Check(((SolidColorBrush)app.FindResource("ShadeTextBrush")).Color.R < 80, "Light theme uses dark foreground");
            new ThemeService().Apply(app, darkMode: true);

            // Bounded dispatcher pumping tests real WPF clocks without starting overlays.
            var motionToggle = new System.Windows.Controls.CheckBox { Style = (Style)app.FindResource("ToggleSwitch") };
            var motionHost = new Window { Content = motionToggle, Width = 180, Height = 100, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false };
            UiMotion.SetScope(motionHost, true);
            motionHost.Show(); Flush(motionHost); Settle(motionHost);
            motionToggle.IsChecked = true; Settle(motionHost);
            var motionThumb = (FrameworkElement)motionToggle.Template.FindName("Thumb", motionToggle);
            Check(motionThumb.RenderTransform is TranslateTransform shiftOn && Math.Abs(shiftOn.X - 18) < .01, "Animated switch reaches its checked position");
            motionToggle.IsChecked = false; motionToggle.IsChecked = true; motionToggle.IsChecked = false; Settle(motionHost);
            Check(motionThumb.RenderTransform is TranslateTransform { X: 0 }, "Rapid toggle reversal settles at the latest state");
            Check(UiMotion.ActiveAnimationCount == 0, "Finished animations release their clocks");
            var status = (TextBlock)main.FindName("Status");
            status.Text = "Preset de teste aplicado"; Settle(main);
            status.Text = "Preset de teste aplicado";
            Check(UiMotion.ActiveAnimationCount == 0, "An unchanged status does not restart feedback");
            UiMotion.Enter((FrameworkElement)main.FindName("PageContent"));
            main.Hide();
            Check(UiMotion.ActiveAnimationCount == 0, "Hiding a UI scope releases pending effects");
            main.Show(); Settle(main); motionHost.Close();

            var canResume = true;
            var quick = new QuickPanelWindow(displays, () => new("Preset Jogo", "Automação pausada até retomar", canResume))
            { Left = -20000, Top = -20000, ShowActivated = false };
            quick.SetPresets(presets);
            Check(quick.WindowStyle == WindowStyle.SingleBorderWindow && quick.ResizeMode == ResizeMode.CanMinimize, "Quick Panel starts with a native frame recognized at HWND creation");
            var quickHandle = new System.Windows.Interop.WindowInteropHelper(quick).EnsureHandle();
            var quickStyle = NativeMethods.GetWindowLongPtr(quickHandle, -16).ToInt64();
            Check((quickStyle & 0x80880000L) == 0x80880000L, "Quick Panel native frame is eligible for Translucent Windows popup effects");
            Check((quickStyle & 0x00C00000L) != 0x00C00000L, "Quick Panel keeps its caption hidden");
            Check((quickStyle & 0x00040000L) == 0, "Quick Panel keeps resizing disabled");
            Check(System.Windows.Shell.WindowChrome.GetWindowChrome(quick) is { CaptionHeight: 0, UseAeroCaptionButtons: false }, "Quick Panel extends the native material without caption buttons");
            Check(((Border)quick.Content).Background is SolidColorBrush quickTint && quickTint.Color.A < 128, "Quick Panel tint preserves backdrop transparency");
            quick.RefreshTheme();
            SendMessage(quickHandle, 0x0112, new nint(0xF060), 0);
            Check(new System.Windows.Interop.WindowInteropHelper(quick).EnsureHandle() == quickHandle, "System close dismisses the cached Quick Panel without destroying its native window");
            // Deactivated is allowed to hide the flyout; render the same content in a plain off-screen host.
            var quickRoot = (FrameworkElement)quick.Content;
            quick.Content = null;
            var quickHost = new Window { Content = quickRoot, Width = 370, SizeToContent = SizeToContent.Height, Left = -20000, Top = -20000, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None };
            quick.UpdateAutomationState();
            quickHost.Show();
            UiMotion.SetScope(quickHost, true);
            Flush(quickHost);
            Check(Descendants<Slider>(quickHost).Count() == 2, "Quick Panel creates both slider templates");
            var resumed = false;
            quick.ResumeRequested += (_, _) => resumed = true;
            var resume = (System.Windows.Controls.Button)quick.FindName("ResumeAutomationButton");
            Check(resume.IsEnabled, "Quick Panel resume available with active schedule");
            resume.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(resumed, "Quick Panel resume invokes shared action event");
            canResume = false; quick.UpdateAutomationState();
            Check(!resume.IsEnabled, "Quick Panel resume disabled without schedule");
            canResume = true; quick.UpdateAutomationState();
            BrightnessPreset? applied = null;
            quick.PresetRequested += p => applied = p;
            var presetButtons = Descendants<System.Windows.Controls.Button>((ItemsControl)quick.FindName("PresetButtons")).ToArray();
            Check(presetButtons.Length == 2, "Quick Panel renders one icon button per preset");
            presetButtons[0].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(applied?.Name == "Jogo", "Preset icon click dispatches correct preset");
            Check(!string.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(presetButtons[0])), "Preset icons have accessible labels");
            Save(quickHost, Path.Combine(output, "QuickPanel.png"));
            var dialog = new PresetDialog(displays, presets, presets[0]) { Left = -20000, Top = -20000, WindowStartupLocation = WindowStartupLocation.Manual, ShowInTaskbar = false, ShowActivated = false };
            var ownerRoot = (UIElement)main.Content;
            var dimLayer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(ownerRoot);
            var dimCount = dimLayer?.GetAdorners(ownerRoot)?.Length ?? 0;
            dialog.Owner = main;
            dialog.Show(); Flush(dialog);
            Check(dimLayer is not null && (dimLayer.GetAdorners(ownerRoot)?.Length ?? 0) == dimCount + 1, "Owned dialogs add a visual dim layer without changing the owner layout");
            Check(((ComboBox)dialog.FindName("IconChoice")).Items.Count == 8, "Preset editor exposes eight named icons");
            Check(Descendants<System.Windows.Controls.TextBox>((ItemsControl)dialog.FindName("MonitorValues")).Count() == 2, "Preset editor shows independent brightness inputs");
            Save(dialog, Path.Combine(output, "PresetEditor.png"));
            dialog.Close();
            Check((dimLayer?.GetAdorners(ownerRoot)?.Length ?? 0) == dimCount, "Closing a dialog removes its dim layer");
            quickHost.Close();
            main.RestoreBeforeFatalError();
            main.Hide();
            Console.WriteLine($"{_checks} native WPF checks passed. No production services started. Rendered controls do not validate the desktop backdrop.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }

    private static void Flush(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void Save(Window window, string path)
    {
        Settle(window);
        var content = (FrameworkElement)window.Content;
        if (window.FindName("PageContent") is UIElement page) page.BeginAnimation(UIElement.OpacityProperty, null);
        // A RenderTargetBitmap captures our controls, not the OS composition behind the HWND.
        if (content is Panel panel) panel.Background = (Brush)Application.Current.FindResource("ShadeBackgroundBrush");
        Flush(window);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth), (int)Math.Ceiling(content.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var composite = new DrawingVisual();
        using (var drawing = composite.RenderOpen())
        {
            drawing.DrawRectangle((Brush)Application.Current.FindResource("ShadeBackgroundBrush"), null, new Rect(0, 0, bitmap.Width, bitmap.Height));
            drawing.DrawImage(bitmap, new Rect(0, 0, bitmap.Width, bitmap.Height));
        }
        var flattened = new RenderTargetBitmap(bitmap.PixelWidth, bitmap.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        flattened.Render(composite);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(flattened));
        using var stream = File.Create(path); encoder.Save(stream);
    }

    private static void Check(bool value, string description)
    {
        if (!value) throw new InvalidOperationException("FAILED: " + description);
        _checks++; Console.WriteLine("PASS: " + description);
    }

    private static void Settle(Window window)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(260) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); Dispatcher.PushFrame(frame); Flush(window);
    }
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, int message, nint wParam, nint lParam);
}
