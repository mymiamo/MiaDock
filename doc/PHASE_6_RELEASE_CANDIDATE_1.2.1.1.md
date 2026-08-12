# MiaDock 1.2.1.1 — Faz 6 Sürüm Adayı Sonucu

## Sonuç

`1.2.1.1` x64 Microsoft Store paketi üretildi. `.msixupload` arşivi MSIX ve sembol paketini içeriyor; paket kimliği, publisher, sürüm, StartupTask ve zorunlu uygulama varlıkları doğrulandı. Partner Center'a otomatik yükleme yapılmadı.

## Store güncelleme düzeltmesi

- `StoreContext` güncelleme sorgusu UI dispatcher üzerinden başlatılıyor. Otomatik kontrolün gecikme sonrasında arka plan thread'inden çağrı yaparak `0x80070578` üretmesi engellendi.
- Kullanıcının “Güncellemeleri denetle” eylemi artık yakın zamanda otomatik kontrol yapılmış olsa bile gerçek Store sorgusunu başlatıyor.
- Otomatik sorgu aralığı Microsoft Store'un 30 dakikalık sorgu sınırıyla hizalandı; manuel sorgu uygulama içi bekleme tarafından engellenmiyor.
- NCSI internet durumunun yanlış negatif üretmesi Store sorgusunu artık baştan engellemiyor. Sorgu deneniyor; gerçek ağ hataları `Offline`, diğer Store hataları `Failed` olarak gösteriliyor.
- Sistem saatinin geriye alınması veya gelecekte bir son-kontrol zamanı kalması sınırsız başlangıç gecikmesi oluşturmuyor.
- Store sorgusu ve Store sayfasını açma işlemi dispatcher reddi, iptal ve istisna sınırlarıyla korunuyor.

## Sürüm bilgileri

- Package manifest: `1.2.1.1`.
- Assembly version: `1.2.1.1`.
- File version: `1.2.1.1`.
- Product/informational version: `1.2.1.1`.
- Ayar şeması: `18`.

## Doğrulama

- Core: 273/273.
- Windows platform: 128/128.
- WinUI: 141/141.
- Toplam: **542/542 başarılı**.
- Release x64 uygulama derlemesi: **0 uyarı, 0 hata**.
- StoreUpload derlemesi: **0 uyarı, 0 hata**.
- Başlangıç smoke testi: başarılı.
- NuGet doğrudan ve transit bağımlılık taraması: bilinen güvenlik açığı bulunmadı.
- `git diff --check`: içerik hatası yok; yalnız çalışma kopyasının LF/CRLF bildirimleri var.

## Teslim paketi

- Dosya: `artifacts/release/1.2.1.1/store-upload-final/package/MiaDock.App_1.2.1.1_x64.msixupload`
- SHA-256: `364FDEB76FF13203FEDD94FB31CD206EBFD083784578D8F96BECD541B9B55ECF`
- İçerik: `MiaDock.App_1.2.1.1_x64.msix` ve `MiaDock.App_1.2.1.1_x64.appxsym`.
- Kimlik: `mymiamo.net.MiaDock`.
- Publisher: `CN=FAC642FD-F594-4E90-B1DB-38F94EA36BCA`.
- Mimari: x64.

## Açık sürüm kapıları

- WACK başlatıldı ancak mevcut PowerShell oturumu yönetici yetkili olmadığı için test çalıştırılmadan durdu. Çıkarılmış MSIX, yükseltilmiş oturumda kullanılmak üzere `artifacts/release/1.2.1.1/store-upload-final/wack/` altındadır.
- Çalışma ağacı Faz 0–6 değişikliklerini içerdiği ve henüz yayın commit'i oluşturulmadığı için paket özeti `WorkingTreeDirty=true` ve `ReleaseEvidence=Bypassed` olarak açıkça işaretlendi.
- Faz 5'in iki saat gerçek oyun/medya, 30 dakika yoğun kullanım, DirectX/exclusive fullscreen, çoklu monitör ve uyku/uyanma manuel kapıları henüz tamamlanmadı.

Bu açık kapılar `.msixupload` dosyasının yapısal olarak oluşturulmasını engellemez; genel Store yayını öncesinde tamamlanmaları gerekir.
