# Faz 3 sonuç raporu

## 1. Faz özeti

* Pil varlığı ve API sağlık durumu birbirinden ayrıldı; `PowerSupplyStatus.NotPresent` kaynaklı yanlış “pil yok” kararı kaldırıldı.
* Bluetooth radyo durumu aktif izlemeye alındı; radyo kapalı/unknown/unavailable durumunda stale cihaz önbelleği güvenli biçimde geçersiz kılındı.
* Faz başarılı mı? Evet. Çözüm genelinde 586/586 test ve son Release build 0 hata/0 uyarı ile geçti.

## 2. Bulunan temel nedenler

* Pil varlığı eski serviste hem `BatteryStatus` hem `PowerSupplyStatus` “NotPresent değil” koşuluna bağlıydı. Güç besleme durumu fiziksel batarya varlığını ifade etmediği halde veto sinyali olarak kullanılıyordu.
* Her pil okuma hatası ya kalıcı `Unavailable` ya da `Faulted` state’e dönüyor, son başarılı veri ile hata türü ayrılmıyordu.
* Uyku/uyanma pil servisine bağlı değildi ve sınırlı retry yoktu.
* Bluetooth yalnız `DeviceWatcher` izliyor, radyo kapanmasını bilmiyordu; watcher cihazları güncellemezse bağlı değerler önbellekte kalıyordu.
* Watcher callback’leri aktif watcher kimliği/generation kontrolü yapmıyordu ve radyo aç-kapa sırasında stale callback yeni state’i ezebilirdi.

## 3. Mimari değişiklikler

* `BatteryAvailabilityState`: Unknown, Available, NotPresent, ApiUnavailable, AccessDenied, TransientError.
* `PowerStatusEvaluator`, fiziksel varlık kararını saf ve deterministic hale getirir; supply status tek başına varlığı etkilemez.
* `WindowsPowerStatusService`, native event kaynağını reader’dan ayırır, resume event’ini izler, generation ile geç callback’i reddeder ve en fazla üç adet 5 saniyelik retry planlar.
* Geçici hatada son başarılı snapshot’ın yüzde/şarj verisi korunur; yalnız availability/state hata olarak işaretlenir.
* `BluetoothRadioState`: On, Off, Unknown, Unavailable.
* `WindowsBluetoothRadioStateProvider`, `Windows.Devices.Radios` üzerinden Bluetooth radyosunu izler.
* `WindowsBluetoothStatusService`, her watcher başlatma/durdurmada generation artırır; callback sender ve generation güncelliğini doğrular.
* Radyo On dışında watcher çalışmaz; cached devices tek boş snapshot’la temizlenir.

## 4. Değiştirilen dosyalar

* DeviceStatus modelleri: yeni batarya availability ve Bluetooth radio enumları; snapshot genişletmeleri.
* `PowerStatusEvaluator.cs`, `WindowsPowerStatusService.cs`: çoklu sinyal, hata sınıflandırma, resume, retry ve dispose güvenliği.
* `IBluetoothRadioStateProvider.cs`, `WindowsBluetoothRadioStateProvider.cs`: Windows radyo izleme adaptörü.
* `BluetoothRadioStatePolicy.cs`, `WindowsBluetoothStatusService.cs`: watcher generation ve cache invalidation.
* `BatteryModuleViewModel.cs`, `BluetoothModuleViewModel.cs`, `BluetoothModule.cs`: kullanıcı durumu ve sahte event önleme.
* `IdleDashboardViewModel.cs`: unknown/unavailable/off durumlarının doğru sunumu.
* `ServiceRegistration.cs`: radio provider DI kaydı.
* `Package.appxmanifest`: `radios` device capability.
* `AppLocalizationService.cs`: yeni Türkçe/İngilizce durum metinleri.
* Core, Platform ve WinUI test dosyaları.

## 5. Ayar değişiklikleri

* Yeni kullanıcı ayarı eklenmedi.
* Kullanıcı metinleri: pil durumu bilinmiyor, pil bilgisi kullanılamıyor, erişilemiyor, geçici okunamadı; Bluetooth kapalı, bilinmiyor, kullanılamıyor.
* Bu state’ler canlı servis snapshot’ından güncellenir; yeniden başlatma gerekmez.

## 6. Migrasyon

* Kalıcı ayar şeması değişmedi; Faz 1’de yükseltilen schema 19 korunur.
* Snapshot modellerindeki yeni parametreler optional/default olduğu için eski test, modül ve serialization çağrıları geriye uyumludur.
* Kullanıcı ayarları ve modül eşikleri etkilenmez.

## 7. Event ve kaynak yaşam döngüsü

