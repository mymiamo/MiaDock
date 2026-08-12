# MiaDock 1.3.0.0 nihai geliştirme ve doğrulama raporu

## 1. Genel sonuç

* Tamamlanan ana özellik: 8/8. Dock kenar mesafesi, bağımsız köşeler, dört tam ekran davranışı, sağ tık/flyout etkileşim koruması, pil değerlendirme, Bluetooth radyo yaşam döngüsü, güvenli medya session yönetimi ve Focus ana kapatma ayarı gerçek servis akışlarına bağlandı.
* Sürüm engelleyici ek düzeltme: ses oturumu/alarm callback yaşam döngüsü ve hata bildirimi için ayrıntılı, gizlilik korumalı tanılama paketi.
* Genel build: Release x64, 0 hata ve 0 uyarı.
* Genel test: Core 299/299, Windows Platform 150/150, WinUI 151/151; toplam 600/600, skip yok.
* Release/Store build: `1.3.0.0`, x64, tek MSIX ve tek appxsym içeren `.msixupload` başarıyla üretildi.
* Tamamlanmayan yayın kapıları: yükseltilmiş WACK, gerçek 30 dakika yoğun kullanıcı etkileşimi, 8 saat boşta test ve fiziksel donanım/Canva matrisi.

## 2. Kullanıcı geri bildirimleri ve çözümleri

* Dock kenara fazla sabitti: placement hesabına 0–48 DIP `EdgeMargin` eklendi; dört kenar, work area, DPI ve clamp kurallarıyla canlı uygulanır.
* Dört köşe ayrı ayarlanamıyordu: tek radius, dört bağımsız radius ve isteğe bağlı link state’ine taşındı; XAML clip, hit-test ve native region aynı geometriyi kullanır.
* Tam ekran kapanınca dock geri gelmiyordu: aynı monitör filtresi, stale fullscreen sinyal süresi, periyodik recovery poll ve dört davranışlı visibility policy eklendi.
* Sağ tık sırasında dock kapanıyordu: context menu/flyout/pointer/basılı tutma nedenleri token/sayaç tabanlı etkileşim oturumunda birleştirildi; timer güncel state’i yeniden kontrol eder.
* Pil “yok” görünebiliyordu: `PowerSupplyStatus.NotPresent` tek başına fiziksel pil yok sayılmaz; availability, son başarılı snapshot, sınırlı retry ve resume yenilemesi ayrıldı.
* Bluetooth kapalıyken cihaz bağlı kalıyordu: radyo state’i watcher generation’ıyla bağlandı; Off durumunda cache temizlenir, eski callback reddedilir, On durumunda tek watcher yeniden kurulur.
* Canva/WebView2 medya geçişinde native kapanma riski vardı: session generation/lease, switch cancellation, session başına serialized native erişim ve managed snapshot yayınlama uygulandı.
* Focus kapatılsa da etkiler sürebiliyordu: global `IsEnabled` tüm profil, zamanlayıcı ve otomasyon girişlerinde fail-closed uygulanır; profil verileri korunur.
* Ses başlayınca uygulama kapanabiliyordu: alarm `MediaEnded` içinde replay/dispose kaldırıldı; işlem callback dışına ertelendi. Core Audio callback’leri sürerken RCW’leri zorla geçersiz kılan `FinalReleaseComObject` yolları kaldırıldı.
* Bug bildirimi için log yetersizdi: sıra, session, process/thread, exception type/HRESULT zinciri, medya/audio fazları, OS/runtime manifesti, olay özeti ve son 250 olay timeline’ı eklendi. Şarkı, sanatçı, bildirim içeriği, cihaz adı ve kişisel yollar hâlâ kaydedilmez.

## 3. Faz bazlı özet

