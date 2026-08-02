# MiaDock 1.2.0.0 Release Validation

Bu belge, MiaDock 1.2.0.0 için yayın öncesi doğrulama kapsamını ve kanıt
durumunu kaydeder. Test çıktıları, paketler ve loglar `artifacts/` altında
üretilir ve Git'e eklenmez.

## Otomatik Doğrulama

| Kontrol | Durum | Beklenen |
| --- | --- | --- |
| Core testleri | Başarılı — 246/246 | Tümü başarılı |
| Windows platform testleri | Başarılı — 105/105 | Tümü başarılı |
| WinUI kaynak testleri | Başarılı — 107/107 | Tümü başarılı |
| x64 Release derlemesi | Başarılı | 0 hata, 0 uyarı |
| Manifest ve sürüm tutarlılığı | Başarılı | `1.2.0.0` |
| Uygulama başlangıç smoke testi | Başarılı | 5 saniye yanıt verir |
| Ölçeklendirilmiş soak smoke testi | Başarılı — 2/2 | İki profil başarılı |
| İmzasız test MSIX üretimi | Başarılı — sembol uyarısıyla | İçerik incelemesi |
| 30 dakika yoğun olay testi | Bekliyor | Kuyruk ve bellek sınırlı |
| 8 saat boşta çalışma testi | Bekliyor | Olay/pending iş oluşmaz |

Temel doğrulama:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/validation/Invoke-MiaDockReleaseValidation.ps1 -SkipRestore -LaunchSmokeTest -StopRunningApp
```

İmzalanmamış ve kurulmayacak paket inceleme çıktısını da üretmek için komuta
`-BuildUnsignedTestPackage` eklenir. Güvenilir sertifika olmadan bu paket
kurulmaz ve mevcut Store kurulumu değiştirilmez.

Paketleme ortamında `mspdbcmf.exe` bulunmadığı için sembol paketi üretilemedi
uyarısı alınmıştır. Ana MSIX başarıyla üretilmiştir; sembol aracı Faz 12
öncesinde Windows SDK/Visual Studio bileşenleriyle tamamlanmalıdır.

Kısa ve gerçek süre yerine geçmeyen soak kontrolü:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/validation/Invoke-MiaDockSoakTest.ps1 -Profile all -Scale 0.0001 -AllowScaled
```

Gerçek süreli doğrulamalar:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/validation/Invoke-MiaDockSoakTest.ps1 -Profile events
powershell -ExecutionPolicy Bypass -File scripts/validation/Invoke-MiaDockSoakTest.ps1 -Profile idle
```

## Paketli Uygulama Kontrolü

Bu kontroller Microsoft Store'dan veya güvenilir test imzasıyla kurulmuş
1.2.0.0 pakette yapılmalıdır. Mevcut Store paketinin üzerine imzasız geliştirme
paketi kurulmaz.

- [ ] Temiz kurulum ve ilk çalıştırma
- [ ] 1.1.1.0 sürümünden 1.2.0.0 sürümüne ayar kaybetmeden güncelleme
- [ ] Windows başlangıcında başlatma ve Görev Yöneticisi durumu
- [ ] Tray menüsünden tamamen çıkış; sistem hata penceresi oluşmaması
- [ ] Tek uygulama örneği ve ikinci başlatmanın mevcut örneğe yönlenmesi
- [ ] Store güncelleme kontrolünün çevrimiçi ve çevrimdışı davranışı
- [ ] Kaldırma sonrasında çalışan süreç veya başlangıç görevi kalmaması

## Gerçek Cihaz Regresyonları

- [ ] Spotify, Apple Music ve tarayıcıda YouTube
- [ ] Birden fazla medya oturumu ve seçili kaynağın kapanması
- [ ] Hızlı şarkı değişimi, kapaksız medya ve desteklenmeyen seek
- [ ] Windows ana sesi ve eşleşen/eşleşmeyen uygulama oturumları
- [ ] Varsayılan ses aygıtının değiştirilmesi ve aygıt çıkarılması
- [ ] Zamanlayıcı alarmının beş tekrar çalması ve hover alanından susturulması
- [ ] Uyku/uyanma ve Explorer yeniden başlatılması
- [ ] Tam ekran ve kenarlıksız tam ekran uygulama
- [ ] Çoklu monitör, monitör çıkarılması ve %100–%200 DPI
- [ ] Türkçe–İngilizce canlı dil değişimi
- [ ] Tüm temalar, yüksek kontrast ve azaltılmış hareket

## Yayın Engelleyiciler

Aşağıdaki durumlardan biri görülürse Faz 11 tamamlanmış sayılmaz:

- Çökme, kalıcı donma veya “Yanıt vermiyor” durumu
- Windows başlangıcının paketli ortamda çalışmaması
- Tamamen çıkışta sistem hatası veya kalan süreç
- Ayarların güncelleme sırasında kaybolması
- Hassas içeriğin teknik loglara yazılması
- Gerçek süreli soak testinde sınırsız bellek, timer veya olay kuyruğu büyümesi

## Sonraki Faz

Faz 12; temiz Store upload üretimi, WACK, özel package flight ve sonuçların
proje sahibi tarafından onaylanmasını kapsar. Genel Store yayını otomatik
yapılmaz.

Faz 12 giriş kapısı için gerçek süreli ve manuel kontroller tamamlandığında
`artifacts/validation/1.2.0/phase11-gates.json` oluşturulur. Bu kanıt olmadan
Store package betiği çalışmaz.
