namespace MiaDock.App.Services;

public sealed record ModuleServiceDisclosure(
    string ModuleId,
    string Title,
    string ServiceName,
    string DataUse,
    bool RequiresWindowsPermission);

public static class ModuleServiceDisclosureCatalog
{
    public static ModuleServiceDisclosure Get(
        string moduleId,
        IAppLocalizationService localization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleId);
        ArgumentNullException.ThrowIfNull(localization);
        string L(string turkish, string english) =>
            localization.Text(turkish, english);

        return moduleId switch
        {
            "media" => new(
                moduleId,
                L("Medya", "Media"),
                "Windows Global System Media Transport Controls",
                L(
                    "Etkin medya oturumunun parça, sanatçı, kapak ve oynatma durumunu cihazda işler; oynatma komutlarını seçili uygulamaya gönderir.",
                    "Processes the active media session's track, artist, artwork, and playback state on this device; sends playback commands to the selected app."),
                false),
            "volume" => new(
                moduleId,
                L("Windows ana sesi", "Windows master volume"),
                "Windows Core Audio · IAudioEndpointVolume",
                L(
                    "Varsayılan çıkış aygıtının ses seviyesini ve sessiz durumunu cihazda okur ve kullanıcı komutuyla değiştirir.",
                    "Reads the default output device's volume and mute state on this device and changes it only on user command."),
                false),
            "privacy" => new(
                moduleId,
                L("Gizlilik", "Privacy"),
                "Windows Capability Access Manager ConsentStore",
                L(
                    "Mikrofon ve kamerayı kullanan uygulamaları yerel olarak gösterir. Ses veya görüntü içeriği okunmaz ve hiçbir veri gönderilmez.",
                    "Locally shows which apps are using the microphone and camera. Audio or video content is never read and nothing is sent off-device."),
                false),
            "system-activity" => new(
                moduleId,
                L("Arama etkinliği", "Call activity"),
                "Windows Core Audio sessions",
                L(
                    "Mikrofon ve iletişim sesi oturumlarından sınırlı yerel arama çıkarımı yapar. Görüşme içeriği okunmaz.",
                    "Infers limited local call activity from microphone and communication audio sessions. Call content is never read."),
                false),
            "battery" => new(
                moduleId,
                L("Pil", "Battery"),
                "Microsoft.Windows.System.Power.PowerManager",
                L(
                    "Pil yüzdesi, şarj kaynağı ve enerji tasarrufu durumunu cihazda izler.",
                    "Monitors battery percentage, power source, and energy-saver state on this device."),
                false),
            "network" => new(
                moduleId,
                L("Ağ", "Network"),
                "NetworkInformation · Windows IP Helper API",
                L(
                    "Bağlantı türünü ve yalnız geniş görünüm açıkken adaptör byte sayaçlarından anlık hızı ölçer. Trafik içeriği okunmaz.",
                    "Reads connection type and, only while the expanded view is open, measures current speed from adapter byte counters. Traffic content is never read."),
                false),
            "bluetooth" => new(
                moduleId,
                "Bluetooth",
                "Windows DeviceWatcher",
                L(
                    "Eşleştirilmiş Bluetooth cihazlarının bağlanma durumunu yerel olarak izler; eşleştirme veya bağlantı yönetimi yapmaz.",
                    "Locally monitors connection state for paired Bluetooth devices; it does not pair or manage connections."),
                false),
            "device-hub" => new(
                moduleId,
                "Device Hub",
                "Windows DeviceWatcher · BluetoothAPIs",
                L(
                    "Eşli Bluetooth, ses ve USB aygıtlarını cihazda listeler. Kullanıcı komutuyla eşli Bluetooth cihazını bağlar veya ayırır; yeni eşleştirme yapmaz.",
                    "Lists paired Bluetooth, audio, and USB devices on this device. Connects or disconnects a paired Bluetooth device only on user command; it does not pair new devices."),
                false),
            "timer" => new(
                moduleId,
                L("Zamanlayıcı ve kronometre", "Timer and stopwatch"),
                L("MiaDock yerel zaman servisi ve alarm oynatıcı", "MiaDock local time service and alarm player"),
                L(
                    "Zamanlayıcı ve kronometre durumunu yerel ayarlarda saklar ve süre dolduğunda yerel alarm sesini çalar.",
                    "Stores timer and stopwatch state in local settings and plays the local alarm sound when time expires."),
                false),
            "notifications" => new(
                moduleId,
                L("Windows bildirimleri", "Windows notifications"),
                "Windows UserNotificationListener",
                L(
                    "İzin verilen uygulamaların kaynak adını ve bildirim başlığını cihazda işler. Gövde ayrıca açılmadıkça okunmaz; içerik loglanmaz.",
                    "Processes the source app and notification title for allowed apps on this device. Body text is not read unless separately enabled; content is not logged."),
                true),
            "transfers" => new(
                moduleId,
                L("Dosya aktarımları", "File transfers"),
                L("Kullanıcıya özel MiaDock named-pipe servisi", "User-scoped MiaDock named-pipe service"),
                L(
                    "Yerel sağlayıcıların gönderdiği aktarım kimliği, durum ve byte ilerlemesini işler. Dosya yolu zorunlu değildir ve loglanmaz.",
                    "Processes transfer ID, state, and byte progress sent by local providers. File paths are optional and are never logged."),
                false),
            "clipboard-peek" => new(
                moduleId,
                "Clipboard Peek",
                "Windows Clipboard API",
                L(
                    "Kopyalanan metin, dosya ve görselleri yalnız cihazda gösterir. Hassas içerik geçmişe eklenmez, içerik loglanmaz ve uygulama kapatılınca geçmiş temizlenebilir.",
                    "Shows copied text, files, and images only on this device. Sensitive content is never added to history, content is not logged, and history can be cleared on exit."),
                false),
            _ => new(
                moduleId,
                moduleId,
                L("MiaDock yerel modül servisi", "MiaDock local module service"),
                L(
                    "Bu modülün durumu yalnız cihaz üzerinde işlenir.",
                    "This module's state is processed only on this device."),
                false)
        };
    }
}
