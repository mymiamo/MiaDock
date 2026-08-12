# MiaDock 1.2.1.1 Release Notes

## Türkçe

- Tam ekran uygulama veya oyun kapatıldıktan sonra dock'un geri gelmemesine neden olan gecikmiş Windows/Direct3D durum sinyali düzeltildi.
- Uzun süreli tam ekran oturumlarında UI thread yükü, yinelenen durum olayları ve zamanlayıcı birikimi azaltıldı.
- Compact, hover, expanded, bildirim ve modül geçiş animasyonları tek koordinatör altında daha pürüzsüz ve güvenli hale getirildi.
- Zamanlayıcı ve kronometre görünüm yenilemeleri, seçim korunumu, özel süre girişi, alarm ve durum kalıcılığı iyileştirildi.
- Global kısayollara etkin, devre dışı, çakışıyor ve geçersiz durumları; temizleme ve varsayılana döndürme eylemleri eklendi.
- Hakkında sayfasına GitHub, Instagram ve web sitesi bağlantı kartları eklendi.
- Microsoft Store güncelleme denetiminin arka plan thread hatası ve manuel denetimi engelleyen bekleme davranışı düzeltildi.
- Kapanış, modül ayarları, timer kalıcılığı, DispatcherQueue ve izlenmeyen görev hata yolları sertleştirildi.

## English

- Fixed a delayed Windows/Direct3D state signal that could keep the dock hidden after a fullscreen app or game closed.
- Reduced UI-thread work, duplicate state notifications, and timer accumulation during long fullscreen sessions.
- Made compact, hover, expanded, notification, and module transition animations smoother and safely coordinated.
- Improved timer and stopwatch view updates, selection retention, custom duration input, alarms, and state persistence.
- Added active, disabled, conflicting, and invalid global shortcut states, plus clear and restore-default actions.
- Added GitHub, Instagram, and website link cards to the About page.
- Fixed the Microsoft Store update check's background-thread failure and the cooldown that blocked explicit manual checks.
- Hardened shutdown, module settings, timer persistence, DispatcherQueue, and unobserved task failure paths.
