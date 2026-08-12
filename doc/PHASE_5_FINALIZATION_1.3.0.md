# Faz 5 sonuç raporu

## 1. Faz özeti

* Tüm sürüm yüzeyleri `1.3.0.0` yapıldı; Store revision alanı sıfırdır.
* README, ROADMAP, Türkçe/İngilizce sürüm notları, Store teslim belgesi ve doğrulama betikleri güncellendi.
* 600 otomatik regresyon testi, iki deterministik fullscreen soak testi, ölçekli yoğun olay testi, Release x64 build, paket içeriği ve 30 saniyelik runtime stabilite ölçümü geçti.
* Ses başlarken görülen yerel çökme riski alarm callback yaşam döngüsü ve Core Audio RCW serbest bırakma yollarında giderildi; gerçek bir medya ses oturumu ekleme/çıkarma denemesi geçti.
* Tanılama kayıtları sıra, process/thread korelasyonu, exception zinciri, medya/Core Audio aşamaları ve güvenli hata raporu ZIP özetiyle genişletildi.
* `.msixupload` üretildi ve içindeki tek MSIX/tek appxsym, kimlik, publisher, sürüm, mimari ve StartupTask doğrulandı.
* Tamamlanmayan noktalar: yükseltilmiş WACK, gerçek 30 dakika yoğun etkileşim, 8 saat boşta çalışma ve fiziksel Canva/Bluetooth/pil/çoklu monitör matrisi bu oturumda tamamlanamadı.
* Faz başarılı mı? Otomatik ve paketleme kapsamı başarılı; dış/uzun süreli yayın kapıları nedeniyle faz kısmen tamamlandı.

## 2. Bulunan temel nedenler

* Eski sürüm yüzeyleri proje, package manifest, app manifest, arama anahtarları, testler ve release betiklerinde `1.2.2.0` olarak dağınıktı. Tek bir yüzeyi değiştirmek Store/uygulama sürüm uyuşmazlığı oluşturabilirdi.
* Partner Center’ın önceki reddi manifest revision değerinin sıfır olmamasından kaynaklanıyordu. `1.3.0.0` bu kurala uygundur.
* Önceki duplicate-package hatası aynı submission içinde aynı package family/version/architecture’ın birden fazla upload tarafından sunulmasından kaynaklanıyordu. Yeni upload kendi içinde yalnız bir x64 paket içerir; Partner Center’da eski duplicate satırların kaldırılması gerekir.
* Package build sonrasında unpackaged bin çıktısı package özellikleriyle kirlenebildiği için final runtime build tekrar `BuildMsix=false` ile iki-pass XAML derlemesinden geçirildi.
* İlk Store build denemesinde orphan XAML compiler ara DLL’i kilitledi; build server kapatılıp paralellik kapatılarak temiz seri package build yapıldı.
* Single-instance kaydının normal shutdown’da açıkça bırakılmaması için `UnregisterKey` dispose yoluna eklendi.
* Alarm `MediaEnded` callback’i içinde aynı `MediaPlayer` nesnesi doğrudan replay/dispose ediliyordu. Yerel medya callback çerçevesi çözülmeden yapılan bu işlem native çökme riski taşıyordu; callback işi artık ThreadPool’a ertelenir.
* Core Audio oturumları yeniden bağlanırken `FinalReleaseComObject`, callback veya başka RCW kullanımı sürerken nesneyi zorla geçersiz kılabiliyordu. Event’ler kaldırıldıktan sonra yaşam süresi CLR’a bırakıldı.
* İlk ayrıntılı ölçer kaydı eşleşen oturum yokken 500 ms yeniden denemeye bağlanmıştı. Gerçek çalıştırmada kayıt fırtınası tespit edildi; kontrol noktası yalnız gerçek medya/cihaz değişiminde bir kez yazılacak ve boş eşleşme yeniden denemesi iki saniyeye çekilecek şekilde düzeltildi.

## 3. Mimari değişiklikler

