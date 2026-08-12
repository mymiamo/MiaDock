# MiaDock 1.3.0 Ayrıntılı Geliştirme Planı

Bu plan, 1.2.2.0 yerel çalışma ağacındaki mevcut değişiklikler korunarak uygulanır. Uygulama Store hedefli paketli WinUI 3 modelini korur. Uzak depo, commit, push, PR, tag ve Store yükleme işlemi yapılmaz.

Başlangıç doğrulaması: `dotnet test MiaDock.sln -c Release --no-restore` komutu 9 Ağustos 2026 tarihinde 548/548 testle başarılıdır (Core 273, Platform 128, WinUI 147).

## Faz 1 — Dock özelleştirmeleri

### Ana özellikler

1. Dock kenar mesafesi
2. Bağımsız köşe yuvarlaklığı

### Mevcut durum

* `OverlayLayoutRequest.MarginInDips` varsayılanı ve `OverlayWindowOptions.MarginInDips` değeri sabit 12 DIP’tir; ayar modelinde karşılığı yoktur.
* `OverlayPlacementCalculator` top/bottom yerleşimleri çoklu monitör çalışma alanı ve DPI ile sınırlar; mevcut enum sol/sağ merkez kenarlarını içermemektedir.
* `AppearanceSettings.CornerRadius`, `IslandLayoutOptions`, `IslandVisualMetrics`, `IslandShell`, `OverlayWindowController`, `LayeredRoundedBackdropWindow` ve hit-test tek simetrik yarıçap kullanır.
* Native region `CreateRoundRectRgn`, Composition clip `CompositionRoundedRectangleGeometry`, XAML `Border/SystemBackdropElement` aynı tek değere bağlıdır.
* Olası temel nedenler: sabit margin’in ayar katmanına taşınmamış olması; tek yarıçap veri modelinin bütün render katmanlarına yayılmış olması; native bölgenin yalnız simetrik geometri üretmesi.

### Teknik uygulama planı

* `AppearanceSettings` içine normalize edilen `EdgeMargin`, dört köşe değeri ve `LinkCornerRadii` alanları eklenecek; eski `CornerRadius` uyumluluk/migrasyon kaynağı olarak korunacak.
* Ortak, immutable bir dört-köşe değer tipi Core katmanına eklenecek; state yüksekliğine göre her köşe ayrı ayrı güvenli şekilde sınırlandırılacak.
* Yerleşim enumları sol/sağ merkez kenarlarını kapsayacak; calculator her kenarda doğru eksene margin uygulayacak ve çalışma alanında clamp edecektir.
* Controller margin ve dört-köşe geometrisini canlı güncelleyecek. Native region, hit-test ve anti-aliased backdrop aynı köşe değerlerini kullanacak; geçici HRGN nesneleri her yolda serbest bırakılacak.
* XAML Border/SystemBackdrop ve clip aynı değerlerle güncellenecek. Simetrik durumda Composition hızlı yolu korunacak; asimetrik durumda dört-köşeli XAML geometri yolu kullanılacak.
* Slider/NumberBox değişiklikleri SettingsService üzerinden canlı kaydedilip layout/region güncellemesini tetikleyecek; mevcut layout coalescing davranışı korunacak.
* Şema 18’den 19’a çıkarılacak. Eski `CornerRadius` dört köşeye kopyalanacak; eski 12 DIP görünümü varsayılan olarak korunacak. Migrasyon idempotent olacaktır.
* Türkçe/İngilizce metin, arama anahtarları, AutomationProperties adları ve yardım metinleri mevcut yerelleştirme altyapısına eklenecek.

### Etkilenecek dosyalar

* `src/MiaDock.Core/Settings/AppearanceSettings.cs`, `MiaDockSettings.cs`, `SettingsValidator.cs`, `SettingsEnums.cs`: yeni ayarlar, enum ve migrasyon.
* `src/MiaDock.Core/Overlay/*`, `src/MiaDock.Core/Presentation/IslandLayoutOptions.cs`: margin/kenar yerleşimi ve ortak köşe geometrisi.
* `src/MiaDock.App/Services/SettingsMapper.cs`, `SettingsViewModel.cs`: canlı ayar akışı.
* `src/MiaDock.App/Views/Settings/AppearanceSettingsPage.xaml(.cs)`: erişilebilir ayar kontrolleri ve preview.
* `src/MiaDock.App/Animations/*`, `Controls/IslandShell.xaml(.cs)`: dört köşeli state/animasyon/clip.
* `src/MiaDock.Platform.Windows/Overlay/*`: native region, backdrop ve hit-test.
* Core/Platform/WinUI test projeleri: calculator, migrasyon, geometri, kaynak ve XAML testleri.

