# Yenilikler / What's New

En yeni sürüm her zaman bu sayfanın en üstünde yer alır.
The latest release is always shown at the top of this page.

## MiaDock 1.5.4

### Türkçe

* Eşleştirilmiş Bluetooth cihazları dock içinden bağlanıp ayrılabilir; yeni eşleştirme akışı eklenmedi.
* Tray menüsündeki komutlar semantic Fluent simgelerle standardize edildi. Koyu flyout üzerinde daha iyi okunmaları için simgeler yüksek kontrastlı açık dolgu kullanır.
* Kenarda gizle artık 15 DIP'lik işlevsel bir durum rafı gösterir: çekme göstergesiyle birlikte ağ, Bluetooth, batarya, gizlilik ve medya etkinliği görünür. Açma hassasiyeti ve tam ekran korumaları korunur.
* Kenarda gizle çentiği artık dock'un seçili temasının yüzey ve çerçevesiyle eşleşir. Kamera ve mikrofon gizlilik noktası dock ile aynı ölçü ve nabız davranışını kullanır.
* Tray yaşam döngüsü ve WinUIEx paket denetimleri güçlendirildi; kapanıştan sonra gelen geç komutlar güvenle yok sayılır.
* Ses aygıtı keşfi NAudio ile sadeleştirildi. Özel uygulama karıştırıcısı ve medya ses ölçümü korunarak mevcut davranış bozulmadı.
* Yinelenen platform proje başvurusu ve kullanılmayan native bildirim temizlendi.
* Yayın doğrulaması Fluent SVG varlıklarını, altı dilde tray metinlerini, yinelenen proje başvurularını, WinUIEx çalışma zamanı dosyalarını ve MSIX içeriğini denetler.

### English

* Paired Bluetooth devices can be connected and disconnected from the dock; pairing new devices is not included.
* Tray commands now use standardized semantic Fluent icons. Their high-contrast light fill remains readable on the dark flyout.
* Edge Reveal now exposes a functional 15-DIP status shelf with a pull cue plus network, Bluetooth, battery, privacy, and media activity. Activation sensitivity and fullscreen safeguards are unchanged.
* The Edge Reveal notch now matches the selected dock theme's surface and border. Camera and microphone privacy dots use the same size and pulse behavior as the dock.
* Tray lifetime handling and WinUIEx package checks were strengthened; late commands after shutdown are safely ignored.
* Audio endpoint discovery was simplified with NAudio while preserving the specialized application mixer and media meter.
* A duplicate platform project reference and an unused native declaration were removed.
* Release validation now verifies Fluent SVG assets, tray strings in six languages, duplicate project references, WinUIEx runtime files, and MSIX contents.

## MiaDock 1.5.3

### Türkçe

* Clipboard Peek; alfa içeren CSS HEX, RGB ve HSL renklerini tek dokunuşla dönüştürür, düz metinde Unicode karakter, kelime ve satır sayısını gösterir.
* Sistem tepsisi menüsü güvenilir klavye gezintisi, alt menüler ve Explorer yeniden başlatma desteği için yerel WinUI yüzeyine taşındı.
* Bildirim sesleri belirli Windows çıkış aygıtını, %0–100 ses seviyesini ve önizlemeyi durdurmayı destekler. Kayıp aygıtlar güvenle varsayılan çıkışa döner.
* Kenarda gizle daha ince bir tutamak kullanır ve yanlışlıkla açılmayı önler. Exclusive Direct3D tam ekranda oyun korunur; borderless tam ekranda kenardan açma sürer.

### English

* Clipboard Peek converts CSS HEX, RGB, and HSL colors with alpha in one tap and shows Unicode character, word, and line counts for plain text.
* The system tray menu moved to a native WinUI surface for reliable keyboard navigation, submenus, and Explorer restart recovery.
* Notification sounds support a selected Windows output device, 0–100% volume, and stopping previews. Missing devices safely fall back to the default output.
* Edge Reveal uses a subtler handle to avoid accidental opening. Exclusive Direct3D games are protected while borderless fullscreen retains edge reveal.

