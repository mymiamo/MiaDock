# MiaDock 1.2.2.0 Release Notes

`1.2.1.1` geliştirme kapsamı Microsoft Store'un sıfır revizyon zorunluluğu nedeniyle Store uyumlu `1.2.2.0` paket sürümüyle yayımlanır.

## Türkçe

- Tam ekran uygulama veya oyun kapatıldıktan sonra dock'un geri gelmemesine neden olan gecikmiş Windows/Direct3D durum sinyali düzeltildi.
- Uzun süreli tam ekran oturumlarında UI thread yükü, yinelenen durum olayları ve zamanlayıcı birikimi azaltıldı.
- Compact, hover, expanded, bildirim ve modül geçiş animasyonları daha pürüzsüz ve güvenli hale getirildi.
- Zamanlayıcı ve kronometre görünüm yenilemeleri, seçim korunumu, özel süre girişi, alarm ve durum kalıcılığı iyileştirildi.
- Global kısayol durumları, temizleme ve varsayılana döndürme eylemleri eklendi.
- Hakkında sayfasına GitHub, Instagram ve web sitesi bağlantıları eklendi.
- Microsoft Store güncelleme denetiminin UI-thread, manuel yenileme, ağ ve saat kayması hataları düzeltildi.
- Kapanış, modül ayarları, DispatcherQueue ve izlenmeyen görev hata yolları sertleştirildi.

## English

The `1.2.1.1` development scope is released as Store-compatible package version `1.2.2.0` because Microsoft Store requires the revision component to be zero.

- Fixed a delayed Windows/Direct3D signal that could keep the dock hidden after a fullscreen app or game closed.
- Reduced UI-thread work, duplicate state notifications, and timer accumulation during long fullscreen sessions.
- Made compact, hover, expanded, notification, and module transitions smoother and safer.
- Improved timer and stopwatch updates, selection retention, custom duration input, alarms, and persistence.
- Added global shortcut states, clear actions, and restore-default actions.
- Added GitHub, Instagram, and website links to the About page.
- Fixed UI-thread, manual refresh, network, and clock-skew issues in Microsoft Store update checks.
- Hardened shutdown, module settings, DispatcherQueue, and unobserved task failure paths.
