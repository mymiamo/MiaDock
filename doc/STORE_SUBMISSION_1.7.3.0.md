# MiaDock 1.7.3.0 — reklamlı Store adayı

Kullanıcı, Microsoft'a en son 1.7.1 ve muhtemelen 1.7.2 yüklendiğini bildirdi;
yeni teslim sürümünü açıkça **1.7.3** olarak istedi. Uygulama, assembly,
manifestler, sürüm testleri ve paketleme kontrolleri **1.7.3.0** olarak
güncellendi. Partner Center'daki mevcut sürümler bu oturumda gözlenmedi.

Teslim: `artifacts/release/1.7.3.0/MiaDock-1.7.3.0-x64.msixupload`.

Bu değişiklik sürüm numarası ve teslim kaydıyla sınırlıdır. Önceki adayın
reklam kodu ve paketleme düzeltmesi korunur: her Store derlemesi ayrı, temiz
ara çıktı dizininde yapılır; arşivdeki App/Core DLL'leri ve `resources.pri`
içindeki `MiamoAdsSlider.xbf` doğrulanmadan teslim üretilmez. Önceki arşivler
korunur; yeni teslim için yalnız 1.7.3.0 dosyasını kullanın.

Sürüm kontrollerini içeren WinUI testleri yeniden çalıştırılır. Değişmeyen
Core/Windows test takımlarının önceki başarılı kayıtları yeniden kullanılır;
ayrım `artifacts/validation/1.7.3.0/test-provenance.json` içindedir. Önceki
34 native reklam kontrolü, değişmeyen bileşen koduna aittir; yeni sürümde
yeniden çalıştırılmış gibi sunulmaz.

Paket kimliği, sürüm, assembly eşleşmesi, reklamın derlenmiş arayüz kaynağı,
semboller ve SHA-256 son arşiv üzerinde yeniden kontrol edilir. Kaynak commit'i
ve sonuçlar teslim klasöründeki `store-package-summary.json` ve
`package-verification.json` dosyalarındadır.

Yeni arşiv Microsoft'a yüklenmiş veya yayımlanmış değildir. Önceden açık
kalan WACK, gerçek cihaz ve paket yaşam döngüsü kabulü bu sürüm numarası
değişikliğiyle tamamlanmış olmaz. Kurulu uygulama değiştirilmez.

## Mağaza metni

Türkçe: Ayarlar menüsüne Sponsorlu etiketli Miamo reklam alanı eklendi.
Görseller mevcut panel genişliğine kırpılmadan sığar. Menü daraltıldığında
veya pencere etkin olmadığında gösterim süresi durur. Reklam arayüzünün
Store paketine eksiksiz alınmasını sağlayan paketleme düzeltmesi yapıldı.

English: Added a labeled Miamo sponsored carousel in Settings. Images fit
the existing navigation pane without cropping. Exposure pauses when the
pane is collapsed or the window is inactive. Fixed packaging to include
the compiled advertising interface in the Store package.