* Faz 1 — Dock özelleştirmeleri: `OverlayPlacementCalculator`, `DockCornerRadii`, settings/schema 19, appearance UI ve native rounded region güncellendi. İlgili unit/WinUI testleri geçti. Fiziksel çoklu monitör/DPI matrisi kullanıcı doğrulaması ister.
* Faz 2 — Görünürlük ve etkileşim: `FullscreenDockVisibilityPolicy`, `DockEdgeRevealGeometry`, `ExclusiveFullscreenSignalTracker`, `DockInteractionSession` ve overlay timer akışı güncellendi. Giriş/çıkış, 1000 cycle ve iki sanal saat recovery testleri geçti.
* Faz 3 — Cihaz durumu: `PowerStatusEvaluator`, battery availability/retry/resume ve Bluetooth radio/watcher generation akışı eklendi. Deterministik testler geçti; farklı fiziksel adaptörler açık kapıdır.
* Faz 4 — Medya ve Focus güvenliği: `GenerationSessionAccessCoordinator`, mapper snapshot ayrımı ve Focus fail-closed kapısı eklendi. 100 paralel okuma, 1000 session switch ve dispose testleri geçti. Gerçek Canva testi ayrıca gerekir.
* Faz 5 — Sonlandırma: 1.3.0.0 sürümleme, 600 regresyon testi, soak/runtime/package doğrulaması, ses callback düzeltmesi, ayrıntılı log export ve release belgeleri tamamlandı. WACK ve tam süreli dış testler açık bırakıldı.

## 4. Önemli dosyalar

* `src/MiaDock.Core/Settings/*`: şema 19, yeni appearance/fullscreen/Focus alanları ve normalizasyon.
* `src/MiaDock.Core/Overlay/*`, `src/MiaDock.Core/Presentation/*`: edge margin, dört köşe ve fullscreen visibility geometrisi.
* `src/MiaDock.App/OverlayWindow.xaml.cs`, `Controls/IslandShell.xaml.cs`, `Services/DockInteractionSession.cs`: canlı geometry, edge reveal ve etkileşim nedenleri.
* `src/MiaDock.Platform.Windows/Fullscreen/*`: aynı monitör algılama, stale signal recovery ve hook/poll yaşam döngüsü.
* `src/MiaDock.Platform.Windows/Power/*`, `Bluetooth/*`: availability, retry/resume ve radio/watcher generation güvenliği.
* `src/MiaDock.Platform.Windows/Media/GenerationSessionAccessCoordinator.cs`, `WindowsMediaSessionService.cs`, `WindowsMediaMapper.cs`: stale session önleme ve managed snapshot hattı.
* `src/MiaDock.App/Services/FocusService.cs`, `FocusAutomationService.cs`, `ViewModels/FocusSettingsViewModel.cs`: global Focus kapısı.
* `src/MiaDock.Platform.Windows/Time/WindowsTimerAlarmPlayer.cs`: native callback dışına ertelenmiş alarm replay/dispose.
* `src/MiaDock.Platform.Windows/Audio/AudioSessionHandle.cs`, `WindowsSystemActivityService.cs`, `WindowsMediaAudioMeterService.cs`: güvenli Core Audio callback/RCW yaşam döngüsü ve bounded meter retry.
* `src/MiaDock.Core/Logging/*`, `src/MiaDock.Platform.Windows/Logging/*`, `src/MiaDock.App/ViewModels/DiagnosticsViewModel.cs`, `Views/Settings/DiagnosticsSettingsPage.xaml`: ayrıntılı, gizlilik korumalı bug-report logları.
* `src/MiaDock.App/MiaDock.App.csproj`, `Package.appxmanifest`, `app.manifest`: `1.3.0.0` Store/assembly/file sürümleri.
* `README.md`, `ROADMAP.md`, `doc/RELEASE_NOTES_1.3.0.md`, `doc/STORE_SUBMISSION_1.3.0.0.md`: yayın belgeleri.

## 5. Ayar modeli ve migrasyon

* Ana ayar şeması 18’den 19’a yükseltildi; Focus alt şeması 4’tür.
* `EdgeMargin`: varsayılan eski görsel boşluğu korur, 0–48 DIP aralığına clamp edilir.
* `TopLeft/TopRight/BottomRight/BottomLeftCornerRadius` ve `LinkCornerRadii`: eski tek `CornerRadius` dört değere kopyalanır.
* Fullscreen enum: HideCompletely, NotificationsOnly, EdgeReveal ve KeepVisible. Eski Enabled değeri en yakın yeni davranışa eşlenir; bilinmeyen değer güvenli varsayılana döner.
* Focus `IsEnabled`: eski kullanıcıda varsayılan `true`; `false` olduğunda aktif etki temizlenir fakat profil/schedule/rule verisi silinmez.
* Eksik, eski veya bozuk JSON; idempotent normalize edilir. Yeni kalıcı log/audio alanı eklenmedi.

## 6. Medya oturumu güvenlik raporu

