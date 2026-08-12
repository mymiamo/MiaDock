# Yenilikler metnini nasıl yazarsın?

Uygulama içindeki **Ayarlar → Destek → Yenilikler** sayfası şu dosyayı okur:

`src/MiaDock.App/Content/YENILIKLER.md`

Bu dosya (`yenilik.md`) yalnızca yazım rehberidir; uygulamada gösterilmez.

## Hangi dosyayı düzenlersin?

| Dosya | Amaç |
|-------|------|
| [`src/MiaDock.App/Content/YENILIKLER.md`](src/MiaDock.App/Content/YENILIKLER.md) | Kullanıcıya görünen yenilik metni |
| [`yenilik.md`](yenilik.md) | Bu rehber (senin için) |

Metni kaydettikten sonra uygulamayı yeniden derleyip çalıştır; içerik çıktıya kopyalanır.

## Biçim kuralları (önemli)

Sayfa sade bir Markdown okuyucu kullanır. Desteklenen satırlar:

```markdown
# Büyük başlık
## Sürüm / bölüm başlığı
### Alt başlık

Kısa paragraf metni.

- Madde 1
- Madde 2
* Madde 3

---
```

### Yap

- Her sürümü `## 1.4.0` gibi ayrı bir başlıkla yaz.
- En yeni sürümü **en üste** koy.
- Her maddeyi tek satırda, `- ` veya `* ` ile başlat.
- Kullanıcı dilinde yaz (MiaDock şu an Türkçe öncelikli).
- Kısa tut: sürüm başına 3–8 madde genelde yeter.

### Yapma

- HTML ekleme (`<br>`, `<b>` vb.)
- Tablolar, kod blokları, görseller, link sözdizimi `[metin](url)` — şu an gösterilmez veya düz metin kalır
- Çok uzun teknik jargonu kullanıcı metnine koyma (teknik notlar `doc/RELEASE_NOTES_*.md` dosyalarında kalsın)

## Örnek şablon

Aşağıyı kopyalayıp `YENILIKLER.md` dosyasının **en üstüne** yapıştır:

```markdown
## 1.4.0

- Dock geçiş animasyonları yenilendi.
- Caps Lock / Num Lock / Scroll Lock durumu dock’ta gösterilebilir.
- Ayarlardan kilit tuşu olayları kapatılabilir.

## 1.3.0

- Köşe yuvarlaklığı ve kenar mesafesi ayarları eklendi.
- Tam ekran davranış seçenekleri genişletildi.
```

## İyi yazım örnekleri

İyi:

- `Caps Lock açık/kapalı olayları dock’ta gösterilir.`
- `Görünüm sayfasındaki canlı önizleme kaldırıldı.`

Zayıf:

- `ToolkitAnimationFactory AnimateShellScaleAsync eklendi.` (kullanıcıya teknik)
- `Birkaç iyileştirme yapıldı.` (belirsiz)

## Sürüm notu vs Yenilikler

| Yer | Kimin için |
|-----|------------|
| `YENILIKLER.md` | Son kullanıcı (Ayarlar → Yenilikler) |
| `doc/RELEASE_NOTES_*.md` | Geliştirici / Store / iç doğrulama |

Aynı sürüm için her iki metni de güncelleyebilirsin; kullanıcıya giden metin her zaman `YENILIKLER.md` olmalıdır.
