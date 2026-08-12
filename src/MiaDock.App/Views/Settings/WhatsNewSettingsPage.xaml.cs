using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace MiaDock.App.Views.Settings;

public sealed partial class WhatsNewSettingsPage : UserControl
{
    private static readonly string ContentRelativePath = Path.Combine("Content", "YENILIKLER.md");

    public WhatsNewSettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        Loaded -= OnLoaded;
        RenderMarkdown(LoadMarkdown());
    }

    private static string LoadMarkdown()
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, ContentRelativePath);
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }
        catch
        {
            // Fall through to empty content message.
        }

        return """
               # Yenilikler

               Yenilik metni bulunamadı. `src/MiaDock.App/Content/YENILIKLER.md` dosyasını düzenleyin.
               """;
    }

    private void RenderMarkdown(string markdown)
    {
        ContentHost.Children.Clear();
        foreach (var line in markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var trimmed = line.TrimEnd();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                ContentHost.Children.Add(new Border { Height = 8 });
                continue;
            }

            if (trimmed.StartsWith("---", StringComparison.Ordinal))
            {
                ContentHost.Children.Add(new Border
                {
                    Height = 1,
                    Margin = new Thickness(0, 8, 0, 8),
                    Opacity = 0.35,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Gray)
                });
                continue;
            }

            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                ContentHost.Children.Add(CreateText(trimmed[4..], 16, opacity: 0.92, topMargin: 4));
                continue;
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                ContentHost.Children.Add(CreateText(trimmed[3..], 20, opacity: 1, topMargin: 12));
                continue;
            }

            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal) ||
                trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                ContentHost.Children.Add(CreateBullet(trimmed[2..]));
                continue;
            }

            ContentHost.Children.Add(CreateText(trimmed, 14, opacity: 0.78));
        }
    }

    private static TextBlock CreateText(
        string text,
        double fontSize,
        double opacity,
        double topMargin = 0) =>
        new()
        {
            Text = text,
            FontSize = fontSize,
            Opacity = opacity,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, topMargin, 0, 4)
        };

    private static Grid CreateBullet(string text)
    {
        var grid = new Grid { Margin = new Thickness(4, 2, 0, 2), ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var bullet = new TextBlock
        {
            Text = "•",
            FontSize = 14,
            Opacity = 0.7
        };
        var body = new TextBlock
        {
            Text = text,
            FontSize = 14,
            Opacity = 0.82,
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(body, 1);
        grid.Children.Add(bullet);
        grid.Children.Add(body);
        return grid;
    }
}