* Yeni kullanıcı özelliği eklenmedi; Faz 1–4 çıktıları ortak doğrulandı.
* Sürüm kaynakları package, assembly, file, informational ve app manifest seviyesinde eşitlendi.
* Release/package scriptlerinin beklenen sürüm ve artifact dizinleri 1.3.0.0’a taşındı.
* Runtime doğrulama çıktısı CPU, working set, private memory, handle, thread ve responding örneklerini JSON kanıtı olarak üretir.
* `WindowsSingleInstanceService.Dispose`, event unsubscribe sonrasında explicit key unregister yapar ve state’i sıfırlar.
* Teknik log girdileri monoton sıra numarası, process/thread kimliği ve mesaj içermeyen güvenli exception type/HRESULT zinciri taşır.
* Log ZIP formatı v2; runtime/OS/mimari manifesti, olay özeti, son 250 olay zaman çizelgesi ve iki dilli hata bildirim rehberi üretir.
* Media session, Core Audio rebind, audio meter binding ve timer alarm başlangıcında native çağrıdan önce en fazla bir saniyelik güvenli disk checkpoint’i alınır.

## 4. Değiştirilen dosyalar

* `src/MiaDock.App/MiaDock.App.csproj`: Version, AssemblyVersion, FileVersion, InformationalVersion 1.3.0.0.
* `src/MiaDock.App/Package.appxmanifest` ve `app.manifest`: Store/application identity sürümü 1.3.0.0.
* `src/MiaDock.App/ViewModels/SettingsViewModel.cs`: Hakkında sayfası fallback sürümü.
* `src/MiaDock.App/SettingsWindow.xaml.cs`: sürüm arama anahtarı.
* `scripts/release/Build-MiaDockStorePackage.ps1`: 1.3.0.0 kimlik ve artifact yolu.
* `scripts/validation/Invoke-MiaDockReleaseValidation.ps1`, `Invoke-MiaDockRuntimeStability.ps1`, `Invoke-MiaDockSoakTest.ps1`, `Invoke-MiaDockWack.ps1`: 1.3.0.0 doğrulama hedefleri.
* `README.md`, `ROADMAP.md`, `doc/RELEASE_NOTES_1.3.0.md`: ürün ve sürüm belgeleri.
* `doc/STORE_SUBMISSION_1.3.0.0.md`: tek upload dosyası, hash ve duplicate-package temizleme talimatı.
* `src/MiaDock.Platform.Windows/Lifecycle/WindowsSingleInstanceService.cs`: explicit unregister yaşam döngüsü.
* `src/MiaDock.Platform.Windows/Time/WindowsTimerAlarmPlayer.cs`: native callback dışına ertelenen replay/dispose ve alarm checkpoint logları.
* `src/MiaDock.Platform.Windows/Audio/AudioSessionHandle.cs`, `WindowsSystemActivityService.cs`, `WindowsMediaAudioMeterService.cs`: zorla RCW release kaldırma, güvenli unregister, Core Audio aşama logları ve retry/log fırtınası sınırı.
* `src/MiaDock.Platform.Windows/Media/WindowsMediaSessionService.cs`: session/topology/snapshot/transport aşama ve hata kayıtları.
* `src/MiaDock.Core/Logging/TechnicalLogEntry.cs`, `TechnicalEventIds.cs`, `src/MiaDock.Platform.Windows/Logging/*`: korelasyon alanları, güvenli allowlist ve v2 hata raporu arşivi.
* `src/MiaDock.App/ViewModels/DiagnosticsViewModel.cs`, `Views/Settings/DiagnosticsSettingsPage.xaml`, `Services/AppExceptionCoordinator.cs`: ayrıntı görünümü ve fatal olaylarda hızlı flush.

## 5. Ayar değişiklikleri

