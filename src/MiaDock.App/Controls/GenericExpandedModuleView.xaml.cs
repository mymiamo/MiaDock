using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.Core.Modules;

namespace MiaDock.App.Controls;

public sealed partial class GenericExpandedModuleView : UserControl
{
    public GenericExpandedModuleView() => InitializeComponent();

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args) =>
        UpdateContentState(args.NewValue as ModulePresentation);

    private void UpdateContentState(ModulePresentation? presentation)
    {
        var hasContent = HasDisplayContent(presentation);
        ContentPanel.Visibility = hasContent ? Visibility.Visible : Visibility.Collapsed;
        EmptyState.Visibility = hasContent ? Visibility.Collapsed : Visibility.Visible;
        EmptyStateText.Text = GetEmptyStateText(presentation?.ModuleId);
    }

    private static bool HasDisplayContent(ModulePresentation? presentation) =>
        presentation is not null &&
        (!string.IsNullOrWhiteSpace(presentation.PrimaryText) ||
         !string.IsNullOrWhiteSpace(presentation.SecondaryText) ||
         !string.IsNullOrWhiteSpace(presentation.ValueText) ||
         presentation.Progress is not null);

    private static string GetEmptyStateText(string? moduleId) => moduleId switch
    {
        "timer" => "Etkin zamanlayıcı yok",
        "bluetooth" => "Bluetooth cihazı bağlı değil",
        "transfers" => "Aktarım bulunmuyor",
        _ => "Etkin olay yok"
    };
}
