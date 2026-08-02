# MiaDock Product Roadmap

Bu yol haritası mevcut geliştirme durumunu ve yayın öncesi kalan doğrulamaları
gösterir. Sürüm numaraları yalnız proje sahibi tarafından kesinleştirilir.

## 1.2.1.0 — Yayın Adayı Kapsamı

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

### Yayından önce kalan doğrulamalar

- Paketli uygulamada Windows başlangıcı, tamamen çıkış ve Store güncelleme kontrolü.
- Spotify, Apple Music, YouTube ve birden fazla medya oturumuyla regresyon testi.
- Ses aygıtı değişimi, desteklenmeyen ses oturumu ve gerçek uygulama karıştırıcısı testi.
- Uyku/uyanma, Explorer yeniden başlatma, çoklu monitör ve farklı DPI senaryoları.
- Türkçe–İngilizce, yüksek kontrast ve bütün temalarda görsel kontrol.
- 30 dakikalık yoğun olay ve gerçek 8 saatlik boşta çalışma testleri.
- Temiz 1.2.1.0 MSIX upload üretimi, WACK ve özel Store flight.

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
