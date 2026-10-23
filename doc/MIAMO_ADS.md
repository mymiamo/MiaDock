# Miamo Ads — sol menü reklam bileşeni

GitHub için yalnız sahte değer içeren API sözleşmesi örnekleri
[`examples/miamo-ads/`](../examples/miamo-ads/README.md) altındadır. Gerçek
API anahtarı, gösterim token'ı veya sunucu imzalama değeri bu depoda tutulmaz.

15 Ekim 2026. Çalışma dalı: `codex/1.7.0-smoothness`.
Başlangıç: `8692a58`; checkpoint: `checkpoint/miadock-before-miamo-ads`.

## Yerleşim

`SettingsWindow` içindeki mevcut NavigationView korunur. Yeniden kullanılabilir
`MiamoAdsSlider`, `PaneFooter` içindedir; mevcut `FooterMenuItems` koleksiyonundaki
“Yenilikler” öğesinin hemen üstünde görünür. Panelin 264 DIP genişliği değişmez.
Kaynak: [WinUI NavigationView şablonu](https://github.com/microsoft/microsoft-ui-xaml/blob/main/controls/dev/NavigationView/NavigationView.xaml).

Görsel alanı kullanılabilir genişlik, menü öğelerinin ölçülen yüksekliği,
Yenilikler alanı, etiket ve sıra kontrollerine göre hesaplanır. Üst sınır
240 DIP, okunabilir alt sınır 96 DIP'tir. Yeterli alan yoksa reklam tamamen
kapanır. ScrollViewer'ın boş viewport yüksekliği menü içeriği sayılmaz.
`Image.Stretch="Uniform"` ve oranı koruyan en fazla 500 piksel decode kullanılır.
Kaynak görsel kırpılmaz veya kareye esnetilmez. Tema fırçaları, 12 DIP mevcut
kart köşesi, küçük Sponsorlu etiketi ve Ionicons okları kullanılır.

## Davranış

- Sıralama API sırasıdır; güvenli olmayan veya eksik kayıtlar elenir.
- Görsel decode edildikten, alan tamamen görünür olduktan ve bir Rendering
  callback'i alındıktan sonra görünür süre başlar.
- Manuel geçiş için en az **5 saniye** gerekir; otomatik geçiş **6 saniyededir**.
  Klavye odağı bileşendeyken otomatik geçiş durur; manuel geçiş kullanılabilir.
- Tek uygun reklam sabit kalır; sıra ve geçiş kontrolleri kapanır.
- Bir sonraki görsel önceden yüklenir; ön yükleme süre/gösterim başlatmaz.
  Bozuk görseller sırayı kilitlemeden atlanır.
- Liste **5 dakikada bir**, tek istek çalışacak şekilde yenilenir. Her slaytta
  API çağrılmaz. Hatalı yanıtta son iyi liste korunur; geçerli boş liste reklam
  alanını kaldırır. Yeni liste mevcut reklamın ilk 5 saniyesini kesmez.
- Daraltılmış menü, görünmeyen/taşan alan, küçültülmüş pencere veya etkin olmayan
  ayar penceresi süreyi durdurur. Geri dönüşte kalan görünür süreden devam eder.
- Ani geçiş kullanılır; dekoratif animasyon yoktur. GIF otomatik oynatılmaz.
  Bu nedenle azaltılmış hareket tercihiyle çelişen bir animasyon başlatılmaz.

## Gösterim, tıklama ve ağ

İlk gerçek görünürlüğün en az **1 saniyesinden** sonra kampanya için pencere
oturumuna ait rastgele UUID ayrılır. Başarılı gösterim aynı kampanya tekrar
görünse de yeniden sayılmaz. Yeniden denemeler aynı UUID ve aynı gösterim
token'ını kullanır. En çok üç deneme yapılır; ilk hatadan sonra 2, ikinci
hatadan sonra 10 saniye beklenir. 5xx, 408 ve 429 tekrar denenebilir; diğer
kalıcı HTTP hataları tekrar döngüsüne sokulmaz. Gizlenme bekleyen gönderimi
iptal eder; tekrar görünmeden yeni deneme başlamaz. Sunucunun da aynı
`event_id` tekrarlarını tek kayıt olarak kabul etmesi gerekir.

Tıklama mevcut `IExternalUriLauncher` üzerinden Windows
`Launcher.LaunchUriAsync` yolunu kullanır. Görsel ve tıklama adresleri yalnız
`https://mymiamo.net` ve `https://www.mymiamo.net`, varsayılan HTTPS portu ve
kullanıcı bilgisi içermeyen URI'lerle sınırlıdır. Görsel yönlendirmelerinde de
bu kontrol uygulanır. POST yönlendirmesi izlenmez.

Paylaşılan gizli API anahtarı **kullanılmadı/gömülmedi**. Sabit yayın uçlarına
anahtarsız istek yapılır; cookie veya Windows kimlik bilgisi gönderilmez.
Gösterim gövdesinde yalnız `campaign_id`, `event_id`, `impression_token` bulunur.
Kullanıcı/cihaz tanımlayıcısı, kişisel yol veya kalıcı izleme ayarı eklenmez.

Her ağ isteği iptal edilebilir ve 8 saniye zaman aşımına sahiptir. JSON en fazla
512 KiB, tek görsel en fazla 4 MiB; bellek önbelleği en fazla 8 kayıt/16 MiB'dir.
Decode sırasında boyut sınırı uygulanır. UI tarafı en fazla üç bitmap görevi
tutar. Disk önbelleği yoktur. Kampanya/token/URL içeren hata logu yazılmaz.

Saat ve abonelik sahibi sayfa değil ayar penceresidir. Tek DispatcherQueueTimer
kullanılır; navigasyon değişimi yeni timer yaratmaz. Başlangıçta daraltılmış
olan öğenin ilk isteği de pencere kökü yüklendiğinde başlatılır. Pencere
kapanışında istekler iptal edilir; timer, Rendering ve pencere/dil/pane
abonelikleri ayrılır. Ağ beklemesi açılış veya navigasyonda await edilmez.

## Doğrulama sonuçları

| Kontrol | Sonuç |
|---|---|
| Release x64 WinUI uygulama derlemesi | Geçti |
| Core testleri | 460 geçti; reklam için 32 yeni test vakası |
| Windows platform testleri | 246 geçti, mevcut ses loopback testi ortam nedeniyle 1 atlandı |
| WinUI kaynak/sözleşme ve çeviri testleri | 216 geçti |
| Gerçek WinUI bileşen/pencere testi | 34 kontrol geçti; gerçek ölçek 1.0 |

Native testte 500×500 sahte PNG, tek/çoklu/bozuk/boş listeler, API kesintisi,
5 saniye sınırı, gerçek menü düğmesiyle daraltma, küçültme/geri dönüş, etkin
pencere değişimi, görünürlükte kalan süre, klavye odağı, CTA adresi, küçük
pencere, kampanya tekilleştirme ve **pencerenin gerçekten kapatılmasıyla**
bekleyen isteğin iptali doğrulandı. Bu testte ağ ve tarayıcı istemcileri
sahtedir; canlı kampanyalara gösterim veya tıklama gönderilmedi.

Beş dakikalık zamanlama sanal monoton zamanla çekirdek testinde doğrulanır.
Native test liste değiştirme senaryolarında yenileme metodunu doğrudan çağırır;
her bir yenileme için beş dakika beklenen bir saha testi olarak sunulmaz.
Native testte odağın kasıtlı olarak değiştirildiği bölümler dışında test
penceresi yeniden etkinleştirilir; aksi durumda bileşen haklı olarak durur.

Kanıtlar: `artifacts/validation/miamo-ads/core.trx`, `windows.trx`, `winui.trx`,
`native.json`, `native.json.png`. Uygulama derleme kaydı:
`artifacts/ads-build-final.log`. Yeni Release çıktısı:
`artifacts/validation/miamo-ads/build/MiaDock.App.exe`.

## Canlı API nedeniyle açık kalanlar

Anahtarsız `GET https://mymiamo.net/api/v1/miadock-ads` ilk kontrolde HTTP 404
döndü. Son kontrolde (15 Ekim 2026, 12:58 İstanbul) yanıt **HTTP 503** ve JSON
`error: service_unavailable` oldu. Güncel kayıt:
`artifacts/validation/miamo-ads/live-api.json`.
Bu nedenle gerçek kampanya listesi, gerçek kreatif sunumu/yönlendirmeleri,
token geçerliliği, Miamo'nun gösterim kabulü/UUID tekilleştirmesi ve gerçek
tıklama hedefleri uçtan uca doğrulanamadı. Üretim API'sine sahte POST gönderilmedi.
Uçlar yayına alındığında bu kontroller canlı, gerçekten görünür kampanyayla
tamamlanmalıdır. Narrator, yüksek kontrast ve farklı fiziksel DPI ölçekleri
manuel cihaz kontrolü olarak ayrıca gözlenmedi.

Ayar şeması 34 ve sürüm 1.7.0.0 korunur. Bu istek kaynak kodu ve Release test
çıktısını kapsar; `artifacts/release/1.7.0.0` içindeki önceki Store arşivi bu
değişiklikle yeniden üretilmemiştir. Store yayını veya uygulama kurulumu yapılmadı.
Geri dönüş için önce UI entegrasyonu, sonra `f38fef7` çekirdek commit'i
`git revert` ile geri alınabilir; başlangıç checkpoint'i korunur.
