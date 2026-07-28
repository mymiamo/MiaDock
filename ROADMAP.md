# MiaDock Product Roadmap

Bu yol haritası, MiaDock’un Microsoft Store sürümünden sonraki planlanan ürün geliştirmelerini açıklar. Tarihler; kararlılık testleri, Windows API kısıtları ve kullanıcı geri bildirimlerine göre belirlenecektir.

## 1.1.x — Kararlılık ve Görsel Kalite

- Dock çevresindeki siyah HWND alanını tüm temalarda ve DPI değerlerinde kaldır.
- Büyük köşe yarıçaplarında yumuşak, tutarlı kapsül geometrisi sağla.
- Ana dock saatini 12/24 saat, saniye, tarih biçimi ve hafta günü seçenekleriyle kişiselleştirilebilir yap.
- Uyku/uyanma, monitör değişimi, Explorer yeniden başlatma ve uzun süreli kullanım testlerini tamamla.
- Donma, medya geç güncellenmesi ve kaynak yaşam döngüsü hatalarını yayın engelleyici kabul et.

## 1.2 — Kişiselleştirilebilir Odak Modları

iPhone’daki Odak yaklaşımından esinlenen, ancak MiaDock içinde çalışan profil sistemi eklenecek.

- Çalışma, Oyun, Uyku ve Rahatsız Etme hazır profilleri.
- Kullanıcının ad, simge, renk ve süre belirleyerek özel profil oluşturabilmesi.
- Manuel, süreli ve haftalık programa bağlı etkinleştirme.
- Tam ekran uygulama veya seçili uygulama açıldığında isteğe bağlı otomasyon.
- Profil başına dock görünürlüğü, izin verilen modüller, hassas içerik görünürlüğü ve bildirim önceliği.
- Kompakt görünümde aktif profil simgesi; geniş görünümde kalan süre ve hızlı kapatma.
- Profil değişikliklerinin yerel olarak saklanması ve yeniden başlatmada geri yüklenmesi.

MiaDock, belgelenmemiş yöntemlerle Windows Rahatsız Etme durumunu değiştirmeyecek. Windows tarafından güvenilir bir genel API sağlanmıyorsa profiller yalnızca MiaDock davranışını yönetecek ve Windows Odak ayarlarına güvenli bir kısayol sunacak.

## 1.3 — Windows Ses Görünümü

Windows ana ses seviyesi değiştiğinde dock, Windows 11 ile uyumlu geçici bir ses kartı gösterecek.

- Varsayılan çıkış aygıtı, ses yüzdesi ve sessiz durumu.
- Ses tuşları, görev çubuğu veya başka uygulamalardan yapılan değişikliklere olay tabanlı tepki.
- Dock üzerinden ana ses sürgüsü, sessize alma ve çıkış aygıtı seçimi.
- Ses değişiminden sonra ayarlanabilir otomatik kapanma süresi.
- Tam ekran için sade ve kontrollü görünüm seçenekleri.
- Windows’un kendi ses panelini bastırmama veya değiştirmeme.

Teknik temel, [`IAudioEndpointVolume`](https://learn.microsoft.com/windows/win32/coreaudio/endpointvolume-api), `IAudioEndpointVolumeCallback` ve `IMMNotificationClient` olacaktır.

## 1.4 — Ses Karıştırıcısı

Genişletilmiş ses modülü, aktif Windows ses oturumlarını uygulama bazında yönetecek.

- Aktif uygulamaları simge, görünen ad, ses seviyesi ve sessiz durumuyla listeleme.
- Uygulama başına ses sürgüsü ve sessize alma.
- Varsayılan çıkış aygıtını değiştirme ve aygıt bağlantı olaylarına güvenli tepki.
- Oturum açılma/kapanma olaylarında listeyi canlı güncelleme.
- Gerçek ses etkinliği göstergesi; yalnız görünürken sınırlı sıklıkta ölçüm.
- Sistem sesleri, eşleştirilemeyen süreçler ve desteklenmeyen özel oturumlar için açık durum mesajları.
- Exclusive-mode ses kullanan uygulamalarda desteklenmeyen kontrolleri devre dışı gösterme.

Uygulama oturumları `IAudioSessionManager2` ile listelenecek ve desteklenen oturumlar `ISimpleAudioVolume` üzerinden yönetilecektir.

## Yayın Koşulları

Her özellik Türkçe ve İngilizce, klavye, dokunma, ekran okuyucu, yüksek kontrast ve %100–%200 DPI ile test edilmelidir. Boşta gereksiz polling yapılmamalı; COM callback, timer ve event handler yaşam döngüleri sızıntısız olmalıdır. Yeni ana sürüm ancak Release derlemesi, otomatik testler ve en az sekiz saatlik boşta çalışma testi başarılı olduğunda Store paketine alınacaktır.
