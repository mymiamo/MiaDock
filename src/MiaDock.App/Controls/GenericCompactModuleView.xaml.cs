using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.Core.Localization;
using MiaDock.Core.Modules;

namespace MiaDock.App.Controls;

public sealed partial class GenericCompactModuleView : UserControl
{
    private ILocalizationService? _localization;

    public GenericCompactModuleView(ILocalizationService? localization = null)
    {
        _localization = localization;
        InitializeComponent();
    }

    public void ConfigureLocalization(ILocalizationService localization)
    {
        _localization = localization;
        UpdateContentState(DataContext as ModulePresentation);
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) =>
        UpdateContentState(args.NewValue as ModulePresentation);

    private void UpdateContentState(ModulePresentation? presentation)
    {
        var hasContent = HasDisplayContent(presentation);
        ContentRow.Visibility = hasContent ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;
        EmptyStateText.Text = GetEmptyStateText(presentation?.ModuleId);
    }

    private static bool HasDisplayContent(ModulePresentation? presentation) =>
        presentation is not null &&
        (!string.IsNullOrWhiteSpace(presentation.PrimaryText) ||
         !string.IsNullOrWhiteSpace(presentation.SecondaryText) ||
         !string.IsNullOrWhiteSpace(presentation.ValueText) ||
         presentation.Progress is not null);

    private string GetEmptyStateText(string? moduleId) => moduleId switch
    {
        "timer" => Text("Dock.Empty.Timer", "Etkin zamanlayıcı yok"),
        "bluetooth" => Text("Dock.Empty.Bluetooth", "Bluetooth cihazı bağlı değil"),
        "transfers" => Text("Dock.Empty.Transfers", "Aktarım bulunmuyor"),
        _ => Text("Dock.NoActiveEvent", "Etkin olay yok")
    };

    private string Text(string key, string fallback) =>
        _localization?.Get(key) is { } value && value != key ? value : fallback;
}
