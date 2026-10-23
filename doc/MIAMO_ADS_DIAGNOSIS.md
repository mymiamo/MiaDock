# Reklam görünmeme incelemesi — 15 Ekim 2026

İki ayrı engel doğrulandı:

1. `mymiamo.net/miamo-db.php` yerel kopyasında `MIADOCK_ADS_SIGNING_SECRET`
   bulunmuyordu. `includes/miadock-ads.php` içindeki gerçek yardımcıyı PHP CLI
   ile çağırmak `MiaDock Ads signing configuration is missing or too short.`
   hatası verdi. GET uç noktası bu kontrolü veritabanından önce yapıyor ve
   hatayı HTTP 503'e çeviriyor. Yeni yayın uçları eski `X-Miamo-App-Key`
   anahtarını kullanmaz; masaüstü istemcisine gizli anahtar eklemek çözüm değildir.
2. Çalışan Release uygulamasının `MiaDock.App.dll` dosyasında `MiamoAdsSlider`
   bileşeni yok. Dosya 09:13 tarihli eski derleme. Reklamı içeren doğrulanmış
   derleme `artifacts/validation/miamo-ads/build/` altında, DLL 12:43 tarihli
   ve reklam bileşenini içeriyor. Önceki Store paketi de reklam değişikliğinden
   önce üretilmişti.

## Yerelde yapılan düzeltme

Web sitesi kökü:
`D:\Masaüstü Kısayolları\Klasörler\Web Dosyaları\2026 Web Siteleri & Kodları\mymiamo.net`

Bu klasördeki `miamo-db.php` dizisine, mevcut değerleri koruyarak, kriptografik
olarak rastgele 32 bayttan üretilen 64 karakterlik ayrı bir imzalama anahtarı
eklendi. Anahtar rapora, MiaDock kaynaklarına veya testlere konulmadı. Eski
dosyanın yedeği web kökü dışında
`C:\Users\User\.codex\backups\miamo-ads-config\miamo-db.before-20261015-101854.php`
konumunda tutuldu. Site klasörü Git deposu değil; bu yapılandırma MiaDock
commit'ine dahil değildir.

PHP sözdizimi kontrolü, gerçek yapılandırmayla token imzalama/doğrulama ve
mevcut 35 saf PHP testi geçti. Canlı veritabanına veya kampanyalara yazılmadı.

## Canlıya yansıması için kalanlar

- Sunucudaki mevcut `miamo-db.php` dizisine yerelde hazırlanan
  `MIADOCK_ADS_SIGNING_SECRET` alanını aktarın. Sunucunun mevcut DB/diğer
  ayarlarını koruyun; yerel dosyayı körlemesine üzerine yazmayın. Anahtarı
  masaüstüne taşımayın. Gerekirse PHP-FPM/OPcache önbelleğini yenileyin.
- Sunucudaki PHP hata günlüğünü ve anahtarsız GET yanıtını kontrol edin.
  15 Ekim 2026 13:19 İstanbul kontrolünde canlı uç hâlâ HTTP 503 verdi;
  kayıt `artifacts/validation/miamo-ads/diagnosis-api.json`. Canlı sunucuya
  dağıtım yapılmadığından uzaktaki hatanın tam metni görülmedi. Yerel eksik
  ayar kesin; canlıda DB veya başka bir hata da varsa sunucu günlüğü ayıracaktır.
- Eski MiaDock örneğini kapatıp reklamı içeren
  `artifacts/validation/miamo-ads/build/MiaDock.App.exe` derlemesini açın.
  Bu incelemede çalışan uygulama kapatılmadı veya değiştirilmedi.
- GET başarılı olduktan sonra uygun kampanya, görsel ve gerçek gösterim
  kaydı doğrulanmalı. Üretime sahte gösterim POST'u gönderilmedi.

Bu inceleme kurulum veya Store paketi üretimi içermiyor. Önceki uçtan uca
yerel arayüz testleri ve genel uygulama testleri `MIAMO_ADS.md` raporunda.