* Pil servisi BatteryStatus, PowerSupplyStatus, PowerSourceKind, RemainingChargePercent ve EnergySaver event’lerine bir kez abone olur.
* Resume aboneliği Stop/Dispose sırasında kaldırılır.
* Retry timer tek atımlıdır, önceki timer dispose edilir, en fazla üç kez çalışır ve generation eskiyse sonuç yayımlamaz.
* UI kuyruğuna alınan pil snapshot’ı dispose/generation kontrolünü Apply anında tekrar yapar.
* Bluetooth radyo event’i service Start/Stop ile eşleşir.
* Watcher event’leri Stop sırasında kaldırıldıktan sonra watcher durdurulur; generation önceden artırılır.
* UI kuyruğundaki Bluetooth snapshot’ları monoton publish revision ile stale ise atılır.
* Dispose sonrası radyo ve watcher callback’leri state değiştiremez.

## 8. Testler

* Supply NotPresent + discharging battery: Available, geçti.
* BatteryStatus NotPresent masaüstü: NotPresent, geçti.
* Charging/discharging, yüzde clamp ve enerji tasarrufu: geçti.
* Geçici hata son başarılı pil verisini korur: geçti.
* AccessDenied yanlış “pil yok” üretmez: geçti.
* Resume yeniden okuma ve dispose sonrası callback reddi: geçti.
* Bluetooth Off/Unknown/Unavailable cache invalidation: geçti.
* Radio enum mapping: geçti.
* Hızlı non-On radyo geçişlerinde stale devices gösterilmeme: geçti.
* Dispose sonrası geç radio callback reddi: geçti.
* Radyo kapanmasının sahte disconnect ModuleEvent üretmemesi: geçti.
* Core 295/295, Platform 141/141, WinUI 150/150; toplam 586/586.

## 9. Çalıştırılan komutlar

* Platform testleri ara ilk koşuda test ortamında WinAppSDK PowerManager aktivasyonunun kayıtlı olmaması nedeniyle iki test başarısız oldu; native event kaynağı ayrıştırılarak test ve runtime hata izolasyonu düzeltildi.
* Platform nihai test: 141/141.
* `dotnet test MiaDock.sln -c Release --no-restore`: 586/586, exit 0.
* İlk Release build açık eski süreçlerin DLL kilidi nedeniyle başarısız oldu; süreçler kapatıldı.
* Nihai `dotnet build ... -c Release -p:Platform=x64 --no-restore`: 0 uyarı, 0 hata.

## 10. Manuel doğrulama

* En güncel yerel Release başlatıldı ve dock süreci açık bırakıldı: PID 43888, `Responding=True`, yerel Release yolu.
* İlk `--settings` başlatma denemesi kalıcı süreç bırakmadı; normal dock başlatıldıktan sonra yeniden çağrıda süreç yanıt verdi. Bu davranış Phase 5 başlangıç/tek-instance regresyonunda tekrar incelenecek.
* Bu makinede UI otomasyon backend’i kullanılamadığı için adaptör tak/çıkar, gerçek Bluetooth toggle ve uyku/uyanma donanım adımları otomatik gerçekleştirilemedi.
* Manuel testte adaptör, pil yüzdesi, uyku/uyanma, Bluetooth On/Off/On, bağlı cihaz ve hızlı toggle doğrulanmalıdır.

## 11. Performans ve kararlılık

* Pil hızlı polling kullanmaz; Windows event’leri ve yalnız geçici hatada sınırlı tek-atımlı retry kullanır.
* Bluetooth watcher yalnız radyo On iken çalışır.
* Aynı watcher iki kez başlatılmaz; hızlı toggle generation değiştirir.
* Radyo Off geçişi cihaz başına event üretmez, tek snapshot yayımlar.
* Snapshot UI publish revision eski queued callback’in yeni state’i ezmesini engeller.
* Son çözüm testinde exception, warning veya kaynak sızıntısı sinyali gözlenmedi.

## 12. Bilinen sınırlamalar

* Gerçek pil donanımı, radyo düğmesi ve uyku/uyanma otomatik test ortamında fiziksel olarak değiştirilemedi.
* `Windows.Devices.Radios` erişimi paket capability ve Windows politikasına bağlıdır; erişim yoksa doğru biçimde Unavailable gösterilir.
* Pil API’si Unknown fakat açık bir batarya sinyali vermiyorsa kesin “pil yok” yerine Unknown gösterilir.
* Pil retry aralığı kullanıcı ayarı değildir; güvenli sabit 5 saniye ve üç denemedir.

## 13. Sonuç

Faz tamamlandı ve doğrulandı.
