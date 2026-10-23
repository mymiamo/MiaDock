using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using Microsoft.Extensions.DependencyInjection;
using MiaDock.App.Services;
using MiaDock.Core.Settings;
using MiaDock.Platform.Windows.Display;

namespace MiaDock.App.Views.Settings;

public sealed partial class MonitorSettingsPage : UserControl
{
    private readonly ISettingsService? _settings;
    private readonly IDisplayTopologyService? _displays;
    private readonly IAppLocalizationService? _localization;

    public MonitorSettingsPage()
    {
        _settings = App.Services?.GetService<ISettingsService>();
        _displays = App.Services?.GetService<IDisplayTopologyService>();
        _localization = App.Services?.GetService<IAppLocalizationService>();
        InitializeComponent();
        Loaded += (_, _) => {
            Refresh();
            if (_displays is not null) _displays.DisplaysChanged += OnDisplaysChanged;
            if (_settings is not null) _settings.SettingsChanged += OnSettingsChanged;
        };
        Unloaded += (_, _) => {
            if (_displays is not null) _displays.DisplaysChanged -= OnDisplaysChanged;
            if (_settings is not null) _settings.SettingsChanged -= OnSettingsChanged;
        };
    }

    private void Refresh()
    {
        if (!DispatcherQueue.HasThreadAccess) { DispatcherQueue.TryEnqueue(Refresh); return; }
        var displayId = (ProfileDisplay.SelectedItem as DisplayDescriptor)?.Id;
        var profileId = (SavedProfile.SelectedItem as MonitorAppearanceProfile)?.Id;
        ProfileDisplay.ItemsSource = _displays?.Displays;
        ProfileDisplay.SelectedItem = _displays?.Find(displayId) ?? _displays?.Primary;
        SavedProfile.ItemsSource = _settings?.Current.DisplayProfiles;
        SavedProfile.SelectedItem = _settings?.Current.DisplayProfiles?.FirstOrDefault(item => item.Id == profileId)
            ?? _settings?.Current.DisplayProfiles?.FirstOrDefault();
    }

    private void OnDisplaysChanged(object? sender, IReadOnlyList<DisplayDescriptor> displays) => Refresh();
    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs args)
    {
        if (args.Previous.DisplayProfiles != args.Current.DisplayProfiles) Refresh();
    }

    private string L(string tr, string en) => _localization?.Text(tr, en) ?? tr;
    private void OnSaveProfileClick(object sender, RoutedEventArgs args)
    {
        if (_settings is null || ProfileDisplay.SelectedItem is not DisplayDescriptor display) return;
        if (string.IsNullOrWhiteSpace(ProfileName.Text)) { ProfileStatus.Text = L("Bir profil adı yazın.", "Enter a profile name."); return; }
        var profiles = (_settings.Current.DisplayProfiles ?? []).ToList();
        var existing = profiles.FindIndex(item => string.Equals(item.Name, ProfileName.Text.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing < 0 && profiles.Count >= 16) { ProfileStatus.Text = L("En fazla 16 profil kaydedilebilir.", "Up to 16 profiles can be saved."); return; }
        var profile = new MonitorAppearanceProfile(existing >= 0 ? profiles[existing].Id : Guid.NewGuid().ToString("N"),
            ProfileName.Text.Trim(), display.Id, _settings.Current.General.Position, _settings.Current.Appearance);
        profiles = profiles.Select(item => item.DisplayId == display.Id ? item with { DisplayId = null } : item).ToList();
        if (existing >= 0) profiles[existing] = profile; else profiles.Add(profile);
        _settings.Update(current => current with { DisplayProfiles = profiles });
        ProfileStatus.Text = L("Görünüm profili kaydedildi.", "Display profile saved.");
    }

    private void OnAssignProfileClick(object sender, RoutedEventArgs args)
    {
        if (_settings is null || SavedProfile.SelectedItem is not MonitorAppearanceProfile profile ||
            ProfileDisplay.SelectedItem is not DisplayDescriptor display) return;
        _settings.Update(current => current with { DisplayProfiles = (current.DisplayProfiles ?? []).Select(item =>
            item.Id == profile.Id ? item with { DisplayId = display.Id } : item.DisplayId == display.Id ? item with { DisplayId = null } : item).ToArray() });
        ProfileStatus.Text = L("Profil bu ekranla eşleştirildi.", "Profile assigned to this display.");
    }

    private void OnUnassignProfileClick(object sender, RoutedEventArgs args)
    {
        if (_settings is null || ProfileDisplay.SelectedItem is not DisplayDescriptor display) return;
        _settings.Update(current => current with { DisplayProfiles = (current.DisplayProfiles ?? []).Select(item =>
            item.DisplayId == display.Id ? item with { DisplayId = null } : item).ToArray() });
        ProfileStatus.Text = L("Bu ekran genel görünümü kullanıyor.", "This display uses the global appearance.");
    }

    private void OnRemoveProfileClick(object sender, RoutedEventArgs args)
    {
        if (_settings is null || SavedProfile.SelectedItem is not MonitorAppearanceProfile profile) return;
        _settings.Update(current => current with { DisplayProfiles = (current.DisplayProfiles ?? []).Where(item => item.Id != profile.Id).ToArray() });
        ProfileStatus.Text = L("Görünüm profili kaldırıldı.", "Display profile removed.");
    }
}