Muhtemel eski yarış sırası şuydu: GSMTC session A için metadata/thumbnail async okuması başlar; WebView2 içeriği değiştirip session B’yi yayınlar; servis seçili referansı B yapar fakat A’nın await’i sürer; devam eden kod A üzerindeki playback/timeline/thumbnail nesnelerine yeniden dokunur; native A kapanmışsa managed `try/catch` dışında `0xC0000005` oluşabilir.

Yeni yaklaşımda her seçimin monoton bir generation taşıyan `SessionLease` nesnesi vardır. Switch önceki lease’i retire eder ve cancellation token’ını iptal eder. Native çağrıdan önce lease current/retired kontrolü yapılır; session başına `SemaphoreSlim` aynı nesne üzerindeki okumaları serialize eder. Eski işlem cancellation’ı yok saysa bile kendi eski gate’inde kalır ve yeni generation’ın erişimini bloke etmez.

Mapper playback/timeline/control primitive değerlerini await öncesi managed değerlere kopyalar. Metadata await’inden sonra eski WinRT session’a yeniden erişmez. Thumbnail stream, `MediaImageReader` içinde okunup kapatılır; UI’ya yalnız immutable/managed `MediaSnapshot` ve cache’lenmiş image verisi taşınır. Yayın öncesi lease referansı, generation, refresh generation ve track revision tekrar kontrol edilir; eski sonuç, artwork veya transport sonucu yayınlanmaz.

Session switch event unsubscribe/subscribe işlemi sync kilidi altında yapılır. Dispose manager ve session event’lerini kaldırır, metadata validation CTS’yi iptal eder, refresh queue’ları ve session coordinator’ı kapatır. Geç callback sender/current eşleşmesini geçemez.

Ek testler: switch cancellation; cancellation’ı yok sayan 100 eski operasyonun yeni session’ı bloke etmemesi; tek session’da 100 paralel isteğin `maxConcurrency=1` olması; 1000 hızlı switch; dispose sonrası erişim reddi. Gerçek Canva/WebView2 videosu bu ortamda otomatik sürülmedi; kullanıcı manuel listesi yayın kapısıdır. Kalan native riskler Windows/üçüncü taraf medya sağlayıcısı kusurlarıdır; kritik native çağrı öncesi güvenli disk checkpoint’i hata zamanını daraltır.

Ses tarafında ek bir native yaşam döngüsü düzeltmesi yapıldı: alarm callback’i içinde MediaPlayer replay/dispose edilmez; Core Audio callback’leri çözülürken RCW `FinalReleaseComObject` ile zorla geçersiz kılınmaz. Gerçek WMP ses oturumu denemesinde session sayısı 9→11→9 değişti, uygulama yanıt vermeyi sürdürdü ve hata kaydı oluşmadı.

## 7. Test matrisi

| Alan | Otomatik sonuç | Gerçek/manuel durum |
| --- | --- | --- |
| Dock kenar mesafesi | Geometri, clamp, dört kenar, DPI örnekleri geçti | Fiziksel çoklu monitör önerilir |
| Bağımsız köşeler | Model, migrasyon, hit-test/rasterizer geçti | Görsel anti-aliasing kontrolü önerilir |
| Tam ekran modları | Dört policy ve geçiş testleri geçti | Oyun/video üzerinde kontrol gerekli |
| Kenardan hover | Reveal geometry/token/timer testleri geçti | Gerçek pointer testi gerekli |
| Sağ tık/flyout | Çoklu token ve stale timer testleri geçti | Escape/dış tık görsel testi gerekli |
| Pil | Availability/retry/resume testleri geçti | Fiziksel adaptör gerekir |
| Bluetooth | Off/On/Unknown/stale generation testleri geçti | Fiziksel radyo/adaptör gerekir |
| Medya session güvenliği | 100 paralel, 1000 switch, dispose geçti | Canva/WebView2 gerekli |
| Ses/alarm güvenliği | Deferred callback ve RCW lifecycle testi geçti | Gerçek WMP ses oturumu geçti; 5 alarm tekrarı manuel |
| Focus kapatma | Profil/timer/automation fail-closed testleri geçti | Ayarlar UI akışı önerilir |
| Migrasyon | Şema 18→19, bozuk/eksik/idempotent testleri geçti | Eski kullanıcı yedeğiyle smoke önerilir |
| Yerelleştirme | TR/EN kaynak taramaları geçti | Metin tonu manuel kontrol edilebilir |
| Erişilebilirlik | Automation name/help ve XAML testleri geçti | Narrator/high contrast manuel |
| Çoklu monitör/DPI | Hesap ve monitor identity testleri geçti | Fiziksel matris açık |
| Dispose/eş zamanlılık | CTS, event, queue, watcher, lease testleri geçti | Uzun cihaz değişimi açık |
| Stres | Ölçekli event soak + fullscreen 1000 cycle/2 sanal saat geçti | 30 dakika/8 saat açık |
| Release build/package | 0 hata/uyarı; tek x64 MSIX/appxsym | WACK açık |