* Faz 5’te yeni kalıcı kullanıcı alanı eklenmedi.
* Genel şema 19’da, Focus alt şeması 4’te kaldı.
* Faz 1–4 ayarlarının varsayılan, sınır, enum ve canlı uygulama davranışları regresyon testlerinden geçti.
* Sürümleme ayar dosyasını değiştirmez veya sıfırlamaz.

## 6. Migrasyon

* Şema 18 → 19 tek yükseltme olarak korunur.
* Eski CornerRadius dört köşeye aktarılır; edge margin clamp edilir; bilinmeyen fullscreen enum güvenli varsayılana döner.
* Eski fullscreen Enabled değeri yeni davranışa eşlenir.
* Eksik Focus IsEnabled alanı `true` olur; kapalı state active profile etkisini temizler, profilleri korur.
* Serialize/deserialize, eksik/bozuk alan, reset ve ikinci kez normalize testleri geçti.

## 7. Event ve kaynak yaşam döngüsü

* Animasyon cancellation, fullscreen hook/timer, interaction token, pil event/retry, Bluetooth radio/watcher, media lease/CTS/semaphore ve Focus watcher/timer dispose yolları test edildi.
* Single-instance key normal dispose sırasında açıkça unregister edilir.
* Alarm callback’i yalnız işi kuyruğa alır; replay ve dispose native callback döndükten sonra yürür.
* Core Audio session event’leri best-effort kaldırılır, geç callback yalnız coalesced rebind ister ve RCW zorla serbest bırakılmaz.
* Medya/audio native çağrı öncesi checkpoint’ler diske flush edilir; checkpoint hatası kullanıcı akışını veya recovery’yi durdurmaz.
* Store update coordinator ve async hata yolları önceki 1.2 düzeltmeleriyle birlikte regresyon testlerinden geçti.
* Final çalışan uygulama kullanıcı oturumunda başlatıldı ve `Responding=True` doğrulandı.

## 8. Testler

* Core: 299/299 başarılı.
* Windows Platform: 150/150 başarılı.
* WinUI kaynak/erişilebilirlik: 151/151 başarılı.
* Toplam: 600/600, skip yok.
* Yeni alarm testi `MediaEnded_CallbackDefersReplayUntilNativeCallbackHasReturned`: callback içinde replay/dispose yapılmadığını doğruladı.
* `DeferredMediaEnded_DoesNotReplayAfterStopDisposedTheSession`: Stop ile kuyruktaki MediaEnded callback yarışında disposed session’ın yeniden oynatılmadığını doğruladı.
* Audio yaşam döngüsü testi Core Audio servislerinde `FinalReleaseComObject` bulunmadığını ve rebind checkpoint’ini doğruladı.
* Log testleri v2 ZIP içindeki manifest, timeline, event summary, hata raporu rehberi ve process/thread/sıra alanlarını doğruladı.
* Fullscreen virtual soak: 2/2; 1000 giriş/çıkış cycle ve iki sanal saat unchanged polling başarılı.
* Ölçekli event soak: 30 dakikalık senaryonun 0.001 ölçeği başarılı; bu tam süreli kabul yerine geçmez.
* Runtime 30 saniye: geçti; ortalama CPU %0.263, working-set büyümesi 2.29 MB, private memory büyümesi 0.80 MB, handle +41, thread -4, not-responding örneği 0.
* Package archive: tek MSIX, tek appxsym, doğru identity/publisher/version/x64 ve StartupTask; başarılı.
* `git diff --check`: whitespace hatası yok; yalnız CRLF dönüşüm uyarıları var.

## 9. Çalıştırılan komutlar

