# MiaDock 1.2.1.1 — Faz 0 Kritik Hata Denetimi

Tarih: 2 Ağustos 2026

## Sonuç

Güncel `master` kaynak ağacında yeniden üretilebilen açık bir P0 çökme bulunmadı. Geçmiş Windows ve MiaDock kayıtlarında üç önemli hata imzası saptandı; güncel Release ikilisi üzerinde ilgili kapatma, modül yükleme ve pencere yaşam döngüsü yolları yeniden sınandı ve hata tekrarlanmadı.

Faz 1'e geçişi engelleyen açık bir kritik hata yoktur. Aşağıdaki eski çökme imzaları, Faz 5 uzun süreli doğrulamasında yeniden izlenmelidir.

## Başlangıç doğrulaması

- Dal: `master`
- Başlangıç commit'i: `a3c91d5` (`Prepare MiaDock 1.2.1 release`)
- Başlangıç çalışma ağacı: temiz
- Release x64 testleri: 499 / 499 başarılı
  - Core: 258
  - Windows platform: 108
  - WinUI: 133
- Release x64 uygulama derlemesi: başarılı, 0 uyarı, 0 hata

## Log ve Olay Görüntüleyicisi bulguları

Son 30 günlük Windows Application kayıtlarında MiaDock ile ilişkili 42 kayıt bulundu:

- 21 adet `.NET Runtime` 1026 yakalanmamış hata kaydı
  - 19 adet `WindowsMediaAudioMeterService.CleanupAudio()` kaynaklı `InvalidComObjectException`
  - 2 adet bildirim servisi kapanış/abonelik temizliği kaynaklı hata
- 6 adet `.NET Runtime` 1023 iç çalışma zamanı hatası (`0x80131506`)
- 15 adet yukarıdaki çökmelere eşlik eden `Application Error` 1000 kaydı

En yeni tarihsel çökme 2 Ağustos 2026 14:18:51'de kurulu Store 1.2.0 üzerinde, medya ses ölçer kapanışında oluştu. Güncel kaynakta `CleanupAudio()` ve işçi sonlandırması `InvalidComObjectException`/COM ayrılması için korunuyor. Güncel Release üzerinde üç temiz tam çıkıştan sonra yeni `.NET Runtime`, `Application Error` veya WER kaydı oluşmadı.

Yerel MiaDock NDJSON loglarında beş `dock.module-navigation-failed` kaydı bulundu. Tümü `TimerExpandedView.InitializeComponent()` içinde `XamlParseException` ve mevcut commit'ten önceki oturumlara aitti. Güncel Release'te dock genişletilip sekiz ardışık modül geçişi çalıştırıldı; zaman görünümü dâhil döngü tamamlandı ve yeni hata üretilmedi.

## Uygulanan stres ve yaşam döngüsü senaryoları

- Ayar penceresini kapatma, tepsiye küçültme ve yeniden açma
- Ayar sayfaları arasında hızlı geçiş
- Temayı Apple benzeri → Adaptive Fluent → Apple benzeri değiştirme
- Compact, hover ve expanded önizlemeleri arasında geçiş
- Animasyon denemesini art arda tetikleyerek iptal/yeniden başlatma yolunu zorlama
- Geçici global kısayollarla gerçek dock'u genişletme ve sekiz ardışık modül geçişi
- Geçici kısayolları temizleme ve global kısayolları tekrar kapatma
- Üç temiz uygulama çıkışı; ses ve bildirim servislerinin kapanışını doğrulama
- 10 saniyelik ölçekli yoğun olay soak testi

Geçici kısayol testi sonunda ayar durumu geri yüklendi:

- `HotKeys.IsEnabled = false`
- Kalıcı kısayol bağı yok
- Tema yeniden Apple benzeri

## Çalışma zamanı ölçümleri

### Aktif etkileşim — 60 saniye

- Ortalama CPU: %4,792
- Çalışma kümesi artışı: 33,48 MB
- Yanıt vermeyen örnek: 0
- Kullanılan aktif stres sınırları: %5 ortalama CPU, 40 MB çalışma kümesi artışı

### Tepside boşta — 60 saniye

- Ortalama CPU: %0,039
- Çalışma kümesi değişimi: -0,22 MB
- Yanıt vermeyen örnek: 0
- Varsayılan sınırlar: %1 ortalama CPU, 20 MB çalışma kümesi artışı

### Ölçekli yoğun olay testi

- Süre: 10 saniye
- Sonuç: 2 / 2 başarılı
- Not: Bu çalışma, Faz 5'teki tam 30 dakikalık yoğun etkileşim testinin yerine geçmez.

## Önceliklendirme

### P0 — Açık kritik hata

Yok.

### P1 — Faz 5'te yeniden doğrulanacak tarihsel riskler

1. Medya ses ölçer COM nesnesinin kapanış sırasında ayrılması
   - Güncel koruma ve üç temiz çıkışla yeniden üretilemedi.
   - Uyku/uyanma, ses aygıtı değişimi ve uzun medya oturumu sonrasında tekrar sınanmalı.
2. `.NET Runtime` iç hata `0x80131506`
   - Eski kayıtlarda yönetilen stack bulunmuyor.
   - Tekrar ederse WER dump alınarak native/WinRT çağrı zinciri ayrıştırılmalı.
3. Bildirim dinleyicisi abonelik temizliği
   - Güncel UI-thread abonelik temizliği ve tam çıkışlarda yeniden üretilemedi.
   - Paketli Store ikilisiyle tekrar doğrulanmalı.

### Kapatılmış tarihsel hata

- `TimerExpandedView` XAML yükleme hatası
  - Güncel ToggleButton uyumlu stil ve yeni görünüm yapısıyla yeniden üretilemedi.
  - Gerçek dock modül döngüsünde yeni `dock.module-navigation-failed` kaydı oluşmadı.

## Faz 0 çıkış kararı

Faz 0 tamamlandı. Güncel Release kaynağında kod düzeltmesi gerektiren yeniden üretilebilir kritik hata bulunmadı. Üretilen ölçüm ve test artefaktları `artifacts/validation/1.2.1.1/` altında tutulur ve Git'e eklenmez.

Faz 1 başlamadan önce önerilen kontrol noktası: bu raporun onaylanması ve tarihsel COM kapanış riskinin Faz 5 doğrulama listesinde korunması.
