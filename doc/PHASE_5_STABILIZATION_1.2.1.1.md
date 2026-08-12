# MiaDock 1.2.1.1 — Faz 5 Stabilizasyon Sonucu

## Durum

Faz 5 kod sertleştirmesi ve otomatik doğrulaması tamamlandı. Tam ekran sonrası dock'un geri gelmemesi P0 hatası için gerçek Windows kabul testi de geçti.

Faz 6 sürüm kapısı henüz açılmadı. İki saatlik gerçek oyun/medya oturumu, 30 dakikalık yoğun etkileşim, çoklu monitör/HDR/uyku-uyanma ve gerçek DirectX oyun matrisi zaman gerektiren manuel kapılar olarak bekliyor. Sanal iki saat testi bu kapıların yerine geçmiş sayılmadı.

## Uygulanan sertleştirmeler

### Tam ekran yaşam döngüsü

- Ön plan ve konum olaylarına destroy, show/hide ve minimize başlangıç/bitiş sinyalleri eklendi.
- Tam ekrandaki 500 ms kurtarma ölçümü korundu; ölçüm arka planda çalışıyor ve UI kuyruğuna yalnız gerçek durum değişiminde iş gönderiyor.
- Eşzamanlı olay/timer yenilemeleri tek çalışan işte birleştirildi; üst üste ölçüm ve DispatcherQueue birikimi engellendi.
- UI dispatcher geçici olarak işi reddederse durum kaybolmadan 100 ms sonra yeniden deneniyor.
- Direct3D bildirim sinyali, onu ilk gözleyen ön plan HWND'sine bağlandı. Kapanan tam ekran pencerenin gecikmiş sinyali yeni ön plan penceresine taşınmıyor.
- Değişmeyen tam ekran durumu yinelenen `StateChanged` veya bilgi logu üretmiyor.
- Algılama hataları dakikada en fazla bir kez loglanıyor; kurtarma ölçümü devam ediyor.
- Dispose sırası timer ve WinEvent callback'lerinin kapanmış servise iş göndermesini engelliyor.
- Durum loglarına yalnız `isFullscreen`, teknik neden ve yenileme kaynağı yazılıyor; pencere başlığı, uygulama yolu ve kullanıcı içeriği yazılmıyor.

### Timer ve genel yaşam döngüsü

- Timer/kronometre kalıcılığı tek yazarlı ve birleştirilen bir kuyruğa alındı. Hızlı değişikliklerde eski durumun sonradan yeni durumu ezmesi engellendi.
- Timer durumunu kaydetme hataları çağırana kaçmıyor ve oran sınırlı teknik loga yazılıyor.
- Kapanış sırasında oluşan hata loglanıyor; `ExitOnUiThread` her durumda çalıştırılıyor.
- Bir modülün ayar uygulama hatası diğer modüllerin uygulanmasını durdurmuyor; hata modül kimliğiyle güvenli biçimde loglanıyor.
- Runtime kararlılık aracı çalışma kümesine ek olarak özel bellek, handle ve thread büyümesini ölçüyor.
- Soak aracı `fullscreen` profili ve `FullscreenSoak` test kategorisini destekliyor; ölçekli koşular gerçek süreli koşulardan açıkça ayrılıyor.

## Otomatik doğrulama

- Sanal iki saat tam ekran: 14.400 kurtarma ölçümü, tek durum yayını, ilk geçişten sonra sıfır UI kuyruğu işi ve yalnız iki timer örneği.
- 1.000 tam ekran giriş/çıkış çevrimi: tam 2.000 durum geçişi, timer sayısı sabit ve son durumda kurtarma kapalı.
- Kaçırılan pencere olayı, DispatcherQueue reddi, algılama hatası, eşzamanlı callback ve dispose yarışları test edildi.
- Gecikmiş Direct3D sinyalinin başka HWND'ye taşınmaması ve sinyal sıfırlanınca yeni sahibin kabul edilmesi test edildi.
- Timer yazma kuyruğunda seri kalıcılık, son durumun korunması ve hata oran sınırlaması test edildi.
- Release test sonucu: Core 272/272, Platform Windows 127/127, WinUI 141/141; toplam **540/540** başarılı.
- Release x64 uygulama derlemesi: **0 uyarı, 0 hata**.

## Gerçek Windows kabul testi

Boş Paint penceresi kullanılarak güncel Release x64 yapısında şu akışlar uygulandı:

1. MiaDock çalışırken Paint F11 ile tam ekrana alındı.
2. Tam ekran 30 saniye açık tutularak iki saniyede bir kaynak örneği alındı.
3. Tam ekrandan çıkışta dock'un yeniden görünmesi gözlemlendi.
4. Beş hızlı tam ekran giriş/çıkış çevrimi uygulandı.
5. Son çevrimde tam ekran yüzeyi `Alt+F4` ile kapatıldı.

Güncel oturumda altı gerçek tam ekran girişi altı normal-duruma dönüşle eşleşti. Kurtarma yolunun kullanıldığı çıkışlar `source=Recovery`, doğrudan pencere olayının yakaladığı çıkış `source=WindowEvent` olarak kaydedildi. Son durum `isFullscreen=False` oldu.

30 saniyelik tam ekran kaynak örneği:

- Ortalama CPU: %0,056; tepe CPU: %0,258.
- Çalışma kümesi büyümesi: +0,10 MB.
- Özel bellek büyümesi: -0,30 MB.
- Handle büyümesi: -17.
- Thread büyümesi: -6.
- Yanıt vermeyen örnek: 0.

MiaDock oturum logunda uyarı/hata yoktu. Aynı zaman aralığında Windows Application günlüğünde MiaDock'a ait Application Error, .NET Runtime veya Windows Error Reporting kaydı bulunmadı.

Kanıt dosyaları:

- `artifacts/validation/1.2.1.1/phase5-runtime/runtime-20260802-180119.json`
- `artifacts/validation/1.2.1.1/phase5-soak/soak-all-scaled.trx`
- `artifacts/validation/1.2.1.1/phase5-soak/soak-fullscreen-virtual-full-horizon.trx`

## Ayar ve temizlik doğrulaması

Test için açılan Paint ve MiaDock örnekleri kapatıldı. Ayarlar `SchemaVersion=18`, `Language=0`, `LaunchMode=0`, `HotKeys.IsEnabled=false` ve sıfır kısayol bağı ile korundu.

## Faz 6 öncesi kalan manuel kapılar

- En az iki DirectX oyunda borderless tam ekran; destekleyen bir uygulamada exclusive fullscreen.
- En az iki saat kesintisiz gerçek oyun/medya tam ekranı ve sonrasında pencereye dönüş/doğrudan kapanış.
- En az 30 dakika hızlı giriş/çıkış, Alt+Tab, bildirim ve dock etkileşimi.
- Çoklu monitör, DPI/HDR/çözünürlük değişimi, Explorer yeniden başlatma ve uyku/uyanma.
- En az 30 dakika yoğun dock etkileşimi ve iki saat gerçek boşta çalışma.

Bu kapılardan herhangi birindeki tam ekran yanlış pozitif/negatif, bir saniyeyi aşan geri dönüş, odak çalma, kaynak sayısı büyümesi veya yakalanmamış hata sürüm engelleyicidir.
