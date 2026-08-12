# MiaDock 1.3.0.0 sürüm notları

## Türkçe

### Yeni özellikler

* Dock ile bağlı olduğu ekran kenarı arasındaki mesafe artık 0–48 DIP arasında ayarlanabilir.
* Dock’un sol üst, sağ üst, sağ alt ve sol alt köşeleri bağımsız ayarlanabilir veya birlikte değiştirilebilir.
* Köşe yuvarlaklığı 0–48 DIP aralığında hem kaydırıcı hem sayı girişiyle ayarlanır ve değişiklik dock’a anında yansır.
* Tam ekran uygulamalarda dört davranış seçilebilir: tamamen gizle, yalnız bildirimlerde göster, ekran kenarında gizle ve görünür tut.
* Odak profilleri ve otomasyonları tek bir ana ayarla tamamen devre dışı bırakılabilir. Oluşturulmuş profiller korunur.

### Kararlılık iyileştirmeleri

* Canva ve diğer WebView2 tabanlı uygulamalarda hızla değişen medya içeriklerinin eski Windows medya oturumuna erişmesine yol açan yarış koşulları giderildi.
* Medya metadata, oynatma, zaman çizelgesi ve kapak görseli okumaları oturum başına sıralanır; eski işler oturum değişiminde iptal edilir.
* Pil bilgisi geçici olarak okunamadığında son geçerli değer korunur ve uyku/uyanma sonrasında sınırlı yeniden deneme yapılır.
* Bluetooth radyosu kapanınca eski bağlı cihaz bilgileri temizlenir; hızlı aç/kapat olayları eski watcher sonucunu geri getiremez.
* Ses oturumları eklenip kaldırılırken Core Audio nesnelerinin callback sırasında zorla serbest bırakılmasına son verildi.
* Zamanlayıcı alarmı tekrar oynatma ve kapatma işlemleri yerel medya callback’i tamamlandıktan sonra güvenli biçimde yürütülür.
* Tanılama ZIP’i artık uygulama/Windows ortamını, olay özetini ve son olay zaman çizelgesini kişisel medya bilgisi kaydetmeden içerir.
* Windows’un uygulamayı çağırdığı bütün noktalar (pencere yordamları, fare kancası, tam ekran ve ön plan kancaları) hata sızdırmayacak biçimde korundu; beklenmedik bir hata artık kayda geçer ve uygulama çalışmayı sürdürür.
* Kapanmakta olan pencerelerin iş kuyruğuna erişim engellendi; kapanış sırasında gelen sistem yayınları ve kanca çağrıları güvenle yok sayılır.

### Hata düzeltmeleri

* Güç kaynağı durumu nedeniyle dizüstü bilgisayar pilinin yanlışlıkla “pil yok” görünmesi düzeltildi.
* Tam ekran uygulama yalnız dock’un bulunduğu monitördeyken seçilen davranışın uygulanması sağlandı.
* Tam ekranda kenarda gizlenen dock’un fareyle güvenilir biçimde açılması ve ayrıldıktan sonra yeniden gizlenmesi düzeltildi.
* Sağ tık menüsü açıkken dock’un yanlışlıkla kapanması giderildi.
* Odak kapalıyken eski aktif profil etkisinin, zamanlayıcıların veya uygulama/tam ekran otomasyonlarının çalışması engellendi.
* Ayarlardan seçilen köşe yuvarlaklığının çalışan dock’a uygulanmaması düzeltildi; bağlı modda dört köşe birlikte güncellenir.
* Yuvarlatılmış köşelerdeki tırtıklı görünüm giderildi; köşeler artık kenar yumuşatmalı çizilir.
* Dock’un çevresinde beliren siyah dikdörtgen kaldırıldı.
* Uygulamanın “Sistem, bu uygulamada yığın tabanlı bir arabelleğin taştığını algıladı” sistem hatasıyla aniden kapanması giderildi.

### Ayar migrasyonu

* Genel ayar şeması 18’den 19’a yükseltildi.
* Eski tek köşe yarıçapı dört köşeye güvenli biçimde aktarılır.
* Eski tam ekran açık/kapalı ayarı en yakın yeni davranışa dönüştürülür.
* Eski kullanıcılar için Odak özellikleri varsayılan olarak açık kalır.
* Bozuk mesafe, köşe ve enum değerleri güvenli aralıklara normalize edilir.

### Erişilebilirlik

* Yeni sayısal dock kontrollerine ekran okuyucu adları ve yardım metinleri eklendi.
* Tam ekran seçenekleri ve Odak anahtarı açıklamalı, klavye ile erişilebilir Fluent kontroller kullanır.
* Türkçe ve İngilizce ayar araması yeni seçenekleri kapsar.

### Teknik iyileştirmeler

