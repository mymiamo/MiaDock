# MiaDock Product Roadmap

Bu yol haritası mevcut geliştirme durumunu ve yayın öncesi kalan doğrulamaları
gösterir. Sürüm numaraları yalnız proje sahibi tarafından kesinleştirilir.

## 1.5.3.0 — Clipboard Peek color and text info

- Clipboard Peek: HEX / RGB / HSL dönüşümü ve düz metinde kelime + karakter sayısı.

## 1.5.2.0 — Tozpembe overlay fix

- Tozpembe: yuvarlak ada dışında şeffaf pencere; HWND köşelerinde beyaz kare yok.

## 1.5.1.0 — Tozpembe theme

- Tozpembe: tozpembe yüzey, koyu ve okunaklı yazı.

## 1.5.0.0 — Device Hub, Clipboard Peek, Sounds

- Device Hub: Bluetooth, ses çıkışı ve çıkarılabilir depolama tek yerde.
- Clipboard Peek: oturum içi kopyalama geçmişi ve gizlilik koruması.
- Saat başı dock bildirimi.
- Ağ, pil, cihaz ve saat başı için isteğe bağlı kısa bildirim sesleri.
- Ayarlar → Kişiselleştir → Notification Sounds.

## 1.4.0.0 — Dock Community Toolkit Animations

- `CommunityToolkit.WinUI.Animations` ile dock içerik opacity / scale / translation.
- Island bounds animasyonu mevcut Composition + `IslandBoundsAnimator` ile kalır.
- Durum geçişlerinde Toolkit crossfade; modül ve içerik yenilemede slide / pulse.
- Ayarlar SettingsControls migrasyonu bu sürümde yok (sonraki aday).

## 1.3.0.0 — Tamamlanan Geliştirme Kapsamı

- Dock’un bağlı olduğu ekran kenarı için canlı uygulanabilen mesafe ayarı.
- Dört köşe için bağımsız yuvarlaklık ve köşeleri bağlama seçeneği.
- Tam ekranda tamamen gizle, yalnız bildirim, kenarda gizle ve görünür tut davranışları.
- Sağ tık menüsü ve hızlı dock etkileşimlerinde güvenli açık kalma/kapanma yaşam döngüsü.
- Pil varlığını güç besleme durumundan ayıran güvenilir algılama ve uyku/uyanma yenilemesi.
- Bluetooth radyo aç/kapat durumunu izleyen, eski cihaz önbelleğini temizleyen watcher yaşam döngüsü.
- Hızlı Canva/WebView2 medya değişimlerinde eski oturum erişimini iptal eden ve sıralayan güvenli medya hattı.
- Profilleri koruyarak Odak etkilerini, otomasyonlarını ve zamanlayıcılarını tamamen durduran ana ayar.
- Ayar şeması 19, Türkçe/İngilizce metinler ve erişilebilirlik iyileştirmeleri.

### Kalan yayın doğrulamaları

- Gerçek Canva/WebView2, Bluetooth donanımı, pil ve uyku/uyanma manuel matrisi.
- 30 dakikalık yoğun etkileşim ve uzun boşta çalışma gözlemi.
- WACK ve özel Microsoft Store flight doğrulaması.

## 1.2.1.0 — Önceki Yayın Kapsamı

### Tamamlanan ürün çalışmaları

- Compact, hover, expanded ve notification durumlarında ortak tasarım sistemi.
- OLED Black, Neutral Frosted Glass ve Adaptive Fluent temaları.
- Gelişmiş hareket profilleri, güvenli animasyon iptali ve azaltılmış hareket desteği.
- Sistem durumu, Odak hızlı eylemleri, medya ve tam genişlikte modül menüsü içeren ana expanded dock.
- Çalışma, Oyun, Uyku ve Rahatsız Etmeyin hazır Odak profilleri.
- Ad, simge, renk, süre, program ve uygulama tetikleyicileri olan özel profiller.
- Profil başına dock görünürlüğü, modül filtresi, olay önceliği ve hassas içerik kuralları.
- Kompakt, hover ve genişletilmiş görünümde Odak durumu ve hızlı kapatma.
- Windows Odak ayarlarına belgelenmiş `ms-settings:quiethours` kısayolu.
- Windows ana sesi için olay tabanlı geçici görünüm ve kontroller.
- Görünürken ölçüm yapan uygulama bazlı ses karıştırıcısı.
- Türkçe–İngilizce modül açıklamaları ve aşamalı izin akışı.
- Yenilenen tray menüsü, ağ hız ölçümü ve genişletilmiş dock hiyerarşisi.
- Apple benzeri tema için adaptif metin, ikon ve kontrol kontrastı.

MiaDock, belgelenmemiş yöntemlerle Windows Rahatsız Etmeyin durumunu
değiştirmez. Odak profilleri yalnızca MiaDock davranışını yönetir.

### Önceki sürümden devreden doğrulamalar

- Paketli uygulamada Windows başlangıcı, tamamen çıkış ve Store güncelleme kontrolü.
- Spotify, Apple Music, YouTube ve birden fazla medya oturumuyla regresyon testi.
- Ses aygıtı değişimi, desteklenmeyen ses oturumu ve gerçek uygulama karıştırıcısı testi.
- Uyku/uyanma, Explorer yeniden başlatma, çoklu monitör ve farklı DPI senaryoları.
- Türkçe–İngilizce, yüksek kontrast ve bütün temalarda görsel kontrol.
- 30 dakikalık yoğun olay ve gerçek 8 saatlik boşta çalışma testleri.
- Temiz MSIX upload üretimi, WACK ve özel Store flight.

## 1.2 Sonrası Aday Çalışmalar

- Dock içinden belgelenmiş yöntemle çıkış aygıtı seçimi.
- Ses karıştırıcısında daha ayrıntılı aygıt ve destek durumu açıklamaları.
- Kullanıcı geri bildirimlerine göre yeni yerleşik modüller ve Odak otomasyonları.
- Performans ölçümlerinin tanılama ekranında gizlilik korumalı özeti.

## Yayın Koşulları

Yeni paket; Release derlemesi, otomatik testler, gerçek cihaz regresyonları ve
uzun çalışma testleri tamamlanmadan Microsoft Store'a gönderilmez. Genel Store
yayını ayrıca açık proje sahibi onayı gerektirir.

## Faz 7 — Sürüm Adayı ve Store Flight Hazırlığı

- Faz 7 doğrulama kanıtı, temiz Git kaynağı ve sembol paketleme aracı zorunlu giriş
  kapılarıdır.
- Store adayı `.msixupload`, WACK paketi ve SHA-256 kanıtları tekrar
  üretilebilir betiklerle hazırlanır.
- WACK aktif kullanıcı oturumunda yönetici olarak çalıştırılır.
- Paket önce yalnız bilinen kullanıcı grubuna ait özel flight'a gönderilir.
- Flight regresyonu tamamlanmadan genel Store yayını yapılmaz.
