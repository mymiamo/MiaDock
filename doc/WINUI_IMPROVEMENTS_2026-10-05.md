# WinUI geliştirmeleri — 5 Ekim 2026

Dal: `codex/winui-improvements`. Başlangıç: `0965f53` (MiaDock 1.6.3).
Electron denemesi sürdürülmedi; bu çalışma WinUI üzerindedir.

## Kullanım

- Ana dock'un sağ üstündeki kalem konum düzenlemesini açar.
- Taşıma simgesini sağa/sola sürükleyin. Taşıma düğmesi odaktayken sol/sağ
  okları 10 DIP kaydırır. Onay simgesi kaydeder; Esc önizlemeyi iptal eder.
- Dairesel ok seçili kenar/konum ayarına göre sıfır yatay kaydırmayı önizler.
  Onay simgesiyle varsayılan konumu kaydedin.
- Kaydırma ekran sınırlarında tutulur ve DPI ile ölçeklenir. Ekrana atanmış
  görünüm profili varsa kaydırma o profile, yoksa genel görünüme kaydedilir.
- Modül simgesinin sağ tık / Shift+F10 menüsü favorilere sabitler, kaldırır,
  öne veya arkaya taşır. Ana ekranda mevcut favorilerin kısa yolları gösterilir.
- Kişiselleştir → Görünüm: tema, dock düzeni, saat, favoriler ve Odak
  profilleri JSON ile dışa/içe aktarılır. Makineye özgü uygulama tetikleyicileri
  ve ekran eşleştirmeleri aktarılmaz. İçe aktarım öncesi onay ve yerel yedek vardır.
- Sistem → Monitör: genel görünümü adlandırılmış profil olarak kaydedin ve
  bir ekranla eşleştirin. Eşleştirilmemiş ekranlar genel görünümü kullanır.
- Destek → Tanılama: açılış, CPU, RAM ve özel bellek; boşta, medya ve
  gerçek sistem spectrum'u için ayrı özetler. Ölçülmeyen senaryo açıkça belirtilir.
- Pil göstergesi ortak XAML vektörüdür: yatay gövde, doluluk oranına göre gri
  dolgu; şarjda yeşil dolgu ve beyaz şimşek. Okuma yokken soru işareti,
  pil yokken çizgi kullanılır. Ana ekran, kompakt, pil modülü ve kenar şeridi aynı
  bileşeni kullanır.

## Otomatik doğrulama

- Release x64: 0 hata, 0 uyarı.
- Core: 410 geçti.
- WinUI: 209 geçti.
- Windows: 245 geçti, 1 mevcut ortam bağımlı test atlandı.
- Aktarımın sekiz bağlantıyla dolması, dokuzuncu bağlantının boşalan yeri
  kullanması ve sunucunun yeniden başlatılması regresyon testiyle kapsandı.
- Sağlayıcı kimlikleri 4096 ile sınırlı; süresi dolmuş kimlikler temizlenir,
  canlı sağlayıcının hız sınırı kapasite baskısıyla sıfırlanmaz.
- `User` kullanıcı adıyla kişisel/diğer yolların ve redaction işaretlerinin
  doğru maskelenmesi kapsandı; başlangıçtaki başarısız maskeleme testi düzeldi.
- Favori sırası bildirim önceliğinden ayrıdır. Paket boyutu/sürüm/null öğe
  kontrolleri, makine ayarlarını koruma, ekran profili geri dönüşü, DPI ve ekran
  sınırları, önizleme/kaydet/iptal/sıfırla ve CPU örnek sınırları kapsandı.
- Yeni metinler altı mevcut dil tablosuna eklendi. WinUI düğmesinin işlediği
  pointer olayları `handledEventsToo` ile yakalanır; hareket simgesi sürükleyebilir.
- Yüksek kontrast sistem renkleri, opak yüzeyler ve hover metin renkleri kullanır.
  Paketlenmemiş Windows'ta `HighContrastChanged` aboneliği reddedilirse uygulama
  açılışı devam eder; sistem renk değişimi bildirimi alternatif yenilemedir.

## Canlı gözlemler ve sınırlar

Son Release uygulaması açıldı, ayarlar ve tanılama sayfası görüntülendi.
CPU tüm mantıksal işlemcilere göre normalleştirilir. Beş saniyede bir örnek
alınır; her senaryo için en fazla 180 örnek saklanır. Açılış ölçümü süreç
başlangıcından dock'un gösterilmesine kadardır; ilk kurulum/çökme iletişim
kutusunda geçen süreyi de içerebilir.

Ayarlar açık ve kullanıcı etkileşimleri varken kısa canlı medya gözlemi:

| Ölçüm | İlk gözlem | Sonraki gözlem |
|---|---:|---:|
| Dock hazır | 5217 ms | 5217 ms |
| Ortalama CPU | %0,81 | %1,22 |
| Tepe CPU | %1,33 | %3,86 |
| RAM (çalışma kümesi) | 265,6 MB | 282,7 MB |
| Özel bellek | 168,9 MB | 180,0 MB |
| Medya örnek sayısı | 11 | 26 |

Bunlar tanılama ekranından okunmuş yuvarlanmış kısa oturum değerleridir;
karşılaştırmalı, kararlı veya uzun süreli performans sonucu değildir.
Performans kayıt seçicisi açıldı ve iptal edildi; dosya kaydetme tamamlanmadı.
Spectrum kısa ölçüm için açıldı. Kullanıcı fiziksel Esc ile Computer Use'u
durdurdu; spectrum ölçümü ve önceki kapalı tercihe UI üzerinden dönüş
tamamlanamadı. Geliştirme uygulamasında spectrum açık olabilir.

| Gerçek cihaz senaryosu | Durum |
|---|---|
| Ayarlar/tanılama ve canlı medya sayacı | Görüntülendi |
| Canlı konum sürükleme, yeniden açılışta kayıt, reset | Kullanıcı Esc ile otomasyonu durdurdu; tam kabul bekliyor |
| Pilin şarj/boşalma görsel karşılaştırması | Donanım durumu değişimiyle doğrulama bekliyor |
| Boşta ve spectrum için kararlı karşılaştırma | Tamamlanmadı |
| Uyku/uyanma, monitör çıkarma, gerçek DPI değişimi | Fiziksel cihaz doğrulaması bekliyor |
| Tam ekran oyun, hızlı gerçek medya geçişleri | Gerçek uygulamalarla doğrulama bekliyor |
| Ekran okuyucu, yüksek kontrast ve tüm temalar | Tam manuel matris bekliyor |
| 30 dakika yoğun kullanım / 8 saat boşta | Yapılmadı |

Başarılı otomatik testler bu manuel kabul maddelerini tamamlanmış saymaz.
Çalışma bir Store yayını veya ana dal birleştirmesi değildir.

## Geri dönüş

Hata düzeltmeleri ve çekirdek özellikler ayrı commit'lerdir. `master` başlangıç
durumunda bırakıldı. Yıkıcı reset gerekmez; geliştirme uygulamasını kapatıp
önceki sürümü kullanabilirsiniz. Canlı kontrol öncesi yerel ayarlar depo dışında
`MiaDock-Backups/settings-before-winui-improvements-20261005.json` olarak saklandı.
İçe aktarım ayrıca ayar klasöründe tam `settings-before-personalization-import.json`
ve taşınabilir `personalization-before-import.json` kopyaları oluşturur.

Erişilebilirlik temel kaynağı: [Microsoft Windows uygulama erişilebilirliği](https://learn.microsoft.com/en-us/windows/apps/develop/accessibility).
