# MiaDock Transfer Publisher

Bu örnek, yerel bir uygulamanın MiaDock'a aktarım ilerlemesi göndermesini gösterir.

## Çalıştırma

Önce MiaDock'u, ardından örnek sağlayıcıyı çalıştırın:

~~~powershell
dotnet run --project .\samples\MiaDock.TransferPublisher -- --name "Örnek aktarım"
~~~

## Protokol

- İletişim yalnızca mevcut Windows kullanıcısına açık named pipe üzerinden yapılır.
- Her mesaj, 4 bayt little-endian uzunluk ve ardından UTF-8 JSON gövdesidir.
- JSON gövdesi en fazla 64 KB olabilir.
- Bir sağlayıcı saniyede en fazla 10 güncelleme göndermelidir.
- protocolVersion, providerId, transferId, safeDisplayName, aktarılan/toplam bayt,
  durum ve UTC zaman damgası zorunludur.
- Dosya yolu göndermeyin. safeDisplayName yalnızca kullanıcıya gösterilecek kısa etikettir.
- 15 saniye güncelleme gelmezse aktarım beklemede, 30 saniye sonra bağlantısı kesilmiş sayılır.

MiaDock üçüncü taraf DLL yüklemez; sağlayıcı yalnızca sürümlü IPC sözleşmesini kullanır.
