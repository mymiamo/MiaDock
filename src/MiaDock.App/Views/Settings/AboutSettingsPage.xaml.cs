using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.Services;
using MiaDock.Core.Applications;

namespace MiaDock.App.Views.Settings;

public sealed partial class AboutSettingsPage : UserControl
{
    private static readonly IReadOnlyDictionary<string, ExternalLink> Links =
        new Dictionary<string, ExternalLink>(StringComparer.Ordinal)
        {
            ["github"] = new(
                "GitHub",
                "GitHub",
                new Uri("https://github.com/mymiamo/MiaDock")),
            ["instagram"] = new(
                "Instagram",
                "Instagram",
                new Uri("https://www.instagram.com/mymiamonet/")),
            ["website"] = new(
                "Web sitesi",
                "Website",
                new Uri("https://mymiamo.net"))
        };

    private readonly IExternalUriLauncher _launcher;
    private readonly IAppLocalizationService _localization;
    private bool _openingLink;

    public AboutSettingsPage(
        IExternalUriLauncher launcher,
        IAppLocalizationService localization)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        InitializeComponent();
    }

    private async void OnExternalLinkClick(object sender, RoutedEventArgs args)
    {
        if (_openingLink ||
            sender is not Button button ||
            button.Tag is not string key ||
            !Links.TryGetValue(key, out var link))
        {
            return;
        }

        _openingLink = true;
        button.IsEnabled = false;
        ExternalLinkError.IsOpen = false;
        try
        {
            if (!await _launcher.LaunchAsync(link.Uri))
            {
                ShowLaunchFailure(link);
            }
        }
        catch (OperationCanceledException)
        {
            // Closing the page may cancel an in-flight launcher operation.
        }
        catch
        {
            ShowLaunchFailure(link);
        }
        finally
        {
            button.IsEnabled = true;
            _openingLink = false;
        }
    }

    private void ShowLaunchFailure(ExternalLink link)
    {
        var name = _localization.Text(link.TurkishName, link.EnglishName);
        ExternalLinkError.Message = _localization.Text(
            $"{name} bağlantısı varsayılan tarayıcıda açılamadı. Adresi kopyalayıp tarayıcınızda deneyebilirsiniz: {link.Uri}",
            $"The {name} link could not be opened in your default browser. You can copy and try the address in your browser: {link.Uri}");
        ExternalLinkError.IsOpen = true;
    }

    private sealed record ExternalLink(
        string TurkishName,
        string EnglishName,
        Uri Uri);
}
