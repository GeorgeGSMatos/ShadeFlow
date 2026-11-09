#if DEBUG
using System.Collections;
using Application = System.Windows.Application;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using System.Xml;

namespace ShadeFlow.Development;

public static class DevelopmentStyles
{
    private static readonly string[] Files = ["Colors.xaml", "Controls.xaml"];

    public static void Reload(string sourceRoot, Action applyTheme)
    {
        var app = Application.Current;
        var old = app.Resources.MergedDictionaries.ToArray();
        var previousValues = app.Resources.Cast<DictionaryEntry>().ToDictionary(entry => entry.Key, entry => entry.Value);
        var keys = new Dictionary<Style, object>();
        void Remember(ResourceDictionary dictionary)
        {
            foreach (var nested in dictionary.MergedDictionaries) Remember(nested);
            foreach (DictionaryEntry entry in dictionary) if (entry.Value is Style style) keys[style] = entry.Key;
        }
        foreach (var dictionary in old) Remember(dictionary);
        var staged = new ResourceDictionary();
        app.Resources.MergedDictionaries.Add(staged);
        try
        {
            foreach (var file in Files)
            {
                var text = File.ReadAllText(Path.Combine(sourceRoot, "Styles", file));
                // Loose XAML has no compiled owning assembly. Resolve project types explicitly.
                text = Regex.Replace(text, "clr-namespace:(ShadeFlow[^\";]*)(?=\")", match => match.Value + ";assembly=" + typeof(App).Assembly.GetName().Name);
                using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
                staged.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load(reader));
            }
            app.Resources.MergedDictionaries.Clear();
            app.Resources.MergedDictionaries.Add(staged);
            foreach (Window openWindow in app.Windows) Rebind(openWindow, keys);
            applyTheme();
        }
        catch
        {
            app.Resources.MergedDictionaries.Clear();
            foreach (var dictionary in old) app.Resources.MergedDictionaries.Add(dictionary);
            foreach (var entry in previousValues) app.Resources[entry.Key] = entry.Value;
            throw;
        }
    }

    private static void Rebind(DependencyObject element, IReadOnlyDictionary<Style, object> keys)
    {
        if (element is FrameworkElement control && control.ReadLocalValue(FrameworkElement.StyleProperty) is Style style && keys.TryGetValue(style, out var key))
            control.SetResourceReference(FrameworkElement.StyleProperty, key);
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(element); index++) Rebind(VisualTreeHelper.GetChild(element, index), keys);
    }
}
#endif
