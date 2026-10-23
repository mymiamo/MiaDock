# MiaDock 1.7.1.0 — reklamlı sürümün ayrı teslimi

21 Ekim 2026 incelemesinde kurulu Store 1.7.0.0 sürümünün App ve Core
assembly'lerinde reklam bileşeninin bulunmadığı doğrulandı. Yereldeki
`artifacts/release/1.7.0.0/MiaDock-1.7.0.0-x64.msixupload` reklam kodunu
içeriyor; fakat `resources.pri` derlenmiş kaynak haritasında
`MiamoAdsSlider.xbf` eksik. Önceki kontrol yalnız DLL varlığını doğruladığı
için reklam arayüzünün paketlenmediğini kaçırmıştı. Ayrıca aynı 1.7.0.0
numarasıyla önceden reklamsız teslim de üretilmişti.
Partner Center'a hangi arşivin yüklendiği bu oturumda gözlenmedi. Dolayısıyla
hatanın kesin olarak Store'a mı, dosya seçimine mi ait olduğu söylenemez.
Kurulu dosyaların kanıtı `artifacts/validation/1.7.1.0/installed-before.json`.

Karışıklığı gidermek için güncel reklamlı kaynak **1.7.1.0** olarak paketlenir.
Eski paketler korunur; yeni teslim kendi sürüm klasöründedir:

`artifacts/release/1.7.1.0/MiaDock-1.7.1.0-x64.msixupload`

Paketleme kontrolü artık arşivin içindeki `MiamoAdsSlider` / `MiamoAdsClient`
uygulamalarını, derlenmiş reklam XAML kaynağını, SettingsWindow kaynağını ve
Ionicons lisansını zorunlu tutar. Kaynak ağaçta dosya bulunması tek başına
teslimin reklamlı olduğunu kanıtlamaz. Son SHA-256 ve kaynak commit'i teslim
klasöründeki `store-package-summary.json` ve `package-verification.json`
dosyalarındadır.

## Kontrol ve yayın durumu

Reklam bileşeni, görünür süre/tekilleştirme ve ağ hatası davranışları
`MIAMO_ADS.md` raporunda açıklanır. Güncel otomatik testler ve paket
doğrulama kayıtları teslim klasörüne eklenir. Native fixture gerçek WinUI
kullanır; istemcileri sahtedir. Canlı API listesi veya görseline GET göndermek
gerçek masaüstü gösterimi veya gösterim POST'u olarak sayılmaz.

Kurulu Store uygulaması değiştirilmez; canlı reklam görünürlüğü bu oturumda
kurulu uygulama üzerinde gözlenmiş sayılmaz. WACK yönetici oturumu gerektirir;
cihaz kabulündeki 30 dakika yoğun kullanım, 8 saat boşta, fiziksel DPI,
uyku/uyanma, monitör ve paket yaşam döngüsü kontrolleri tamamlanmış sayılmaz.
Yeni sürüm Partner Center'a yüklenmiş veya yayımlanmış değildir.

## Mağaza metni

Türkçe: Ayarlar menüsüne Sponsorlu etiketli Miamo reklam alanı eklendi.
Reklamlar mevcut panel genişliğine sığar ve görseller kırpılmadan gösterilir.
Menü daraltıldığında veya pencere etkin olmadığında gösterim süresi durur.
Bağlantı sorunu yaşandığında MiaDock ve ayarlar menüsü kullanılabilir kalır.

English: Added a labeled Miamo sponsored carousel in Settings. Ads fit the
existing navigation pane and images retain their aspect ratio. Exposure
pauses when the pane is collapsed or the window is inactive. MiaDock and
Settings remain available during connection failures.

## Gönderim

Kalan kabul kontrollerinden sonra Partner Center'daki MiaDock güncellemesine
yalnız 1.7.1.0 arşivini yükleyin; eski 1.7.0.0 arşivini kullanmayın. Reklam
beyanı ve gizlilik bilgileri yeni davranışla tutarlı olmalıdır. Sertifikasyon
ve genel yayın durumu ayrıca kontrol edilir. İç MSIX'i Store'a ayrıca
yüklemeyin; WACK girdisi olarak kullanın.

Geri dönüş: bu sürüm commit'i `git revert` ile geri alınabilir. Bu işlem
Store'daki bir yayını otomatik olarak geri almaz.
