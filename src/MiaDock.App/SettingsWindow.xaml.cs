using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using System.Text;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using MiaDock.App.Dialogs;
using MiaDock.App.Infrastructure;
using MiaDock.App.Models;
using MiaDock.App.Services;
using MiaDock.App.ViewModels;
using MiaDock.App.Views.Settings;
using MiaDock.Core.Applications;
using MiaDock.Core.Settings;
using MiaDock.Platform.Windows.Windowing;
using Windows.Graphics;
using Windows.UI.ViewManagement;

namespace MiaDock.App;

public sealed partial class SettingsWindow : Window
{
    private const double MinimumWindowWidth = 972;
    private const double MinimumWindowHeight = 692;
    private static string s_lastCategoryId = "home";
    private static string s_lastSubpageId = "home";

    private readonly SettingsViewModel _viewModel;
    private readonly ISettingsService _settings;
    private readonly IApplicationLifetimeService _lifetime;
    private readonly IAppLocalizationService _localization;
    private readonly HomeSettingsPage _homePage;
    private readonly ModulesSettingsPage _modulesPage;
    private readonly Dictionary<string, UserControl> _pages;
    private readonly WindowMinimumSizeMonitor _minimumSizeMonitor;
    private readonly Dictionary<string, string> _lastSubpageByCategory = new(StringComparer.Ordinal);
    private readonly List<string> _backStack = [];
    private IReadOnlyList<SettingsCategoryDefinition> _categories = [];
    private IReadOnlyList<SearchItem> _searchItems = [];
    private bool _closeDecisionPending;
    private bool _allowClose;
    private bool _micaApplied;
    private bool _isNarrow;
    private bool _syncingSubpageSelection;
    private bool _isNavigatingBack;