## MiaDock 1.5.2

### Türkçe

* Tozpembe tema, yuvarlak adanın çevresindeki pencere saydam kalırken dock tamamen pembe görünecek şekilde iyileştirildi.

### English

* The Tozpembe theme was refined so the dock stays fully pink while the window around the rounded island remains transparent.

## MiaDock 1.5.1

### Türkçe

* Tozpembe tema eklendi: koyu ve okunaklı yazılı pudramsı pembe dock yüzeyi.

### English

* Added the Tozpembe theme: a dusty-pink dock surface with dark, readable text.

## MiaDock 1.5.0

### Türkçe

* Varsayılan açık Device Hub, Bluetooth'u, hoparlörleri, kulaklıkları, mikrofonları ve USB depolamayı tek yerde gösterir.
* Cihaz bağlanma/ayrılma, ses çıkışı değişimi ve desteklenen aygıtlarda düşük pil uyarıları eklenir.
* USB sürücüleri dock'tan güvenle çıkarılabilir; Windows Bluetooth ve ses ayarları açılabilir.
* Varsayılan kapalı Clipboard Peek, kopyalanan metni, bağlantıyı, e-postayı, rengi, dosyayı, klasörü ve görseli kısaca gösterir.
* Son kopyalar yalnız bu oturumun belleğinde tutulur ve MiaDock kapandığında temizlenir; parolalar ve benzeri hassas içerik siz açana kadar gizli kalır.
* Akıllı bildirim, her kopya veya hiçbiri seçenekleri; bağlantı açma, e-posta oluşturma, görsel kaydetme ve klasörde gösterme eylemleri eklendi.
* Saat başı hatırlatıcı ve ağ, pil, cihaz ve saat başı için isteğe bağlı kısa bildirim sesleri eklendi.
* Boştaki genişletilmiş dock Wi-Fi ve Bluetooth'u açıp kapatabilir; tam ekran kenar-açma ince durum şeridini korur.

### English

* The default-enabled Device Hub shows Bluetooth, speakers, headphones, microphones, and USB storage in one place.
* Alerts are available for device connections and disconnections, audio output changes, and low battery on supported devices.
* USB drives can be safely ejected from the dock, and Windows Bluetooth or sound settings can be opened.
* The default-disabled Clipboard Peek briefly shows copied text, links, email, colors, files, folders, and images.
* Recent copies remain only in session memory and are cleared when MiaDock closes; passwords and similar sensitive content stay hidden until you reveal them.
* Smart, every-copy, and no-notification modes were added alongside opening links, composing email, saving images, and showing files in their folders.
* An hourly reminder and optional short sounds for network, battery, device, and hourly events were added.
* The expanded idle dock can toggle Wi-Fi and Bluetooth, while fullscreen edge reveal retains a slim status strip.

## MiaDock 1.4.3

### Türkçe

* Genişletilmiş Zaman görünümü, Zamanlayıcı ve Kronometre için Fluent sekme çubuğuyla yenilendi.
* Hazır süre yerleşimi, özel süre girişi ve kronometre tur okunabilirliği iyileştirildi.
* Sekmeler arasında geçerken zamanlayıcı ve kronometre durumları bağımsız tutuldu.

### English

* The expanded Time view was redesigned with a Fluent tab bar for Timer and Stopwatch.
* Preset durations, custom duration entry, and stopwatch lap readability were improved.
* Timer and stopwatch states remain independent when switching tabs.

## MiaDock 1.4.2

### Türkçe

* Medya kaynakları ve Microsoft Store güncelleme durumu yenilenirken açık yükleme geri bildirimi eklendi.
* Daha tutarlı bir deneyim için Ayarlar güncelleme durumu yönetimi iyileştirildi.

