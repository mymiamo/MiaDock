# MiaDock 1.4.0.0 sürüm notları

## Türkçe

### Yeni özellikler

* Dock içerik hareketi Community Toolkit animasyonlarına taşındı; boyut ve köşe yuvarlaklığı mevcut Composition bounds animatöründe kaldı.
* Compact ↔ olay bildirimi geçişlerinde yumuşak yay (spring) morph kullanılıyor.
* Yeni **Gizlilik** modülü, mikrofon ve kamerayı kullanan uygulamaları yerel olarak gösterir (Capability Access Manager ConsentStore).
* Caps Lock, Num Lock ve Scroll Lock durumu dock üzerinde kısa olay olarak gösterilebilir (varsayılan açık).
* USB bellek takılma / çıkarılma olayları dock üzerinde gösterilebilir (varsayılan açık).
* Ayarlar menüsü yeniden düzenlendi: Genel Bakış, Kişiselleştir (Tam Ekran dahil), Odak, Modüller (İsteğe Bağlı dahil), Kısayollar, Sistem, Destek; **Yenilikler** alt menüde.
* Ayarlarda geri düğmesi ve Alt+Sol kısayolu ile sayfa geçmişinde dolaşılabilir.
* Ayarlar penceresinin minimum boyutu 972×692 oldu.

### Deneyim iyileştirmeleri

* Sistem Etkinliği artık yalnız arama durumuna odaklanır; mikrofon/kamera göstergesi Gizlilik modülüne taşındı.
* Aktivite noktası: boşta beyaz, mikrofon yeşil, kamera turuncu (hoparlör noktayı etkilemez).
* İsteğe bağlı olaylar (klavye kilitleri, USB) **Modüller → İsteğe Bağlı** sekmesinden kapatılıp açılabilir.
* Global kısayollar ayrı **Kısayollar** bölümünde yönetilir.
* Onboarding adımları sadeleştirildi.
* Görünüm sayfasındaki canlı dock önizlemesi kaldırıldı.

### Ayar migrasyonu

* Genel ayar şeması 22’ye yükseltildi.
* Şema &lt; 20: kilit tuşu olayları varsayılan açık.
* Şema &lt; 22: USB tak/çıkar olayları varsayılan açık.
* Eksik veya bozuk değerler güvenli aralıklara normalize edilir.

### Teknik notlar

* `CommunityToolkit.WinUI.Animations` **8.2.251219** eklendi.
* Windows App SDK **2.3.1** / .NET 10 hedefi korunuyor.
* Hareket önayarları, azaltılmış hareket ve iptal-üzerine-kesme davranışı korundu.
* Community Toolkit SettingsControls / SettingsCard migrasyonu 1.4.0 kapsamında değil.

### Bilinen sınırlamalar

* USB olayları çıkarılabilir (removable) birimlere odaklanır; bazı harici diskler “sabit” görünebilir.
* Gizlilik ConsentStore okuması Windows sürümüne ve izin durumuna bağlıdır.
* Store / WACK / uzun soak doğrulaması yayın sürecinin ayrı kapılarıdır.

---

## English

### New features

* Dock content motion now uses Community Toolkit animations; island size and corner radii stay on the Composition bounds animator.
* Compact ↔ notification transitions use a soft spring morph.
* New **Privacy** module locally shows which apps are using the microphone and camera (Capability Access Manager ConsentStore).
* Caps Lock, Num Lock, and Scroll Lock changes can appear as short dock events (on by default).
* USB drive connect/remove events can appear on the dock (on by default).
* Settings navigation was reorganized: Overview, Personalize (including Fullscreen), Focus, Modules (including Optional), Shortcuts, System, Support; **What's New** sits in the footer.
* Settings back button and Alt+Left walk the page history.
* Settings window minimum size is 972×692.

### Experience improvements

* System Activity focuses on call status; mic/camera indication moved to Privacy.
* Activity dot: idle white, microphone green, camera orange (speaker does not drive the dot).
* Optional events (keyboard locks, USB) toggle under **Modules → Optional**.
* Global hotkeys live in a dedicated **Shortcuts** section.
* Onboarding steps were simplified.
* The live dock preview on Appearance was removed.

### Settings migration

* Settings schema bumped to 22.
* Schema &lt; 20 enables keyboard-lock events by default.
* Schema &lt; 22 enables USB connect/remove events by default.
* Invalid values are normalized to safe ranges.

### Technical notes

* Added `CommunityToolkit.WinUI.Animations` **8.2.251219**.
* Remains on Windows App SDK **2.3.1** / .NET 10.
* Motion presets, reduced motion, and cancel-on-interrupt behavior are preserved.
* Community Toolkit SettingsControls / SettingsCard migration is out of scope for 1.4.0.

### Known limitations

* USB events target removable volumes; some external disks may appear as fixed drives.
* Privacy ConsentStore readout depends on Windows version and permission state.
* Store / WACK / long soak validation remain separate release gates.
