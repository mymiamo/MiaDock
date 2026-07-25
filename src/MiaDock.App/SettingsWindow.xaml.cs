using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MiaDock.App.ViewModels;
using MiaDock.App.Views.Settings;
using MiaDock.App.Dialogs;
using MiaDock.App.Services;
using MiaDock.Core.Settings;
using Windows.Graphics;
using MiaDock.App.Infrastructure;

namespace MiaDock.App;

public sealed partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly IApplicationLifetimeService _lifetime;
    private readonly IAppLocalizationService _localization;
    private readonly ModulesSettingsPage _modulesPage;
    private readonly Dictionary<string, UserControl> _pages;
    private bool _closeDecisionPending;
    private bool _allowClose;
    private IReadOnlyList<SearchItem> _searchItems = [];

    public SettingsWindow(
        SettingsViewModel viewModel,
        ISettingsService settings,
        IApplicationLifetimeService lifetime,
        DiagnosticsViewModel diagnosticsViewModel,
        IDiagnosticsFileService diagnosticsFileService,
        IAppLocalizationService localization)
    {
        InitializeComponent();
        WindowBranding.ApplyIcon(this);
        _viewModel = viewModel;
        _settings = settings;
        _lifetime = lifetime;
        _localization = localization;
        Root.DataContext = viewModel;
        _modulesPage = CreatePage(new ModulesSettingsPage());
        _modulesPage.DetailsRequested += OnModuleDetailsRequested;
        _pages = new Dictionary<string, UserControl>(StringComparer.Ordinal)
        {
            ["general"] = CreatePage(new GeneralSettingsPage()),
            ["modules"] = _modulesPage,
            ["appearance"] = CreatePage(new AppearanceSettingsPage()),
            ["media"] = CreatePage(new MediaSettingsPage()),
            ["notifications"] = CreatePage(new NotificationSettingsPage()),
            ["time"] = CreatePage(new TimeAndHotKeysSettingsPage()),
            ["fullscreen"] = CreatePage(new FullscreenSettingsPage()),
            ["monitor"] = CreatePage(new MonitorSettingsPage()),
            ["tray"] = CreatePage(new TraySettingsPage()),
            ["startup"] = CreatePage(new StartupShutdownSettingsPage()),
            ["diagnostics"] = new DiagnosticsSettingsPage(diagnosticsViewModel, diagnosticsFileService, this),
            ["about"] = CreatePage(new AboutSettingsPage())
        };

        foreach (var page in _pages.Values) page.Loaded += OnPageLoaded;
        _localization.LanguageChanged += OnLanguageChanged;
        Closed += OnClosed;
        ApplyLocalization();

        AppWindow.Resize(new SizeInt32(1040, 760));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

        Navigation.SelectedItem = Navigation.MenuItems[0];
        PageHost.Content = _pages["general"];
        AppWindow.Closing += OnAppWindowClosing;
    }

    private void ApplyLocalization()
    {
        string L(string turkish, string english) => _localization.Text(turkish, english);
        Title = L("MiaDock Ayarları", "MiaDock Settings");
        _searchItems =
        [
            new(L("Genel", "General"), L("Görünürlük, etkileşim ve ada konumu", "Visibility, interaction and island position"), "general"),
            new(L("Modüller", "Modules"), L("Özellikler, izinler ve hassas içerik", "Features, permissions and sensitive content"), "modules"),
            new(L("Görünüm", "Appearance"), L("Tema, boyut, renk ve animasyon", "Theme, size, color and animation"), "appearance"),
            new(L("Medya", "Media"), L("Kaynak uygulama ve ses denetimi", "Source app and volume controls"), "media"),
            new(L("Bildirimler", "Notifications"), L("Windows izni, uygulama filtreleri ve gizlilik", "Windows access, app filters and privacy"), "notifications"),
            new(L("Zaman ve kısayollar", "Time and shortcuts"), L("Zamanlayıcı, kronometre ve global kısayollar", "Timer, stopwatch and global shortcuts"), "time"),
            new(L("Tam ekran", "Fullscreen"), L("Oyun ve tam ekran bildirim davranışı", "Games and fullscreen notification behavior"), "fullscreen"),
            new(L("Monitör", "Monitor"), L("Ekran seçimi ve konumlandırma", "Display selection and positioning"), "monitor"),
            new(L("Sistem tepsisi", "System tray"), L("Tepsi simgesi ve geçici bildirimler", "Tray icon and temporary notifications"), "tray"),
            new(L("Başlangıç ve kapanış", "Startup and shutdown"), L("Windows başlangıcı ve kapatma davranışı", "Windows startup and close behavior"), "startup"),
            new(L("Tanılama", "Diagnostics"), L("Yerel loglar ve dışa aktarma", "Local logs and export"), "diagnostics"),
            new(L("Hakkında", "About"), L("Sürüm ve gizlilik bilgileri", "Version and privacy information"), "about")
        ];
        _localization.Apply(Root);
        foreach (var page in _pages.Values) _localization.Apply(page);
    }

    private void OnLanguageChanged(object? sender, EventArgs args) => ApplyLocalization();

    private void OnPageLoaded(object sender, RoutedEventArgs args) =>
        _localization.Apply((DependencyObject)sender);

    private void OnClosed(object sender, WindowEventArgs args)
    {
        _localization.LanguageChanged -= OnLanguageChanged;
        _modulesPage.DetailsRequested -= OnModuleDetailsRequested;
        foreach (var page in _pages.Values) page.Loaded -= OnPageLoaded;
        Closed -= OnClosed;
    }

    private void OnHomeClick(object sender, RoutedEventArgs args) => Navigate("general");

    private void OnModuleDetailsRequested(object? sender, string moduleId) => Navigate(moduleId switch
    {
        "media" => "media",
        "notifications" => "notifications",
        "timer" => "time",
        "battery" => "general",
        _ => "modules"
    });

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var query = sender.Text.Trim();
        sender.ItemsSource = string.IsNullOrEmpty(query)
            ? Array.Empty<SearchItem>()
            : _searchItems.Where(item => item.SearchText.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();
    }

    private void OnSearchSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchItem item) sender.Text = item.Title;
    }

    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var item = args.ChosenSuggestion as SearchItem ??
                   _searchItems.FirstOrDefault(candidate => candidate.SearchText.Contains(args.QueryText, StringComparison.CurrentCultureIgnoreCase));
        if (item is not null) Navigate(item.Tag);
    }

    private void Navigate(string tag)
    {
        var item = Navigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(candidate => Equals(candidate.Tag, tag));
        if (item is null) return;
        Navigation.SelectedItem = item;
        PageHost.Content = _pages[tag];
    }

    public void AllowCloseAndClose()
    {
        _allowClose = true;
        AppWindow.Closing -= OnAppWindowClosing;
        Close();
    }

    private T CreatePage<T>(T page) where T : UserControl
    {
        page.DataContext = _viewModel;
        return page;
    }

    private void OnNavigationSelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string tag && _pages.TryGetValue(tag, out var page))
        {
            PageHost.Content = page;
        }
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || _lifetime.IsShuttingDown)
        {
            return;
        }

        args.Cancel = true;
        if (_closeDecisionPending)
        {
            return;
        }

        var startup = _settings.Current.StartupShutdown;
        if (startup.HasConfirmedCloseBehavior)
        {
            ApplyCloseBehavior(startup.CloseBehavior);
            return;
        }

        _closeDecisionPending = true;
        try
        {
            var dialog = new CloseBehaviorDialog { XamlRoot = Root.XamlRoot };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            if (dialog.RememberChoice)
            {
                _settings.Update(settings => settings with
                {
                    StartupShutdown = settings.StartupShutdown with
                    {
                        CloseBehavior = dialog.SelectedBehavior,
                        HasConfirmedCloseBehavior = true
                    }
                });
            }

            ApplyCloseBehavior(dialog.SelectedBehavior);
        }
        finally
        {
            _closeDecisionPending = false;
        }
    }

    private void ApplyCloseBehavior(CloseBehaviorSetting behavior)
    {
        if (behavior == CloseBehaviorSetting.Exit)
        {
            _lifetime.RequestExit();
        }
        else
        {
            AppWindow.Hide();
        }
    }

    private sealed record SearchItem(string Title, string Description, string Tag)
    {
        public string SearchText => $"{Title} {Description}";
        public override string ToString() => Title;
    }
}
