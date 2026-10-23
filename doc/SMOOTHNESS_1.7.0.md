# MiaDock 1.7.0 validation and comparison

Baseline: `2616398` (source recorded by the 1.6.5.0 upload).
Checkpoint: `checkpoint/miadock-before-1.7.0`.
Branch: `codex/1.7.0-smoothness`. Settings schema stays 34.

## Measurement stage

Opt-in, UI-thread-owned 60-second recording. Frame interval nearest-rank p95/p99
use WinUI Rendering callbacks, not GPU presentation FPS. A delayed interval is
greater than 25 ms (1.5 times the 60 Hz budget); estimated missed intervals use
rounded 16.667 ms budgets. The first callback is excluded. Up to 16,384 frame
intervals and 1,024 transition records are retained. Evictions are reported.
Completed/cancelled totals cover the session; transition details retain the latest
records. No paths, module content, song titles or persistent preferences are added.
The independent deadline timer stops recording even without rendering callbacks.

Release build passed. Core tests: 427 passed, 0 failed (including four new
measurement tests). Evidence: `artifacts/validation/1.7.0.0/tests/measurement.trx`.

## Değişiklikler

Boyut animasyonları, iptal ve tamamlanma işlemleri için ayrı oturum sahipliği
kullanır. İşçi iş parçacığından iptal yalnız UI kuyruğuna bildirim bırakır.
Eski iptal veya son kare içinde yeniden başlatma yeni oturumu kapatamaz.
Toolkit iptal devamları yeni sahibin opacity/scale değerlerini sıfırlamaz;
yeniden hedeflemede Composition mevcut değerden başlar. İçerik yenilemesi devam
eden pencere büyümesini son boyuta sıçratmaz. Hareket süreleri/eğrileri korunur.

Aynı fiziksel pencere sınırları için SetWindowPos atlanır. Köşe/tıklama alanı
verileri güncellenir; DPI, ekran/topoloji, çalışma alanı ve dışarıdan pencere
hareketi konum önbelleğini gerektiğinde geçersiz kılar. Gizli dock ve kenara
çekilmiş ana yüzey modüllerin ses ölçümü taleplerini bırakır. Ana ekranın
genişletilmiş görünümü de artık aktivasyon sözleşmesini uygular. Görünmeyen ses
çubukları/dekoratif hareketler ve görünüm saati yenilemeleri durur. Zamanlayıcı,
bildirim, Odak ve gerekli medya servisleri kapatılmaz. Ayar/pencere kapanışı
sırasında tanılama abonelikleri UI üzerinde ayrılır. Mikro etkileşimlerde eski
kuyruk iptali yeni oturumun sahipliğini alamaz.

## Doğrulama

- Core: **428 geçti**; beş yeni test sınırları, yüzdelikleri, süre bitimini,
  eski oturumların dışlanmasını ve mevcut performans JSON sözleşmesini kapsar.
- Windows: **246 geçti, 1 atlandı**. Atlama mevcut varsayılan ses çıkışında
  loopback tonu üretilememesiyle ilgilidir.
- WinUI kaynak/sözleşme testleri: **216 geçti**. Bunlar gerçek UI otomasyonunun
  yerine geçmez. Yeni düğmeler altı dilin mevcut çeviri tablolarına eklendi.
- Gerçek WinUI üzerinde sahte modül içerikleriyle **1.000 hızlı etkileşim**:
  hover, aç/kapat, yön/boyut değiştirme, bildirimden dönüş ve içerik yenileme.
  Son geometri/görünürlük/opacity ve animasyonun durması doğrulandı.
- Ek native testler: işçi iptali, son kareden yeniden hedefleme, eski Toolkit
  tamamlanmasının yeni görseli sıfırlamaması, azaltılmış hareket için sahte
  tercih sağlayıcısı, tamamlanan geçiş ölçümü, durdurma/dispose ve tercih
  aboneliğinin kaldırılması. Son fixture normal biçimde kapanır.
- Aynı boyutta 1.000 layout çağrısı ve yalnız köşe değişikliği **0** native
  konumlandırma; gerçek boyut değişikliği **1** konumlandırma üretmek zorundadır.
- Ionicons native kontrolü: **146/146 kaynak, 7/7 boyut/raster kontrolü geçti**,
  gerçek ölçek 1.0. İkon çizim kodu bu sürümde değiştirilmedi.

Kanıtlar: `artifacts/validation/1.7.0.0/tests/*.trx`, `icons.json`,
`native/baseline-2616398/result.json`, `native/candidate/result.json`,
`native/final-stress/result.json`, `native/final-comparison/result.json`.
Native tekrar komutları `scripts/validation/NativeMotion/README.md` içindedir.

## Karşılaştırma

Referansın sekiz animasyon kaynak dosyası doğrudan `git show 2616398:...` ile
alınır. Fixture, Core/UI/Windows kütüphaneleri ve sahte içerikler ortaktır.
Bu, **animasyon kaynağı karşılaştırmasıdır**; kurulu 1.6.5 ve bütün 1.7.0
uygulamalarının eş koşullarda karşılaştırıldığı anlamına gelmez. Çalışan mevcut
Debug/Release MiaDock süreçleri korunmuştur. Arka plan yükü tam sabitlenmediği
için bu sonuçlardan genel bir hızlanma iddiası çıkarılmamalıdır.

