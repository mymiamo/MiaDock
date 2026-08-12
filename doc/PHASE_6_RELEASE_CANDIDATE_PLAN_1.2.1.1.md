# MiaDock 1.2.1.1 — Faz 6 Sürüm Adayı Planı

## Hedef

Faz 0–5 değişikliklerini `1.2.1.1` sürümü altında doğrulamak, Microsoft Store'a yüklenebilir x64 `.msixupload` dosyasını üretmek ve paketin kimlik/içerik bütünlüğünü kontrol etmek.

## Uygulama sırası

1. Store güncelleme denetiminin UI-thread, ağ ve manuel yeniden deneme yollarını düzelt.
2. Paket, assembly, dosya ve uygulama manifest sürümlerini `1.2.1.1` yap.
3. Türkçe ve İngilizce sürüm notlarını hazırla.
4. Release x64 tam test, uygulama derleme ve başlangıç smoke testini çalıştır.
5. Unsigned Store MSIX ve sembol paketini içeren `.msixupload` üret.
6. Paket içindeki manifest kimliği, publisher, sürüm, StartupTask ve zorunlu varlıkları doğrula; SHA-256 özeti üret.
7. WACK çalıştırılabiliyorsa çalıştır; yükseltilmiş oturum veya ortam bağımlılığı varsa açık sürüm kapısı olarak raporla.

## Sürüm kapıları

- Paket sürümü ve assembly sürümü: `1.2.1.1`.
- Ayar şeması: `18`.
- Release x64: sıfır hata ve yeni uyarı yok.
- Tüm testler: sıfır başarısız.
- `.msixupload`: x64 MSIX ve `.appxsym` içeriyor.
- Paket kimliği: `mymiamo.net.MiaDock`.
- Publisher: `CN=FAC642FD-F594-4E90-B1DB-38F94EA36BCA`.
- Partner Center'a otomatik yükleme yapılmayacak.

## Bilinen manuel kapılar

Faz 5 sonuç raporunda bekleyen gerçek süreli oyun/medya, yoğun etkileşim ve sistem geçiş testleri paket üretimini teknik olarak engellemez; ancak genel Store yayın kararından önce tamamlanmalıdır.