### Riskler

* Regresyon: animasyon koordinatörü tek yarıçap varsayımına bağlıdır.
* Yarış durumu: hızlı ayar değişimi eski region/layout sonucunu uygulayabilir; controller UI thread ve son state ile çalışmalıdır.
* Native kaynak: custom HRGN üretimindeki her geçici GDI nesnesi silinmelidir.
* Çoklu monitör/DPI: DIP-pixel dönüşümü yalnız controller/calculator sınırında yapılmalıdır.
* Performans: asimetrik region ve clip yalnız gerçek geometri değişiminde yenilenmelidir.
* Migrasyon: eski tek değer görünümü birebir korunmalıdır.

### Test planı

* Unit: 0/12/maksimum/out-of-range margin; dört kenar; clamp; dört köşe normalize/link/migrasyon/idempotence.
* Entegrasyon: SettingsService canlı değişiminden controller/layout güncellemesine kadar veri akışı.
* Eş zamanlılık: hızlı ardışık geometri güncellemelerinde son değerin kazanması.
* Stres: yüzlerce region/hit-test/rasterizer üretimi; GDI sahiplik sözleşmesi kaynak testi.
* Manuel: dört konum kenarı, birden çok çözünürlük/DPI/monitör, taskbar kenarları, compact/hover/expanded/notification ve animasyon.

### Tamamlanma kriterleri

* Margin 0 dahil tüm geçerli değerlerde doğru kenara uygulanır ve dock ekran dışına çıkmaz.
* Dört köşe görsel, clip, native region, backdrop ve hit-test katmanlarında eşleşir.
* Eski ayarlar görünümü değiştirmeden schema 19’a migrate olur.
* Faz testleri ve makul kapsamlı regresyon testleri sıfır hata ile geçer.

## Faz 2 — Görünürlük ve etkileşim

### Ana özellikler

1. Tam ekran davranış seçenekleri
2. Sağ tık menüsü ve dock etkileşim hatası

### Mevcut durum

* `FullscreenSettings.Enabled` yalnız geçici bildirim iznidir; normal dock tam ekranda gizlenir.
* `OverlayWindow.ApplyEnvironment` tam ekran pencerenin monitörüne dock’u taşıdığı için sabit/ana monitördeki dock başka monitördeki tam ekrandan etkilenebilir.
* Edge-hover için ayrı visibility reason/state yoktur.
* `DockInteractionSession` reference-owner kümesi ve `IslandAutoCollapseController` askıya alma desteği mevcuttur; fakat ana `MenuFlyout` açılış/kapanış olaylarına bağlı değildir.
* Olası temel nedenler: tam ekran davranışının bool ile modellenmesi; görünürlük nedenlerinin tek `shouldShow` ifadesinde birleşmesi; flyout yaşam döngüsünün merkezi ve token tabanlı olmaması.

### Teknik uygulama planı

* `FullscreenBehavior` enumu: `Hide`, `NotificationsOnly`, `EdgeReveal`, `KeepVisible` eklenecek; legacy `Enabled` değeri NotificationsOnly/Hide davranışına migrate edilecek.
* Dock hedef monitörü ayardan çözülecek; tam ekran etkisi yalnız aynı monitörde uygulanacak.
* Görünürlük nedenleri (normal, manual, notification, fullscreen-edge-hover, expanded, transient interaction, pressed) ayrı state olarak tutulacak ve tek saf politika fonksiyonuyla birleştirilecek.
* EdgeReveal yalnız ilgili tam ekran ve monitörde düşük frekanslı, yaşam döngüsü sınırlı bir cursor kontrolü kullanacak; global hook eklenmeyecek. Kenardan çıkış gecikmeli ve güncel state yeniden kontrol edilerek yapılacak.
* Controller gizli/reveal offset’ini bağlı kenara göre uygulayacak; 2 DIP güvenli aktivasyon şeridi bırakacak.
* `MenuFlyout.Opened/Closed` merkezi DockInteractionSession token’ına bağlanacak. Token/sayaç idempotent olacak; başka flyout açıkken tek kapanış auto-collapse’ı başlatmayacak.
* Timer callback’leri güncel pointer, expanded, interaction ve fullscreen state’ini yeniden kontrol edecek. Kapanışta timer/event abonelikleri temizlenecek.
* Şema, yerelleştirme, erişilebilirlik ve ayarlar araması Faz 1’de açılan schema 19 içinde tamamlanacak.

