using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;
using MiaDock.Modules.Transfers;
using MiaDock.Modules.Transfers.Models;

namespace MiaDock.Platform.Windows.Transfers;

public static class LengthPrefixedJsonProtocol
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async ValueTask<TransferProgressMessage?> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var prefix = new byte[sizeof(int)];
        var prefixBytes = await ReadAtMostAsync(stream, prefix, cancellationToken).ConfigureAwait(false);
        if (prefixBytes == 0) return null;
        if (prefixBytes != prefix.Length) throw new InvalidDataException("Incomplete transfer message prefix.");

        var length = BinaryPrimitives.ReadInt32LittleEndian(prefix);
        if (length <= 0 || length > TransferProtocol.MaximumMessageBytes)
        {
            throw new InvalidDataException("Transfer message length is outside the supported range.");
        }

        var payload = new byte[length];
        await ReadExactlyAsync(stream, payload, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<TransferProgressMessage>(payload, SerializerOptions)
            ?? throw new InvalidDataException("Transfer message payload is empty.");
    }

    public static async ValueTask WriteAsync(
        Stream stream,
        TransferProgressMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(message);
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        if (payload.Length > TransferProtocol.MaximumMessageBytes)
        {
            throw new InvalidDataException("Transfer message exceeds the maximum payload size.");
        }

        var prefix = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(prefix, payload.Length);
        await stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
        await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<int> ReadAtMostAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer[total..], cancellationToken).ConfigureAwait(false);
            if (count == 0) break;
            total += count;
        }
        return total;
    }

    private static async ValueTask ReadExactlyAsync(
        Stream stream,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        if (await ReadAtMostAsync(stream, buffer, cancellationToken).ConfigureAwait(false) != buffer.Length)
        {
            throw new EndOfStreamException("Transfer message ended before the declared payload length.");
        }
    }
}