## 8. Çalıştırılan bütün komutlar

* `dotnet test MiaDock.sln -c Release --no-restore -m:1 ...`: 600/600; exit 0.
* `dotnet test tests/MiaDock.Platform.Windows.Tests/...`: ses/log değişikliklerinden sonra 150/150; exit 0.
* `dotnet build src/MiaDock.App/MiaDock.App.csproj -c Release -p:Platform=x64 -p:BuildMsix=false ...`: 0 hata, 0 uyarı; exit 0.
* StoreUpload MSBuild (`BuildMsix=true`, x64, symbol package, serial build): tek `.msixupload`; 0 hata, 0 uyarı; exit 0.
* ZipArchive manifest incelemesi: identity `mymiamo.net.MiaDock`, publisher doğru, version `1.3.0.0`, x64, StartupTask var, MSIX=1, appxsym=1; başarılı.
* WMP COM ses probe: üç saniye oynat/durdur; MiaDock PID 43912, pencere `MiaDock`, `Responding=True`; başarılı.
* Log incelemesi: ses probe sonrasında Warning/Error/Critical=0; Core Audio topology 9→11→9; başarılı.
* `Invoke-MiaDockSoakTest.ps1 -Scale 0.001 -AllowScaled` ve fullscreen soak filtreleri: başarılı; tam süreli kabul yerine geçmez.
* `Invoke-MiaDockRuntimeStability.ps1 -DurationSeconds 30`: başarılı; ortalama CPU %0.263, WS +2.29 MB, private +0.80 MB, handle +41, non-responding=0.
* `git diff --check`: whitespace hatası yok; çalışma ağacının line-ending ayarından gelen CRLF uyarıları var.
* WACK preflight: `appcert.exe` mevcut fakat shell yükseltilmiş değil; çalıştırılmadı.
* Son masaüstü launch: PID 29356, `MiaDock`, `Responding=True`; ilk 20 kayıt içinde Warning/Error/Critical=0.

## 9. Manuel test listesi

### Dock ve köşeler

Ön koşul: Ayarlar > Görünüm açık. İşlem: Edge margin’i 0, varsayılan ve maksimum yapın; dock’u dört kenarda ve farklı monitörde deneyin, DPI değiştirip yeniden başlatın. Beklenen: dock çalışma alanından çıkmaz ve değer kalır. Dört köşeyi aynı/farklı yapın, birini sıfırlayın, link’i açıp kapatın; compact/hover/expanded/notification ve sağ tık görünümünü kontrol edin. Beklenen: clip, tıklama alanı, backdrop ve native köşeler uyumludur.

### Tam ekran ve sağ tık

Ön koşul: İki monitör varsa dock birinde. İşlem: dört modu sırayla seçin; video/oyunu aynı ve farklı monitörde tam ekran açıp kapatın. EdgeReveal’da fareyi kenara getirip uzaklaştırın; bildirim/hover/flyout çakışmasını deneyin. Beklenen: yalnız aynı monitör etkilenir, tam ekran kapanınca dock geri gelir, etkileşim sürerken gizlenmez. Menüyü hızlı aç/kapatın, PointerExited, Escape, dış tık ve tam ekran geçişini deneyin. Beklenen: dock titremez/küçülmez ve menü kapanınca doğru gecikme uygulanır.

### Pil ve Bluetooth

Ön koşul: İlgili donanım mevcut. İşlem: adaptörü takıp çıkarın, yüzde/şarjı gözleyin, uykuya alıp uyandırın. Beklenen: geçici hata “pil yok” olmaz ve veri yenilenir. Bluetooth açıkken bağlı cihazı kontrol edin, radyoyu kapatıp açın ve hızlı birkaç geçiş yapın. Beklenen: Off iken bağlı cihaz kalmaz, On iken tek watcher yeniden keşfeder, bildirim fırtınası olmaz.

