# MiaDock 1.2.1.1 — Tam Ekran Sonrası Dock Kurtarma Düzeltmesi

## Bildirim

Bir uygulama tam ekrandayken kapatıldığında MiaDock'un bazı sistemlerde yeniden görünmediği bildirildi.

## Kök neden

Tam ekran algılama, ön plan veya pencere konumu olayından 100 ms sonra tek ölçüm yapıyordu. Windows'un Direct3D tam ekran bildirim durumu uygulama kapanırken kısa süre gecikebildiğinden, yeni ön plan penceresi yanlışlıkla tam ekran kabul edilebiliyordu. Bu ölçümden sonra başka pencere olayı oluşmazsa eski `FullscreenSnapshot` kalıyor ve dock gizli tutuluyordu.

## Düzeltme

- Normal pencere olaylarına dayalı hızlı algılama korundu.
- Algılanan durum tam ekranken 500 ms aralıklı düşük maliyetli kurtarma ölçümü etkinleştirildi.
- Tam ekran sona erdiği anda kurtarma zamanlayıcısı durduruluyor.
- Yenilemeler mevcut UI dispatcher ve eşzamanlı çağrı korumasından geçmeye devam ediyor.
- Zamanlayıcı algılama servisiyle birlikte dispose ediliyor.

## Regresyon testi

`RecoveryPoll_ClearsStaleFullscreenSnapshotWithoutAnotherWindowEvent` testi şu akışı doğruluyor:

1. İlk ölçüm gecikmiş Windows sinyali nedeniyle tam ekran sonucunu üretir.
2. Yeni bir pencere olayı gönderilmez.
3. Kurtarma ölçümü normal durumu algılar.
4. `StateChanged` sırasıyla `true` ve `false` üretir.
5. Normal duruma dönüldüğünde kurtarma zamanlayıcısı kapanır.

## Doğrulama

- Boş Paint penceresi gerçek Windows oturumunda F11 ile tam ekrana alındı ve tam ekrandayken kapatıldı.
- Paint süreci kapandı, MiaDock süreci çalışmaya devam etti.
- UI yardımcı aracı etkinleşmeyen overlay penceresini bağımsız hedef olarak yakalayamadığından yeniden görünme geçişi deterministik servis testiyle doğrulandı.
- Gerçek test oturumunda 7 bilgi, 0 uyarı ve 0 hata kaydı oluştu.
- `Release x64` uygulama derlemesi: 0 uyarı, 0 hata.
- Tam test sonucu: Core 270/270, Platform Windows 117/117, WinUI 139/139; toplam 526/526 başarılı.
- `git diff --check` içerik hatası vermedi; yalnızca mevcut LF/CRLF çalışma kopyası uyarıları görüldü.

Doğrulama sonunda MiaDock ve test için açılan Paint kapatıldı. Kullanıcı ayarları `Language=0`, `LaunchMode=0`, `HotKeys.IsEnabled=false`, boş kısayol bağları ve `SchemaVersion=18` durumunda bırakıldı.

## Faz 5 sürüm engeli

Bu düzeltme Faz 5'in P0 sürüm engelleyicisi olarak izlenecek. Mevcut 500 ms kurtarma yolu; uzun süreli oyun/medya tam ekranı, Direct3D sinyal gecikmesi, uygulamanın doğrudan kapanması, hızlı mod geçişleri, çoklu monitör ve uyku/uyanma altında yeniden doğrulanacak. Tam kabul matrisi `PHASE_5_STABILIZATION_PLAN_1.2.1.1.md` belgesindedir. Bu kapılar geçilmeden Faz 6 sürüm adayı başlamayacak.

Faz 5'te uygulanan ek sertleştirmeler, 540/540 test sonucu ve gerçek Windows kabul kanıtı `PHASE_5_STABILIZATION_1.2.1.1.md` belgesinde kayıtlıdır.
