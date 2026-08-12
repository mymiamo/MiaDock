# MiaDock 1.2.1.1 — Faz 5 Stabilizasyon Planı

## Faz hedefi

Faz 5 yeni özellik eklemeyecek. Faz 0–4 değişikliklerini yaşam döngüsü, uzun süreli kullanım, hızlı etkileşim ve işletim sistemi geçişleri altında kararlı hale getirecek.

Tam ekran sonrası dock'un geri gelmemesi bildirimi bu fazın **P0 sürüm engelleyicisidir**. Aşağıdaki tam ekran kabul kapıları geçilmeden Faz 6 sürüm adayı başlamayacak.

## 5.0 — Başlangıç çizgisi ve gözlemlenebilirlik

- Faz 0–4 boyunca değişen timer, animasyon, event handler, DispatcherQueue ve iptal kaynaklarının sahiplik tablosu çıkarılacak.
- Tam ekran durum geçişleri; algılama nedeni, eski/yeni durum, geçiş gecikmesi ve kurtarma yolunun kullanılıp kullanılmadığıyla loglanacak.
- Pencere başlığı, uygulama yolu veya kullanıcı içeriği loglanmayacak; yalnızca teknik durum ve gerekirse süreç/pencere kimliğinin anonimleştirilmiş değeri tutulacak.
- Tekrarlanan sağlık ölçümleri logu şişirmeyecek; yalnızca geçişler ve oran sınırlı hatalar yazılacak.
- İzlenmeyen `Task` hataları mevcut uygulama hata koordinatörüne güvenli şekilde aktarılacak.
- Başlangıç ölçümü olarak boşta ve tam ekranda CPU, özel bellek, thread, USER/GDI handle ve timer sayıları kaydedilecek.

## 5.1 — P0 tam ekran durum makinesi sertleştirmesi

### Algılama kaynakları

- `EVENT_SYSTEM_FOREGROUND` ve `EVENT_OBJECT_LOCATIONCHANGE` ana hızlı sinyaller olarak korunacak.
- Pencere kapanması/yok edilmesi, küçültme/geri yükleme, görünürlük ve ekran değişimi sinyallerinin eklenmesi değerlendirilecek.
- Pencere boyutları, DWM cloak durumu, standart maximize durumu, monitör sınırları ve `SHQueryUserNotificationState` birlikte değerlendirilecek.
- Direct3D sinyali gecikse bile kapanmış veya artık ön planda olmayan eski tam ekran HWND'si durumu kilitlemeyecek.
- Borderless fullscreen ile yalnızca çalışma alanını kaplayan normal maximize pencere ayrımı korunacak.

### Kurtarma ve uzun süreli çalışma

- Mevcut 500 ms kurtarma ölçümü P0 güvenlik ağı olarak korunacak ve profillenecek.
- Saatlerce açık kalan tam ekranda ölçümler üst üste binmeyecek; aynı anda en fazla bir yenileme bekleyecek.
- Değişmeyen durum UI thread'e ve `StateChanged` abonelerine tekrar gönderilmeyecek.
- Mümkünse değişmeyen sağlık ölçümü arka planda yapılacak; UI thread yalnızca gerçek durum geçişinde kullanılacak.
- Optimizasyon yapılırsa tam ekrandan çıkış üst sınırı kötüleşmeyecek. Olay kaçırılsa veya Windows sinyali gecikse bile dock en geç 1 saniye içinde normal görünürlüğüne dönecek.
- Tam ekran boyunca tek timer örneği kullanılacak; yeniden girişlerde timer, callback, hook veya handle birikmeyecek.
- Algılama hatası son başarılı tam ekran durumunu sonsuza kadar korumayacak; sınırlı geri deneme ve güvenli toparlanma uygulanacak.

### Görünürlük kuralları

- Tam ekran oyun veya video boyunca dock, izin verilen geçici bildirim dışında görünmeyecek ve odak çalmayacak.
- İzin verilen bildirim süresi dolunca dock yeniden gizlenecek.
- Tam ekran kapatılınca, uygulama kapanınca veya pencere normale dönünce genel görünürlük/focus politikası yeniden uygulanacak.
- Kullanıcının elle gizleme tercihi tam ekran çıkışı tarafından yanlışlıkla sıfırlanmayacak.
- `Always`, `EventsOnly`, etkin focus profili ve gizlilik kurallarının her birleşimi doğrulanacak.

## 5.2 — Deterministik otomatik test matrisi

Sanal zaman ve sahte pencere durumlarıyla en az şu diziler test edilecek:

- Normal → borderless fullscreen → normal.
- Normal → exclusive Direct3D → uygulamanın doğrudan kapanması.
- Direct3D çıkış sinyalinin birden fazla ölçüm boyunca gecikmesi.
- Aynı uygulamanın fullscreen/windowed arasında hızlı geçişi.
- Tam ekran HWND'sinin yok olması ve yeni ön plan penceresine geçiş.
- Olayın tamamen kaçırılması ve yalnız kurtarma ölçümüyle çıkış.
- İki monitörde tam ekran pencerenin monitör değiştirmesi.
- Hızlı Alt+Tab ve art arda farklı tam ekran uygulamalar.
- DispatcherQueue'nun geçici olarak callback reddetmesi.
- Ölçüm sırasında Win32/DWM hatası ve sonraki ölçümde toparlanma.
- Dispose sırasında bekleyen timer callback'i.
- Sanal iki saatlik tam ekran: tek timer, yinelenen `StateChanged` yok, kuyruk büyümesi yok.
- Bin tam ekran giriş/çıkış çevrimi: event/timer/handle sayısı sabit.