### Etkilenecek dosyalar

* `FullscreenSettings.cs`, `SettingsEnums.cs`, `SettingsValidator.cs`: enum, defaults ve legacy migrasyon.
* `OverlayWindow.xaml(.cs)`, `DockInteractionSession.cs`, `IslandAutoCollapseController.cs`: görünürlük state’i ve flyout yaşam döngüsü.
* `IOverlayWindowController.cs`, `OverlayWindowController.cs`: edge reveal yerleşimi.
* `FullscreenSettingsPage.xaml`, `SettingsViewModel.cs`, `AppLocalizationService.cs`, `SettingsWindow.xaml.cs`: UI/arama/yerelleştirme.
* Core/Platform/WinUI tests: politika, monitör izolasyonu, timer/flyout yaşam döngüsü.

### Riskler

* Regresyon: mevcut fullscreen recovery düzeltmesi ve bildirim davranışı korunmalıdır.
* Yarış durumu: hover çıkışı, flyout kapanışı, notification süresi ve fullscreen çıkışı aynı anda olabilir.
* Native kaynak: cursor/placement için yeni global hook kullanılmayacaktır.
* Çoklu monitör/DPI: aktivasyon şeridi doğru monitör work area/outer bounds üzerinde hesaplanmalıdır.
* Performans: edge polling yalnız gereken durumda ve düşük frekansta çalışmalıdır.
* Migrasyon: legacy `Enabled=false` Hide, `Enabled=true` NotificationsOnly olarak korunacaktır.

### Test planı

* Unit: dört mod politika matrisi; visibility reason birleşimi; farklı monitör; çıkış gecikmesi generation kontrolü.
* Entegrasyon: fullscreen giriş/çıkış, notification+hover, expanded/flyout/pressed koruması.
* Eş zamanlılık: hızlı fullscreen A/B, hızlı menü aç/kapat, stale timer callback.
* Stres: binlerce state sinyali altında sınırlı timer ve event sayısı.
* Manuel: oyun/video, farklı monitör, edge hover, menü Escape/dış tık, bildirim, uygulama çöküşü/kapanışı.

### Tamamlanma kriterleri

* Dört mod açıklanan davranışı verir; başka monitördeki tam ekran sabit dock’u etkilemez.
* Flyout/context menu açıkken dock küçülmez, gizlenmez veya geometri değiştirmez.
* Edge hover kalıcı yüksek CPU/global hook oluşturmaz; çıkışta tüm timer/event’ler temizlenir.
* Faz ve regresyon testleri geçer.

## Faz 3 — Sistem bilgisi servisleri

### Ana özellikler

1. Pil algılama düzeltmesi
2. Bluetooth bağlantı durumu düzeltmesi

### Mevcut durum

* `WindowsPowerStatusService.ReadSnapshot`, BatteryStatus ve PowerSupplyStatus değerlerinden herhangi biri `NotPresent` ise fiziksel pil yok sonucunu üretir.
* Snapshot yalnız `Ready/Unavailable/Faulted` servis state’i ve bool `IsBatteryPresent` taşır; unknown/temporary ayrımı yoktur.
* `WindowsBluetoothStatusService` yalnız paired AssociationEndpoint watcher kullanır; `Windows.Devices.Radios.Radio.StateChanged` izlenmez.
* Radyo kapanınca watcher cache’i bağlı cihazları koruyabilir; geç watcher callback’i generation kontrolü yapmaz.

### Teknik uygulama planı