Üçer gerçek 60 saniyelik oturum, gerçek ölçek 1.0. Aşağıdaki son kaynak turunda
yeni kodun iptal edilen geçiş telemetrisi de açıktır. Kanıt:
`native/final-comparison/result.json` (toplam 3.305 etkileşim).
Sayılar UI callback aralığıdır; GPU FPS'i değildir.

| Tur | Referans p95 / p99 (ms) | Yeni kod p95 / p99 (ms) | Yeni kod >25 ms aralık |
|---|---:|---:|---:|
| 1 | 6.4240 / 7.0236 | 6.5286 / 9.2884 | 1 |
| 2 | 6.4914 / 7.2912 | 6.5664 / 10.2066 | 0 |
| 3 | 6.5114 / 7.9244 | 6.5595 / 9.8796 | 1 |

Bu fixture ölçümlerinde p95 ≤20 ms ve p99 ≤50 ms hedefleri sağlandı; referans da
hedefleri sağlar. Yeni kodun p99 değerleri daha yüksektir. Kazanım olarak yalnız
doğrulanmış yarış düzeltmeleri ve gereksiz native çağrıların elenmesi belirtilir.
Fixture hazır olma süresi bir saniyelik ısınmayı içerir ve uygulama açılış süresi
olarak kullanılamaz.

73 ms aralıklarla yinelenen ölçüm senaryosu hareketleri tamamlanmadan yeniden
hedefler; bu üç kayıtta tamamlanmış geçiş sayısı bu yüzden 0, iptaller sırasıyla
766/769/767'dir. Ayrı native kontrolde iki tam geçişin süre kaydı doğrulanır.
Bu durum, tamamlanmış geçişlerin ortalama süresinin 0 ms olduğu şeklinde
yorumlanmamalıdır. Yeni tanılama örneklerinin çalışması dışında tam uygulama
geçiş süresi kazanımı henüz ölçülmemiştir.

## Açık kabul kapıları

| Kontrol | Durum |
|---|---|
| Tam uygulamada kontrollü başlangıç/CPU/bellek referans karşılaştırması | Bekliyor |
| Isınma sonrası gerçek 8 saat boşta; CPU ≤%1, özel bellek ≤20 MB, handle ≤64, thread ≤8 artış | Çalıştırılmadı |
| Gerçek uygulamada 30 dakika yoğun kullanım | Çalıştırılmadı |
| Açılışta referansa göre en fazla %10 gerileme | Ölçülmedi |
| Fiziksel %150/%200 DPI, monitör çıkarma, uyku/uyanma, ekran kilidi, tam ekran oyun | Gözlenmedi |
| Klavye, ekran okuyucu, yüksek kontrast, sistem azaltılmış hareket ayarı, konum sürükleme | Manuel kabul bekliyor |
| Paket kurulum/güncelleme/temiz kapanış, WACK | Bekliyor |
| Özel flight / genel Store yayını | Yapılmadı |

`Invoke-MiaDockRuntimeStability.ps1` artık özel bellek artışını kapı olarak
kullanır, 60 saniye ısınır ve gerçek geçen süreyi monoton saatle kaydeder.
`-Scenario Idle -DurationSeconds 28800` ve `-Scenario Heavy -DurationSeconds 1800`
yalnız gerçek koşullarda, doğru sürüm çalışırken kullanılmalıdır. Süreyi doldurmak
etkileşimlerin gerçekten yapıldığını kanıtlamaz. Hızlandırılmış çekirdek soak
testleri bu kapılardan geçirilmez.

## Sürüm, paket ve geri dönüş

Proje, assembly, uygulama manifesti, MSIX manifesti ve paket kontrol betikleri
**1.7.0.0** sürümündedir; ayar şeması **34** kalır. Yerel ölçüm ayarı eklenmez.
Paketleme ayrı çıktı dizininde yapılır; çalışan kullanıcı süreçleri durdurulmaz.
Otomatik test kanıtıyla üretilen paket `AllowUnverifiedCandidate` kullanır ve
cihaz kapıları tamamlanmış gibi işaretlenmez. `.msixupload` Store aday arşividir.
Kurulum yapılmadı ve Store'a yüklenmedi.

Teslim dosyası:
`artifacts/release/1.7.0.0/MiaDock-1.7.0.0-x64.msixupload`.
Son teslimin boyutu, kaynak commit'i ve SHA-256 değeri
`package-verification.json` ve `store-package-summary.json` içindedir.
Paket temiz çalışma ağacından üretilir. Son tanılama düzeltmesi, tamamlanan
geçiş örneği yokken ortalamayı 0 ms yerine “ölçüm yok” olarak gösterir.

Arşivin içindeki x64 MSIX kimliği/sürümü, .NET/WinUI çalışma zamanı, derlenen
App/Core/UI/Windows assembly hash'leri ve **10 PDB** içeren sembol arşivi
doğrulandı. Store arşivi imzasızdır. `package-verification.json`,
`store-package-summary.json`, `validation/acceptance.json`, üç TRX ve native
JSON kayıtları teslim klasöründedir. Cihaz kabulü `PendingDeviceValidation`,
flight hazırlığı `false` olarak kaydedilmiştir.

Geri dönüş: bu daldaki aşama commit'lerini ters sırayla `git revert` ile geri
almak. Başlangıç checkpoint'i korunur, master değişmez. Genel Store yayını için
ayrıca kullanıcının açık onayı gerekir.
