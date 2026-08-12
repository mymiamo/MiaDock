# MiaDock 1.2.1.1 — Microsoft Store Gönderim Notları

## Paket

Bu sürüm için x64 `.msixupload` üretilecek. Dosya Partner Center'ın **Packages** bölümüne manuel olarak yüklenecek; betikler Partner Center'a otomatik gönderim yapmaz.

## Türkçe “Bu sürümdeki yenilikler”

Tam ekran uygulama kapandıktan sonra dock'un gizli kalması düzeltildi. Animasyonlar, zamanlayıcı ve kronometre, global kısayollar, Store güncelleme denetimi ve genel yaşam döngüsü kararlılığı iyileştirildi. Hakkında sayfasına GitHub, Instagram ve web sitesi bağlantıları eklendi.

## English “What's new”

Fixed the dock remaining hidden after a fullscreen app closes. Improved animations, timer and stopwatch behavior, global shortcuts, Microsoft Store update checks, and overall lifecycle stability. Added GitHub, Instagram, and website links to the About page.

## Sertifikasyon notu

MiaDock, Windows 11 için always-on-top bir sistem dock'udur. `runFullTrust`; Win32 overlay, sistem tepsisi, ses, monitör/DPI ve yaşam döngüsü entegrasyonlarında kullanılır. StartupTask yalnız kullanıcı tarafından etkinleştirilir. Bildirim erişimi yalnız bildirim modülü açıldığında Windows izin akışıyla istenir. Uygulama telemetri veya özel güncelleyici kullanmaz; Store güncellemeleri `Windows.Services.Store` üzerinden denetlenir.

## Yayından önce manuel kontrol

- Faz 5'te bekleyen gerçek oyun/medya ve sistem geçiş kapıları tamamlanmalı.
- Partner Center paket doğrulama sonucu ve WACK raporu incelenmeli.
- Mağaza listeleme metinleri, gizlilik ve destek bağlantıları doğrulanmalı.
- Genel yayın veya flight seçimi kullanıcı tarafından yapılmalı.
