using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MiaDock.App.Services;

namespace MiaDock.App.Dialogs;

public sealed class ModuleServiceConsentDialog : ContentDialog
{
    public ModuleServiceConsentDialog(
        IReadOnlyList<ModuleServiceDisclosure> disclosures,
        IAppLocalizationService localization,
        bool isOnboarding,
        bool isReviewOnly = false)
    {
        ArgumentNullException.ThrowIfNull(disclosures);
        ArgumentNullException.ThrowIfNull(localization);

        Title = isReviewOnly
            ? localization.Text(
                "Modül servisleri ve izinler",
                "Module services and permissions")
            : localization.Text(
                isOnboarding
                    ? "Seçili modüllere izin verilsin mi?"
                    : "Bu modüle izin verilsin mi?",
                isOnboarding
                    ? "Allow the selected modules?"
                    : "Allow this module?");
        if (!isReviewOnly)
        {
            PrimaryButtonText = localization.Text("İzin ver", "Allow");
            DefaultButton = ContentDialogButton.Primary;
        }
        CloseButtonText = isReviewOnly
            ? localization.Text("Kapat", "Close")
            : localization.Text("Vazgeç", "Not now");
        Content = BuildContent(disclosures, localization);
    }

    private static UIElement BuildContent(
        IReadOnlyList<ModuleServiceDisclosure> disclosures,
        IAppLocalizationService localization)
    {
        var list = new StackPanel { Spacing = 10 };
        list.Children.Add(new TextBlock
        {
            Text = localization.Text(
                "MiaDock seçtiğiniz özellikler için aşağıdaki Windows API'lerini veya yalnız cihazda çalışan yerel servisleri kullanacak. Veriler sunucuya gönderilmez.",
                "MiaDock will use the following Windows APIs or device-local services for the features you selected. Data is not sent to a server."),
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.78
        });

        foreach (var disclosure in disclosures)
        {
            list.Children.Add(BuildDisclosureCard(disclosure, localization));
        }

        return new ScrollViewer
        {
            MaxHeight = 440,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = list
        };
    }

    private static UIElement BuildDisclosureCard(
        ModuleServiceDisclosure disclosure,
        IAppLocalizationService localization)
    {
        var content = new StackPanel { Spacing = 4 };
        content.Children.Add(new TextBlock
        {
            Text = disclosure.Title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = 15
        });
        content.Children.Add(new TextBlock
        {
            Text = localization.Text(
                $"Kullanılan servis: {disclosure.ServiceName}",
                $"Service used: {disclosure.ServiceName}"),
            TextWrapping = TextWrapping.Wrap,
            Foreground = Application.Current.Resources[
                "SystemAccentColor"] as Brush
        });
        content.Children.Add(new TextBlock
        {
            Text = disclosure.DataUse,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.72
        });
        if (disclosure.RequiresWindowsPermission)
        {
            content.Children.Add(new TextBlock
            {
                Text = localization.Text(
                    "Bu modül ayrıca Windows izin penceresini açacaktır.",
                    "This module will also open the Windows permission prompt."),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
        }

        return new Border
        {
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(10),
            Background = Application.Current.Resources[
                "CardBackgroundFillColorDefaultBrush"] as Brush,
            BorderBrush = Application.Current.Resources[
                "CardStrokeColorDefaultBrush"] as Brush,
            BorderThickness = new Thickness(1),
            Child = content
        };
    }
}
