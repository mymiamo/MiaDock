using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MiaDock.App.Services;
using MiaDock.Core.Settings;
using Windows.Storage.Pickers;
using System.Text.Json;

namespace MiaDock.App.Views.Settings;

public sealed partial class AppearanceSettingsPage : UserControl
{
    private readonly Window? _owner;
    private readonly ISettingsService? _settings;
    private readonly IAppLocalizationService? _localization;

    public AppearanceSettingsPage(Window? owner = null)
    {
        _owner = owner;
        _settings = App.Services?.GetService<ISettingsService>();
        _localization = App.Services?.GetService<IAppLocalizationService>();
        InitializeComponent();
        ExportCustomizationButton.IsEnabled = ImportCustomizationButton.IsEnabled = owner is not null && _settings is not null;
    }

    private string L(string tr, string en) => _localization?.Text(tr, en) ?? tr;

    private async void OnExportCustomizationClick(object sender, RoutedEventArgs args)
    {
        if (_owner is null || _settings is null) return;
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = "MiaDock-personalization" };
            picker.FileTypeChoices.Add("MiaDock JSON", [".json"]);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(_owner));
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            await File.WriteAllTextAsync(file.Path, PortableCustomization.Export(_settings.Current));
            TransferStatus.Text = L("Kişiselleştirme dışa aktarıldı.", "Personalization exported.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
        { TransferStatus.Text = L("Dosya işlemi tamamlanamadı.", "The file operation could not be completed."); }
    }

    private async void OnImportCustomizationClick(object sender, RoutedEventArgs args)
    {
        if (_owner is null || _settings is null) return;
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(_owner));
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            // Bound the read itself, including files changed after the picker.
            await using var stream = File.OpenRead(file.Path);
            var bytes = new byte[PortableCustomization.MaximumBytes + 1];
            var length = 0;
            while (length < bytes.Length)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(length));
                if (read == 0) break;
                length += read;
            }
            if (length > PortableCustomization.MaximumBytes) throw new InvalidDataException();
            var package = PortableCustomization.Read(System.Text.Encoding.UTF8.GetString(bytes, 0, length).TrimStart('\uFEFF'));
            // Validate and preview before changing any live settings.
            _ = PortableCustomization.Apply(_settings.Current, package);
            var dialog = new ContentDialog { XamlRoot = XamlRoot,
                Title = L("Kişiselleştirme içe aktarılsın mı?", "Import personalization?"),
                Content = L("Tema, düzen, favoriler ve Odak profilleri değiştirilir. Diğer cihaz ayarları korunur. Geri dönüş kopyası yerel ayar klasörüne kaydedilir.",
                    "Theme, layout, favorites and Focus profiles will be replaced. Other device settings are preserved. A recovery copy is saved in the local settings folder."),
                PrimaryButtonText = L("İçe aktar", "Import"), CloseButtonText = L("İptal", "Cancel"), DefaultButton = ContentDialogButton.Close };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
            var directory = Path.GetDirectoryName(_settings.SettingsFilePath)!;
            Directory.CreateDirectory(directory);
            // A local recovery copy retains machine assignments and Focus rules as well.
            // Keep the portable copy too, so users can restore the visual settings with this picker.
            var beforeImport = _settings.Current;
            await File.WriteAllTextAsync(Path.Combine(directory, "settings-before-personalization-import.json"),
                JsonSerializer.Serialize(beforeImport, new JsonSerializerOptions { WriteIndented = true }));
            await File.WriteAllTextAsync(Path.Combine(directory, "personalization-before-import.json"), PortableCustomization.Export(beforeImport));
            // Read the current unrelated settings again in case they changed while the dialog was open.
            _settings.Update(current => PortableCustomization.Apply(current, package));
            await _settings.FlushAsync();
            TransferStatus.Text = L("Kişiselleştirme içe aktarıldı. Monitör profillerini bu bilgisayarın ekranlarıyla eşleştirebilirsiniz.",
                "Personalization imported. You can assign display profiles to this computer's monitors.");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException or JsonException or NotSupportedException or System.Runtime.InteropServices.COMException)
        { TransferStatus.Text = L("Dosya okunamadı veya kişiselleştirme biçimi desteklenmiyor.", "The file could not be read or its personalization format is unsupported."); }
    }
}
