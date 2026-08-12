# MiaDock 1.3.0.0 Microsoft Store teslimi

Yüklenecek tek dosya:

`artifacts/release/1.3.0.0/store-upload/MiaDock.App_1.3.0.0_x64.msixupload`

* Boyut: 59,453,586 bayt
* SHA-256: `3D93B06886A5BAC1FF533A8E61D9988E40E59DAD54C25D281CD178B118A8993A`
* Paket kimliği: `mymiamo.net.MiaDock`
* Publisher: `CN=FAC642FD-F594-4E90-B1DB-38F94EA36BCA`
* Mimari: x64
* Manifest sürümü: `1.3.0.0`
* İç MSIX sayısı: 1
* İç sembol paketi sayısı: 1
* Startup task: mevcut

Partner Center’da önceki denemelerden kalan aynı kimlik/sürüm paketlerinin tümünü mevcut submission’dan kaldırın ve **Save** ile kaldırmayı onaylayın. Ardından yalnız yukarıdaki yeni `.msixupload` dosyasını yükleyin. Aynı submission içinde bu dosyayı ikinci kez veya içindeki `.msix` dosyasını ayrıca yüklemeyin; aksi durumda “Multiple uploaded files contain the same package” doğrulaması tekrar oluşur.

`1.3.0.0` sürümü Microsoft Store’un package manifest revision alanının sıfır olması kuralına uygundur. Store’a yükleme yapılmadı.

WACK bu oturumun yükseltilmemiş PowerShell bağlamında çalıştırılamadı. `appcert.exe` kurulu ancak WACK betiği yönetici oturumu gerektiriyor. Store gönderiminden önce yükseltilmiş PowerShell’de iç MSIX ile çalıştırın:

WACK için iç paket ayrıca `artifacts/release/1.3.0.0/wack/MiaDock.App_1.3.0.0_x64.msix` konumuna çıkarıldı (SHA-256: `FF38A711E400D9D65DCFB3DED1FCAA692249429F09FD27D1E7D0B21EDF4D8E28`). Bu `.msix` Partner Center’a ayrıca yüklenmemelidir.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\validation\Invoke-MiaDockWack.ps1 `
  -PackagePath ".\artifacts\release\1.3.0.0\wack\MiaDock.App_1.3.0.0_x64.msix"
```
