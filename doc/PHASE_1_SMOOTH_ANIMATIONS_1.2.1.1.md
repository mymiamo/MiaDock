# MiaDock 1.2.1.1 — Faz 1 Pürüzsüz Animasyonlar

Tarih: 2 Ağustos 2026

## Sonuç

Faz 1 tamamlandı. Durum, pencere ölçüsü ve içerik animasyonları tek geçiş oturumu altında birleştirildi. Yeni bir hareket isteği önceki oturumu iptal ediyor, Composition görsellerini güvenli son değerlere getiriyor ve eski async tamamlanmaların yeni animasyonu sıfırlamasını engelliyor.

## Uygulanan değişiklikler

- Gövde ve içerik için ayrı iptal belirteçleri kaldırıldı; tek `_transitionCancellation` ve monoton `_transitionSequence` kullanılıyor.
- Modül değişimi ile modüle bağlı genişletilmiş yükseklik değişimi `RequestModuleTransition` üzerinden birlikte başlatılıyor.
- Compact, hover, expanded ve notification durum geçişlerinde opacity ve scale'e Composition translation eklendi.
- İptal edilmiş Composition batch tamamlanmasının yeni görsel durumu sıfırlaması engellendi.
- İçerik animasyonunun son görsel reset'i yalnız güncel geçiş oturumunun koordinatörü tarafından yapılıyor.
- Windows “Animasyonları azalt” tercihi ve `MotionPreset.Off` tüm geçiş türleri için tek `ShouldAnimate` korumasında tutuluyor.
- Boyut animatöründe 0,25 DIP altındaki tekrar kareleri elendi ve hedef metriklerin son karede iki kez uygulanması kaldırıldı.
- RoundedRectangle Composition geometry her karede yeniden oluşturulmak yerine `IslandShell` içinde önbelleğe alındı.
- Aynı metriklerin tekrar uygulanması engellendi; yalnız boyut gerçekten değiştiğinde XAML Width/Height güncelleniyor.
- Modül görünüm önbellekleri korundu. Rutin timer ve medya sunum güncellemeleri içerik hareketi veya görünüm yeniden oluşturması başlatmıyor.

## Regresyon korumaları

WinUI performans korumalarına aşağıdaki denetimler eklendi:

- tek iptal edilebilir animasyon oturumu ve stale-completion reddi;
- layout kare birleştirme ve Composition clip yeniden kullanımı;
- opacity, scale, translation ve azaltılmış hareket koruması.

## Doğrulama

- Release x64 uygulama derlemesi: başarılı, 0 uyarı, 0 hata.
- Release x64 testleri: 501 / 501 başarılı.
  - Core: 258
  - Windows platform: 108
  - WinUI: 135
- Gerçek Release dock üzerinde:
  - genişletme ve küçültme;
  - görünürlüğü kapatma ve geri açma;
  - sekiz ardışık modül geçişi;
  - zaman görünümü dâhil tüm modül yükleme yolları tamamlandı.
- 30 saniyelik çalışma zamanı izlemesi:
  - ortalama CPU: %0,030;
  - en yüksek CPU: %0,516;
  - çalışma kümesi artışı: 0,04 MB;
  - yanıt vermeyen örnek: 0.
- Test oturumunda yeni yerel hata kaydı ve yeni MiaDock çökme olayı: 0.

Çalışma zamanı ölçümü `artifacts/validation/1.2.1.1/phase1-runtime/` altında tutulur ve Git'e eklenmez.

## Test ortamı notu

MiaDock overlay penceresi no-activate/tool-window niteliğinde olduğundan Windows otomasyon yakalamasında hedeflenebilir pencere olarak listelenmedi. Global kısayol yolları gerçek Release ikilisi üzerinde doğrulandı; kare-kare görsel karşılaştırma alınmadı. Geçici test kısayolları kaldırıldı, `HotKeys.IsEnabled = false` ve kalıcı binding sayısı sıfır olarak geri yüklendi. Uygulama test sonunda durduruldu.

## Faz 1 çıkış kararı

Faz 1 kapsamı tamamlandı ve Faz 2'ye geçişi engelleyen açık hata bulunmadı. Faz 5 uzun süreli etkileşim testinde hızlı giriş iptali ve tarihsel medya COM kapanış riski yeniden izlenmelidir.