    public SettingsWindow(
        SettingsViewModel viewModel,
        ISettingsService settings,
        IApplicationLifetimeService lifetime,
        DiagnosticsViewModel diagnosticsViewModel,
        IDiagnosticsFileService diagnosticsFileService,
        IAppLocalizationService localization,
        FocusSettingsViewModel focusSettingsViewModel,
        IExternalUriLauncher externalUriLauncher)
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
            ["appearance"] = CreatePage(new AppearanceSettingsPage()),
            ["audible-notifications"] = CreatePage(new AudibleNotificationsSettingsPage()),
            ["monitor"] = CreatePage(new MonitorSettingsPage()),
            ["fullscreen"] = CreatePage(new FullscreenSettingsPage()),
            ["focus"] = new FocusSettingsPage(focusSettingsViewModel, localization),
            ["modules"] = _modulesPage,
            ["media"] = CreatePage(new MediaSettingsPage()),
            ["notifications"] = CreatePage(new NotificationSettingsPage(localization)),
            ["optional"] = CreatePage(new OptionalModulesSettingsPage()),
            ["shortcuts"] = CreatePage(new HotKeysSettingsPage()),
            ["tray"] = CreatePage(new TraySettingsPage()),
            ["startup"] = CreatePage(new StartupShutdownSettingsPage()),
            ["diagnostics"] = new DiagnosticsSettingsPage(
                diagnosticsViewModel,
                diagnosticsFileService,
                this,
                localization,
                externalUriLauncher),
            ["whats-new"] = CreatePage(new WhatsNewSettingsPage()),
            ["about"] = CreatePage(new AboutSettingsPage(externalUriLauncher, localization))
        };

        foreach (var page in _pages.Values) page.Loaded += OnPageLoaded;
        _localization.LanguageChanged += OnLanguageChanged;
        Closed += OnClosed;

        AppWindow.Resize(new SizeInt32(1040, 760));
        _minimumSizeMonitor = new WindowMinimumSizeMonitor(
            this,
            MinimumWindowWidth,
            MinimumWindowHeight);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsResizable = true;
            presenter.IsMaximizable = true;
            presenter.IsMinimizable = true;
        }

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        ApplySettingsTheme();
        AppWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
        AppWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);

        ApplyLocalization();
        AppWindow.Closing += OnAppWindowClosing;
    }

    internal IReadOnlyList<SettingsSubpageDefinition> CurrentSubpages { get; private set; } = [];

    private void ApplyLocalization()
    {
        string L(string turkish, string english) => _localization.Text(turkish, english);
        Title = L("MiaDock Ayarları", "MiaDock Settings");
        _categories = BuildCategories(L);
        _searchItems = BuildSearchItems(L);
        _localization.Apply(Root);
        foreach (var page in _pages.Values) _localization.Apply(page);
        ApplyNavigationLocalization();
        SettingsSearch.ItemsSource = Array.Empty<SearchItem>();
        RefreshCurrentCategory();
    }

    private IReadOnlyList<SettingsCategoryDefinition> BuildCategories(
        Func<string, string, string> localize) =>
    [
        Category("home", "Genel Bakış", "Overview", "Durum ve hızlı işlemler", "Status and quick actions", "\uE80F", "SettingsCategoryHomeBrush",
            Subpage("home", "home", "Genel Bakış", "Overview", "Durum, güncellemeler ve hızlı ayarlar", "Status, updates and quick settings", "\uE80F")),
        Category("personalization", "Kişiselleştir", "Personalize", "Dock görünümü, yerleşimi ve tam ekran", "Dock appearance, placement and fullscreen", "\uE771", "SettingsCategoryPersonalizationBrush",
            Subpage("general", "personalization", "Genel", "General", "Görünürlük, etkileşim ve dil", "Visibility, interaction and language", "\uE713"),
            Subpage("appearance", "personalization", "Görünüm", "Appearance", "Tema, boyut ve yüzey", "Theme, size and surface", "\uE790"),
            Subpage("audible-notifications", "personalization", "Sesli Bildirimler", "Notification Sounds", "Olay sesleri ve önizlemeler", "Event sounds and previews", "\uE995"),
            Subpage("monitor", "personalization", "Monitör", "Monitor", "Ekran ve konumlandırma", "Display and positioning", "\uE7F4"),
            Subpage("fullscreen", "personalization", "Tam Ekran", "Fullscreen", "Oyun ve tam ekran davranışı", "Game and fullscreen behavior", "\uE740")),
        Category("focus", "Odak", "Focus", "Odak profilleri ve otomasyon", "Focus profiles and automation", "\uE734", "SettingsCategoryFocusBrush",
            Subpage("focus", "focus", "Odak", "Focus", "Odak profilleri ve otomasyon", "Focus profiles and automation", "\uE734")),
        Category("modules", "Modüller", "Modules", "Özellikler, medya ve bildirimler", "Features, media and notifications", "\uE74C", "SettingsCategoryModulesBrush",
            Subpage("modules", "modules", "Modüller ve İzinler", "Modules and Permissions", "Özellikler ve kullandıkları Windows servisleri", "Features and their Windows services", "\uE74C"),
            Subpage("media", "modules", "Medya", "Media", "Medya kaynağı ve oynatma", "Media source and playback", "\uE8D6"),
            Subpage("notifications", "modules", "Bildirimler", "Notifications", "Bildirim erişimi ve gizlilik", "Notification access and privacy", "\uEA8F"),
            Subpage("optional", "modules", "İsteğe Bağlı", "Optional", "Klavye kilitleri, USB ve yardımcı özellikler", "Keyboard locks, USB and helper features", "\uE945")),
        Category("shortcuts", "Kısayollar", "Shortcuts", "Global klavye kısayolları", "Global keyboard shortcuts", "\uE765", "SettingsCategoryShortcutsBrush",
            Subpage("shortcuts", "shortcuts", "Kısayollar", "Shortcuts", "Dock ve modül global kısayolları", "Dock and module global shortcuts", "\uE765")),
        Category("system", "Sistem", "System", "Tepsi, başlangıç ve kapanış", "Tray, startup and shutdown", "\uE7F4", "SettingsCategorySystemBrush",
            Subpage("tray", "system", "Sistem Tepsisi", "System Tray", "Tepsi simgesi ve tıklama davranışı", "Tray icon and click behavior", "\uE712"),
            Subpage("startup", "system", "Başlangıç ve Kapanış", "Startup and Shutdown", "Windows başlangıcı ve pencere kapatma", "Windows startup and window close", "\uE7E8")),
        Category("support", "Destek", "Support", "Tanılama ve ürün bilgileri", "Diagnostics and product information", "\uE897", "SettingsCategorySupportBrush",
            Subpage("diagnostics", "support", "Tanılama", "Diagnostics", "Yerel loglar ve hata raporu", "Local logs and bug report", "\uE9D9"),
            Subpage("about", "support", "Hakkında", "About", "Sürüm ve bağlantılar", "Version and links", "\uE946")),
        Category("whats-new", "Yenilikler", "What's New", "Sürüm notları ve değişiklikler", "Release notes and changes", "\uE8BD", "SettingsCategoryWhatsNewBrush",
            Subpage("whats-new", "whats-new", "Yenilikler", "What's New", "Sürüm notları ve değişiklikler", "Release notes and changes", "\uE8BD"))
    ];

    private SettingsCategoryDefinition Category(
        string id, string trTitle, string enTitle, string trDescription, string enDescription,
        string glyph, string colorResourceKey, params SettingsSubpageDefinition[] subpages) =>
        new(id, _localization.Text(trTitle, enTitle), _localization.Text(trDescription, enDescription), glyph, colorResourceKey, subpages);

    private SettingsSubpageDefinition Subpage(
        string id, string categoryId, string trTitle, string enTitle, string trDescription,
        string enDescription, string glyph, string? focusTarget = null) =>
        new(id, categoryId, _localization.Text(trTitle, enTitle),
            _localization.Text(trDescription, enDescription), glyph, focusTarget);

    private IReadOnlyList<SearchItem> BuildSearchItems(Func<string, string, string> localize) =>
    [
        Search("Genel Bakış", "Overview", "Hızlı ayarlar, durum ve güncellemeler", "Quick settings, status and updates", "home", "home", null, localize, "dashboard başlangıç özeti ana sayfa home modül durumu update check status"),
        Search("Genel", "General", "Görünürlük, etkileşim, dil, saat ve dock konumu", "Visibility, interaction, language, clock and dock position", "personalization", "general", null, localize, "türkçe english language dil hover fare tıklama click always visible events only saat clock saniye seconds tarih date weekday konum position otomatik güncelleme"),
        Search("Görünüm", "Appearance", "Tema, boyut, renk, opaklık, radius ve animasyon", "Theme, size, color, opacity, radius and animation", "personalization", "appearance", null, localize, "apple mica acrylic blur glass köşe corner radius kenar mesafe edge spacing margin width height opacity shadow animation"),
        Search("Sesli Bildirimler", "Notification Sounds", "Ağ, pil ve cihaz olay sesleri", "Network, battery and device event sounds", "personalization", "audible-notifications", null, localize, "ses sound audio uyarı notification internet wifi ethernet pil battery cihaz device bağlandı ayrıldı saat başı hourly chime preview önizleme alarm"),
        Search("Monitör", "Monitor", "Ekran seçimi, DPI ve konumlandırma", "Display selection, DPI and positioning", "personalization", "monitor", null, localize, "primary active fixed ana aktif sabit monitor display screen ölçek scaling dpi"),
        Search("Tam Ekran", "Fullscreen", "Oyun ve tam ekran bildirim davranışı", "Games and fullscreen notification behavior", "personalization", "fullscreen", null, localize, "game oyun borderless minimal controls hide edge hover reveal"),
        Search("Odak", "Focus", "Etkinleştir, devre dışı bırak, profiller ve otomasyon", "Enable, disable, profiles and automation", "focus", "focus", null, localize, "dnd rahatsız etmeyin work gaming sleep schedule profile"),
        Search("Modüller ve İzinler", "Modules and Permissions", "Özellikler, Windows servisleri ve izinler", "Features, Windows services and permissions", "modules", "modules", null, localize, "media volume microphone camera battery network bluetooth timer notifications transfer servis izin permission privacy hassas içerik"),
        Search("Medya", "Media", "Kaynak uygulama, oynatma ve ses denetimi", "Source app, playback and volume controls", "modules", "media", null, localize, "spotify apple music youtube browser play pause previous next seek album cover ses volume"),
        Search("Bildirimler", "Notifications", "Windows izni, uygulama filtreleri ve gizlilik", "Windows access, app filters and privacy", "modules", "notifications", null, localize, "usernotificationlistener allow list block list izin permission title body başlık gövde"),
        Search("İsteğe Bağlı", "Optional", "Klavye kilitleri, USB ve yardımcı özellikler", "Keyboard locks, USB and helper features", "modules", "optional", null, localize, "caps lock num lock scroll lock klavye keyboard usb bellek zamanlayıcı timer saat başı hourly chime isteğe bağlı optional"),
        Search("Kısayollar", "Shortcuts", "Global klavye kısayolları", "Global keyboard shortcuts", "shortcuts", "shortcuts", null, localize, "hotkey registerhotkey ctrl alt shift kısayol shortcut dock göster gizle"),
        Search("Sistem Tepsisi", "System Tray", "Tepsi simgesi ve tek tık davranışı", "Tray icon and single-click behavior", "system", "tray", null, localize, "tray icon right click menu göster gizle exit çıkış"),
        Search("Başlangıç ve Kapanış", "Startup and Shutdown", "Windows başlangıcı ve kapatma davranışı", "Windows startup and close behavior", "system", "startup", null, localize, "start with windows startup task açılışta çalıştır minimize küçült tamamen çık"),
        Search("Tanılama", "Diagnostics", "Yerel loglar, temizleme ve ZIP dışa aktarma", "Local logs, cleanup and ZIP export", "support", "diagnostics", null, localize, "log logs hata error bug report clear temizle folder export zip mymiamo.net/bug"),
        Search("Hakkında", "About", "Sürüm, Microsoft Store ve sosyal bağlantılar", "Version, Microsoft Store and social links", "support", "about", null, localize, "version sürüm update store privacy github repository repo source code kaynak kod depo instagram social social media sosyal sosyal medya website web web sitesi mymiamo mymiamo.net"),
        Search("Yenilikler", "What's New", "Sürüm notları ve değişiklik listesi", "Release notes and change list", "whats-new", "whats-new", null, localize, "changelog release notes yenilik sürüm notes whats new değişiklik")
    ];

    private SearchItem Search(
        string trTitle, string enTitle, string trDescription, string enDescription,
        string categoryId, string subpageId, string? focusTarget,
        Func<string, string, string> localize, string keywords)
    {
        var category = _categories.First(item => item.Id == categoryId);
        return SearchItem.Create(trTitle, enTitle, trDescription, enDescription,
            categoryId, subpageId, focusTarget, category.Title, localize, keywords);
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
        _minimumSizeMonitor.Dispose();
        AppWindow.Closing -= OnAppWindowClosing;
        Closed -= OnClosed;
    }

    private void OnHomeNavigationRequested(object? sender, string subpageId) => Navigate(subpageId);

    private void OnRootSizeChanged(object sender, SizeChangedEventArgs args)
    {
        var wide = args.NewSize.Width >= 1000;
        _isNarrow = args.NewSize.Width < 720;
        Navigation.PaneDisplayMode = wide
            ? NavigationViewPaneDisplayMode.Left
            : _isNarrow
                ? NavigationViewPaneDisplayMode.LeftMinimal
                : NavigationViewPaneDisplayMode.LeftCompact;
        Navigation.IsPaneOpen = wide;
        TitleBarAppName.Visibility = wide ? Visibility.Visible : Visibility.Collapsed;
        TitleBrandColumn.Width = new GridLength(wide ? 214 : 42);
        SubpageTabs.Visibility = !_isNarrow && CurrentSubpages.Count > 1
            ? Visibility.Visible
            : Visibility.Collapsed;
        SubpagePicker.Visibility = _isNarrow && CurrentSubpages.Count > 1
            ? Visibility.Visible
            : Visibility.Collapsed;
        SubpageNavigation.Padding = _isNarrow
            ? new Thickness(16, 12, 16, 2)
            : new Thickness(28, 18, 28, 4);
    }

    private void OnSearchKeyboardAcceleratorInvoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        SettingsSearch.Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private void ApplySettingsTheme()
    {
        Root.RequestedTheme = ElementTheme.Default;
        if (!_micaApplied && !new AccessibilitySettings().HighContrast)
        {
            TryApplySystemBackdrop();
        }

        Root.Background = GetSettingsBrush(_micaApplied
            ? "SettingsWindowBackgroundBrush"
            : "SettingsWindowFallbackBackgroundBrush");
    }

    private Brush? GetSettingsBrush(string key) =>
        Application.Current.Resources.TryGetValue(key, out var value) ? value as Brush : null;

    private void TryApplySystemBackdrop()
    {
        try
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.Base };
            _micaApplied = true;
        }
        catch
        {
            SystemBackdrop = null;
            _micaApplied = false;
        }
    }

    private void OnModuleDetailsRequested(object? sender, string moduleId) => Navigate(moduleId switch
    {
        "media" => "media",
        "notifications" => "notifications",
        "timer" => "optional",
        "battery" => "general",
        _ => "modules"
    });

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var query = sender.Text.Trim();
        sender.ItemsSource = string.IsNullOrEmpty(query)
            ? Array.Empty<SearchItem>()
            : _searchItems.Where(item => item.Matches(query))
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
                   _searchItems.Where(candidate => candidate.Matches(args.QueryText))
                       .OrderBy(candidate => candidate.Score(args.QueryText))
                       .FirstOrDefault();
        if (item is not null) Navigate(item.SubpageId, item.FocusTarget ?? string.Empty);
    }

    private void ApplyNavigationLocalization()
    {
        foreach (var item in EnumerateNavigationItems())
        {
            if (item.Tag is not string categoryId) continue;
            var category = _categories.FirstOrDefault(candidate => candidate.Id == categoryId);
            if (category is null) continue;
            item.Content = category.Title;
            ToolTipService.SetToolTip(item, category.Title);
        }
    }

    private IEnumerable<NavigationViewItem> EnumerateNavigationItems() =>
        Navigation.MenuItems.OfType<NavigationViewItem>()
            .Concat(Navigation.FooterMenuItems.OfType<NavigationViewItem>());

    private void Navigate(string subpageId, string? focusTarget = null, bool announce = true)
    {
        var category = _categories.FirstOrDefault(candidate =>
            candidate.Subpages.Any(subpage => subpage.Id == subpageId));
        if (category is null || !_pages.TryGetValue(subpageId, out var page)) return;

        var item = EnumerateNavigationItems()
            .FirstOrDefault(candidate => Equals(candidate.Tag, category.Id));
        if (item is not null && !ReferenceEquals(Navigation.SelectedItem, item))
        {
            _lastSubpageByCategory[category.Id] = subpageId;
            Navigation.SelectedItem = item;
            return;
        }

        SelectCategory(category, subpageId, page, focusTarget, announce);
    }

    private void SelectCategory(
        SettingsCategoryDefinition category,
        string subpageId,
        UserControl page,
        string? focusTarget,
        bool announce)
    {
        PushBackEntry(s_lastSubpageId, subpageId);

        CurrentSubpages = category.Subpages;
        Bindings.Update();
        var selectedIndex = Math.Max(0, CurrentSubpages.ToList().FindIndex(item => item.Id == subpageId));
        _syncingSubpageSelection = true;
        SubpagePicker.SelectedIndex = selectedIndex;
        _syncingSubpageSelection = false;
        RebuildSubpageTabs(category, subpageId);

        SubpageNavigation.Visibility = CurrentSubpages.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        SubpageTabs.Visibility = !_isNarrow && CurrentSubpages.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        SubpagePicker.Visibility = _isNarrow && CurrentSubpages.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        _lastSubpageByCategory[category.Id] = subpageId;
        s_lastCategoryId = category.Id;
        s_lastSubpageId = subpageId;
        UpdateBackButton();
        ShowPage(page);

        var selected = CurrentSubpages[selectedIndex];
        if (announce)
        {
            NavigationAnnouncement.Text = _localization.Text(
                $"{category.Title}, {selected.Title} sayfası açıldı",
                $"{category.Title}, {selected.Title} page opened");
            AutomationProperties.SetName(PageHost, NavigationAnnouncement.Text);
        }

        if (!string.IsNullOrWhiteSpace(focusTarget) && page.FindName(focusTarget) is Control target)
        {
            target.Focus(FocusState.Keyboard);
        }
        else if (focusTarget is not null)
        {
            page.Focus(FocusState.Keyboard);
        }

        if (_isNarrow) Navigation.IsPaneOpen = false;
    }

    private void ShowPage(UserControl page)
    {
        PageHost.Content = page;
        ElementCompositionPreview.SetIsTranslationEnabled(PageHost, true);
        var visual = ElementCompositionPreview.GetElementVisual(PageHost);
        visual.StopAnimation("Opacity");
        visual.StopAnimation("Translation");
        visual.Properties.InsertVector3("Translation", Vector3.Zero);
        if (!new UISettings().AnimationsEnabled)
        {
            visual.Opacity = 1;
            return;
        }

        var compositor = visual.Compositor;
        var opacity = compositor.CreateScalarKeyFrameAnimation();
        opacity.InsertKeyFrame(0, 0.35f);
        opacity.InsertKeyFrame(1, 1);
        opacity.Duration = TimeSpan.FromMilliseconds(200);
        var translation = compositor.CreateVector3KeyFrameAnimation();
        translation.InsertKeyFrame(0, new Vector3(0, 10, 0));
        translation.InsertKeyFrame(1, Vector3.Zero);
        translation.Duration = TimeSpan.FromMilliseconds(200);
        visual.StartAnimation("Opacity", opacity);
        visual.StartAnimation("Translation", translation);
    }

    private void RefreshCurrentCategory()
    {
        var subpageId = _pages.ContainsKey(s_lastSubpageId) ? s_lastSubpageId : "home";
        Navigate(subpageId, announce: false);
    }

    private void OnSubpagePickerSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_syncingSubpageSelection || SubpagePicker.SelectedItem is not SettingsSubpageDefinition subpage) return;
        Navigate(subpage.Id);
    }

    private void RebuildSubpageTabs(SettingsCategoryDefinition category, string selectedSubpageId)
    {
        SubpageTabPanel.Children.Clear();
        var categoryBrush = GetSettingsBrush(category.ColorResourceKey);
        foreach (var subpage in category.Subpages)
        {
            var isSelected = subpage.Id == selectedSubpageId;
            var label = new TextBlock
            {
                Text = subpage.Title,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = GetSettingsBrush("TextFillColorPrimaryBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            var icon = new FontIcon
            {
                Glyph = subpage.Glyph,
                FontSize = 14,
                Foreground = categoryBrush
            };
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            row.Children.Add(icon);
            row.Children.Add(label);

            var underline = new Border
            {
                Height = 2,
                Margin = new Thickness(2, 4, 2, 0),
                CornerRadius = new CornerRadius(1),
                Background = categoryBrush,
                Visibility = isSelected ? Visibility.Visible : Visibility.Collapsed
            };
            var content = new StackPanel();
            content.Children.Add(row);
            content.Children.Add(underline);

            var tab = new ToggleButton
            {
                Tag = subpage.Id,
                Content = content,
                IsChecked = isSelected,
                Style = Application.Current.Resources["SettingsSubpageTabToggleStyle"] as Style
            };
            var transparentBrush = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            foreach (var resourceKey in new[]
                     {
                         "ToggleButtonBackgroundChecked",
                         "ToggleButtonBackgroundCheckedPointerOver",
                         "ToggleButtonBackgroundCheckedPressed",
                         "ToggleButtonBackgroundCheckedDisabled",
                         "ToggleButtonBorderBrushChecked",
                         "ToggleButtonBorderBrushCheckedPointerOver",
                         "ToggleButtonBorderBrushCheckedPressed",
                         "ToggleButtonBorderBrushCheckedDisabled"
                     })
            {
                tab.Resources[resourceKey] = transparentBrush;
            }
            AutomationProperties.SetName(tab, subpage.Title);
            tab.Click += OnSubpageTabClick;
            SubpageTabPanel.Children.Add(tab);
        }
    }

    private void OnSubpageTabClick(object sender, RoutedEventArgs args)
    {
        if (sender is ToggleButton { Tag: string subpageId })
        {
            Navigate(subpageId);
        }
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string categoryId) return;
        var category = _categories.FirstOrDefault(candidate => candidate.Id == categoryId);
        if (category is null) return;
        var subpageId = _lastSubpageByCategory.TryGetValue(categoryId, out var last)
            ? last
            : category.Subpages[0].Id;
        if (_pages.TryGetValue(subpageId, out var page))
        {
            SelectCategory(category, subpageId, page, null, announce: true);
        }
    }

    private void OnNavigationBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args) =>
        TryGoBack();

    private void OnBackKeyboardAcceleratorInvoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (!TryGoBack()) return;
        args.Handled = true;
    }

    private bool TryGoBack()
    {
        while (_backStack.Count > 0)
        {
            var previous = _backStack[^1];
            _backStack.RemoveAt(_backStack.Count - 1);
            if (string.Equals(previous, s_lastSubpageId, StringComparison.Ordinal) ||
                !_pages.ContainsKey(previous))
            {
                continue;
            }

            _isNavigatingBack = true;
            try
            {
                Navigate(previous);
            }
            finally
            {
                _isNavigatingBack = false;
                UpdateBackButton();
            }

            return true;
        }

        UpdateBackButton();
        return false;
    }

    private void PushBackEntry(string previousSubpageId, string nextSubpageId)
    {
        if (_isNavigatingBack) return;
        if (string.IsNullOrEmpty(previousSubpageId)) return;
        if (string.Equals(previousSubpageId, nextSubpageId, StringComparison.Ordinal)) return;
        if (!_pages.ContainsKey(previousSubpageId)) return;
        if (_backStack.Count > 0 &&
            string.Equals(_backStack[^1], previousSubpageId, StringComparison.Ordinal))
        {
            return;
        }

        _backStack.Add(previousSubpageId);
        const int maxDepth = 32;
        if (_backStack.Count > maxDepth) _backStack.RemoveAt(0);
    }

    private void UpdateBackButton() => Navigation.IsBackEnabled = _backStack.Count > 0;

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

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_allowClose || _lifetime.IsShuttingDown) return;
        args.Cancel = true;
        if (_closeDecisionPending) return;

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
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
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
        if (behavior == CloseBehaviorSetting.Exit) _lifetime.RequestExit();
        else AppWindow.Hide();
    }

    private sealed record SearchItem(
        string Title,
        string Description,
        string PathText,
        string CategoryId,
        string SubpageId,
        string? FocusTarget,
        string TurkishTitle,
        string EnglishTitle,
        string TurkishDescription,
        string EnglishDescription,
        string Keywords)
    {
        // The displayed strings come first so a query typed in the active
        // language matches even when that language is neither Turkish nor
        // English; the other two stay searchable for bilingual users.
        public string SearchText =>
            $"{Title} {Description} {TurkishTitle} {EnglishTitle} {TurkishDescription} {EnglishDescription} {Keywords}";

        public bool Matches(string query)
        {
            var search = Normalize(SearchText);
            return Normalize(query).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .All(token => search.Contains(token, StringComparison.Ordinal));
        }

        public int Score(string query)
        {
            var normalizedQuery = Normalize(query);
            var currentTitle = Normalize(Title);
            var bothTitles = Normalize($"{Title} {TurkishTitle} {EnglishTitle}");
            return currentTitle.StartsWith(normalizedQuery, StringComparison.Ordinal)
                ? 0
                : bothTitles.Contains(normalizedQuery, StringComparison.Ordinal) ? 1 : 2;
        }

        public static SearchItem Create(
            string turkishTitle, string englishTitle,
            string turkishDescription, string englishDescription,
            string categoryId, string subpageId, string? focusTarget,
            string categoryTitle, Func<string, string, string> localize, string keywords)
        {
            var title = localize(turkishTitle, englishTitle);
            return new(title,
                localize(turkishDescription, englishDescription),
                $"{categoryTitle} › {title}",
                categoryId, subpageId, focusTarget,
                turkishTitle, englishTitle, turkishDescription, englishDescription, keywords);
        }

        private static string Normalize(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var character in value.Replace('ı', 'i').Replace('İ', 'I')
                         .Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
            }
            return builder.ToString().Normalize(NormalizationForm.FormC);
        }

        public override string ToString() => Title;
    }
}
