# MiaDock 1.2.1.1 - Faz 3 Global Kısayollar

Tarih: 2 Ağustos 2026

## Sonuç

Faz 3 tamamlandı. Global kısayollar satır bazında durum bildiriyor, geçersiz ve yinelenen kombinasyonlar kalıcı ayara yazılmadan reddediliyor, değişiklikler uygulama yeniden başlatılmadan Windows'a yeniden kaydediliyor ve native kayıt/komut hataları uygulamayı kapatmadan teknik loga aktarılıyor.

## Uygulanan değişiklikler

- Her kısayol için `Etkin`, `Devre dışı`, `Çakışıyor` ve `Geçersiz` durumları eklendi.
- Her satıra temizleme ve varsayılan atamaya döndürme eylemleri; sayfaya bütün varsayılanları geri yükleme eylemi eklendi.
- Önerilen, geçerli ve benzersiz varsayılanlar tanımlandı:
  - Dock'u göster/gizle: `Ctrl + Alt + Shift + D`
  - Dock'u genişlet/küçült: `Ctrl + Alt + Shift + E`
  - Sonraki modül: `Ctrl + Alt + Shift + N`
  - Medyayı oynat/duraklat: `Ctrl + Alt + Shift + P`
  - Zamanlayıcıyı duraklat/sürdür: `Ctrl + Alt + Shift + T`
- Yinelenen kombinasyon ViewModel katmanında mevcut değer korunarak reddediliyor; savunma amaçlı ayar normalizasyonu da korunuyor.
- F12, Windows tuşu, yalnız değiştirici tuş ve değiştiricisiz kombinasyonlar native API'ye gönderilmeden geçersiz sayılıyor.
- Kısayollar kapalıysa native mesaj penceresi gereksiz yere oluşturulmuyor.
- `RegisterHotKey` başarısızlığı veya native başlatma istisnası `Çakışıyor` durumuna çevriliyor ve uyarı seviyesinde loglanıyor.
- Ayar değişikliği mevcut kayıtları kaldırıp yenilerini aynı oturumda uyguluyor.
- Kısayol olayları `IUiDispatcher` üzerinden UI thread'e aktarılıyor. Dispatcher başarısızlığı ve modül komut hataları izlenen/loglanan güvenli akışlara alındı.
- Kayıt düğmesi, temizleme, varsayılana dönme ve canlı durum metinlerine ekran okuyucu adları/yardım metinleri eklendi.
- Türkçe ve İngilizce başlıklar, açıklamalar, eylemler, durumlar ve açık/kapalı metinleri tamamlandı.
- Yeni kalıcı alan eklenmedi; ayar şeması `18` olarak kaldı.

## Otomatik doğrulama

- `dotnet test MiaDock.sln -c Release -p:Platform=x64 --no-restore`: **519/519 başarılı**
  - MiaDock.Core.Tests: 270/270
  - MiaDock.Platform.Windows.Tests: 112/112
  - MiaDock.WinUI.Tests: 137/137
- Release x64 uygulama derlemesi: **başarılı, 0 uyarı, 0 hata**
- Yeni test kapsamı:
  - önerilen atamaların geçerliliği ve benzersizliği
  - aynı eylem düzenlenirken yanlış pozitif üretmeyen yinelenen kombinasyon denetimi
  - devre dışı ve geçersiz ayarların native kaydı çağırmaması
  - `RegisterHotKey` başarısızlığının istisna üretmeden `Conflict` dönmesi
  - canlı yeniden kaydın önceki native kaydı kaldırması
  - XAML durum, varsayılan eylem ve erişilebilirlik sözleşmeleri
  - UI dispatcher ve izlenen komut yürütme sözleşmeleri

## Gerçek uygulama doğrulaması

Release ayarlar penceresi gerçek masaüstü oturumunda açıldı ve aşağıdaki akışlar doğrulandı:

- Boş atamalar satır bazında `Devre dışı` gösterildi.
- Bütün varsayılanlar tek eylemle yüklendi.
- Global kısayollar açıldığında beş atama da yeniden başlatma olmadan `Etkin` durumuna geçti.
- İkinci eyleme ilk eylemin kombinasyonu girildiğinde değer `Ctrl + Alt + Shift + E` olarak korundu, genel açıklama yinelenen kaydın reddedildiğini söyledi ve satır `Çakışıyor` oldu.
- `Ctrl + F12` denemesi kaydedilmedi ve satır `Geçersiz` oldu.
- UI Automation ağacı her kayıt, temizleme ve varsayılana dönme düğmesini eylem adıyla; durum metnini canlı ve açıklamalı olarak sundu.
- İngilizce görünümde başlıklar, yardım metni, eylemler ve durumlar doğrulandı. Sistem dilinden kalan Türkçe ToggleSwitch durumu ViewModel'e bağlı `On/Off` metinleriyle düzeltildi ve Release XAML derlemesinden geçti.

Çalışma zamanı oturumunda yerel loglarda 0 hata/istisna, Windows Application günlüğünde 0 MiaDock kritik/hata/uyarı kaydı bulundu. Test için değiştirilen dil, başlangıç modu ve kısayol ayarları çalışma sonunda eski değerlerine geri yüklendi; test uygulaması kapatıldı.