* Üç Release test projesi: exit 0, toplam 600/600.
* `dotnet build ... -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64`: iki-pass WinUI build sonunda 0 hata/0 uyarı.
* StoreUpload MSBuild: ilk deneme orphan `XamlCompiler` kilidi nedeniyle CS2012 ile başarısız; build server shutdown ve serial build sonrası başarılı.
* Package içeriği PowerShell ZipArchive ile okundu: başarılı.
* `Invoke-MiaDockSoakTest.ps1 -Scale 0.001 -AllowScaled`: Core scaled event soak başarılı; fullscreen soak ayrıca 2/2 çalıştırıldı.
* `Invoke-MiaDockRuntimeStability.ps1 -DurationSeconds 30`: interactive desktop izniyle başarılı.
* WACK preflight: appcert mevcut, oturum yönetici değil; test çalıştırılmadı.
* Gerçek ses testi: WMP COM ile üç saniyelik ringtone oynatılıp durduruldu; Core Audio session sayısı 9→11→9 değişti, süreç `Responding=True` kaldı ve Warning/Error/Critical oluşmadı.
* İlk ses denemesinde ölçer log fırtınası gözlendi ve düzeltildi; son build’de ölçer başlangıç/bitiş toplam iki kayıtla sınırlı kaldı.

## 10. Manuel doğrulama

* Ses probe uygulanan unpackaged Release 1.3.0.0 kullanıcı masaüstü oturumunda PID 43912 ile çalıştı; üç saniyelik gerçek medya sesi ve dört saniyelik kapanış beklemesinden sonra süreç açık/yanıt verir kaldı, hata seviyesinde kayıt oluşmadı.
* Son 600-test build’i ayrıca PID 29356 ile açıldı; pencere başlığı MiaDock, `Responding=True`, başlangıç loglarında Warning/Error/Critical=0 ve kullanıcı için açık bırakıldı.
* 30 saniyelik read-only runtime örneklemesinde süreç kapanmadı veya yanıt vermeme üretmedi.
* Store paketi uygulamaya yüklenmedi ve Partner Center’a gönderilmedi.
* Kullanıcının Canva/WebView2, fiziksel Bluetooth, pil adaptörü, uyku/uyanma, dört dock kenarı, çoklu monitör/DPI ve tüm fullscreen modlarını nihai rapordaki listeyle denemesi gerekir.

## 11. Performans ve kararlılık

* Runtime kısa örneği tüm eşikleri karşıladı.
* Fullscreen polling yalnız EdgeReveal + aynı monitör fullscreen durumunda 200 ms aralıkla çalışır; inactive durumda durur.
* Media refresh queue coalesced, session erişimi serialized ve eski generation yeni session’ı bloke etmiyor.
* Audio meter boş eşleşmede iki saniyelik bounded retry uygular; ayrıntılı checkpoint yalnız gerçek rebind sebebinde bir kez yazılır.
* Ses testi sırasında Core Audio rebind olayları coalesced worker üzerinde tamamlandı; UI thread’e doğrudan COM işi taşınmadı.
* Pil retry üç denemeyle, Bluetooth watcher radio On ile sınırlıdır.
* Animasyon ve timer güncellemeleri tam görünüm rebuild etmez.

## 12. Bilinen sınırlamalar

* WACK yönetici PowerShell gerektirdiği için çalıştırılmadı; Store submission öncesi zorunlu manuel kapıdır.
* Tam 30 dakika yoğun olay ve 8 saat boşta soak bu oturumda beklenmedi; yalnız ölçekli deterministik test ve 30 saniyelik gerçek process ölçümü var.
* Gerçek Canva/WebView2 ile native crash üretimi, uygulama içi zamanlayıcı alarmının beş gerçek tekrarının kullanıcı tarafından doğrulanması, farklı Bluetooth donanımları, pil ve sleep/resume fiziksel doğrulama gerektirir.
* Çoklu monitör ve farklı DPI cihaz matrisi otomatik ortamda fiziksel olarak sürülmedi.
* Çalışma ağacı kullanıcıya ait önceki 1.2 değişiklikleri dahil dirty durumdadır; commit/push/PR/tag yapılmadı.

## 13. Sonuç

Faz kısmen tamamlandı. Kod, otomatik test, Release build, runtime kısa doğrulama ve Store upload paketi hazırdır; WACK, tam süreli soak ve gerçek donanım/Canva testleri yayın öncesi açık kapılardır.