* Pil için test edilebilir reader abstraction ve `BatteryPresenceState` (Present/NotPresent/Unknown/Unavailable) eklenecek.
* Fiziksel pil varlığı RemainingChargePercent, BatteryStatus ve PowerSourceKind sinyalleriyle değerlendirilecek; `PowerSupplyStatus.NotPresent` tek başına yokluk sayılmayacak.
* Geçici hata son başarılı snapshot’ı koruyup state’i Unknown/Faulted olarak işaretleyecek; sınırlı gecikmeli retry ve resume refresh eklenecek.
* Bluetooth için radio abstraction/state modeli (On/Off/Unknown/Unavailable), watcher generation ve serialized restart uygulanacak.
* Radio Off durumunda device cache atomik temizlenip tek snapshot yayınlanacak; watcher durdurulup abonelikleri kaldırılacak. On durumunda yalnız bir watcher başlatılacak.
* Her callback sender/generation/disposed kontrolü yapacak; geç callback etkisiz kalacak. Tek radio-off geçişinde cihaz başına notification fırtınası oluşmayacak.
* Türkçe/İngilizce durum metinleri ve erişilebilir durumlar view model/localization katmanına eklenecek.

### Etkilenecek dosyalar

* DeviceStatus model/viewmodel/module dosyaları: presence/radio state ve kullanıcı metinleri.
* `WindowsPowerStatusService.cs`, yeni power reader/presence evaluator dosyaları.
* `WindowsBluetoothStatusService.cs`, yeni radio/watcher adapter ve reducer/state machine dosyaları.
* `WindowsSystemResumeService` bağlantısı/DI kayıtları.
* Platform/Core tests: fake reader/radio/watcher, retry, generation, dispose ve notification testleri.

### Riskler

* Regresyon: masaüstü cihazlar yanlış “pil bilinmiyor” göstermemelidir.
* Yarış durumu: hızlı radio On/Off ve watcher Stopped/Added callback sıralaması.
* Native kaynak: Radio/DeviceWatcher event abonelikleri ve WinRT nesneleri idempotent temizlenmelidir.
* Donanım: gerçek pil/Bluetooth varyasyonları otomasyonda bütünüyle üretilemez.
* Performans: sürekli polling yok; retry sınırlı ve backoff’lu olmalıdır.
* Ayar migrasyonu: bu faz kalıcı alan gerektirmez.

### Test planı

* Unit: pil sinyal matrisi; transient hata; desktop; charging/discharging; radio reducer/generation.
* Entegrasyon: adapter event’leri, resume, watcher yeniden başlatma ve dispose.
* Eş zamanlılık: hızlı adaptör ve radio toggle; stale watcher callback.
* Stres: yüzlerce radio geçişi, tek watcher ve sınırlı yayın sayısı.
* Manuel: laptop/desktop, AC çıkar-tak, sleep/resume, Bluetooth cihaz bağlıyken kapat/aç.

### Tamamlanma kriterleri

* `PowerSupplyStatus.NotPresent` tek başına fiziksel pili yok saymaz; belirsiz ve unavailable durumları doğru metinle gösterilir.
* Bluetooth Off anında stale bağlantı görünmez; On yeniden keşfi tek watcher ile başlar.
* Dispose sonrasında hiçbir geç callback state değiştirmez.
* Faz ve regresyon testleri geçer; donanım gerektiren kalan kontroller açıkça raporlanır.

## Faz 4 — Güvenlik ve özellik kontrolü

### Ana özellikler

1. Güvenli Windows medya oturumu yönetimi
2. Odak özelliklerini tamamen kapatma

### Mevcut durum

* `WindowsMediaSessionService` gerçek `GlobalSystemMediaTransportControlsSession` nesnesini `_selectedSession` içinde tutar; Mapper playback/timeline okur, metadata await eder ve daha sonra eski session’ın alanlarını kullanır.
* Generation kontrolü yalnız `MapAsync` bittikten sonra yapılır; session değişimi devam eden mapper/artwork görevini iptal etmez.
* `CoalescingRefreshQueue` yalnız servis lifetime token’ına sahiptir. Transport komutları snapshot okumasıyla paralel çalışabilir.
* `ScheduleMetadataValidation` fire-and-forget görevi gözlemlenmez ve session’a özgü generation taşımaz.
* Focus servisleri uygulama başında koşulsuz başlar; `FocusSettings` ana enable alanı ve runtime stop/start yolu yoktur.

