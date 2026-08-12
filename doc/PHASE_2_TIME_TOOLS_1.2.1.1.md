# MiaDock 1.2.1.1 - Faz 2 Zaman Araçları

Tarih: 2 Ağustos 2026

## Sonuç

Faz 2 tamamlandı. Zamanlayıcı ve kronometre görünüm durumu birbirinden ayrıldı, periyodik güncellemeler yalnızca değişen metin ve komut durumlarını bildirecek şekilde daraltıldı ve genişletilmiş zamanlayıcı düzeni küçük genişliklerde kırpılmayı azaltacak biçimde yeniden düzenlendi.

## Uygulanan değişiklikler

- Zamanlayıcı/kronometre sekme seçimi görünüm güncellemelerinden bağımsız hale getirildi; geçersiz seçimler zamanlayıcı sekmesine güvenli biçimde dönüyor.
- Her servis anlık görüntüsünde bütün görünümü yenilemek yerine yalnızca gerçekten değişen özellikler için bildirim gönderiliyor.
- Kronometrenin saniye içindeki sık güncellemeleri kompakt metni ve ilgisiz zamanlayıcı alanlarını yeniden oluşturmuyor.
- Hazır süreler veri güdümlü, sarılabilen bir listeye dönüştürüldü. Özel saat/dakika/saniye alanları ile başlatma/durdurma eylemleri ayrı satırlara alındı.
- Sayı kutuları `Compact` artırma/azaltma düğmeleri kullanıyor; özel süreler sonlu olmayan veya aşırı değerlerde güvenli biçimde normalize edilip 99 saatle sınırlandırılıyor.
- Zamanlayıcı ve kronometrenin çalışma durumlarının birbirini etkilememesi güvence altına alındı.
- Uyku/uyanma uzlaştırması, duraklatılmış ve çalışan zamanlayıcı geri yükleme davranışları, tur kayıtlarının son 100 kayıtla sınırlandırılması ve kronolojik kalması doğrulandı.
- Alarmın beş kez çalması mevcut testlerle; hover üzerinden susturma ve tamamlanmış zamanlayıcıyı kapatma davranışı yeni regresyon testiyle doğrulandı.
- Türkçe ve İngilizce hazır/özel süre metinleri ile dakika kısaltmaları yerelleştirme servisine eklendi.
- Kalıcı veri değişikliği yapılmadı: ana ayar şeması `18`, zaman modülü şeması `1` olarak kaldı.

## Otomatik doğrulama

- `dotnet test MiaDock.sln -c Release -p:Platform=x64 --no-restore`: **511/511 başarılı**
  - MiaDock.Core.Tests: 268/268
  - MiaDock.Platform.Windows.Tests: 108/108
  - MiaDock.WinUI.Tests: 135/135
- Release x64 uygulama derlemesi: **başarılı, 0 uyarı, 0 hata**
- `git diff --check`: içerik/boşluk hatası yok; yalnızca çalışma ağacının LF/CRLF dönüşüm bilgilendirmeleri var.

## Çalışma zamanı doğrulaması

Release uygulaması gerçek masaüstü oturumunda başlatıldı ve zaman araçlarının kompakt görünümü yüklendi. 30 saniyelik kararlılık örneklemesi şu sonuçlarla geçti:

- Ortalama CPU: %1,858 (eşik %5)
- En yüksek CPU: %3,116
- Çalışma kümesi artışı: 11,97 MB (eşik 40 MB)
- Yanıt vermeyen örnek: 0
- Yerel uygulama logu: 7 bilgi kaydı, 0 hata/istisna
- Windows Application günlüğü: MiaDock için 0 kritik/hata/uyarı kaydı

Ölçüm çıktısı `artifacts/validation/1.2.1.1/phase2-runtime/runtime-20260802-162255.json` altında üretildi.

Overlay penceresi odak almayan ve otomatik kapanan bir pencere olduğu için genişletilmiş zamanlayıcı görünümü masaüstü otomasyonuyla güvenilir biçimde hedeflenemedi. Bu görünümün XAML'i gerçek Release uygulama derlemesinde doğrulandı; kırpılma için tam görsel kabul testi Faz 5 yoğun etkileşim çalışmasına bırakıldı.

Test için geçici olarak etkinleştirilen global kısayollar çalışma sonunda geri alındı ve test uygulaması kapatıldı.
