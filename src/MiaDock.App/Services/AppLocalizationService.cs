using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MiaDock.Core.Localization;
using MiaDock.Core.Settings;

namespace MiaDock.App.Services;

public sealed class AppLocalizationService : IAppLocalizationService
{
    private readonly ConditionalWeakTable<DependencyObject, Dictionary<string, string>> _originals = new();

    private static readonly IReadOnlyDictionary<string, (string Turkish, string English)> Catalog =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["Common.Available"] = ("Kullanılabilir", "Available"),
            ["Common.Unavailable"] = ("Kullanılamıyor", "Unavailable"),
            ["Common.Unknown"] = ("Bilinmiyor", "Unknown"),
            ["Common.Cancel"] = ("İptal", "Cancel"),
            ["Common.Start"] = ("Başlat", "Start"),
            ["Common.Pause"] = ("Duraklat", "Pause"),
            ["Common.Resume"] = ("Devam et", "Resume"),
            ["Common.Play"] = ("Oynat", "Play"),
            ["Common.Reset"] = ("Sıfırla", "Reset"),
            ["Common.Lap"] = ("Tur", "Lap"),
            ["Common.None"] = ("Yok", "None"),
            ["Common.Disabled"] = ("Kapalı", "Disabled"),
            ["Module.media.Name"] = ("Müzik", "Music"),
            ["Module.system-activity.Name"] = ("Sistem", "System"),
            ["Module.battery.Name"] = ("Pil", "Battery"),
            ["Module.network.Name"] = ("Ağ", "Network"),
            ["Module.bluetooth.Name"] = ("Bluetooth", "Bluetooth"),
            ["Module.timer.Name"] = ("Zaman", "Time"),
            ["Module.notifications.Name"] = ("Bildirimler", "Notifications"),
            ["Module.transfers.Name"] = ("Aktarımlar", "Transfers"),
            ["Dock.Home"] = ("Ana dock", "Home dock"),
            ["Dock.Show"] = ("Dock'u göster", "Show dock"),
            ["Dock.Hide"] = ("Dock'u gizle", "Hide dock"),
            ["Dock.NoActiveEvent"] = ("Etkin olay yok", "No active event"),
            ["Dock.NoActiveEvent.Description"] = ("Yeni bir etkinlik olduğunda burada görünecek.", "New activity will appear here."),
            ["Dock.NowPlaying"] = ("Şimdi çalıyor", "Now playing"),
            ["Dock.Music"] = ("Müzik", "Music"),
            ["Dock.PreviousTrack"] = ("Önceki parça", "Previous track"),
            ["Dock.PlayPause"] = ("Oynat veya duraklat", "Play or pause"),
            ["Dock.NextTrack"] = ("Sonraki parça", "Next track"),
            ["Dock.Settings"] = ("Ayarlar", "Settings"),
            ["Dock.SystemSession"] = ("Sistem sesi", "System audio"),
            ["Dock.CallPossible"] = ("Olası arama etkinliği", "Possible call activity"),
            ["Dock.Progress"] = ("İlerleme", "Progress"),
            ["Dock.PreviousModule"] = ("Önceki modül", "Previous module"),
            ["Dock.NextModule"] = ("Sonraki modül", "Next module"),
            ["Dock.SwitchModules"] = ("Modüller arasında geçiş", "Switch between modules"),
            ["Dock.Empty.Timer"] = ("Etkin zamanlayıcı yok", "No active timer"),
            ["Dock.Empty.Bluetooth"] = ("Bluetooth cihazı bağlı değil", "No Bluetooth device connected"),
            ["Dock.Empty.Transfers"] = ("Aktarım bulunmuyor", "No transfer found"),
            ["Dock.Clock"] = ("Saat {0}, {1}", "Time {0}, {1}"),
            ["Dock.Clock.Short"] = ("Saat {0}", "Time {0}"),
            ["Dock.Home.Automation"] = ("Ana dock, {0}", "Home dock, {0}"),
            ["Dock.MicrophoneInUse"] = ("Mikrofon kullanılıyor", "Microphone in use"),
            ["Dock.SpeakerInUse"] = ("Hoparlör kullanılıyor", "Speaker in use"),
            ["Dock.NoAudioActivity"] = ("Ses etkinliği yok", "No audio activity"),
            ["Dock.Media.Previous.Help"] = ("Önceki parçaya geçer", "Skips to the previous track"),
            ["Dock.Media.PlayPause.Help"] = ("Geçerli parçayı oynatır veya duraklatır", "Plays or pauses the current track"),
            ["Dock.Media.Next.Help"] = ("Sonraki parçaya geçer", "Skips to the next track"),
            ["Battery.None"] = ("Pil yok", "No battery"),
            ["Battery.NotPresent"] = ("Pil bulunmuyor", "No battery present"),
            ["Battery.NotDetected"] = ("Bu cihazda pil algılanmadı", "No battery was detected on this device"),
            ["Battery.Charging"] = ("Şarj oluyor", "Charging"),
            ["Battery.Charging.Title"] = ("Pil şarj oluyor", "Battery charging"),
            ["Battery.EnergySaver"] = ("Enerji tasarrufu açık", "Energy saver is on"),
            ["Battery.OnBattery"] = ("Pilde çalışıyor", "On battery power"),
            ["Battery.DesktopPower"] = ("Masaüstü güç sistemi", "Desktop power"),
            ["Battery.PowerSource.AC"] = ("Şebeke gücü", "AC power"),
            ["Battery.PowerSource.DC"] = ("Pil gücü", "Battery power"),
            ["Battery.PowerSource.USB"] = ("USB gücü", "USB power"),
            ["Battery.PowerSource.Unknown"] = ("Güç kaynağı bilinmiyor", "Unknown power source"),
            ["Battery.Level"] = ("Pil seviyesi", "Battery level"),
            ["Battery.Percent"] = ("Pil %{0}", "Battery {0}%"),
            ["Battery.Percent.Automation"] = ("Pil yüzde {0}", "Battery {0} percent"),
            ["Battery.Percent.Charging.Automation"] = ("Pil yüzde {0}, şarj oluyor", "Battery {0} percent, charging"),
            ["Battery.Percent.Saver.Automation"] = ("Pil yüzde {0}, enerji tasarrufu açık", "Battery {0} percent, energy saver is on"),
            ["Network.Internet"] = ("İnternete bağlı", "Connected to the internet"),
            ["Network.Constrained"] = ("Sınırlı internet", "Limited internet"),
            ["Network.Local"] = ("Yalnızca yerel ağ", "Local network only"),
            ["Network.Offline"] = ("Çevrimdışı", "Offline"),
            ["Network.Cellular"] = ("Mobil ağ", "Cellular"),
            ["Network.Other"] = ("Diğer bağlantı", "Other connection"),
            ["Network.None"] = ("Bağlantı yok", "No connection"),
            ["Network.Metered"] = ("Tarifeli bağlantı", "Metered connection"),
            ["Network.Unmetered"] = ("Tarifesiz bağlantı", "Unmetered connection"),
            ["Network.Download"] = ("İndirme", "Download"),
            ["Network.Upload"] = ("Yükleme", "Upload"),
            ["Network.Rate.Megabytes"] = ("{0:0.0} MB/sn", "{0:0.0} MB/s"),
            ["Network.Rate.Kilobytes"] = ("{0:0} KB/sn", "{0:0} KB/s"),
            ["Network.WiFi.Connected"] = ("Wi-Fi bağlı", "Wi-Fi connected"),
            ["Network.WiFi.Metered"] = ("Wi-Fi, tarifeli bağlantı", "Wi-Fi, metered connection"),
            ["Network.Ethernet.Connected"] = ("Ethernet bağlı", "Ethernet connected"),
            ["Network.Ethernet.Metered"] = ("Ethernet, tarifeli bağlantı", "Ethernet, metered connection"),
            ["Network.Cellular.Connected"] = ("Mobil ağa bağlı", "Connected to cellular"),
            ["Network.Constrained.Full"] = ("Sınırlı internet bağlantısı", "Limited internet connection"),
            ["Network.Local.Full"] = ("Yalnızca yerel ağ bağlantısı", "Local network connection only"),
            ["Bluetooth.Unavailable"] = ("Bluetooth kullanılamıyor", "Bluetooth unavailable"),
            ["Bluetooth.Faulted"] = ("Bluetooth izleme hatası", "Bluetooth monitoring error"),
            ["Bluetooth.Searching"] = ("Cihazlar aranıyor", "Searching for devices"),
            ["Bluetooth.None"] = ("Bağlı cihaz yok", "No connected devices"),
            ["Bluetooth.Count"] = ("{0} cihaz bağlı", "{0} devices connected"),
            ["Bluetooth.Connected"] = ("Bluetooth cihazı bağlandı", "Bluetooth device connected"),
            ["Bluetooth.Disconnected"] = ("Bluetooth cihazı ayrıldı", "Bluetooth device disconnected"),
            ["Bluetooth.None.Automation"] = ("Bağlı Bluetooth cihazı yok", "No connected Bluetooth devices"),
            ["Bluetooth.One.Automation"] = ("Bir Bluetooth cihazı bağlı", "One Bluetooth device connected"),
            ["Bluetooth.Many.Automation"] = ("{0} Bluetooth cihazı bağlı", "{0} Bluetooth devices connected"),
            ["System.Unavailable"] = ("Kullanılamıyor", "Unavailable"),
            ["System.NoApplication"] = ("Uygulama seçilmedi", "No application selected"),
            ["System.SessionNotFound"] = ("Ses oturumu bulunamadı", "Audio session not found"),
            ["System.Microphone.Active"] = ("Mikrofon etkin", "Microphone active"),
            ["System.Microphone.Idle"] = ("Mikrofon boşta", "Microphone idle"),
            ["System.Microphone.Unavailable"] = ("Mikrofon kullanılamıyor", "Microphone unavailable"),
            ["System.Camera.Unavailable"] = ("Kamera cihaz durumu kullanılamıyor", "Camera device status unavailable"),
            ["System.Camera.NotFound"] = ("Kamera bulunamadı", "No camera found"),
            ["System.Camera.Allowed"] = ("Kamera mevcut · erişim izinli", "Camera available · access allowed"),
            ["System.Camera.DeniedUser"] = ("Kamera mevcut · kullanıcı engelledi", "Camera available · blocked by user"),
            ["System.Camera.DeniedSystem"] = ("Kamera mevcut · sistem engelledi", "Camera available · blocked by system"),
            ["System.Camera.Prompt"] = ("Kamera mevcut · izin istenmedi", "Camera available · permission not requested"),
            ["System.Camera.NotDeclared"] = ("Kamera mevcut · yetenek tanımlı değil", "Camera available · capability not declared"),
            ["System.Camera.Unknown"] = ("Kamera mevcut · erişim bilinmiyor", "Camera available · access unknown"),
            ["System.Call.None"] = ("Arama algılanmadı", "No call detected"),
            ["System.Call.Ended"] = ("Arama etkinliği sona erdi", "Call activity ended"),
            ["System.Call.Detail"] = ("Mikrofon ve iletişim sesi etkin", "Microphone and communication audio active"),
            ["System.AudioSession.State"] = ("Windows ses oturumu durumu", "Windows audio session state"),
            ["System.Audio.Muted"] = ("Ses kapalı", "Audio muted"),
            ["System.Audio.Master"] = ("Sistem sesi", "System audio"),
            ["System.Audio.Master.Down"] = ("Ana sesi azalt", "Decrease master volume"),
            ["System.Audio.Master.Toggle"] = ("Ana sesi aç veya kapat", "Mute or unmute master volume"),
            ["System.Audio.Master.Up"] = ("Ana sesi artır", "Increase master volume"),
            ["System.Audio.Application.Toggle"] = ("Uygulama sesini aç veya kapat", "Mute or unmute application volume"),
            ["System.Audio.Application.Muted"] = ("Uygulama sesi kapalı", "Application audio muted"),
            ["System.Audio.Application"] = ("Uygulama sesi", "Application audio"),
            ["System.Audio.SelectedMedia"] = ("Seçili medya uygulaması", "Selected media application"),
            ["System.Camera.Changed"] = ("Kamera kullanılabilirliği değişti", "Camera availability changed"),
            ["Timer.Tools"] = ("Zaman araçları", "Time tools"),
            ["Timer.TimerAndStopwatch"] = ("Zamanlayıcı ve kronometre", "Timer and stopwatch"),
            ["Timer.Running"] = ("Zamanlayıcı çalışıyor", "Timer running"),
            ["Timer.Paused"] = ("Zamanlayıcı duraklatıldı", "Timer paused"),
            ["Timer.Completed"] = ("Süre doldu", "Time is up"),
            ["Timer.SelectDuration"] = ("Bir süre seçin", "Choose a duration"),
            ["Timer.Completed.Description"] = ("Zamanlayıcı tamamlandı", "Timer completed"),
            ["Timer.StopwatchRunning"] = ("Kronometre çalışıyor", "Stopwatch running"),
            ["Timer.StopwatchPaused"] = ("Kronometre duraklatıldı", "Stopwatch paused"),
            ["Timer.Cancel"] = ("Zamanlayıcıyı iptal et", "Cancel timer"),
            ["Timer.StopAlarm"] = ("Alarmı sustur", "Silence alarm"),
            ["Timer.AlarmRepeats"] = ("Süre dolduğunda alarm sesi en fazla 5 kez çalar.", "The alarm sounds up to 5 times when the timer ends."),
            ["Timer.ResetStopwatch"] = ("Kronometreyi sıfırla", "Reset stopwatch"),
            ["Timer.PauseResume"] = ("Duraklat veya devam et", "Pause or resume"),
            ["Timer.Lap"] = ("Tur {0}  {1}", "Lap {0}  {1}"),
            ["Transfer.Title"] = ("Dosya aktarımları", "File transfers"),
            ["Transfer.None"] = ("Etkin aktarım yok", "No active transfers"),
            ["Transfer.Waiting"] = ("Aktarım bekleniyor", "Waiting for transfer"),
            ["Transfer.Active.One"] = ("1 etkin aktarım", "1 active transfer"),
            ["Transfer.Active.Many"] = ("{0} etkin aktarım", "{0} active transfers"),
            ["Transfer.Queued"] = ("Sırada", "Queued"),
            ["Transfer.Running"] = ("Aktarılıyor", "Transferring"),
            ["Transfer.Paused"] = ("Duraklatıldı", "Paused"),
            ["Transfer.ProviderWaiting"] = ("Sağlayıcı bekleniyor", "Waiting for provider"),
            ["Transfer.Completed"] = ("Tamamlandı", "Completed"),
            ["Transfer.Failed"] = ("Başarısız", "Failed"),
            ["Transfer.Cancelled"] = ("İptal edildi", "Cancelled"),
            ["Transfer.Disconnected"] = ("Bağlantı kesildi", "Disconnected"),
            ["Tray.Previous"] = ("Önceki", "Previous"),
            ["Tray.Next"] = ("Sonraki", "Next"),
            ["Tray.MediaNotFound"] = ("Medya uygulaması bulunamadı", "No media app found"),
            ["Tray.PrimaryMonitor"] = ("Ana monitör", "Primary monitor"),
            ["Tray.ActiveMonitor"] = ("Aktif pencerenin monitörü", "Active window monitor"),
            ["Tray.DefaultMedia"] = ("Varsayılan medya uygulaması", "Default media app"),
            ["Tray.StartWithWindows"] = ("Windows başlangıcında çalıştır", "Run at Windows startup"),
            ["Tray.FullscreenBehavior"] = ("Tam ekran davranışı", "Fullscreen behavior"),
            ["Tray.Fullscreen.Show"] = ("Tam ekranda bildirimleri göster", "Show notifications in fullscreen"),
            ["Tray.Fullscreen.Minimal"] = ("Sade görünüm", "Minimal view"),
            ["Tray.Fullscreen.Controls"] = ("Kontrollü görünüm", "View with controls"),
            ["Tray.SelectMonitor"] = ("Monitör seç", "Select monitor"),
            ["Tray.TemporaryNotifications"] = ("Geçici bildirimleri göster", "Show temporary notifications"),
            ["Tray.Exit"] = ("Uygulamadan tamamen çık", "Exit application"),
            ["Dialog.Close.Title"] = ("MiaDock kapatılsın mı?", "Close MiaDock?"),
            ["Dialog.Close.Description"] = ("Kapat düğmesine bastığınızda ne yapılacağını seçin.", "Choose what happens when you select the close button."),
            ["Dialog.Close.Minimize"] = ("Sistem tepsisine küçült", "Minimize to system tray"),
            ["Dialog.Close.Exit"] = ("Uygulamadan tamamen çık", "Exit application"),
            ["Dialog.Close.Remember"] = ("Seçimimi hatırla", "Remember my choice"),
            ["Dialog.Apply"] = ("Uygula", "Apply"),
            ["Dialog.Permission.Notification.Title"] = ("Bildirim erişimine izin verilsin mi?", "Allow notification access?"),
            ["Dialog.Permission.Notification.Summary"] = ("MiaDock kaynak uygulama ve bildirim başlığını okuyacak. Gövde metni ayrıca açılmadıkça gösterilmez; içerik teknik loglara yazılmaz.", "MiaDock will read the source application and notification title. Body text is hidden unless separately enabled, and content is never written to technical logs."),
            ["Dialog.Permission.Notification.Detail"] = ("MiaDock, Windows Bildirim Merkezi'ndeki uygulama adını ve başlığı okuyacak. Gövde metni yalnız uygulama bazında açılır; içerik loglanmaz ve bildirimler silinmez ya da okundu işaretlenmez.", "MiaDock will read application names and titles from Windows Notification Center. Body text is enabled per application; content is not logged, and notifications are never deleted or marked as read."),
            ["Dialog.Permission.Request"] = ("Windows iznini iste", "Request Windows permission"),
            ["Dialog.Permission.Cancel"] = ("Vazgeç", "Not now"),
            ["Dialog.Logs.Clear.Title"] = ("Yerel loglar temizlensin mi?", "Clear local logs?"),
            ["Dialog.Logs.Clear.Description"] = ("Bu işlem mevcut teknik log dosyalarını kalıcı olarak siler.", "This permanently deletes the current technical log files."),
            ["Dialog.Logs.Clear.Action"] = ("Logları temizle", "Clear logs"),
            ["Onboarding.Step.Welcome"] = ("Hoş geldiniz", "Welcome"),
            ["Onboarding.Step.Startup"] = ("Windows başlangıcı", "Windows startup"),
            ["Onboarding.Step.Appearance"] = ("Tema", "Theme"),
            ["Onboarding.Step.Media"] = ("Medya", "Media"),
            ["Onboarding.Step.Display"] = ("Monitör ve konum", "Monitor and position"),
            ["Onboarding.Step.Interaction"] = ("Etkileşim", "Interaction"),
            ["Onboarding.Step.Fullscreen"] = ("Tam ekran", "Fullscreen"),
            ["Onboarding.Step.Modules"] = ("Modüller", "Modules"),
            ["Onboarding.Step.Summary"] = ("Özet", "Summary"),
            ["Onboarding.Option.Theme.Apple"] = ("Apple benzeri", "Apple-like"),
            ["Onboarding.Option.Theme.Glass"] = ("Bulanık Cam", "Blurred Glass"),
            ["Onboarding.Option.Theme.Solid"] = ("Özel Düz Renk", "Custom Solid Color"),
            ["Onboarding.Option.Monitor.Primary"] = ("Ana monitör", "Primary monitor"),
            ["Onboarding.Option.Monitor.Active"] = ("Aktif pencerenin monitörü", "Active window monitor"),
            ["Onboarding.Option.Monitor.Fixed"] = ("Sabit monitör", "Fixed monitor"),
            ["Onboarding.Option.Position.TopCenter"] = ("Üst orta", "Top center"),
            ["Onboarding.Option.Position.TopLeft"] = ("Üst sol", "Top left"),
            ["Onboarding.Option.Position.TopRight"] = ("Üst sağ", "Top right"),
            ["Onboarding.Option.Position.BottomCenter"] = ("Alt orta", "Bottom center"),
            ["Onboarding.Option.Position.BottomLeft"] = ("Alt sol", "Bottom left"),
            ["Onboarding.Option.Position.BottomRight"] = ("Alt sağ", "Bottom right"),
            ["Onboarding.Option.Interaction.Hover"] = ("Fare üzerine gelince", "On pointer hover"),
            ["Onboarding.Option.Interaction.Click"] = ("Tıklayınca", "On click"),
            ["Onboarding.Option.Interaction.Both"] = ("Fare ve tıklama", "Hover and click"),
            ["Onboarding.Option.Fullscreen.Minimal"] = ("Sade", "Minimal"),
            ["Onboarding.Option.Fullscreen.Controls"] = ("Kontrollü", "With controls"),
            ["Onboarding.Option.Media.Auto"] = ("Otomatik seçim", "Automatic selection"),
            ["Onboarding.Summary.Theme"] = ("Tema: {0}", "Theme: {0}"),
            ["Onboarding.Summary.Media"] = ("Medya: {0}", "Media: {0}"),
            ["Onboarding.Summary.Monitor"] = ("Monitör: {0}", "Monitor: {0}"),
            ["Onboarding.Summary.Position"] = ("Konum: {0}", "Position: {0}"),
            ["Onboarding.Summary.Interaction"] = ("Etkileşim: {0}", "Interaction: {0}"),
            ["Onboarding.Summary.Fullscreen"] = ("Tam ekran: {0}", "Fullscreen: {0}"),
            ["Onboarding.Summary.Modules"] = ("Modüller: {0}", "Modules: {0}"),
            ["Onboarding.Startup.Unavailable"] = ("Windows ile başlatma, MSIX paketi kurulduğunda kullanılabilir.", "Start with Windows is available after installing the MSIX package."),
            ["Onboarding.Startup.Failed"] = ("Başlangıç ayarı değiştirilemedi. Windows Başlangıç Uygulamaları ayarını kontrol edin.", "The startup setting could not be changed. Check Windows Startup Apps settings."),
            ["Onboarding.Startup.DisabledByUser"] = ("Windows bu başlangıç görevini devre dışı bıraktı.", "Windows disabled this startup task."),
            ["Onboarding.Startup.DisabledByPolicy"] = ("Başlangıç görevi sistem ilkesi tarafından engelleniyor.", "The startup task is blocked by system policy."),
            ["Onboarding.Startup.EnabledByPolicy"] = ("Başlangıç görevi sistem ilkesi tarafından etkinleştirildi.", "The startup task is enabled by system policy."),
            ["Onboarding.Startup.Enabled"] = ("MiaDock Windows ile başlayacak.", "MiaDock will start with Windows."),
            ["Onboarding.Startup.Disabled"] = ("MiaDock Windows ile başlamayacak.", "MiaDock will not start with Windows."),
            ["Onboarding.Validation.FixedMonitor"] = ("Sabit monitör kullanmak için bağlı bir monitör seçin.", "Select a connected display to use a fixed monitor."),
            ["Onboarding.Module.Media.Title"] = ("Medya", "Media"),
            ["Onboarding.Module.Media.Description"] = ("Windows medya oturumları ve oynatma kontrolleri.", "Windows media sessions and playback controls."),
            ["Onboarding.Module.System.Title"] = ("Ses ve gizlilik göstergeleri", "Audio and privacy indicators"),
            ["Onboarding.Module.System.Description"] = ("Ses, mikrofon, kamera durumu ve yerel arama çıkarımı.", "Audio, microphone, camera status, and local call detection."),
            ["Onboarding.Module.Battery.Title"] = ("Pil", "Battery"),
            ["Onboarding.Module.Battery.Description"] = ("Şarj, enerji tasarrufu ve düşük pil olayları.", "Charging, energy saver, and low-battery events."),
            ["Onboarding.Module.Network.Title"] = ("Ağ", "Network"),
            ["Onboarding.Module.Network.Description"] = ("Bağlantı türü ve isteğe bağlı hız görünümü.", "Connection type and optional throughput view."),
            ["Onboarding.Module.Bluetooth.Title"] = ("Bluetooth", "Bluetooth"),
            ["Onboarding.Module.Bluetooth.Description"] = ("Eşleştirilmiş cihaz bağlantı değişiklikleri.", "Connection changes for paired devices."),
            ["Onboarding.Module.Timer.Title"] = ("Zamanlayıcı ve kronometre", "Timer and stopwatch"),
            ["Onboarding.Module.Timer.Description"] = ("Yerel zaman araçları ve tamamlanma olayları.", "Local time tools and completion events."),
            ["Onboarding.Module.Transfers.Title"] = ("Dosya aktarımları", "File transfers"),
            ["Onboarding.Module.Transfers.Description"] = ("Yerel sağlayıcılardan gelen aktarım ilerlemesi.", "Transfer progress from local providers."),
            ["Onboarding.Module.Notifications.Title"] = ("Windows bildirimleri", "Windows notifications"),
            ["Onboarding.Module.Notifications.Description"] = ("Kullanıcı izni gerektiği için ilk kurulumdan sonra Modüller sayfasından açılır.", "Because user permission is required, enable it from the Modules page after setup."),
            ["Onboarding.Button.Next"] = ("İleri", "Next"),
            ["Onboarding.Button.Finish"] = ("Tamamla", "Finish"),
            ["Onboarding.Button.Next.Automation"] = ("Sonraki kurulum adımı", "Next setup step"),
            ["Onboarding.Button.Finish.Automation"] = ("İlk kurulumu tamamla", "Finish setup"),
            ["Onboarding.Dialog.Incomplete.Title"] = ("Kurulum tamamlanmadı", "Setup is not complete"),
            ["Onboarding.Dialog.Incomplete.Description"] = ("Şimdi çıkarsanız dil tercihiniz korunur; diğer seçimler kaydedilmez ve sihirbaz sonraki açılışta yeniden gösterilir.", "If you exit now, your language preference is kept; other choices are not saved, and setup appears again next time."),
            ["Onboarding.Dialog.Return"] = ("Kuruluma dön", "Return to setup"),
            ["Onboarding.Dialog.Exit"] = ("Çıkış", "Exit"),
            ["Update.Available"] = ("Yeni sürüm mevcut", "A new version is available"),
            ["Update.VersionPair"] = ("MiaDock {0} → {1}", "MiaDock {0} → {1}"),
            ["Update.OpenStore"] = ("Microsoft Store'da aç", "Open in Microsoft Store"),
            ["Update.Check"] = ("Güncellemeleri denetle", "Check for updates"),
            ["Update.Checking"] = ("Güncellemeler denetleniyor", "Checking for updates"),
            ["Update.UpToDate"] = ("MiaDock güncel", "MiaDock is up to date"),
            ["Update.StoreOnly"] = ("Yalnızca Microsoft Store sürümünde kullanılabilir", "Available only in the Microsoft Store version"),
            ["Update.Offline"] = ("Güncelleme denetlenemedi: çevrimdışı", "Could not check for updates: offline"),
            ["Update.Failed"] = ("Güncelleme denetlenemedi", "Could not check for updates")
        };

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
            ["Dock'un temel durumunu görün ve sık kullanılan ayarlara hızla ulaşın."] = "See the dock's status and quickly reach frequently used settings.",
            ["Hızlı dock ayarları"] = "Quick dock settings",
            ["Görünürlük ve etkileşim davranışını doğrudan değiştirin."] = "Change visibility and interaction behavior directly.",
            ["Genel ayarları aç"] = "Open General settings",
            ["Windows başlangıcı"] = "Windows startup",
            ["Başlangıç ayarlarını aç"] = "Open startup settings",
            ["Modül durumu"] = "Module status",
            ["Yönet"] = "Manage",
            ["Dock görünümü"] = "Dock appearance",
            ["Görünüm ayarlarını aç"] = "Open Appearance settings",
            ["Microsoft Store güncellemeleri"] = "Microsoft Store updates",
            ["Güncellemeleri denetle"] = "Check for updates",
            ["Microsoft Store'da aç"] = "Open in Microsoft Store",
            ["Otomatik güncelleme denetimi"] = "Automatic update checks",
            ["MiaDock, Microsoft Store'daki yeni sürümleri dört saatte bir denetler."] = "MiaDock checks Microsoft Store for new versions every four hours.",
            ["Dock'un görünürlüğünü, etkileşimini, konumunu ve dilini yapılandırın."] = "Configure the dock's visibility, interaction, position, and language.",
            ["Dock'un temel davranışlarını hızlıca yapılandırın."] = "Quickly configure the dock's core behavior.",
            ["Dil"] = "Language",
            ["Arayüz dilini seçin"] = "Choose the interface language",
            ["Görünürlük"] = "Visibility",
            ["Etkileşim"] = "Interaction",
            ["Pasif modülden dönüş"] = "Return from inactive module",
            ["Etkin işi olmayan modülün ana dock'a dönme süresi"] = "Time before a module with no active task returns to the home dock",
            ["Pasif modülden dönüş süresi"] = "Inactive module return time",
            ["Saat ve tarih"] = "Time and date",
            ["Ana dock'taki zaman bilgisinin görünümünü kişiselleştirin."] = "Customize how time information appears on the home dock.",
            ["Saat biçimi"] = "Time format",
            ["Saniyeler"] = "Seconds",
            ["Tarih"] = "Date",
            ["Tarih biçimi"] = "Date format",
            ["Haftanın günü"] = "Day of the week",
            ["Göster"] = "Show",
            ["Gizle"] = "Hide",
            ["Süre dolduğunda alarm sesi en fazla 5 kez çalar."] = "The alarm sounds up to 5 times when the timer ends.",
            ["Saniyeleri göster"] = "Show seconds",
            ["Tarihi göster"] = "Show date",
            ["Haftanın gününü göster"] = "Show day of the week",
            ["Sistem modülleri"] = "System modules",
            ["Pil, ağ, Bluetooth ve zaman araçları etkin"] = "Battery, network, Bluetooth and time tools are enabled",
            ["Pil uyarıları"] = "Battery alerts",
            ["Düşük, kritik ve acil pil bildirimlerinin gösterileceği yüzdeleri belirleyin."] = "Set the percentages for low, critical and emergency battery alerts.",
            ["Düşük"] = "Low",
            ["Kritik"] = "Critical",
            ["Acil"] = "Emergency",
            ["Değişiklikler dock üzerinde anında önizlenir."] = "Changes are previewed on the dock immediately.",
            ["Tema, boyut ve hareket ayarları dock üzerinde anında önizlenir."] = "Theme, size and motion settings are previewed on the dock immediately.",
            ["Tema"] = "Theme",
            ["Yüzey stili"] = "Surface style",
            ["Saydam Bulanık Cam"] = "Transparent Blurred Glass",
            ["Bu tema sabit bir arka plan rengi kullanmaz; masaüstünü gerçek Windows Acrylic ile bulanık gösterir. Opaklık, yalnızca ince cam katmanının yoğunluğunu değiştirir."] = "This theme uses no fixed background color; it blurs the desktop with real Windows Acrylic. Opacity changes only the intensity of the thin glass layer.",
            ["Dock boyutları"] = "Dock dimensions",
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
            ["Dock'un hangi ekranda gösterileceğini seçin."] = "Choose which display shows the dock.",
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
            ["Windows 11 ile doğal şekilde bütünleşen modüler sistem dock'u."] = "A modular system dock that integrates naturally with Windows 11.",
            ["Sürüm"] = "Version",
            ["Ayar dosyası"] = "Settings file",
            ["Temel özellikler çevrimdışı çalışır. İlk sürüm telemetri, kullanıcı hesabı veya harici sunucu bağlantısı kullanmaz."] = "Core features work offline. This release uses no telemetry, user account or external server connection.",
            ["Temel özellikler çevrimdışı çalışır. MiaDock telemetri, kullanıcı hesabı veya otomatik hata gönderimi kullanmaz."] = "Core features work offline. MiaDock uses no telemetry, user account, or automatic error reporting.",
            ["Windows bildirimlerini izin verdiğiniz kapsamda MiaDock üzerinde gösterin."] = "Show Windows notifications on MiaDock within the permissions you grant.",
            ["Bildirim modülünü etkinleştir"] = "Enable notification module",
            ["İzin verilirse kaynak uygulama ve bildirim başlığı gösterilir."] = "When allowed, the source app and notification title are shown.",
            ["Görünme süresi"] = "Display duration",
            ["Bildirimin dock üzerinde kaç saniye kalacağını belirler"] = "Sets how long the notification remains on the dock",
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
            ["Dock'u göster / gizle"] = "Show / hide dock",
            ["Dock'u genişlet / küçült"] = "Expand / collapse dock",
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
            ["Ana dock"] = "Home dock",
            ["Bir modüle geçmek için üstteki simgeleri veya kaydırmayı kullanın."] = "Use the icons above or scroll to switch modules.",
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
            ["Yeni bir etkinlik olduğunda burada görünecek."] = "New activity will appear here.",
            ["Etkin olay yok"] = "No active event",
            ["Etkin zamanlayıcı yok"] = "No active timer",
            ["Bluetooth cihazı bağlı değil"] = "No Bluetooth device connected",
            ["Aktarım bulunmuyor"] = "No transfer found",
            ["İlerleme"] = "Progress",
            ["Önceki parçaya geçer"] = "Skips to the previous track",
            ["Geçerli parçayı oynatır veya duraklatır"] = "Plays or pauses the current track",
            ["Sonraki parçaya geçer"] = "Skips to the next track",
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
            ["Zamanlayıcı ve kronometreyi genişletilmiş dock içindeki Zaman modülünden yönetin. Fare tekeriyle varsayılan görünüm ve modüller arasında geçebilirsiniz."] = "Manage the timer and stopwatch from the Time module in the expanded dock. Use the mouse wheel to move between the default view and modules.",
            ["Faz 1 arayüz önizlemesi"] = "Phase 1 interface preview",
            ["Yerel sahte veri"] = "Local mock data",
            ["Tema stili"] = "Theme style",
            ["Dock durumu"] = "Dock state",
            ["Genişletilmiş müzik"] = "Expanded music",
            ["Parça bildirimi"] = "Track notification",
            ["Bu pencere geliştirme önizlemesidir. Dock canlı Windows medya oturumlarını kullanır; kalıcılık ve animasyonlar sonraki fazlarda eklenir."] = "This window is a development preview. The dock uses live Windows media sessions; persistence and animations are added in later phases.",
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

    public CultureInfo CurrentCulture { get; private set; } = new("tr-TR");

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
        CurrentCulture = culture;
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

    public string Get(string key, params object?[] arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (!Catalog.TryGetValue(key, out var entry))
        {
            return key;
        }

        var value = CurrentLanguage == AppLanguage.English
            ? entry.English
            : entry.Turkish;
        return arguments.Length == 0
            ? value
            : string.Format(CurrentCulture, value, arguments);
    }

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
        if (element is TextBlock textBlock &&
            textBlock.ReadLocalValue(TextBlock.TextProperty) is string)
            Localize(element, "Text", textBlock.Text, value => textBlock.Text = value);
        if (element is ContentControl contentControl &&
            contentControl.ReadLocalValue(ContentControl.ContentProperty) is string content)
            Localize(element, "Content", content, value => contentControl.Content = value);
        if (element is ToggleSwitch toggleSwitch)
        {
            if (toggleSwitch.ReadLocalValue(ToggleSwitch.HeaderProperty) is string toggleHeader)
            {
                Localize(element, "ToggleHeader", toggleHeader, value => toggleSwitch.Header = value);
            }
            if (toggleSwitch.ReadLocalValue(ToggleSwitch.OnContentProperty) is string onContent)
            {
                Localize(element, "ToggleOnContent", onContent, value => toggleSwitch.OnContent = value);
            }
            if (toggleSwitch.ReadLocalValue(ToggleSwitch.OffContentProperty) is string offContent)
            {
                Localize(element, "ToggleOffContent", offContent, value => toggleSwitch.OffContent = value);
            }
        }
        if (element is NumberBox numberBox &&
            numberBox.ReadLocalValue(NumberBox.HeaderProperty) is string numberHeader)
            Localize(element, "NumberHeader", numberHeader, value => numberBox.Header = value);
        if (element is Slider slider &&
            slider.ReadLocalValue(Slider.HeaderProperty) is string sliderHeader)
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
        if (element is TabViewItem tabViewItem &&
            tabViewItem.ReadLocalValue(TabViewItem.HeaderProperty) is string tabHeader)
            Localize(element, "TabHeader", tabHeader, value => tabViewItem.Header = value);

        if (element.ReadLocalValue(ToolTipService.ToolTipProperty) is string tooltip)
            Localize(element, "ToolTip", tooltip, value => ToolTipService.SetToolTip(element, value));
        if (element.ReadLocalValue(AutomationProperties.NameProperty) is string automationName &&
            !string.IsNullOrWhiteSpace(automationName))
            Localize(element, "AutomationName", automationName, value => AutomationProperties.SetName(element, value));
        if (element.ReadLocalValue(AutomationProperties.HelpTextProperty) is string automationHelpText &&
            !string.IsNullOrWhiteSpace(automationHelpText))
            Localize(element, "AutomationHelpText", automationHelpText, value => AutomationProperties.SetHelpText(element, value));
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