### Focus

Ön koşul: Aktif profil, schedule ve automation oluşturulmuş. İşlem: Focus ana ayarını kapatın, timer/app/fullscreen tetiklerini çalıştırın, sonra yeniden açın. Beklenen: kapalıyken tüm etkiler kalkar ve yeni tetik çalışmaz; profiller silinmez, açınca tekrar kullanılabilir.

### Ses ve zamanlayıcı alarmı

Ön koşul: Medya modülü görünür ve timer alarm sesi açık. İşlem: farklı bir uygulamada sesi hızlı başlat/durdurup uygulama değiştirin; ardından kısa timer kurup alarmın beş tekrarını ve hover ile susturmayı deneyin. Beklenen: MiaDock kapanmaz, UI yanıt verir, alarm callback’leri çakışmaz ve susturma kalan tekrarları keser. Ayarlar > Tanılama’da `audio.topology-rebind`, `media.audio-meter-binding` ve `time.alarm-*` olaylarını kontrol edin; saniyede tekrarlayan kayıt olmamalıdır.

### Canva/WebView2 medya testi

Ön koşul: MiaDock ve Canva/WebView2 video içeren tasarım açık. İşlem: videoyu oynat/durdur, farklı videolara ve sekmelere hızlı geç, oynarken sekmeyi/pencereyi kapat, Canva’yı yeniden aç, sonra başka medya uygulamasına geç; birkaç dakika tekrarla. Beklenen: MiaDock kapanmaz/donmaz; eski title/artwork taşınmaz; kontroller güncel session’ı yönetir; CPU/bellek kalıcı yükselmez. Sorun olursa Ayarlar > Tanılama > ZIP dışa aktar ile paketi hata saati ve yeniden üretme adımlarıyla gönderin.

### Paket/Store

Ön koşul: Partner Center’daki aynı identity/version eski upload satırları kaldırılmış ve Save edilmiştir. İşlem: yalnız `MiaDock.App_1.3.0.0_x64.msixupload` yükleyin; iç MSIX’i ayrıca yüklemeyin. Beklenen: revision sıfır ve duplicate-package doğrulaması geçer. Gönderimden önce WACK’i yönetici PowerShell’de çalıştırın.

## 10. Bilinen sınırlamalar ve riskler

* Gerçek Canva/WebView2 üretim sağlayıcısı otomatik ortamda sürülmedi; native access violation doğrudan yeniden üretilemedi. Stale-session yolu kod ve concurrency testleriyle kapatıldı, gerçek sağlayıcı testi yine yayın kapısıdır.
* Pil, Bluetooth, uyku/uyanma, çoklu monitör ve farklı DPI fiziksel donanım matrisi tamamlanmadı.
* Uygulama içi alarmın gerçek beş tekrar/susturma akışı UI otomasyonu olmadan çalıştırılmadı; callback state’i unit testle, genel ses oturumu gerçek WMP probe ile doğrulandı.
* 30 dakikalık tam yoğun etkileşim ve 8 saatlik idle soak çalıştırılmadı. Ölçekli soak ve kısa runtime ölçümü bunların yerine nihai kabul sayılmaz.
* WACK yönetici oturumu gerektirdiği için açık kapıdır. Partner Center’a otomatik yükleme yapılmadı.
* Teknik borç: Core Audio rebind callback’leri kısa patlamada birden fazla coalesced tur üretebilir; iş UI thread dışında ve bounded olsa da gelecekte debounce penceresi ölçülebilir.

## 11. Git durumu

* Branch: `master`.
* Çalışma ağacı dirty; kullanıcının önceki 1.2.x değişiklikleri ile bu 1.3.0 çalışması birlikte duruyor.
* Yerel değişiklikler korunmuştur; hiçbir dosya geri alınmadı veya kullanıcı değişikliği silinmedi.
* Commit, push, PR, tag veya GitHub değişikliği yapılmadı.
* Teslim anında 129 değiştirilmiş, 180 izlenmeyen/yeni ve 0 silinmiş yerel dosya vardır. Artifact içerikleri dahil `git status --porcelain --untracked-files=all` ile sayılmıştır.
