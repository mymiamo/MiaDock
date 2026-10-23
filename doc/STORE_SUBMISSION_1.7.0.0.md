# MiaDock 1.7.0.0 — güncel Store adayı

15 Ekim 2026: kullanıcı genel Microsoft Store güncellemesinin yayımlanmasını
istedi. Yayın izni mevcut; bu oturumda Partner Center için oturum açmış
tarayıcı kontrolü veya Store Submission API bağlantısı yok. Bu nedenle
yükleme, sertifikaya gönderme ve yayınlama gerçekleşmiş sayılmaz.

Teslim yolu: `artifacts/release/1.7.0.0/MiaDock-1.7.0.0-x64.msixupload`.
Kaynak commit'i, SHA-256 ve dosya boyutu aynı klasördeki
`store-package-summary.json` ve `package-verification.json` içindedir.
Önceki, reklam bileşeni bulunmayan teslim `artifacts/release/1.7.0.0-before-ads/`
altında korunur. Güncel teslim akıcılık geliştirmeleriyle birlikte reklam
bileşenini ve Türkçe/İngilizce yenilik notlarını içerir.

## Doğrulama durumu

- Güncel üç otomatik test takımının kayıtları:
  `artifacts/validation/store-ads/tests/`.
- Reklam bileşeninin önceki gerçek WinUI fixture'ı 34 kontrolü geçti;
  bu fixture canlı kampanya veya canlı gösterim gönderimi kullanmaz.
- 15 Ekim canlı GET kontrolünde iki kampanya ve her iki PNG kaynağı HTTP 200
  verdi. Anahtar, kampanya kimliği ve token rapora yazılmadı. Üretime sahte
  gösterim/tıklama gönderilmedi. Kayıtlar:
  `artifacts/validation/store-ads/live-api.json` ve `live-creatives.json`.
- WACK kurulu; mevcut PowerShell bağlamı yönetici değil. WACK çalıştırılmadı.
- Tam uygulamada gerçek 30 dakika yoğun kullanım, 8 saat boşta, paket yaşam
  döngüsü ve fiziksel cihaz kabulü halen bekliyor; eski animasyon fixture'ı
  bu kabulün yerine konulmaz. Ayrıntılar `SMOOTHNESS_1.7.0.md`.

Paket, doğrulanmamış aday seçeneğiyle hazırlanır; bu seçenek cihaz kabulünü
başarılıya çevirmek veya genel yayının tamamlandığını iddia etmek için
kullanılmaz. Önce kabul kontrolleri tamamlanmalıdır.

## Gönderim adımları

1. Yönetici PowerShell'de `scripts/validation/Invoke-MiaDockWack.ps1` betiğini
   `-PackagePath artifacts/release/1.7.0.0/wack/MiaDock.App_1.7.0.0_x64.msix`
   ile çalıştırın; sonuçları ve kalan cihaz kabulünü değerlendirin.
2. Partner Center'da mevcut MiaDock ürününün yeni gönderimini açın. Son
   yayımlanmış paket sürümünü kontrol edin. 1.7.0.0 zaten yayımlanmışsa bu
   arşivi yeniden kullanmak yerine sürümü tutarlı biçimde artırıp yeniden
   paketlemek gerekir; bu oturumda paneldeki sürüm görülemedi.
3. Packages bölümüne yalnız güncel `.msixupload` arşivini yükleyin. İç MSIX'i
   ayrıca yüklemeyin. Aynı sürümün eski adayını bu yeni arşivle karıştırmayın.
4. Mevcut mağaza açıklamaları ve gizlilik adresini, reklam beyanını ve yaş
   derecelendirmesi yanıtlarını reklam özelliğiyle tutarlı olacak şekilde
   gözden geçirin. Kontrol edilmeyen panel alanları tamamlandı sayılmaz.
5. Yenilik notlarını ve sertifikasyon notunu girin; mevcut gönderimin tüm
   bölümleri ve kabul kontrolleri tamamlandığında sertifikaya gönderin.
   Microsoft'un sertifikasyon sonucu ve yayın durumu ayrıca izlenmelidir.

Resmî kaynaklar:
[Gönderim oluşturma](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/create-app-submission),
[Paket gereksinimleri](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/app-package-requirements).

## Yenilik metni

Türkçe: Hızlı dock etkileşimlerinde animasyon sürekliliği ve görünürlük
yönetimi iyileştirildi. Tanılama ekranına 60 saniyelik UI akıcılık ölçümü
eklendi. Ayarlar menüsünde Sponsorlu etiketli Miamo reklam alanı bulunuyor.

English: Improved animation continuity and visibility handling during rapid
dock interactions. Diagnostics now offers a 60-second UI smoothness capture.
Settings includes a labeled Miamo sponsored carousel.

Sertifikasyon notu: Windows 11 x64 masaüstü dock uygulaması; mevcut full-trust
ve başlangıç görevi davranışı korunur. Reklamlar yalnız Ayarlar sol menüsünde
gösterilir. Kullanıcı kimlik doğrulaması veya gizli istemci API anahtarı
gerekmez. Ağ kesintisinde reklam alanı gizlenir, uygulama kullanılabilir kalır.

## Yayından sonraki özellik önerisi — henüz uygulanmadı

Akıllı öneriler: müzik başladığında Medya, ses cihazı bağlandığında ses
çıkışı ve kullanıcının belirlediği çalışma saatlerinde Odak önerisi.
İlk sürüm cihazda çalışan açık kurallarla başlayabilir; öneriyi kabul etmek
kullanıcıya bırakılır. İsteğe bağlı kullanım sıklığı kaydı daha sonra
eklenebilir; pano, bildirim içeriği veya uygulama metinlerini toplamak
gerekmez. Kapatma ve yerel geçmişi temizleme seçenekleri tasarıma dahil
edilmelidir. Bu öneri mevcut Store paketinin özelliği olarak sunulmaz.
