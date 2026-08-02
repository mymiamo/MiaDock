# MiaDock 1.2.1.0 Release Validation

Bu belge sürüm adayının doğrulama durumunu kaydeder. Üretilen test, performans ve paket kanıtları `artifacts/` altında tutulur ve Git'e eklenmez.

## Otomatik Kapılar

- [x] Paket, assembly ve uygulama manifesti `1.2.1.0`
- [x] Ayar şeması `18`
- [x] Release x64 derleme: 0 hata, 0 uyarı
- [x] Başlangıç otomatik test tabanı: 499 test
- [x] Son değişikliklerden sonra tam çözüm doğrulaması: 499/499
- [x] İzole başlangıç ve yanıt verme smoke testi
- [x] İmzasız test MSIX içerik doğrulaması

## Manuel Regresyonlar

- [ ] Dropdown ve süre artırma/azaltma kontrolleri dock'u kapatmadan kullanılabiliyor
- [ ] Zamanlayıcı ve medya güncellemelerinde titreme bulunmuyor
- [ ] Alt modül menüsü expanded dock genişliğini kullanıyor ve kırpılmıyor
- [ ] Spotify, Apple Music, YouTube ve çoklu medya oturumu
- [ ] Ana ses, uygulama sesleri ve ses aygıtı değişimi
- [ ] Zamanlayıcı alarmı beş kez çalıyor ve hover görünümünden susturulabiliyor
- [ ] Windows başlangıç görevi ve tray üzerinden tamamen çıkış
- [ ] Uyku/uyanma, Explorer yeniden başlatma, çoklu monitör ve %100-%200 DPI
- [ ] Türkçe/İngilizce, tüm temalar, yüksek kontrast ve azaltılmış hareket

## Uzun Çalışma Kapıları

- [x] Ölçekli olay/boşta soak smoke testi: 2/2
- [x] 60 saniyelik runtime sağlık testi: ortalama CPU %0,112; bellek artışı -3,27 MB; yanıtsız örnek 0
- [ ] 30 dakika yoğun olay testi
- [ ] 8 saat boşta çalışma testi
- [ ] Ortalama boşta CPU en fazla %1
- [ ] Sekiz saatte çalışma seti artışı en fazla 20 MB
- [ ] Yakalanmamış exception, kalıcı donma veya yanıt vermeme: 0

## Komutlar

```powershell
powershell -ExecutionPolicy Bypass -File scripts/validation/Invoke-MiaDockReleaseValidation.ps1 -SkipRestore -LaunchSmokeTest -StopRunningApp
powershell -ExecutionPolicy Bypass -File scripts/validation/Invoke-MiaDockSoakTest.ps1 -Profile events
powershell -ExecutionPolicy Bypass -File scripts/validation/Invoke-MiaDockSoakTest.ps1 -Profile idle
```

Microsoft Store yüklemesi bu doğrulamanın parçası değildir ve proje sahibinin ayrıca açık onayını gerektirir.