### English

* Clear loading feedback was added while refreshing media sources and Microsoft Store update status.
* Settings update-state handling was improved for a more consistent experience.

## MiaDock 1.4.1

### Türkçe

* Bir medya uygulaması etkinken Windows parlaklık tuşlarının güvenilirliği iyileştirildi.
* Kaybolan aygıtların MiaDock'u beklenmedik biçimde kapatmaması için ses ve medya yönetimi güçlendirildi.
* USB çıkarılabilir sürücü algılama güvenilirliği artırıldı.

### English

* Reliability improved when Windows brightness keys are used while a media application is active.
* Audio and media handling was strengthened so disappearing devices no longer close MiaDock unexpectedly.
* USB removable-drive detection became more reliable.

## MiaDock 1.4.0

### Türkçe

* Dock geçişleri ve olay bildirimleri yumuşatıldı.
* Gizlilik modülü, mikrofonu veya kamerayı kullanan uygulamaları gösterir.
* Caps Lock, Num Lock, Scroll Lock ve USB olayları dock'ta görünebilir.
* Ayarlar çalışır geri düğmesiyle yeniden düzenlendi.

### English

* Dock transitions and event notifications were smoothed.
* The privacy module shows apps using the microphone or camera.
* Caps Lock, Num Lock, Scroll Lock, and USB events can appear on the dock.
* Settings were reorganized with a working back button.

## MiaDock 1.3.0

### Türkçe

* Dock'un bağlı olduğu ekran kenarı için canlı mesafe ayarı ve dört köşe için bağımsız yuvarlaklık eklendi.
* Tam ekranda görünür tut, yalnız bildirim, tamamen gizle ve kenarda gizle davranışları eklendi.
* Pil ve uyku-uyanma, Bluetooth watcher ve hızlı medya oturumu değişimlerinde kararlılık iyileştirildi.
* Odak profillerini ve otomasyonlarını tek yerden tamamen durdurma seçeneği eklendi.

### English

* Live edge-spacing control and independent corner radii for the dock's attached screen edge were added.
* Keep visible, notifications only, hide completely, and hide at edge fullscreen behaviors were added.
* Reliability improved for battery and resume handling, the Bluetooth watcher, and rapid media-session changes.
* A single control to fully stop Focus profiles and automations was added.

## MiaDock 1.2.1

### Türkçe

* Compact, hover, expanded ve bildirim görünümleri ortak tasarım sistemi altında yenilendi.
* OLED Black, Neutral Frosted Glass ve Adaptive Fluent temaları; güvenli animasyon iptali ve azaltılmış hareket desteği eklendi.
* Zamanlayıcı ve medya titremesi, modül geçişleri, ses görünümü ve pencere yaşam döngüsündeki sorunlar giderildi.

### English

* Compact, hover, expanded, and notification views were unified under a common design system.
* OLED Black, Neutral Frosted Glass, and Adaptive Fluent themes were added alongside safe animation cancellation and reduced-motion support.
* Timer and media flicker, module switching, audio views, and window-lifetime issues were addressed.

## MiaDock 1.2.0

### Türkçe

* Kişiselleştirilebilir Odak profilleri; süre, haftalık program, uygulama tetikleyicisi, modül filtresi ve gizlilik kuralları eklendi.
* Windows ana ses görünümü, uygulama bazlı ses karıştırıcısı, yenilenen tray menüsü ve genişletilmiş dock tasarımı eklendi.
* Ağ hız ölçümü, Windows başlangıç görevi, yerelleştirme ve genel kararlılık iyileştirildi.

### English

* Customizable Focus profiles were added with durations, weekly schedules, application triggers, module filters, and privacy rules.
* The Windows master-volume view, per-application audio mixer, refreshed tray menu, and expanded dock design were added.
* Network speed measurement, the Windows startup task, localization, and overall stability were improved.
