# Altı ses çubuğu ve ana dock pil göstergesi

Ses göstergesi altı yuvarlak uçlu çubuk kullanır. Mevcut üç ses kaynağı
korunur; soldan sağa iki tiz, iki bas ve iki vokal çubuğu gösterilir. Her
çiftin ilk çubuğu hızlı, ikincisi daha yumuşak tepki/sönümle aynı bandı izler;
farklı bantlar birbirine karıştırılmaz. Yeni ses yakalayıcısı
veya sürekli işleyen ek timer eklenmez. Çubukların genişliği toplam 19,5 DIP;
kompakt ev sahibi 20 DIP olarak ayrılır. Her çubuk kendi dikey merkezinde
büyür. Duraklatmada aynı alanda altı eşit küçük nokta kalır: `......`.
Noktanın yüksekliği çubuk genişliğine eşitlenir; özel çubuk boyutlarında da
merkez ve dairesel görünüm korunur. Çevresinde üç noktalı menü simgesi yoktur.
Gizlenen görünümler ve azaltılmış hareket tercihi önceki yaşam döngüsünü korur.

Ana dock pil göstergesinin rozet zemini kaldırıldı. Yüzde simgenin içinde,
yüzde işareti olmadan gösterilir; dolgunun üzerinden geçen yazı bölümü koyu,
boş kısmındaki yazı açık renktir. Dolu bölüm pil yüzdesiyle orantılıdır.
Normalde ana dock'un tema metin rengi, yüzde 20 ve altında sarı, yüzde 10 ve
altında kırmızı, şarjda yeşil kullanılır. Şarj rengi düşük pil renginden
önceliklidir; şimşek yüzdeyi kapatmayacak şekilde yana alınır. Yüksek kontrastta
sistem renkleri kullanılır. Eksik okuma `?`, pil yoksa `–` gösterilir.

Diğer pil görünümlerinin ayrı metinleri korunur; simge içi yüzde yalnız ana
dock'ta etkinleştirilir. Ayar şeması veya kullanıcı ayarları değiştirilmez.

Doğrulama kayıtları `artifacts/validation/dock-indicators/` altındadır.
Release uygulama derlemesi ve 216 WinUI testi geçti. Native fixture'da
24 kontrol, gerçek ölçek 1.0 ile geçti; bileşen önizlemesi `native.json.png`
dosyasındadır. Geliştirme çıktısı `build/MiaDock.App.exe` konumundadır.
Native fixture üretim bileşenleriyle çalışır; kurulu Store uygulamasını
güncellemez. Fiziksel farklı DPI, ekran okuyucu ve yüksek kontrast elle
gözlenmiş sayılmaz. Mevcut 1.7.3.0 Store teslimi bu kaynak değişikliğinden
öncedir; yeni kaynak doğrulaması ayrı geliştirme derlemesine yapılır.
