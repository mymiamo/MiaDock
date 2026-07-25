using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public sealed class AppLocalizationService : IAppLocalizationService
{
    private readonly ConditionalWeakTable<DependencyObject, Dictionary<string, string>> _originals = new();

    private static readonly IReadOnlyDictionary<string, string> English =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MiaDock Ayarları"] = "MiaDock Settings",
            ["Ayarlarda ara"] = "Search settings",
            ["Ana sayfa"] = "Home",
            ["Genel"] = "General",
            ["Modüller"] = "Modules",
            ["Görünüm"] = "Appearance",
            ["Medya"] = "Media",
            ["Bildirimler"] = "Notifications",
            ["Zaman ve kısayollar"] = "Time and shortcuts",
            ["Tam ekran"] = "Fullscreen",
            ["Monitör"] = "Monitor",
            ["Sistem tepsisi"] = "System tray",
            ["Başlangıç ve kapanış"] = "Startup and shutdown",
            ["Tanılama"] = "Diagnostics",
            ["Hakkında"] = "About",
            ["Ana Sayfa"] = "Home",
            ["Adanın temel davranışlarını hızlıca yapılandırın."] = "Quickly configure the island's core behavior.",
            ["Dil"] = "Language",
            ["Arayüz dilini seçin"] = "Choose the interface language",
            ["Görünürlük"] = "Visibility",
            ["Etkileşim"] = "Interaction",
            ["Ada konumu"] = "Island position",
            ["Pasif modülden dönüş"] = "Return from inactive module",
            ["Etkin işi olmayan modülün ana dock'a dönme süresi"] = "Time before a module with no active task returns to the home dock",
            ["Pasif modülden dönüş süresi"] = "Inactive module return time",
            ["Sistem modülleri"] = "System modules",
            ["Pil, ağ, Bluetooth ve zaman araçları etkin"] = "Battery, network, Bluetooth and time tools are enabled",
            ["Pil uyarıları"] = "Battery alerts",
            ["Düşük, kritik ve acil pil bildirimlerinin gösterileceği yüzdeleri belirleyin."] = "Set the percentages for low, critical and emergency battery alerts.",
            ["Düşük"] = "Low",
            ["Kritik"] = "Critical",
            ["Acil"] = "Emergency",
            ["Değişiklikler adada anında önizlenir."] = "Changes are previewed on the island immediately.",
            ["Tema, boyut ve hareket ayarları adada anında önizlenir."] = "Theme, size and motion settings are previewed on the island immediately.",
            ["Tema"] = "Theme",
            ["Yüzey stili"] = "Surface style",
            ["Saydam Bulanık Cam"] = "Transparent Blurred Glass",
            ["Bu tema sabit bir arka plan rengi kullanmaz; masaüstünü gerçek Windows Acrylic ile bulanık gösterir. Opaklık, yalnızca ince cam katmanının yoğunluğunu değiştirir."] = "This theme uses no fixed background color; it blurs the desktop with real Windows Acrylic. Opacity changes only the intensity of the thin glass layer.",
            ["Ada boyutları"] = "Island dimensions",
            ["Yüzey ve renk"] = "Surface and color",
            ["Cam / yüzey yoğunluğu"] = "Glass / surface intensity",
            ["Cam veya yüzey yoğunluğu"] = "Glass or surface intensity",
            ["Hareket"] = "Motion",
            ["Kapalı genişlik"] = "Collapsed width",
            ["Kapalı yükseklik"] = "Collapsed height",
            ["Hover genişliği"] = "Hover width",
            ["Hover yüksekliği"] = "Hover height",
            ["Genişletilmiş genişlik"] = "Expanded width",
            ["Genişletilmiş yükseklik"] = "Expanded height",
            ["Bildirim genişliği"] = "Notification width",
            ["Bildirim yüksekliği"] = "Notification height",
            ["Köşe yuvarlaklığı"] = "Corner radius",
            ["Arka plan rengi"] = "Background color",
            ["Vurgu rengi"] = "Accent color",
            ["Opaklık"] = "Opacity",
            ["Gölge yoğunluğu"] = "Shadow intensity",
            ["Animasyon türü"] = "Animation type",
            ["Animasyon hızı"] = "Animation speed",
            ["Tüm ayarları varsayılana döndür"] = "Reset all settings",
            ["Görünüm ayarlarını varsayılana döndür"] = "Reset appearance settings",
            ["Windows medya oturumlarından kullanılacak kaynağı ve kontrol davranışını seçin."] = "Choose the Windows media source and control behavior.",
            ["Varsayılan medya uygulaması"] = "Default media app",
            ["Dock yalnız seçtiğiniz kaynağı izleyebilir"] = "The dock can follow only the selected source",
            ["Sistem oturumu"] = "System session",
            ["Kaynak kullanılamadığında"] = "When the source is unavailable",
            ["Başka bir aktif oturuma geçilip geçilmeyeceğini belirler"] = "Controls whether another active session is used",
            ["Ses denetimi hedefi"] = "Volume control target",
            ["Ana sesi veya eşleşen uygulama oturumunu kontrol eder"] = "Controls master volume or the matching app session",
            ["Tam ekran uygulamalarda gösterilecek geçici medya bildirimlerini yapılandırın."] = "Configure temporary media alerts shown over fullscreen apps.",
            ["Tam ekran uygulamalarda gösterilecek geçici modül bildirimlerini yapılandırın."] = "Configure temporary module alerts shown over fullscreen apps.",
            ["Tam ekran bildirimleri"] = "Fullscreen notifications",
            ["Şarkı değişikliklerini göster"] = "Show track changes",
            ["Bildirim görünümü"] = "Notification style",
            ["Görünme süresi (saniye)"] = "Display duration (seconds)",
            ["Tam ekran davranışı"] = "Fullscreen behavior",
            ["Normal dock gizlenir; seçtiğiniz modül olayları geçici olarak gösterilir."] = "The normal dock is hidden; selected module events appear temporarily.",
            ["Normal dock gizlenir; kritik zamanlayıcı, pil ve izin verdiğiniz diğer modül olayları geçici olarak gösterilir. Şarkı değişimi dock'u kendiliğinden genişletmez."] = "The normal dock is hidden; critical timer, battery and other allowed module events appear temporarily. Track changes do not expand the dock automatically.",
            ["Kronometre çalışıyor"] = "Stopwatch running",
            ["Kronometre duraklatıldı"] = "Stopwatch paused",
            ["Kronometreyi sıfırla"] = "Reset stopwatch",
            ["Adanın hangi ekranda gösterileceğini seçin."] = "Choose which display shows the island.",
            ["Monitör davranışı"] = "Monitor behavior",
            ["Sabit monitör"] = "Fixed monitor",
            ["Bir monitör seçin"] = "Select a monitor",
            ["Güvenli geçiş"] = "Safe fallback",
            ["Seçilen monitör bağlantısı kesilirse dock otomatik olarak ana monitöre taşınır."] = "If the selected display disconnects, the dock moves to the primary display.",
            ["Sistem tepsisi simgesinde sunulacak davranışları seçin."] = "Choose the behaviors available from the system tray icon.",
            ["Sistem tepsisi simgesini göster"] = "Show system tray icon",
            ["Medya kontrollerini menüde göster"] = "Show media controls in the menu",
            ["Geçici bildirimleri etkinleştir"] = "Enable temporary notifications",
            ["Erişim"] = "Access",
            ["Simgeyi kapatırsanız Ayarlara dock üzerindeki sağ tık menüsünden ulaşabilirsiniz."] = "If you hide the icon, open Settings from the dock's context menu.",
            ["MiaDock'un Windows ile ve pencere kapatıldığında nasıl davranacağını seçin."] = "Choose how MiaDock behaves with Windows and when its window closes.",
            ["Windows açılışında başlat"] = "Start with Windows",
            ["Uygulama açıldığında"] = "When the app starts",
            ["Pencere kapatıldığında"] = "When the window closes",
            ["Güvenli başlangıç"] = "Safe startup",
            ["MiaDock, kayıt defteri yerine Microsoft Store uyumlu StartupTask API'sini kullanır."] = "MiaDock uses the Microsoft Store-compatible StartupTask API instead of the registry.",
            ["Yerel teknik logları görüntüleyin ve yönetin."] = "View and manage local technical logs.",
            ["Gizlilik"] = "Privacy",
            ["Loglar yalnızca bu cihazda tutulur. Şarkı, sanatçı, kullanıcı adı, kişisel yol ve medya geçmişi kaydedilmez."] = "Logs remain on this device. Tracks, artists, usernames, personal paths and media history are not recorded.",
            ["Yenile"] = "Refresh",
            ["Log klasörünü aç"] = "Open log folder",
            ["ZIP olarak dışa aktar"] = "Export as ZIP",
            ["Logları temizle"] = "Clear logs",
            ["Windows 11 ile doğal şekilde bütünleşen modüler medya adası."] = "A modular media island that integrates naturally with Windows 11.",
            ["Sürüm"] = "Version",
            ["Ayar dosyası"] = "Settings file",
            ["Temel özellikler çevrimdışı çalışır. İlk sürüm telemetri, kullanıcı hesabı veya harici sunucu bağlantısı kullanmaz."] = "Core features work offline. This release uses no telemetry, user account or external server connection.",
            ["Windows bildirimlerini izin verdiğiniz kapsamda MiaDock üzerinde gösterin."] = "Show Windows notifications on MiaDock within the permissions you grant.",
            ["Bildirim modülünü etkinleştir"] = "Enable notification module",
            ["İzin verilirse kaynak uygulama ve bildirim başlığı gösterilir."] = "When allowed, the source app and notification title are shown.",
            ["Görünme süresi"] = "Display duration",
            ["Bildirim adada kaç saniye kalır"] = "How long the notification remains on the island",
            ["Tam ekranda göster"] = "Show in fullscreen",
            ["Hassas içerik varsayılan olarak kapalıdır"] = "Sensitive content is off by default",
            ["Yalnızca izin listesini kullan"] = "Use allow list only",
            ["Açıkken yalnız aşağıda etkinleştirilen uygulamalar gösterilir"] = "Only apps enabled below are shown",
            ["Uygulamalar"] = "Applications",
            ["Göster"] = "Show",
            ["Gövde"] = "Body",
            ["Gövde metni her uygulama için ayrıca açılmalıdır. Bildirim içeriği teknik loglara yazılmaz."] = "Body text must be enabled per app. Notification content is never written to technical logs.",
            ["Uygulama listesi, Windows bildirim erişimi verildikten sonra dolacaktır."] = "The app list appears after Windows notification access is granted.",
            ["Global kısayollar"] = "Global shortcuts",
            ["Global kısayolları etkinleştir"] = "Enable global shortcuts",
            ["Adayı göster / gizle"] = "Show / hide island",
            ["Adayı genişlet / küçült"] = "Expand / collapse island",
            ["Sonraki modül"] = "Next module",
            ["Medyayı oynat / duraklat"] = "Play / pause media",
            ["Zamanlayıcıyı duraklat / sürdür"] = "Pause / resume timer"
            ,
            ["Dock'ta çalışacak özellikleri, olay sürelerini ve tam ekran davranışını yönetin."] = "Manage the features, event durations and fullscreen behavior used by the dock.",
            ["İzinler isteğe bağlıdır"] = "Permissions are optional",
            ["MiaDock başlangıçta toplu izin istemez. İzin yalnız ilgili modülü açtığınızda istenir."] = "MiaDock does not request permissions in bulk at startup. Permission is requested only when you enable the related module.",
            ["Olay süresi (saniye)"] = "Event duration (seconds)",
            ["Ayrıntılar"] = "Details",
            ["Hassas içerik"] = "Sensitive content",
            ["Bildirim ve aktarım gibi hassas içerikler için iki koruma da varsayılan olarak kapalıdır."] = "Both protections are off by default for sensitive content such as notifications and transfers.",
            ["Hassas içeriği tam ekranda göster"] = "Show sensitive content in fullscreen",
            ["Oyun veya tam ekran uygulama üzerinde bildirim içeriğine izin verir."] = "Allows sensitive content over games or fullscreen apps.",
            ["Hassas içeriği Windows kilitliyken göster"] = "Show sensitive content while Windows is locked",
            ["Windows oturumu kilitliyken hassas modül içeriğine izin verir."] = "Allows sensitive module content while the Windows session is locked.",
            ["Hazır"] = "Ready",
            ["Kapalı"] = "Disabled",
            ["İzin gerekli"] = "Permission required",
            ["İzin reddedildi"] = "Permission denied",
            ["Windows API kullanılamıyor"] = "Windows API unavailable",
            ["Uyumlu cihaz bulunamadı"] = "No compatible device",
            ["Geçici hata"] = "Temporary error",
            ["Albüm kapağı"] = "Album artwork",
            ["Ana sesi aç veya kapat"] = "Mute or unmute master volume",
            ["Aktarım ilerlemesi"] = "Transfer progress",
            ["Ayarlar"] = "Settings",
            ["Bağlı Bluetooth cihazları"] = "Connected Bluetooth devices",
            ["Bluetooth"] = "Bluetooth",
            ["Dakika"] = "Minutes",
            ["1 dk"] = "1 min",
            ["5 dk"] = "5 min",
            ["10 dk"] = "10 min",
            ["25 dk"] = "25 min",
            ["45 dk"] = "45 min",
            ["1 dakikalık zamanlayıcıyı başlat"] = "Start a 1-minute timer",
            ["5 dakikalık zamanlayıcıyı başlat"] = "Start a 5-minute timer",
            ["10 dakikalık zamanlayıcıyı başlat"] = "Start a 10-minute timer",
            ["25 dakikalık zamanlayıcıyı başlat"] = "Start a 25-minute timer",
            ["45 dakikalık zamanlayıcıyı başlat"] = "Start a 45-minute timer",
            ["Zaman araçları"] = "Time tools",
            ["Süre dolduğunda alarm sesi çalar."] = "An alarm sounds when the timer ends.",
            ["Saat : dakika : saniye"] = "Hours : minutes : seconds",
            ["Turlar"] = "Laps",
            ["En yeni tur üstte"] = "Newest lap first",
            ["Dock etkileşim davranışı"] = "Dock interaction behavior",
            ["Dock konumu"] = "Dock position",
            ["Dock teması"] = "Dock theme",
            ["Dock'un hangi monitörde ve ekranın hangi bölümünde gösterileceğini seçin."] = "Choose the monitor and screen area where the dock appears.",
            ["Dock'un ne zaman genişleyeceğini seçin. Klavye ve tıklama erişimi her durumda kullanılabilir."] = "Choose when the dock expands. Keyboard and click access remain available.",
            ["Düşük pil eşiği"] = "Low battery threshold",
            ["Kritik pil eşiği"] = "Critical battery threshold",
            ["Acil pil eşiği"] = "Emergency battery threshold",
            ["Etkileşim davranışı"] = "Interaction behavior",
            ["Global kısayolu kaydet"] = "Record global shortcut",
            ["Hassas içeriği kilit ekranında göster"] = "Show sensitive content on the lock screen",
            ["Hız, yalnızca bu görünüm açıkken ölçülür."] = "Speed is measured only while this view is open.",
            ["Hover ile açılırken MiaDock aktif uygulamanın klavye odağını almaz."] = "MiaDock does not take keyboard focus from the active app when opened by hover.",
            ["İndirme"] = "Download",
            ["İndirme hızı"] = "Download speed",
            ["Yükleme"] = "Upload",
            ["Yükleme hızı"] = "Upload speed",
            ["İptal"] = "Cancel",
            ["Kapat düğmesine bastığınızda ne yapılacağını seçin."] = "Choose what happens when you select the close button.",
            ["Kaynak görünmüyor mu?"] = "Source not listed?",
            ["Kısayol atamasını temizle"] = "Clear shortcut assignment",
            ["Kronometre"] = "Stopwatch",
            ["Kronometre turları"] = "Stopwatch laps",
            ["Kurulum adımları"] = "Setup steps",
            ["Medya uygulaması"] = "Media app",
            ["MiaDock İlk Kurulum"] = "MiaDock Setup",
            ["MiaDock kapatılsın mı?"] = "Close MiaDock?",
            ["MiaDock logosu"] = "MiaDock logo",
            ["Modül ayrıntılarını aç"] = "Open module details",
            ["Modüller arasında geçiş"] = "Switch between modules",
            ["Monitör ve konum"] = "Monitor and position",
            ["Müzik etkinliği"] = "Music activity",
            ["Müzik"] = "Music",
            ["Olası arama etkinliği"] = "Possible call activity",
            ["Sonraki parça"] = "Next track",
            ["Oynat veya duraklat"] = "Play or pause",
            ["Önceki parça"] = "Previous track",
            ["Şimdi çalıyor"] = "Now playing",
            ["Odak davranışı"] = "Focus behavior",
            ["Önceki kurulum adımı"] = "Previous setup step",
            ["Önceki modül"] = "Previous module",
            ["Sonraki modül"] = "Next module",
            ["Önizleme"] = "Preview",
            ["Pil seviyesi"] = "Battery level",
            ["Saat"] = "Hours",
            ["Saniye"] = "Seconds",
            ["Seçili medya uygulamasının ses seviyesi"] = "Selected media app volume",
            ["Seçili uygulamanın sesini aç veya kapat"] = "Mute or unmute the selected app",
            ["Seçimimi hatırla"] = "Remember my choice",
            ["Sıfırla"] = "Reset",
            ["Sistem sesi"] = "System audio",
            ["Sistem tepsisine küçült"] = "Minimize to system tray",
            ["Son teknik log kayıtları"] = "Recent technical log entries",
            ["Sonraki kurulum adımı"] = "Next setup step",
            ["Süre doldu"] = "Time is up",
            ["Tam ekran bildirim görünümü"] = "Fullscreen notification style",
            ["Tam ekranda medya olaylarını göster"] = "Show media events in fullscreen",
            ["Tema seçin"] = "Choose a theme",
            ["Temizle"] = "Clear",
            ["Parça konumu"] = "Track position",
            ["Tur"] = "Lap",
            ["Uygula"] = "Apply",
            ["Uygulamadan tamamen çık"] = "Exit application",
            ["Ses seviyesi"] = "Volume",
            ["Windows açılışında MiaDock'u başlat"] = "Start MiaDock with Windows",
            ["Windows başlangıcı"] = "Windows startup",
            ["Windows başlangıcında çalıştır"] = "Run at Windows startup",
            ["Zamanlayıcı"] = "Timer",
            ["Zamanlayıcı ilerlemesi"] = "Timer progress",
            ["Zamanlayıcı tamamlandı"] = "Timer completed",
            ["Zamanlayıcıyı iptal et"] = "Cancel timer",
            ["Bu seçimlerin tümünü daha sonra Ayarlar penceresinden değiştirebilirsiniz."] = "You can change all of these choices later in Settings.",
            ["İlk sürümde medya modülü kullanılabilir. Pil, mikrofon, zamanlayıcı ve diğer modüller daha sonra aynı altyapıya eklenebilir."] = "The media module is available initially. Battery, microphone, timer, and other modules can use the same architecture.",
            ["Kısayol kaydetmek için düğmeye tıklayın ve Ctrl, Alt veya Shift içeren kombinasyona basın. Windows tuşu ve F12 kullanılamaz."] = "Select the record button and press a combination containing Ctrl, Alt, or Shift. The Windows key and F12 are unavailable.",
            ["MiaDock çevrimdışı çalışır; hesap, sunucu bağlantısı, telemetri veya otomatik hata gönderimi kullanmaz."] = "MiaDock works offline and uses no account, server connection, telemetry, or automatic error reporting.",
            ["MiaDock hazır"] = "MiaDock is ready",
            ["MiaDock'un takip edeceği varsayılan medya kaynağını seçin. Liste, Windows medya oturumları değiştikçe güncellenir."] = "Choose the default media source MiaDock follows. The list updates as Windows media sessions change.",
            ["MiaDock'un Windows oturumu açıldığında otomatik başlamasını seçebilirsiniz."] = "Choose whether MiaDock starts automatically when you sign in to Windows.",
            ["Normal dock tam ekran sırasında gizlenir. Seçilen olaylar varsayılan olarak beş saniye görünür."] = "The normal dock is hidden in fullscreen. Selected events appear for five seconds by default.",
            ["Oynatma göstergesi"] = "Playback indicator",
            ["Spotify, Apple Music veya tarayıcıda bir medya başlatabilirsiniz. Kaynak seçmeden de kuruluma devam edebilirsiniz."] = "Start media in Spotify, Apple Music, or a browser. You can continue setup without selecting a source.",
            ["Tema değişikliği anında önizlenir. Kurulum iptal edilirse önceki tema geri yüklenir."] = "Theme changes are previewed immediately. The previous theme is restored if setup is canceled.",
            ["Windows ana ses seviyesi"] = "Windows master volume",
            ["Zamanlayıcı ve kronometreyi genişletilmiş adadaki Zaman modülünden yönetin. Fare tekeriyle varsayılan görünüm ve modüller arasında geçebilirsiniz."] = "Manage the timer and stopwatch from the Time module in the expanded island. Use the mouse wheel to move between the default view and modules.",
            ["Faz 1 arayüz önizlemesi"] = "Phase 1 interface preview",
            ["Yerel sahte veri"] = "Local mock data",
            ["Tema stili"] = "Theme style",
            ["Ada durumu"] = "Island state",
            ["Genişletilmiş müzik"] = "Expanded music",
            ["Parça bildirimi"] = "Track notification",
            ["Bu pencere geliştirme önizlemesidir. Ada canlı Windows medya oturumlarını kullanır; kalıcılık ve animasyonlar sonraki fazlarda eklenir."] = "This window is a development preview. The island uses live Windows media sessions; persistence and animations are added in later phases.",
            ["MiaDock Önizleme"] = "MiaDock Preview",
            ["İlk kurulum"] = "Setup",
            ["Geri"] = "Back",
            ["İleri"] = "Next",
            ["MiaDock'a hoş geldiniz"] = "Welcome to MiaDock",
            ["Windows 11 ile doğal biçimde çalışan, modüler ve etkileşimli dock'unuzu birkaç adımda hazırlayalım."] = "Let's set up your modular, interactive dock that works naturally with Windows 11.",
            ["Modüllerinizi seçin"] = "Choose your modules",
            ["Dock'ta kullanmak istediğiniz yerleşik özellikleri seçin. Bu adım hiçbir Windows izni istemez."] = "Choose the built-in features you want in the dock. This step requests no Windows permissions.",
            ["Kurulum özeti"] = "Setup summary",
            ["Seçimlerinizi kontrol edin. Tamamla düğmesine bastığınızda ayarlar tek seferde kaydedilecektir."] = "Review your choices. Settings are saved together when you select Finish."
        };

    public AppLanguage CurrentLanguage { get; private set; } = AppLanguage.Turkish;

    public event EventHandler? LanguageChanged;

    public void SetLanguage(AppLanguage language)
    {
        if (!Enum.IsDefined(language))
        {
            language = AppLanguage.Turkish;
        }

        var changed = CurrentLanguage != language;
        CurrentLanguage = language;
        var culture = new CultureInfo(language == AppLanguage.English ? "en-US" : "tr-TR");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        if (changed)
        {
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Text(string turkish, string english) =>
        CurrentLanguage == AppLanguage.English ? english : turkish;

    public void Apply(DependencyObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        LocalizeElement(root);
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            Apply(VisualTreeHelper.GetChild(root, index));
        }
    }

    private void LocalizeElement(DependencyObject element)
    {
        if (element is TextBlock textBlock) Localize(element, "Text", textBlock.Text, value => textBlock.Text = value);
        if (element is ContentControl contentControl && contentControl.Content is string content)
            Localize(element, "Content", content, value => contentControl.Content = value);
        if (element is ToggleSwitch toggleSwitch && toggleSwitch.Header is string toggleHeader)
            Localize(element, "ToggleHeader", toggleHeader, value => toggleSwitch.Header = value);
        if (element is NumberBox numberBox && numberBox.Header is string numberHeader)
            Localize(element, "NumberHeader", numberHeader, value => numberBox.Header = value);
        if (element is Slider slider && slider.Header is string sliderHeader)
            Localize(element, "SliderHeader", sliderHeader, value => slider.Header = value);
        if (element is InfoBar infoBar)
        {
            Localize(element, "InfoTitle", infoBar.Title, value => infoBar.Title = value);
            Localize(element, "InfoMessage", infoBar.Message, value => infoBar.Message = value);
        }
        if (element is TextBox textBox)
            Localize(element, "TextPlaceholder", textBox.PlaceholderText, value => textBox.PlaceholderText = value);
        if (element is AutoSuggestBox suggestBox)
            Localize(element, "SuggestPlaceholder", suggestBox.PlaceholderText, value => suggestBox.PlaceholderText = value);
        if (element is ComboBox comboBox)
            Localize(element, "ComboPlaceholder", comboBox.PlaceholderText, value => comboBox.PlaceholderText = value);

        if (ToolTipService.GetToolTip(element) is string tooltip)
            Localize(element, "ToolTip", tooltip, value => ToolTipService.SetToolTip(element, value));
        var automationName = AutomationProperties.GetName(element);
        if (!string.IsNullOrWhiteSpace(automationName))
            Localize(element, "AutomationName", automationName, value => AutomationProperties.SetName(element, value));
    }

    private void Localize(DependencyObject owner, string property, string? current, Action<string> setter)
    {
        if (string.IsNullOrWhiteSpace(current))
        {
            return;
        }

        var originals = _originals.GetOrCreateValue(owner);
        if (!originals.TryGetValue(property, out var original))
        {
            original = English.ContainsKey(current) ? current :
                English.FirstOrDefault(pair => pair.Value == current).Key;
            if (string.IsNullOrEmpty(original))
            {
                return;
            }
            originals[property] = original;
        }

        setter(CurrentLanguage == AppLanguage.English ? English[original] : original);
    }
}