* Medya oturumlarında generation/lease tabanlı cancellation ve aynı oturumda tekil native erişim hattı eklendi.
* Pil ve Bluetooth servislerine geç callback reddi, idempotent abonelik yönetimi ve dispose korumaları eklendi.
* Tam ekran kenar algılama düşük frekanslı ve yalnız gerektiğinde çalışan bir mekanizmaya taşındı.
* Dock silüeti artık 1 bitlik pencere bölgesi maskesi yerine kenar yumuşatmalı backdrop öğesiyle çizilir; XAML kırpma, tıklama alanı ve görsel köşeler aynı geometriyi paylaşır.
* Arayüz metinleri `Strings\tr-TR` ve `Strings\en-US` altındaki `.resw` dosyalarına taşındı. Yeni bir dil eklemek için klasörü kopyalayıp çevirmek yeterlidir; uygulama dilleri kaynak adlarından kendisi keşfeder.
* Depodaki tüm metin dosyaları UTF-8’e normalize edildi ve kodlamayı denetleyen bir bakım betiği eklendi.
* Regresyon testi sayısı 600’den 630’a çıktı; yeni testler dil tablolarının bütünlüğünü ve native geri çağrımların korunmasını doğrular.

### Bilinen sınırlamalar

* Gerçek Canva/WebView2, farklı Bluetooth adaptörleri, pil donanımı ve uyku/uyanma senaryoları cihaz üzerinde manuel doğrulama gerektirir.
* Uzun süreli boşta çalışma, WACK ve Microsoft Store özel flight doğrulaması yayın sürecinin ayrı kapılarıdır.
* Köşe kenar yumuşatmasının ve dock çevresindeki saydamlığın son hâli farklı tema, ölçek ve duvar kâğıtlarında gözle doğrulanmalıdır.

## English

### New features

* The gap between the dock and its attached screen edge can now be adjusted from 0–48 DIPs.
* Top-left, top-right, bottom-right, and bottom-left dock corners can be adjusted independently or linked together.
* Corner rounding is set from 0–48 DIPs with either a slider or a numeric box, and the dock updates as you change it.
* Four fullscreen behaviors are available: hide completely, show notifications only, hide at the screen edge, and keep visible.
* Focus profiles and automations can be disabled globally with one master switch while preserving created profiles.

### Stability improvements

* Fixed lifecycle races where rapidly changing media in Canva and other WebView2-based apps could leave work running against an obsolete Windows media session.
* Media metadata, playback, timeline, and artwork reads are serialized per session; obsolete work is cancelled when the session changes.
* The last valid battery reading is retained during transient failures, with bounded retries after resume.
* Turning Bluetooth off clears cached connected devices, and rapid radio transitions cannot publish an obsolete watcher result.
* Core Audio objects are no longer force-released while audio-session callbacks may still be unwinding.
* Timer alarm replay and cleanup now run safely after the native media callback returns.
* Diagnostic ZIP exports now include app/Windows environment details, an event summary, and a recent timeline without storing personal media details.
* Every point where Windows calls into the app now contains its own failures, including window procedures, the pointer hook, and the fullscreen and foreground hooks; an unexpected error is recorded and the app keeps running.
* Closing windows are no longer asked for their dispatcher queue, so system broadcasts and hook callbacks that arrive during shutdown are ignored safely.

### Bug fixes

* Fixed laptop batteries being reported as absent based only on power-supply status.
* Fullscreen policy now applies only when the fullscreen window is on the dock’s monitor.
* Improved pointer reveal and re-hide behavior for the edge-hidden fullscreen dock.
* Prevented the dock from collapsing while its context menu is open.
* Disabled Focus now prevents previous profile effects, timers, and application/fullscreen automations from running.
* Fixed the corner rounding chosen in Settings not reaching the running dock; linked mode now updates all four corners together.
* Fixed the staircase edges on rounded corners, which are now drawn anti-aliased.
* Removed the black rectangle that could appear around the dock.
* Fixed the abrupt shutdown reported by Windows as “a stack-based buffer overrun was detected in this application”.

### Settings migration

* The main settings schema moves from 18 to 19.
* The legacy single corner radius is safely copied to all four corners.
* The legacy fullscreen on/off setting is mapped to the closest new behavior.
* Focus remains enabled by default for existing users.
* Invalid margin, corner, and enum values are normalized to safe values.

### Accessibility

* Screen-reader names and help text were added to the new dock geometry controls.
* Fullscreen choices and the Focus master switch use described, keyboard-accessible Fluent controls.
* Turkish and English settings search now covers the new options.

### Technical improvements

* Added generation/lease cancellation and a single native-access lane per media session.
* Added stale-callback rejection, idempotent subscriptions, and disposal guards to battery and Bluetooth services.
* Fullscreen edge detection now uses a low-frequency mechanism that runs only when needed.
* The dock silhouette is drawn by an anti-aliased backdrop element instead of a 1-bit window region mask, so XAML clipping, the click area, and the visible corners share one geometry.
* UI strings moved into `.resw` tables under `Strings\tr-TR` and `Strings\en-US`. Adding a language means copying a folder and translating it; the app discovers cultures from the resource names.
* All repository text files were normalized to UTF-8, with a maintenance script that verifies the encoding.
* The regression suite grew from 600 to 630 tests, covering string-table integrity and the guarded native callbacks.

### Known limitations

* Real Canva/WebView2, Bluetooth-adapter, battery-hardware, and sleep/resume scenarios still require manual device verification.
* Long idle soak, WACK, and Microsoft Store private-flight validation remain separate release gates.
* Corner anti-aliasing and the transparency around the dock still need a visual pass across themes, display scales, and wallpapers.