### Teknik uygulama planı

* Native session ve manager için test edilebilir adapter arayüzleri oluşturulacak. UI/Modules katmanına yalnız mevcut immutable managed `MediaSnapshot` aktarılacak.
* Her seçili session için atomik generation + ayrı CTS oluşturulacak. Switch sırası: generation artır, eski token cancel, eski event’leri unsubscribe et, yeni session/token ata, bir kez subscribe et.
* Session erişimi tek serialized/coalescing worker üzerinden yapılacak; metadata/playback/timeline/artwork adımları aynı generation bağlamında çalışacak. Her native çağrıdan önce, her await sonrasında ve publish öncesinde context geçerliliği denetlenecek.
* Eski worker yeni session’ı bloke etmeyecek; session switch cancellation ile eski okuma sonlandırılacak ve yeni generation için ayrı bounded/coalesced request işlenecek.
* Thumbnail stream adapter içinde managed byte dizisine dönüştürülüp dispose edilecek; thumbnail hatası metadata snapshot’ını düşürmeyecek.
* Fire-and-forget yolları gözlemlenen helper veya worker’a taşınacak; beklenen cancellation loglanmayacak, tekrar eden COM hatası rate-limit edilecek.
* Transport komutları generation context’i ve serialization kapısından geçecek; stale session’a await sonrası yeni çağrı yapılmayacak.
* FocusSettings’e `IsEnabled=true` eklenecek. `FocusFeatureCoordinator`, ayarı izleyip FocusService/Automation için idempotent Start/Stop yapacak.
* Kapatırken aktif state temizlenecek, policy inactive yayınlanacak, timer/event’ler duracak; profiller korunacak. Yeniden açınca eski aktif profil otomatik geri uygulanmayacak.
* Focus settings page ana toggle ve disabled explanation gösterecek; dock focus badge/quick panel `IsEnabled` false iken gizlenecek/etkisiz olacak.

### Etkilenecek dosyalar

* `MiaDock.Platform.Windows/Media/WindowsMediaSessionService.cs`, mapper/image reader ve yeni adapter/worker/context dosyaları.
* Media concurrency/stress tests ve fake session modelleri.
* `FocusSettings.cs`, `SettingsValidator.cs`, Focus service/interface/automation/policy/coordinator dosyaları.
* `App.xaml.cs`, `ServiceRegistration.cs`, Focus view models/XAML ve dock focus kontrolleri.
* AppLocalizationService, settings search ve test projeleri.

### Riskler

* Regresyon: transport command ve media source selection davranışı korunmalıdır.
* Yarış durumu: A→B→C, null session, dispose, delayed metadata/thumbnail, event sırasında switch.
* Native kaynak: WinRT event tokenları, stream, CTS ve adapter dispose yolları.
* Performans: bounded/coalesced worker event fırtınasında kuyruk büyütmemelidir.
* Ayar migrasyonu: eski kullanıcılar Focus açık ve profilleri/aktif state’i korunmuş başlamalıdır; kullanıcı kapattığında active state temizlenir.

### Test planı

* Unit/concurrency: zorunlu A/B/C, null, delayed metadata/playback/timeline/thumbnail, stale result, cancellation, dispose, COM error ve no-parallel-read testlerinin tamamı.
* Stres: en az 1000 deterministik session geçişi; bounded iş sayısı, stale çağrı/publish yok, dispose sonrası erişim yok.
* Focus: default/migration, active/automation/timer sırasında kapatma, data preservation, restart tekilliği, disabled startup ve unsubscribe.
* Regresyon: media selection/control/cache ve Focus model/policy/viewmodel testleri.
* Manuel: Canva/WebView2, tarayıcı sekmeleri, Spotify/Apple Music/YouTube, hızlı kaynak değişimi; Focus toggle ve profil korunması.

### Tamamlanma kriterleri

