using System.Buffers.Binary;
using System.IO.Pipes;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiaDock.Modules.Transfers;
using MiaDock.Modules.Transfers.Models;

var providerId = ReadArgument(args, "--provider") ?? "sample.publisher";
var displayName = ReadArgument(args, "--name") ?? "Örnek aktarım";
var totalBytes = long.TryParse(ReadArgument(args, "--total"), out var parsedTotal)
    ? Math.Max(1, parsedTotal)
    : 100_000_000;
var transferId = Guid.NewGuid().ToString("N");

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

await using var pipe = new NamedPipeClientStream(
    ".",
    TransferProtocol.CurrentUserPipeName,
    PipeDirection.Out,
    PipeOptions.Asynchronous);
Console.WriteLine("MiaDock transfer sağlayıcısına bağlanılıyor...");
await pipe.ConnectAsync(5000, cancellation.Token);

for (var step = 0; step <= 20; step++)
{
    var transferred = totalBytes * step / 20;
    var status = step == 20 ? TransferStatus.Completed : TransferStatus.Running;
    var message = new TransferProgressMessage(
        TransferProtocol.CurrentVersion,
        providerId,
        transferId,
        displayName,
        transferred,
        totalBytes,
        status,
        DateTimeOffset.UtcNow);
    await WriteMessageAsync(pipe, message, cancellation.Token);
    Console.WriteLine($"{step * 5}%");
    if (step < 20) await Task.Delay(250, cancellation.Token);
}

static string? ReadArgument(string[] values, string name)
{
    var index = Array.IndexOf(values, name);
    return index >= 0 && index + 1 < values.Length ? values[index + 1] : null;
}

static async Task WriteMessageAsync(
    Stream stream,
    TransferProgressMessage message,
    CancellationToken cancellationToken)
{
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    var payload = JsonSerializer.SerializeToUtf8Bytes(message, options);
    if (payload.Length > TransferProtocol.MaximumMessageBytes)
        throw new InvalidDataException("Mesaj 64 KB sınırını aşıyor.");

    var prefix = new byte[sizeof(int)];
    BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
    await stream.WriteAsync(prefix, cancellationToken);
    await stream.WriteAsync(payload, cancellationToken);
    await stream.FlushAsync(cancellationToken);
}
