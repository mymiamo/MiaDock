using Microsoft.UI.Windowing;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
    private readonly HomeSettingsPage _homePage;
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
        _homePage = CreatePage(new HomeSettingsPage());
        _homePage.NavigationRequested += OnHomeNavigationRequested;
        _modulesPage = CreatePage(new ModulesSettingsPage(localization));
        _modulesPage.DetailsRequested += OnModuleDetailsRequested;
        _pages = new Dictionary<string, UserControl>(StringComparer.Ordinal)
        {
            ["home"] = _homePage,
            ["general"] = CreatePage(new GeneralSettingsPage()),
            ["modules"] = _modulesPage,
            ["appearance"] = CreatePage(new AppearanceSettingsPage()),
            ["media"] = CreatePage(new MediaSettingsPage()),
            ["notifications"] = CreatePage(new NotificationSettingsPage(localization)),
            ["time"] = CreatePage(new TimeAndHotKeysSettingsPage()),
            ["fullscreen"] = CreatePage(new FullscreenSettingsPage()),
            ["monitor"] = CreatePage(new MonitorSettingsPage()),
            ["tray"] = CreatePage(new TraySettingsPage()),
            ["startup"] = CreatePage(new StartupShutdownSettingsPage()),
            ["diagnostics"] = new DiagnosticsSettingsPage(diagnosticsViewModel, diagnosticsFileService, this, localization),
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
        TryApplySystemBackdrop();
        AppWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

        Navigation.SelectedItem = Navigation.MenuItems[0];
        PageHost.Content = _pages["home"];
        AppWindow.Closing += OnAppWindowClosing;
    }

    private void ApplyLocalization()
    {
        string L(string turkish, string english) => _localization.Text(turkish, english);
        Title = L("MiaDock Ayarları", "MiaDock Settings");
        var english = _localization.CurrentLanguage == AppLanguage.English;
        _searchItems =
        [
            SearchItem.Create("Ana sayfa", "Home", "Hızlı ayarlar, durum ve güncellemeler", "Quick settings, status and updates", "home", english),
            SearchItem.Create("Genel", "General", "Görünürlük, etkileşim ve dock konumu", "Visibility, interaction and dock position", "general", english),
            SearchItem.Create("Modüller", "Modules", "Özellikler, izinler ve hassas içerik", "Features, permissions and sensitive content", "modules", english),
            SearchItem.Create("Görünüm", "Appearance", "Tema, boyut, renk ve animasyon", "Theme, size, color and animation", "appearance", english),
            SearchItem.Create("Medya", "Media", "Kaynak uygulama ve ses denetimi", "Source app and volume controls", "media", english),
            SearchItem.Create("Bildirimler", "Notifications", "Windows izni, uygulama filtreleri ve gizlilik", "Windows access, app filters and privacy", "notifications", english),
            SearchItem.Create("Zaman ve kısayollar", "Time and shortcuts", "Zamanlayıcı, kronometre ve global kısayollar", "Timer, stopwatch and global shortcuts", "time", english),
            SearchItem.Create("Tam ekran", "Fullscreen", "Oyun ve tam ekran bildirim davranışı", "Games and fullscreen notification behavior", "fullscreen", english),
            SearchItem.Create("Monitör", "Monitor", "Ekran seçimi ve konumlandırma", "Display selection and positioning", "monitor", english),
            SearchItem.Create("Sistem tepsisi", "System tray", "Tepsi simgesi ve geçici bildirimler", "Tray icon and temporary notifications", "tray", english),
            SearchItem.Create("Başlangıç ve kapanış", "Startup and shutdown", "Windows başlangıcı ve kapatma davranışı", "Windows startup and close behavior", "startup", english),
            SearchItem.Create("Tanılama", "Diagnostics", "Yerel loglar ve dışa aktarma", "Local logs and export", "diagnostics", english),
            SearchItem.Create("Hakkında", "About", "Sürüm ve gizlilik bilgileri", "Version and privacy information", "about", english)
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
        _homePage.NavigationRequested -= OnHomeNavigationRequested;
        _modulesPage.DetailsRequested -= OnModuleDetailsRequested;
        foreach (var page in _pages.Values) page.Loaded -= OnPageLoaded;
        Closed -= OnClosed;
    }

    private void OnHomeNavigationRequested(object? sender, string tag) => Navigate(tag);

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var wide = args.NewSize.Width >= 920;
        Navigation.PaneDisplayMode = wide
            ? NavigationViewPaneDisplayMode.Left
            : NavigationViewPaneDisplayMode.LeftCompact;
        Navigation.IsPaneOpen = wide;
        TitleBarAppName.Visibility = args.NewSize.Width >= 720
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void TryApplySystemBackdrop()
    {
        try
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
        }
        catch
        {
            SystemBackdrop = null;
        }
    }

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
            var dialog = new CloseBehaviorDialog(_localization) { XamlRoot = Root.XamlRoot };
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

    private sealed record SearchItem(
        string Title,
        string Description,
        string Tag,
        string TurkishTitle,
        string EnglishTitle,
        string TurkishDescription,
        string EnglishDescription)
    {
        public string SearchText =>
            $"{TurkishTitle} {EnglishTitle} {TurkishDescription} {EnglishDescription}";

        public static SearchItem Create(
            string turkishTitle,
            string englishTitle,
            string turkishDescription,
            string englishDescription,
            string tag,
            bool english) =>
            new(
                english ? englishTitle : turkishTitle,
                english ? englishDescription : turkishDescription,
                tag,
                turkishTitle,
                englishTitle,
                turkishDescription,
                englishDescription);

        public override string ToString() => Title;
    }
}
