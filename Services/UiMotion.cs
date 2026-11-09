using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Documents;
using ButtonBase = System.Windows.Controls.Primitives.ButtonBase;

namespace ShadeFlow.Services;

/// <summary>Event-driven effects for explicitly opted-in UI. Never animates a Window or an overlay.</summary>
public static class UiMotion
{
    public const int ControlMilliseconds = 150;
    public const int PageMilliseconds = 200;
    private static readonly Dictionary<(DependencyObject, DependencyProperty), DoubleAnimation> Active = new();
    private static readonly DependencyPropertyDescriptor TextDescriptor = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
    public static bool IsEnabled => SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;
    public static int ActiveAnimationCount => Active.Count;

    public static readonly DependencyProperty ScopeProperty = DependencyProperty.RegisterAttached("Scope", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, ScopeChanged));
    public static void SetScope(DependencyObject d, bool value) => d.SetValue(ScopeProperty, value);
    public static bool GetScope(DependencyObject d) => (bool)d.GetValue(ScopeProperty);
    public static readonly DependencyProperty HoverProperty = DependencyProperty.RegisterAttached("Hover", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, HoverChanged));
    public static void SetHover(DependencyObject d, bool value) => d.SetValue(HoverProperty, value);
    public static bool GetHover(DependencyObject d) => (bool)d.GetValue(HoverProperty);
    public static readonly DependencyProperty HoverBrushProperty = DependencyProperty.RegisterAttached(
        "HoverBrush", typeof(System.Windows.Media.Brush), typeof(UiMotion), new PropertyMetadata(null));
    public static void SetHoverBrush(DependencyObject target, System.Windows.Media.Brush value) => target.SetValue(HoverBrushProperty, value);
    public static System.Windows.Media.Brush? GetHoverBrush(DependencyObject target) => (System.Windows.Media.Brush?)target.GetValue(HoverBrushProperty);
    public static readonly DependencyProperty SwitchProperty = DependencyProperty.RegisterAttached("Switch", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, SwitchChanged));
    public static void SetSwitch(DependencyObject d, bool value) => d.SetValue(SwitchProperty, value);
    public static bool GetSwitch(DependencyObject d) => (bool)d.GetValue(SwitchProperty);
    public static readonly DependencyProperty EntranceProperty = DependencyProperty.RegisterAttached("Entrance", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, EntranceChanged));
    public static void SetEntrance(DependencyObject d, bool value) => d.SetValue(EntranceProperty, value);
    public static bool GetEntrance(DependencyObject d) => (bool)d.GetValue(EntranceProperty);
    public static readonly DependencyProperty TextFeedbackProperty = DependencyProperty.RegisterAttached("TextFeedback", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, TextFeedbackChanged));
    public static void SetTextFeedback(DependencyObject d, bool value) => d.SetValue(TextFeedbackProperty, value);
    public static bool GetTextFeedback(DependencyObject d) => (bool)d.GetValue(TextFeedbackProperty);
    private static readonly DependencyProperty LastTextProperty = DependencyProperty.RegisterAttached("LastText", typeof(string), typeof(UiMotion), new PropertyMetadata(null));
    private static readonly DependencyProperty OwnerDimProperty = DependencyProperty.RegisterAttached("OwnerDim", typeof(OwnerDimAdorner), typeof(UiMotion), new PropertyMetadata(null));

    static UiMotion()
    {
        SystemParameters.StaticPropertyChanged += (_, _) => { if (!IsEnabled) StopAll(); };
    }
    private static void StopAll() { foreach (var key in Active.Keys.ToArray()) Stop(key.Item1, key.Item2); }
    private static bool InScope(FrameworkElement element) => Window.GetWindow(element) is { } window && GetScope(window) && element.IsVisible;
    private static void ScopeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Window window) return;
        window.IsVisibleChanged -= ScopeVisibilityChanged;
        if ((bool)e.NewValue) window.IsVisibleChanged += ScopeVisibilityChanged;
    }
    private static void ScopeVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not Window window) return;
        if (!(bool)e.NewValue)
        {
            if (window.GetValue(OwnerDimProperty) is OwnerDimAdorner dim) { dim.Layer.Remove(dim); window.ClearValue(OwnerDimProperty); }
            StopAll(); return;
        }
        if (window is MainWindow) return;
        if (window.Owner is { Content: UIElement ownerContent } owner && GetScope(owner) && AdornerLayer.GetAdornerLayer(ownerContent) is { } layer)
        {
            var dim = new OwnerDimAdorner(ownerContent, layer);
            window.SetValue(OwnerDimProperty, dim); layer.Add(dim);
            Animate(dim, UIElement.OpacityProperty, .3, ControlMilliseconds, 0);
        }
        window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (window.IsVisible && window.Content is FrameworkElement content) Enter(content, dialog: true);
        }));
    }
    private sealed class OwnerDimAdorner(UIElement content, AdornerLayer layer) : Adorner(content)
    {
        public AdornerLayer Layer { get; } = layer;
        protected override void OnRender(DrawingContext context) => context.DrawRectangle(System.Windows.Media.Brushes.Black, null, new System.Windows.Rect(AdornedElement.RenderSize));
        protected override HitTestResult? HitTestCore(PointHitTestParameters parameters) => null;
    }
    public static void Enter(FrameworkElement element, bool dialog = false)
    {
        if (!InScope(element)) return;
        if (element.RenderTransform is not TransformGroup group || group.Children.Count != 2 || group.Children[0] is not ScaleTransform || group.Children[1] is not TranslateTransform)
        {
            if (!element.RenderTransform.Value.IsIdentity) return;
            group = new TransformGroup(); group.Children.Add(new ScaleTransform(1, 1)); group.Children.Add(new TranslateTransform());
            element.RenderTransform = group;
        }
        var scale = (ScaleTransform)group.Children[0]; var slide = (TranslateTransform)group.Children[1];
        element.RenderTransformOrigin = new System.Windows.Point(.5, .5);
        Animate(element, UIElement.OpacityProperty, 1, PageMilliseconds, .65);
        Animate(slide, TranslateTransform.YProperty, 0, PageMilliseconds, dialog ? 0 : 6);
        if (dialog) { Animate(scale, ScaleTransform.ScaleXProperty, 1, ControlMilliseconds, .985); Animate(scale, ScaleTransform.ScaleYProperty, 1, ControlMilliseconds, .985); }
    }
    private static void HoverChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ButtonBase button) return;
        button.MouseEnter -= HoverEntered; button.MouseLeave -= HoverLeft; button.Click -= Clicked;
        button.IsEnabledChanged -= HoverEnabledChanged; button.Unloaded -= HoverUnloaded;
        if (!(bool)e.NewValue) return;
        button.MouseEnter += HoverEntered; button.MouseLeave += HoverLeft; button.Click += Clicked;
        button.IsEnabledChanged += HoverEnabledChanged; button.Unloaded += HoverUnloaded;
    }
    private static void HoverEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    { if (!(bool)e.NewValue) ResetHover((ButtonBase)sender); }
    private static void HoverUnloaded(object sender, RoutedEventArgs e) => ResetHover((ButtonBase)sender);
    private static void ResetHover(ButtonBase button)
    {
        if (button.Template?.FindName("Hover", button) is not Border hover) return;
        Stop(hover, UIElement.OpacityProperty);
        hover.Opacity = 0;
    }
    private static void HoverEntered(object sender, System.Windows.Input.MouseEventArgs e) => SetHover((ButtonBase)sender, 1);
    private static void HoverLeft(object sender, System.Windows.Input.MouseEventArgs e) => SetHover((ButtonBase)sender, 0);
    private static void SetHover(ButtonBase button, double value)
    {
        if (button.IsEnabled && InScope(button) && button.Template?.FindName("Hover", button) is Border hover) Animate(hover, UIElement.OpacityProperty, value, ControlMilliseconds);
    }
    private static void Clicked(object sender, RoutedEventArgs e)
    {
        if (sender is ButtonBase button && InScope(button) && button.Template?.FindName("Hover", button) is Border hover)
            Animate(hover, UIElement.OpacityProperty, button.IsMouseOver ? 1 : 0, 150, .25);
    }
    private static void SwitchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ToggleButton toggle) return;
        toggle.Checked -= ToggleChanged; toggle.Unchecked -= ToggleChanged; toggle.Loaded -= ToggleLoaded;
        if (!(bool)e.NewValue) return;
        toggle.Checked += ToggleChanged; toggle.Unchecked += ToggleChanged; toggle.Loaded += ToggleLoaded;
    }
    private static void ToggleLoaded(object sender, RoutedEventArgs e) => SyncSwitch((ToggleButton)sender, false);
    private static void ToggleChanged(object sender, RoutedEventArgs e) => SyncSwitch((ToggleButton)sender, true);
    private static void SyncSwitch(ToggleButton toggle, bool animate)
    {
        toggle.ApplyTemplate();
        if (toggle.Template?.FindName("Thumb", toggle) is not FrameworkElement thumb) return;
        if (thumb.RenderTransform is not TranslateTransform shift) { shift = new TranslateTransform(); thumb.RenderTransform = shift; }
        var on = toggle.IsChecked == true;
        Animate(shift, TranslateTransform.XProperty, on ? 18 : 0, ControlMilliseconds, immediate: !animate || !InScope(toggle));
        if (toggle.Template.FindName("ActiveTrack", toggle) is Border track) Animate(track, UIElement.OpacityProperty, on ? 1 : 0, ControlMilliseconds, immediate: !animate || !InScope(toggle));
    }
    private static void EntranceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement element) return;
        element.Loaded -= ItemLoaded;
        if ((bool)e.NewValue) element.Loaded += ItemLoaded;
    }
    private static void ItemLoaded(object sender, RoutedEventArgs e) => Enter((FrameworkElement)sender);
    private static void TextFeedbackChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock text) return;
        text.Loaded -= TextLoaded; text.Unloaded -= TextUnloaded; TextDescriptor.RemoveValueChanged(text, TextChanged);
        if (!(bool)e.NewValue) return;
        text.Loaded += TextLoaded; text.Unloaded += TextUnloaded;
        if (text.IsLoaded) TextLoaded(text, new RoutedEventArgs());
    }
    private static void TextLoaded(object sender, RoutedEventArgs e)
    {
        var text = (TextBlock)sender; text.SetValue(LastTextProperty, text.Text);
        TextDescriptor.RemoveValueChanged(text, TextChanged); TextDescriptor.AddValueChanged(text, TextChanged);
    }
    private static void TextUnloaded(object sender, RoutedEventArgs e) => TextDescriptor.RemoveValueChanged(sender, TextChanged);
    private static void TextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextBlock text || Equals(text.GetValue(LastTextProperty), text.Text)) return;
        text.SetValue(LastTextProperty, text.Text);
        if (InScope(text)) Animate(text, UIElement.OpacityProperty, 1, ControlMilliseconds, .65);
    }
    private static void Animate(DependencyObject target, DependencyProperty property, double to, int milliseconds, double? from = null, bool immediate = false)
    {
        var start = from ?? (double)target.GetValue(property);
        // These transforms/opacity slots are owned by this helper. Set the base value
        // so removing a clock cannot restore an earlier switch position.
        Stop(target, property); target.SetValue(property, to);
        if (!IsEnabled || immediate) return;
        var animation = new DoubleAnimation(start, to, TimeSpan.FromMilliseconds(milliseconds))
        { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop };
        Active[(target, property)] = animation;
        animation.Completed += (_, _) => { if (Active.TryGetValue((target, property), out var current) && ReferenceEquals(current, animation)) Stop(target, property); };
        if (target is UIElement element) element.BeginAnimation(property, animation);
        else if (target is Animatable animatable) animatable.BeginAnimation(property, animation);
    }
    private static void Stop(DependencyObject target, DependencyProperty property)
    {
        if (!Active.Remove((target, property))) return;
        if (target is UIElement element) element.BeginAnimation(property, null);
        else if (target is Animatable animatable) animatable.BeginAnimation(property, null);
    }
}
