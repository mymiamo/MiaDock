# Miamo Ads — yalnız örnek değerler

Bu klasörde gerçek API anahtarı, gösterim token'ı veya imzalama sırrı yoktur.
Kampanya ve UUID örnektir. JSON dosyalarını canlı API'ye göndermeyin.

- `ads-response.example.json`: örnek GET yanıtı.
- `impression-request.example.json`: örnek POST gövdesi; aynı gösterimin
  tekrarında `event_id` değişmez.
- `.env.example`: yalnız sunucuya ait ayarın adı; değer özellikle boştur.

MiaDock masaüstü istemcisi gizli API anahtarı kullanmaz. Genel reklam listesi
ucundan alınan kısa ömürlü `impression_token` yalnız çalışma anında bellekte
tutulur. İstek modeli ve alan adı kaynak kodda kalır; gerçek token kaynak
koda, örneklere, ayar dosyasına veya loglara yazılmaz.

`MIADOCK_ADS_SIGNING_SECRET` sunucuda rastgele üretilir ve masaüstü istemcisine
verilmez. Gerçek `.env`, `miamo-db.php`, imzalama dosyaları, yerel çıktı ve
doğrulama arşivleri Git dışında tutulur. Üretim sitenizin özel yapılandırmasını
bu klasöre kopyalamayın.