Overlay sözleşme testleri ayrıca normal duruma dönüşte `ApplyEnvironment` çağrısını, genel görünürlük politikasını ve geçici bildirim durumunun temizlenmesini koruyacak.

## 5.3 — Gerçek Windows tam ekran doğrulaması

### Uygulama türleri

- WinUI/masaüstü test penceresinde F11 tam ekran ve doğrudan kapatma.
- En az iki DirectX oyununda borderless fullscreen.
- Destekleyen en az bir uygulamada exclusive fullscreen.
- Tarayıcı videosu ve yerel medya oynatıcı tam ekranı.
- Standart maximize pencerenin yanlış pozitif üretmediğinin kontrolü.
- Steam, Xbox Game Bar veya Discord gibi oyun overlay'i açık/kapalı karşılaştırması.

### Geçiş senaryoları

- F11 ile gir/çık, Alt+Enter ile gir/çık ve uygulamayı tam ekrandayken kapat.
- Alt+Tab, Win+D yerine görev çubuğu/uygulama geçişi ve farklı monitöre geçiş.
- Oyun çözünürlüğü, yenileme hızı, HDR ve DPI değişimi.
- Monitör çıkarma/takma ve birincil monitör değişimi.
- Uyku/uyanma, ekran kilidi açma ve Explorer yeniden başlatma.
- Tam ekran uygulamanın çökmesi veya Görev Yöneticisi tarafından sonlandırılması.

Güvenlik veya oturum açma ekranları otomasyonla kontrol edilmeyecek; ilgili senaryolar kullanıcı kontrollü manuel test olarak yürütülecek.

## 5.4 — Uzun süreli dayanıklılık

- En az **2 saat kesintisiz gerçek tam ekran oyun/medya** testi yapılacak.
- Bu sürede her 15 dakikada CPU, bellek, thread, USER/GDI handle, timer ve log büyümesi örneklenecek.
- Uzun oturum sonunda tam ekrandan normal pencereye dönüş ve uygulamayı doğrudan kapatma ayrı ayrı doğrulanacak.
- En az **30 dakika yoğun tam ekran geçiş testi** uygulanacak: hızlı giriş/çıkış, Alt+Tab, monitör geçişi, bildirim ve dock etkileşimi.
- Genel Faz 5 gereği ayrıca en az **30 dakika yoğun dock etkileşimi** ve **2 saat MiaDock boşta çalışma** testi yapılacak.

## 5.5 — Diğer stabilizasyon kapsamı

- Animasyon iptali, timer, event handler, COM nesnesi ve DispatcherQueue yaşam döngüleri denetlenecek.
- Hızlı dock kaydırma, art arda düğme tıklama ve modül değiştirme testleri yapılacak.
- Timer/kronometre çalışma, alarm, tur kaydı, uyku/uyanma ve geri yükleme senaryoları doğrulanacak.
- Global kısayolların yeniden kaydı, çakışma/başarısız kayıt ve UI thread aktarımı stres altında test edilecek.
- Tray'den çıkış, ayar penceresini tekrar açma, Explorer yeniden başlatılması ve sistem resume akışları doğrulanacak.
- Yerel loglar ve Windows Application olay günlüğü her uzun test sonunda incelenecek.

## Ölçülebilir kabul kriterleri

- Tam ekrana giriş algılama hedefi: en geç 300 ms.
- Tam ekrandan normal duruma dönüş: normal olay akışında hedef 300 ms; olay kaçırma/sinyal gecikmesinde mutlak üst sınır 1 saniye.
- İki saatlik tam ekran boyunca izinsiz dock görünmesi: 0.
- İzin verilen geçici bildirim sonrası gizlenememe: 0.
- Yanlış tam ekran pozitif/negatif: test matrisinde 0.
- Yinelenen durum olayı, timer birikmesi veya DispatcherQueue backlog'u: 0.
- Uzun testte sürekli yükselen thread, USER/GDI handle veya timer sayısı: 0.
- MiaDock kaynaklı yakalanmamış istisna, uygulama kapanması veya UI donması: 0.
- Release x64 derlemesi: 0 hata; yeni uyarı: 0.
- Tüm testler: 0 başarısız.

## Sürüm kapısı

Tam ekran P0 matrisindeki herhangi bir başarısızlık Faz 6'yı bloke eder. Geçici olarak yeniden başlatmayla düzelen, yalnız belirli oyun/ekran modunda oluşan veya düşük sıklıklı bir hata da sürüm engelleyici kabul edilecek. Test kanıtları, performans örnekleri ve olay/log özeti Faz 5 sonuç raporuna eklenmeden `1.2.1.1` sürüm adayı oluşturulmayacak.

Ayar şeması yeni kalıcı alan gerekmiyorsa `18` olarak kalacak.
