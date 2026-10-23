# Dock ikonlarında bulanıklık düzeltmesi — 15 Ekim 2026

`DockIcon`, FontIcon yerine ImageIcon kullandığı için bir Grid/Border içinde
varsayılan Stretch hizalamasıyla büyüyordu. Örneğin 17 DIP Odak ikonu, 48 DIP
çemberin kenarlık içindeki 46 DIP alanına geriliyordu; SVG kaynağı ise 17
boyutunda rasterleştiriliyordu. Bu, hem gereğinden büyük hem bulanık simgelere
yol açıyordu. Önceki katalog önizlemesi StackPanel içinde çalıştığından bu
gerilmeyi yakalamıyordu.

Kontrol artık varsayılan olarak iki yönde ortalanıyor ve düzen boyutunu koruyor.
Boyut değişimleri de izleniyor; açıkça büyük boyut/Stretch istendiğinde kaynak
son yerleşim boyutunda yeniden hazırlanıyor. Düğmelerin veya kartların tıklama
alanı değişmiyor. Yerel SVG kaynakları ve ikon paketi aynı.

NativePreview'a gerçek dock kutularını temsil eden regresyon örnekleri eklendi.
Düzeltmeden önce kontrol çıkış kodu 1, düzeltmeden sonra 0 oldu:

| Örnek | Beklenen DIP | Önceki boyut | Sonraki boyut / raster bütçesi (%100) |
| --- | ---: | ---: | ---: |
| Odak | 17 | 46 | 17 / 17 |
| Ağ | 22 | 54 | 22 / 22 |
| İndirme/yükleme satırı | 16 | 30 | 16 / 16 |
| Güç | 22 | 42 | 22 / 22 |
| Gizlilik | 24 | 54 | 24 / 24 |
| Arama | 28 | 70 | 28 / 28 |
| Açıkça Stretch istenen ikon | 56 | 56, kaynak 16 | 56 / 56 |

146/146 katalog kaynağı ve 7/7 yerleşim kontrolü geçti. WinUI testleri yeniden
çalıştırıldı: 216 geçti, başarısızlık yok. Kanıtlar `artifacts/icons-blur-before.json`,
`icons-blur-after.json`, `tests-icons-blur-winui.log` ve ilgili TRX dosyasındadır.
Fiziksel %150/%200 DPI veya ekran okuyucu kabulü bu ölçümden çıkarılmamalıdır.

Yerel kurulum için `scripts/release/Build-MiaDockLocalPackage.ps1` eklendi.
Bilgisayardaki paket 1.6.4.0 olduğundan test paketi 1.6.4.1 olarak hazırlanır;
Store kaynak manifesti ve normal proje sürümü değiştirilmez. Paket aynı uygulama
kimliğini kullanır ve .NET/Windows App SDK çalışma zamanlarını içerir. Ayrı
çıktı klasörü, çalışan geliştirme sürümünün dosyalarının üzerine yazılmasını önler.

İmza için mevcut kullanıcıya ait, dışarı aktarılamayan özel anahtarlı bir test
sertifikası oluşturulur/yeniden kullanılır. Paket yanında yalnız herkese açık
`.cer` dosyası verilir. Paket oluşturma sertifikayı güvenilen depoya eklemez veya
uygulamayı kurmaz; bunlar kullanıcının kurulum adımlarıdır. Bu bir Store yayını
değildir. Paket özeti sürüm, SHA-256, imza sertifikası ve derleme logunu kaydeder.

İlgili WinUI davranışları: [varsayılan Stretch hizalaması](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.frameworkelement.horizontalalignment),
[SVG raster boyutu](https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/microsoft.ui.xaml.media.imaging.svgimagesource.rasterizepixelwidth).

## Oluşturulan kurulum paketi

- Konum: `artifacts/installers/MiaDock_1.6.4.1_x64/`.
- Dosya: `MiaDock.App_1.6.4.1_x64.msix` (112.077.394 bayt; yaklaşık 107 MB).
- Sürüm/mimari: 1.6.4.1 / x64. Mevcut paket kimliği ve yayıncı adı korundu.
- SHA-256: `73C8A0EC92C94519F70F4F732B97E3FFF8E8EA04CECC5E358C1E9EFBD3F76421`.
- Manifest, UI assembly, çalışma zamanı dosyaları ve Ionicons lisansı pakette
  doğrulandı. CMS imzasının kriptografik doğrulaması başarılı; teslim kopyasının
  hash'i de aynı.
- Test sertifikası henüz güvenilen depoda olmadığından Windows imza zincirini
  güvenilmeyen kök olarak bildirir. Bu beklenen ilk kurulum durumudur; imza
  güveninin sağlandığı iddia edilmez. `.cer` ve `KURULUM.txt` aynı klasördedir.
- Uygulama ve sertifika kurulmadı; kullanıcıya açık MiaDock sürümlerini kapatıp
  kurulum yönergesini izlemesi için dosyalar teslim edildi.

Paketleme logu `artifacts/local-package-work/1.6.4.1/20261015-092818/build.log`
altındadır. Normal proje sürümüne dokunmadan yerel manifest kullanan paketleme
tamamlandı. Windows App SDK'nın self-contained ayarı yalnız çalıştırılabilir
projede bırakılır; sınıf kütüphanelerine global olarak uygulanmaz.
