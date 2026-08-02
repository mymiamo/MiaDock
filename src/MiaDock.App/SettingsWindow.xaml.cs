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
using System.Globalization;
using System.Text;

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
        IAppLocalizationService localization,
        FocusSettingsViewModel focusSettingsViewModel)
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
            ["focus"] = new FocusSettingsPage(
                focusSettingsViewModel,
                localization),
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
            SearchItem.Create("Ana sayfa", "Home", "Hızlı ayarlar, durum ve güncellemeler", "Quick settings, status and updates", "home", english,
                "dashboard başlangıç özeti modül durumu güncelleme denetle update check status"),
            SearchItem.Create("Genel", "General", "Görünürlük, etkileşim, dil, saat ve dock konumu", "Visibility, interaction, language, clock and dock position", "general", english,
                "türkçe english language dil hover fare tıklama click always visible events only saat clock saniye seconds tarih date weekday konum position otomatik güncelleme"),
            SearchItem.Create("Odak", "Focus", "Profiller, Rahatsız Etmeyin, görünürlük ve gizlilik", "Profiles, Do Not Disturb, visibility and privacy", "focus", english,
                "dnd rahatsız etmeyin work gaming sleep çalışma oyun uyku schedule zamanlama otomasyon profile"),
            SearchItem.Create("Modüller", "Modules", "Özellikler, kullanılan Windows servisleri ve izinler", "Features, Windows services used and permissions", "modules", english,
                "media volume microphone camera battery network bluetooth timer notifications transfer servis izin permission privacy hassas içerik"),
            SearchItem.Create("Görünüm", "Appearance", "Tema, boyut, renk, opaklık, radius ve animasyon", "Theme, size, color, opacity, radius and animation", "appearance", english,
                "apple mica acrylic blur blurred glass saydam cam köşe corner radius genişlik width height yükseklik opacity şeffaflık shadow gölge animation"),
            SearchItem.Create("Medya", "Media", "Kaynak uygulama, oynatma ve ses denetimi", "Source app, playback and volume controls", "media", english,
                "spotify apple music youtube browser tarayıcı play pause previous next seek album cover kapak ses volume"),
            SearchItem.Create("Bildirimler", "Notifications", "Windows izni, uygulama filtreleri, başlık ve gövde gizliliği", "Windows access, app filters, title and body privacy", "notifications", english,
                "usernotificationlistener allow list block list izin permission title body başlık gövde uygulama"),
            SearchItem.Create("Zaman ve kısayollar", "Time and shortcuts", "Zamanlayıcı, kronometre, alarm ve global kısayollar", "Timer, stopwatch, alarm and global shortcuts", "time", english,
                "countdown sayaç lap tur ringtone alarm sustur hotkey registerhotkey ctrl alt shift"),
            SearchItem.Create("Tam ekran", "Fullscreen", "Oyun ve tam ekran bildirim davranışı", "Games and fullscreen notification behavior", "fullscreen", english,
                "game oyun borderless fullscreen minimal controls notification süre"),
            SearchItem.Create("Monitör", "Monitor", "Ekran seçimi, DPI ve konumlandırma", "Display selection, DPI and positioning", "monitor", english,
                "primary active fixed ana aktif sabit monitor display screen ölçek scaling dpi"),
            SearchItem.Create("Sistem tepsisi", "System tray", "Koyu tepsi menüsü, simge ve geçici bildirimler", "Dark tray menu, icon and temporary notifications", "tray", english,
                "tray icon sağ tık right click menu göster gizle exit çıkış"),
            SearchItem.Create("Başlangıç ve kapanış", "Startup and shutdown", "Windows başlangıcı ve kapatma davranışı", "Windows startup and close behavior", "startup", english,
                "start with windows startup task açılışta çalıştır minimize küçült tamamen çık silent tray"),
            SearchItem.Create("Tanılama", "Diagnostics", "Yerel loglar, temizleme ve ZIP dışa aktarma", "Local logs, cleanup and ZIP export", "diagnostics", english,
                "log logs hata error clear temizle folder klasör export dışa aktar zip"),
            SearchItem.Create("Hakkında", "About", "Sürüm, Microsoft Store ve gizlilik bilgileri", "Version, Microsoft Store and privacy information", "about", english,
                "version sürüm update güncelle store mağaza privacy gizlilik 1.2.1.0")
        ];
        _localization.Apply(Root);
        foreach (var page in _pages.Values) _localization.Apply(page);
        ApplyNavigationLocalization();
        SettingsSearch.ItemsSource = Array.Empty<SearchItem>();
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
            : _searchItems
                .Where(item => item.Matches(query))
                .OrderBy(item => item.Score(query))
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .Take(8)
                .ToArray();
    }

    private void OnSearchSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SearchItem item) sender.Text = item.Title;
    }

    private void OnSearchQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var item = args.ChosenSuggestion as SearchItem ??
                   _searchItems
                       .Where(candidate => candidate.Matches(args.QueryText))
                       .OrderBy(candidate => candidate.Score(args.QueryText))
                       .FirstOrDefault();
        if (item is not null) Navigate(item.Tag);
    }

    private void ApplyNavigationLocalization()
    {
        var labels = new Dictionary<string, (string Turkish, string English)>(
            StringComparer.Ordinal)
        {
            ["home"] = ("Ana sayfa", "Home"),
            ["general"] = ("Genel", "General"),
            ["focus"] = ("Odak", "Focus"),
            ["modules"] = ("Modüller", "Modules"),
            ["appearance"] = ("Görünüm", "Appearance"),
            ["media"] = ("Medya", "Media"),
            ["notifications"] = ("Bildirimler", "Notifications"),
            ["time"] = ("Zaman ve kısayollar", "Time and shortcuts"),
            ["fullscreen"] = ("Tam ekran", "Fullscreen"),
            ["monitor"] = ("Monitör", "Monitor"),
            ["tray"] = ("Sistem tepsisi", "System tray"),
            ["startup"] = ("Başlangıç ve kapanış", "Startup and shutdown"),
            ["diagnostics"] = ("Tanılama", "Diagnostics"),
            ["about"] = ("Hakkında", "About")
        };

        foreach (var item in Navigation.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Tag is not string tag ||
                !labels.TryGetValue(tag, out var label))
            {
                continue;
            }

            var text = _localization.Text(label.Turkish, label.English);
            item.Content = text;
            ToolTipService.SetToolTip(item, text);
        }
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
        string EnglishDescription,
        string Keywords)
    {
        public string SearchText =>
            $"{TurkishTitle} {EnglishTitle} {TurkishDescription} {EnglishDescription} {Keywords}";

        public bool Matches(string query)
        {
            var search = Normalize(SearchText);
            return Normalize(query)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .All(token => search.Contains(token, StringComparison.Ordinal));
        }

        public int Score(string query)
        {
            var normalizedQuery = Normalize(query);
            var currentTitle = Normalize(Title);
            var bothTitles = Normalize($"{TurkishTitle} {EnglishTitle}");
            return currentTitle.StartsWith(normalizedQuery, StringComparison.Ordinal)
                ? 0
                : bothTitles.Contains(normalizedQuery, StringComparison.Ordinal)
                    ? 1
                    : 2;
        }

        public static SearchItem Create(
            string turkishTitle,
            string englishTitle,
            string turkishDescription,
            string englishDescription,
            string tag,
            bool english,
            string keywords) =>
            new(
                english ? englishTitle : turkishTitle,
                english ? englishDescription : turkishDescription,
                tag,
                turkishTitle,
                englishTitle,
                turkishDescription,
                englishDescription,
                keywords);

        private static string Normalize(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value
                         .Replace('ı', 'i')
                         .Replace('İ', 'I')
                         .Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) !=
                    UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        public override string ToString() => Title;
    }
}
