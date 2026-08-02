# MiaDock 1.2.0.0 Store Submission

Bu kontrol listesi Microsoft Store özel package flight hazırlığı içindir. Genel
yayın, proje sahibinin ayrıca vereceği açık onay olmadan yapılmaz.

## Mevcut Durum — 30 Temmuz 2026

- Store build ve WACK betiklerinin PowerShell 5.1 sözdizimi doğrulandı.
- Faz 11 kanıtı olmadan Store package üretiminin durduğu doğrulandı.
- WACK'ın yükseltilmemiş oturumda güvenli biçimde durduğu doğrulandı.
- Windows App Certification Kit kurulu.
- `mspdbcmf.exe` eksik.
- Gerçek 30 dakika ve 8 saat testleri henüz tamamlanmadı.
- Çalışma ağacı henüz yayın commit'i olarak temizlenmedi.
- Store adayı, WACK raporu veya Partner Center flight henüz oluşturulmadı.

## Yayın Kapıları

- [ ] 30 dakikalık gerçek yoğun olay testi başarılı
- [ ] 8 saatlik gerçek boşta çalışma testi başarılı
- [ ] Paketli başlangıç, tamamen çıkış ve güncelleme regresyonu başarılı
- [ ] Gerçek cihaz medya, ses, monitör, DPI ve uyku/uyanma regresyonu başarılı
- [ ] Store adayı için proje sahibi onayı kaydedildi
- [ ] Git çalışma ağacı temiz ve yayın commit'i kesinleşti
- [ ] `mspdbcmf.exe` mevcut

Store paket betiği bu koşulların ilk beşini makine tarafından okunabilen Faz 11
kanıt dosyasından doğrular. Örnek şema:

```json
{
  "SchemaVersion": 1,
  "Product": "MiaDock",
  "Version": "1.2.0.0",
  "FullEventSoak": { "Result": "Passed" },
  "FullIdleSoak": { "Result": "Passed" },
  "PackagedLifecycle": { "Result": "Passed" },
  "RealDeviceRegression": { "Result": "Passed" },
  "ApprovedForStoreCandidate": true
}
```

Kanıt dosyası `artifacts/validation/1.2.0/` altında tutulur ve Git'e eklenmez.

## Store Paketi

Yükseltilmemiş normal PowerShell oturumunda:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/release/Build-MiaDockStorePackage.ps1 -Phase11EvidencePath artifacts/validation/1.2.0/phase11-gates.json
```

Betik temiz Git kaynağı, sembol aracı, manifest kimliği, `1.2.0.0` sürümü,
StartupTask, çalışma zamanı varlıkları, MSIX ve Appx sembol paketini doğrular.
`.msixupload`, çıkarılmış WACK MSIX'i, SHA-256 değerleri ve sonuç JSON'u
`artifacts/release/1.2.0/` altında oluşturulur.

## WACK

Aktif kullanıcı oturumundaki yönetici PowerShell penceresinde:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/validation/Invoke-MiaDockWack.ps1 -PackagePath <çıkarılan-msix>
```

- [ ] WACK işlemi hata kodu olmadan tamamlandı
- [ ] Raporda başarısız test yok
- [ ] Bütün uyarılar manuel incelendi
- [ ] Paket SHA-256 değeri Store package özetiyle aynı

## Partner Center Flight

- [ ] Bilinen kullanıcı test grubu doğrulandı
- [ ] Yeni `1.2.0.0 .msixupload` özel flight'a yüklendi
- [ ] Paket doğrulama tablosu yalnız x64 Windows 11 hedefini gösteriyor
- [ ] Türkçe ve İngilizce “Bu sürümdeki yenilikler” metni eklendi
- [ ] Gizlilik politikası bağlantısı ve destek bilgileri güncel
- [ ] Sertifikasyon notları eklendi
- [ ] Flight sertifikasyonu tamamlandı

## Sertifikasyon Notları Taslağı

MiaDock, Windows 11 üzerinde always-on-top bir sistem dock'u olarak çalışır.
`runFullTrust`; Win32 overlay, sistem tepsisi, Core Audio, monitör/DPI ve
uygulama yaşam döngüsü entegrasyonları için kullanılır. StartupTask yalnız
kullanıcı Ayarlar'dan etkinleştirdiğinde istenir. Bildirim erişimi yalnız
bildirim modülü kullanıcı tarafından etkinleştirildiğinde Windows izin akışı
üzerinden talep edilir. Bildirim içeriği ve kişisel medya bilgileri teknik
loglara yazılmaz. Uygulama yönetici yetkisi istemez, telemetri ve özel
güncelleyici kullanmaz.

## Flight Sonrası Regresyon

- [ ] Store `1.1.1.0 → 1.2.0.0` güncellemesi
- [ ] Ayarların ve onboarding durumunun korunması
- [ ] Windows başlangıcı ve Görev Yöneticisi durumu
- [ ] Tray menüsünden tamamen çıkış ve kalan süreç kontrolü
- [ ] Store güncelleme denetimi
- [ ] Medya, ses karıştırıcısı, bildirim, alarm ve Odak profilleri
- [ ] Uyku/uyanma, Explorer, monitör ve DPI

Flight başarısızsa genel gönderim oluşturulmaz. Paket veya sürüm numarası
proje sahibi kararı olmadan değiştirilmez.