* Stale session üzerinde yeni veya await sonrası kontrolsüz native çağrı yolu kodda kalmaz.
* Session switch eski işleri iptal eder; event abonelikleri tekil ve idempotenttir; UI yalnız managed snapshot alır.
* 1000 geçişli deterministik stres testi ve tüm concurrency testleri geçer.
* Focus kapalı başlangıçta arka plan servisleri başlamaz; runtime kapatma policy etkilerini kaldırır ve veriyi korur.
* Gerçek native `0xC0000005` unit testte üretilemiyorsa bu sınırlama raporlanır; hataya yol açan stale/paralel yollar testle kapatılır.

## Faz 5 — Ortak sonlandırma ve doğrulama

### Ana özellikler

1. Ortak stabilizasyon ve yayın doğrulaması

### Mevcut durum

* Başlangıç sürümü proje/manifest/scriptlerde 1.2.2.0; README/ROADMAP kısmen 1.2.1.0 içeriği taşımaktadır.
* Başlangıç otomatik regresyonu 548/548 geçmektedir.
* Çalışma ağacı önceki 1.2.1/1.2.2 yerel değişiklikleri içerir; bunlar korunacaktır.

### Teknik uygulama planı

* Schema 19 migrasyonu JSON recovery, serialize/deserialize, reset ve ikinci normalize geçişleriyle doğrulanacak.
* Tüm testler, media/fullscreen/device soak kategorileri, Release x64 build ve paketli modele uygun launch smoke çalıştırılacak.
* UI-thread, DispatcherQueue, timer, event, CTS, stream, HRGN/HBITMAP/DC ve service Dispose yolları gözden geçirilecek.
* Yerelleştirme anahtarları ve Türkçe/İngilizce arama metinleri; AutomationProperties, keyboard tab order ve yüksek kontrast kaynak kullanımı statik/runtime testlerle doğrulanacak.
* Sürüm proje, assembly, file, informational, manifest ve release/validation scriptlerinde Store uyumlu `1.3.0.0` yapılacak.
* README ve ROADMAP yalnız doğrulanan kapsamla güncellenecek; çift dilli `doc/RELEASE_NOTES_1.3.0.md`, faz raporları ve manuel test listesi oluşturulacak.
* Store upload dosyası bu talebin kapsamı değildir; Store’a yükleme yapılmayacaktır.

### Etkilenecek dosyalar

* Tüm test projeleri ve validation/soak scriptleri.
* `MiaDock.App.csproj`, `Package.appxmanifest`, release/validation script sürüm sabitleri.
* `README.md`, `ROADMAP.md`, `doc/RELEASE_NOTES_1.3.0.md`, faz sonuç ve nihai rapor belgeleri.

### Riskler

* Regresyon: geniş ayar ve yaşam döngüsü değişiklikleri birlikte etkileşebilir.
* Yarış/native kaynak: yalnız gerçek cihaz/Canva/donanım bazı yolları tetikleyebilir.
* Çoklu monitör/DPI/pil/Bluetooth testleri mevcut makinenin donanımıyla sınırlıdır.
* Performans: uzun idle/30 dakika etkileşim testi bu otomatik oturum süresinde bütünüyle gözlenemeyebilir; script ve kullanıcı kontrol listesi sağlanacaktır.
* Dirty tree: paket/release kanıt betikleri temiz ağaç isteyebilir; yerel değişiklikleri silmeden `AllowDirtyWorkingTree` yalnız doğrulama amacıyla kullanılabilir, Store adayı olarak sunulmaz.

### Test planı

* Unit/integration/concurrency/cancellation/dispose/native-resource/stress/regression testlerinin tamamı.
* Release x64 build, launch smoke ve gerçek top-level window doğrulaması.
* Manuel: tüm talep kontrol listesi; özellikle Canva/WebView2, çoklu monitör/DPI, laptop pil ve Bluetooth donanımı.

### Tamamlanma kriterleri

* Tüm otomatik testler sıfır başarısızlıkla geçer; Release x64 build sıfır uyarı/hata verir.
* Uygulama gerçek top-level dock penceresiyle açılır ve çalışma sonunda doğrulanmış örnek kullanıcıya açık bırakılır.
* Sürüm bütün aktif noktalarda 1.3.0.0’dır; schema 19’dur.
* Yalnız tamamlanan özellikler çift dilli sürüm notuna girer; doğrulanamayan donanım/native senaryoları açıkça bilinen sınırlama olarak raporlanır.
