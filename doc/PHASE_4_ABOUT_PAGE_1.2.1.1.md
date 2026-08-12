# MiaDock 1.2.1.1 — Faz 4 Hakkında Sayfası

## Sonuç

Faz 4 tamamlandı. Hakkında sayfasına Fluent bağlantı kartları, güvenli harici bağlantı açma katmanı, kullanıcı dostu hata bildirimi, Türkçe/İngilizce metinler, arama anahtarları ve erişilebilirlik adları eklendi.

Faz 6'ya kadar paket ve uygulama sürümü değiştirilmedi. Bu nedenle çalışma zamanı doğrulamasında mevcut `1.2.1.0` sürüm metninin görünmesi beklenen durumdur.

## Uygulanan değişiklikler

### Bağlantı kartları

Hakkında sayfasına ortak `SettingsLinkCardButtonStyle` kullanan üç kart eklendi:

- GitHub: `https://github.com/mymiamo/MiaDock`
- Instagram: `https://www.instagram.com/mymiamonet/`
- Web sitesi: `https://mymiamo.net`

Kartlar başlık, açıklama, görüntülenen alan adı, ayırt edici simge ve harici bağlantı işareti içeriyor. Her kartın varsayılan tarayıcıda açılacağını belirten bağımsız ekran okuyucu adı bulunuyor.

### Güvenli bağlantı açma

- Uygulama katmanı platformdan bağımsız `IExternalUriLauncher` sözleşmesini kullanıyor.
- Windows uygulaması bağlantıları `Windows.System.Launcher.LaunchUriAsync` ile açıyor.
- Yalnızca önceden tanımlanan üç mutlak HTTPS adresi kabul ediliyor.
- Hatalar uygulamayı kapatmıyor; teknik ayrıntı yerel loga yazılıyor ve kullanıcıya sayfa içi `InfoBar` gösteriliyor.
- Hata mesajı bağlantıyı kopyalayıp tarayıcıda açma seçeneğini belirtiyor.
- Devam eden açma işlemi sırasında aynı kart devre dışı bırakılarak yinelenen hızlı tıklamalar engelleniyor.
- İptal işlemi korunuyor; diğer beklenmeyen hatalar sayfa olay işleyicisinden dışarı taşmıyor.

### Yerelleştirme, arama ve erişilebilirlik

- Kart başlıkları, açıklamaları, hata bildirimi ve erişilebilirlik adları Türkçe ve İngilizce olarak tanımlandı.
- Hakkında aramasına GitHub, repository/repo, source code/kaynak kod, Instagram, social media/sosyal medya, website/web sitesi ve MiaMo alan adı anahtarları eklendi.
- Hata `InfoBar` denetimi ekran okuyucular için `Assertive` canlı bölge kullanıyor.
- Ayar şeması değişmedi ve `18` olarak kaldı.

## Test kapsamı

- Windows URI adaptörü için başarılı açma, işletim sistemi reddi, istemci istisnası ve HTTP adresi reddi test edildi.
- WinUI sözleşme testleri üç kartın sayısını, kesin URL'leri, ortak stili, hata `InfoBar`ını ve arama anahtarlarını doğruluyor.
- Erişilebilirlik testleri üç kartın da boş olmayan `AutomationProperties.Name` değerine sahip olduğunu doğruluyor.
- Türkçe/İngilizce yerelleştirme eşlemeleri test kapsamına alındı.

## Doğrulama

### Otomatik doğrulama

`Release x64` tam çözüm testi:

- MiaDock.Core.Tests: 270/270
- MiaDock.Platform.Windows.Tests: 116/116
- MiaDock.WinUI.Tests: 139/139
- Toplam: 525/525 başarılı

`MiaDock.App` için `Release x64 / win-x64` derlemesi 0 uyarı ve 0 hata ile tamamlandı. `git diff --check` içerik hatası vermedi; yalnızca çalışma kopyasının mevcut LF/CRLF dönüşüm uyarıları görüldü.

### Gerçek UI doğrulaması

Paketlenmemiş `Release x64` uygulaması çalıştırıldı ve Ayarlar > Hakkında sayfası gerçek WinUI penceresinde incelendi:

- Üç bağlantı kartı aynı anda görünür ve kırpılmadan yerleşti.
- Türkçe başlıklar, açıklamalar ve alan adları doğru görüntülendi.
- Erişilebilirlik ağacında üç kart bağımsız düğme ve açıklayıcı Türkçe adlarla bulundu.
- Harici tarayıcı yan etkisi oluşturmamak için kartlar çalışma zamanı testinde tıklanmadı; açma ve hata yolları otomatik testlerle doğrulandı.
- Bu oturumdaki yerel logda 7 bilgi kaydı, 0 uyarı ve 0 hata kaydı oluştu.
- Aynı zaman aralığındaki Windows Application olay günlüğünde MiaDock kaynaklı hata bulunmadı.

Doğrulama sonunda uygulama kapatıldı. Geçici başlangıç modu geri alındı; kullanıcı ayarları `Language=0`, `LaunchMode=0`, `HotKeys.IsEnabled=false`, boş kısayol bağları ve `SchemaVersion=18` durumunda bırakıldı.

## Faz sınırı

Bu faz Microsoft Store yüklemesi, sürüm numarası değişikliği veya Faz 5 stabilizasyon çalışmalarını başlatmadı.
