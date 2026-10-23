# Dock göstergeleri — native doğrulama

Repo kökünden `scripts/validation/Invoke-MiaDockNativeIndicators.ps1` çalıştırılır.
Gerçek üretim `AudioActivityIndicator` ve `BatteryStatusIcon` XAML/C# kaynakları
ayrı WinUI penceresinde derlenir. Gerçek donanım sağlayıcıları veya MiaDock
başlatılmaz; kullanıcı ayarları okunmaz/yazılmaz. Animasyon tercihi fixture'dır.

Altı çubuğun kompakt alana sığması, sabit merkezleri, gerçek spektrum
alanlarından beslenmesi, azaltılmış harekette son seviyeleri, duraklatıldığında
eşit altı nokta/timer durması ve unload temizliği kontrol edilir. Tiz/bas/vokal
çiftlerinin birbirine karışmaması azaltılmış hareketle doğrudan sınanır. Pil için
100/50/0 dolgu, simge içi yüzde, düşük/kritik/şarj renkleri, şimşek, sınır dışı
ve geçersiz/eksik okuma sınanır. Yüksek kontrast etkinse sabit RGB kontrolleri
atlanır; bu test Windows'un yüksek kontrast ayarını değiştirmez.

Çıktılar: `artifacts/validation/dock-indicators/native.json` ve
`native.json.png`. Görsel, fixture penceresindeki üretim bileşenleridir;
kurulu uygulamanın ekran görüntüsü veya fiziksel DPI test sonucu değildir.
