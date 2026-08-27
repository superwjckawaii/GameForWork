using System.Security.Cryptography;
using System.Text;

namespace GameForWork.Core.Persistence;

public static class BinaryEnvelope
{
    private static readonly byte[] Magic = "GFWS"u8.ToArray();
    public const ushort Version = 1;

    public static byte[] Wrap(ReadOnlySpan<byte> payload)
    {
        byte[] checksum = SHA256.HashData(payload);
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(payload.Length);
        writer.Write(checksum.Length);
        writer.Write(checksum);
        writer.Write(payload);
        writer.Flush();
        return stream.ToArray();
    }

    public static byte[] Unwrap(ReadOnlySpan<byte> envelope)
    {
        using var stream = new MemoryStream(envelope.ToArray(), writable: false);
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic))
        {
            throw new InvalidDataException("Snapshot envelope magic is invalid.");
        }

        ushort version = reader.ReadUInt16();
        if (version != Version)
        {
            throw new InvalidDataException($"Unsupported snapshot envelope version: {version}.");
        }

        int payloadLength = reader.ReadInt32();
        int checksumLength = reader.ReadInt32();
        if (payloadLength < 0 || checksumLength != 32)
        {
            throw new InvalidDataException("Snapshot envelope length is invalid.");
        }

        long remaining = stream.Length - stream.Position;
        if (remaining != checksumLength + (long)payloadLength)
        {
            throw new InvalidDataException("Snapshot envelope length does not match its contents.");
        }

        byte[] expectedChecksum = reader.ReadBytes(checksumLength);
        byte[] payload = reader.ReadBytes(payloadLength);
        if (payload.Length != payloadLength || stream.Position != stream.Length)
        {
            throw new InvalidDataException("Snapshot envelope is truncated or has trailing bytes.");
        }

        byte[] actualChecksum = SHA256.HashData(payload);
        if (!CryptographicOperations.FixedTimeEquals(expectedChecksum, actualChecksum))
        {
            throw new InvalidDataException("Snapshot envelope checksum does not match.");
        }

        return payload;
    }
}
