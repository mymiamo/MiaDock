# MiaDock dock ikonları — 15 Ekim 2026

## Uygulanan değişiklik

Dock, favoriler, modül gezinmesi ve modüllerin kompakt, hover, genişletilmiş ve
bildirim görünümleri yerel Ionicons 7.4.0 kaynaklarını kullanıyor. Katalogda
73 anahtarın çizgisel/dolu çiftleri, toplam 146 SVG bulunuyor. MIT lisansı
`src/MiaDock.UI/Assets/Ionicons/LICENSE` içinde ve derleme çıktısına kopyalanıyor.
SVG dosyaları UI assembly'sine gömülü; uygulamanın ikon yüklerken internete veya
ek ikon yazı tipine ihtiyacı yok.

`DockIcon` mevcut `IconGlyph` değerlerini bir eşleme katmanıyla karşılıyor.
Modül kimlikleri, ayar şeması ve ikon paketi seçme davranışı değişmedi.
Bilinmeyen anahtar/glyph aynı paketteki soru işaretini gösteriyor.

Normal ikonlar çizgisel; gezinmede seçili modül, aktif Odak profili ve bağlı
cihaz/ağ durumları dolu varyantı kullanıyor. Hover tek başına varyantı değiştirmiyor.
Oynatma/duraklatma, ses düzeyi/sessizlik, Bluetooth ve gizlilik simgeleri mevcut
durumu takip ediyor. Medya ses göstergesine sıfır/düşük/yüksek seviye eşlemesi
ve ilgili değişiklik bildirimleri eklendi.

Yatay pil göstergesi kendi çizimini koruyor: normal dolgu gri, şarj dolgusu yeşil
ve şimşekli. Ayarlar, onboarding, tepsi simgeleri, uygulama logoları ve medya
kapakları bu değişikliğin dışında. Konum düzenlemedeki kalem, taşıma, sıfırlama ve
kaydetme ikonları değişti; Thumb olayları, ekran koordinatı hesabı ve klavye
hareketleri önceki düzeltmeden aynen devam ediyor.

## Kaynaklar ve çizim

- `scripts/icons/Import-Ionicons.py` resmi npm arşivinden sürümü sabitlenmiş
  alt kümeyi ve lisansı alır. Kaynak adresi ve SHA-256 değerleri manifestte bulunur.
- SVG dolguları ve çizgileri tema fırçasının rengiyle işlenir; `fill="none"`
  alanları ve şeffaflık korunur. Fırçanın rengi/opaklığı, tema ve DPI değişimleri
  izlenir. Yüksek kontrast için sistem renklerine dönüş uygulanır.
- Kaynak önbelleği renk/boyut/varyanta göre en fazla 128 girdi tutar. Raster
  boyutu pencerenin ölçeğine göre hesaplanır; eski bir asenkron yükleme yeni
  modülün veya temanın ikonunu sonradan değiştiremez.
- Dekoratif ikonlar hit testing'e katılmaz. Erişilebilir adlar ve etkileşim
  düğmelerde kalır; ikonların ek bir klavye durağı oluşturması amaçlanmaz.

## Doğrulama

Yeni proje konumu `C:\Users\User\OneDrive\MiaDock` üzerinde 15 Ekim'de yeniden
çalıştırılan kontrollerin sonuçları:

| Kontrol | Sonuç |
| --- | --- |
| Release x64 uygulama derlemesi | Rebuild başarılı; hata/uyarı bildirilmedi |
| Core testleri | 423 geçti |
| WinUI testleri | 216 geçti |
| Windows testleri | 246 geçti; 1 ortam bağımlı ses yakalama testi atlandı |
| Yerel WinUI ikon yüklemesi | 146/146 kaynak yüklendi; ölçek 1; tümü 24 × 24 DIP; çıkış kodu 0 |

Toplam 885 test geçti, 1 test atlandı; yeni başarısızlık yok.

Katalog testleri modül ikonlarını, dinamik ses/medya/ağ/cihaz eşlemelerini,
bilinmeyen anahtar geri dönüşünü, bütün SVG çiftlerinin varlığını ve kaynak
hashlerini denetliyor. Renk uygulamasının çizgisel ikonların içini doldurmadığı
kontrol ediliyor. Dock XAML'lerinde eski FontIcon kalmaması, ayar ikonlarının ve
özel pil çiziminin korunması da kapsanıyor. Yeni davranış testleri ağ/Bluetooth
bağlantı geçişlerini, aynı cihaz satırındaki durum yenilenmesini ve medya ses
seviyesinin simgeye yansımasını doğruluyor.

Önceki oturumda ayrı WinUI doğrulama penceresinde 146/146 SVG yüklendi;
%100 ölçekte 24 × 24 DIP boyutlar kaydedildi. Açık ve koyu zeminde çizgisel/dolu
çiftler ve aynı fırçanın rengi değiştirilince ikon renginin güncellenmesi canlı
olarak gözlendi. Bu, bütün dock akışlarının canlı kabulü anlamına gelmez.

Tekrarlanabilir geliştirici aracı `scripts/icons/NativePreview` altındadır.
`--check` seçeneği JSON sonucu yazar ve kendiliğinden çıkar. Bu araç MiaDock'un
ayarlarını veya servislerini kullanmaz; normal uygulama paketine eklenmez.
Kullanım komutları aynı klasördeki README dosyasındadır.

Güncel kanıt dosyaları Git dışında tutulan `artifacts` klasöründedir:

- `build-ionicons-app-2026-10-15.log`
- `build-ionicons-preview-2026-10-15.log`
- `tests-ionicons-{core,winui,windows}-2026-10-15.log`
- `test-results/ionicons-{core,winui,windows}-2026-10-15.trx`
- `ionicons-native-result-2026-10-15.json`

## Canlı kabulde kalanlar

Gerçek dock üzerinde %150/%200 DPI, monitörler arasında ölçek değişimi, yüksek
kontrast, ekran okuyucu konuşması, hızlı modül/medya geçişleri ve konum düzenleme
akışının sürükle/kaydet/sıfırla/iptal kontrolleri bu raporla kabul edilmemiştir.
Önceki oturumda dock'un araç penceresi Computer Use pencere listesinde
hedeflenemedi; bu devam oturumunda Windows UI giriş aracı sağlanmadı. Kaynak ve
olay bağlantısı testleri bu fiziksel kontrollerin yerine geçmez.

## Git kaydı ve geri dönüş

Çalışma `codex/winui-improvements` dalındadır:

1. `ed5d290`: yerel paket, katalog, tema/DPI uyumlu ortak çizim bileşeni.
2. `e315b85`: dock/modül geçişi, durum eşlemeleri ve regresyon testleri.
3. `checkpoint/miadock-ionicons-complete` etiketinin gösterdiği teslim commit'i:
   yerel doğrulama aracı ve bu rapor.

İkon denemesinden önceki nokta `04aa684`'tür. Önceki WinUI geliştirmeleri
korunarak yalnız bu ikon çalışmasını geri almak için, uygulamayı kapattıktan
ve yerel değişiklikleri kaydettikten sonra:

```powershell
git revert --no-commit checkpoint/miadock-ionicons-complete e315b85 ed5d290
git commit -m "Revert dock Ionicons experiment"
```

Ardından Release sürümünü yeniden derleyin. Geri dönüş ayar dosyası silmeyi veya
`reset --hard` kullanmayı gerektirmez. Bu çalışma ana dala birleştirilmedi ve
yayımlanmadı.
