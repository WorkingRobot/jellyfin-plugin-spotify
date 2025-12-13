using System;

namespace Jellyfin.Plugin.Spotify;

public readonly record struct FileId
{
    private const int Size = 20;
    private const int SizeBase16 = 40;

    public FileId(ReadOnlySpan<byte> bytes)
    {
        Id = FromByteArray(bytes);
    }

    public FileId(Google.Protobuf.ByteString bytes)
    {
        Id = FromByteArray(bytes.Span);
    }

    public FileId(string base16Id)
    {
        Id = FromBase16(base16Id);
    }

    public byte[] Id { get; init; }

    public string Base16 => ToBase16(Id);

    public override string ToString() => ToBase16(Id);

    private static byte[] FromByteArray(ReadOnlySpan<byte> bytes)
    {
        var ret = new byte[Size];

        if (bytes.Length <= Size)
        {
            bytes.CopyTo(ret.AsSpan(..bytes.Length));
        }

        return ret;
    }

    private static byte[] FromBase16(string base16Id)
    {
        if (string.IsNullOrEmpty(base16Id))
        {
            throw new ArgumentException("Base16 string cannot be null or empty.", nameof(base16Id));
        }

        if (base16Id.Length != SizeBase16)
        {
            throw new ArgumentException($"Base16 string must be exactly {SizeBase16} characters long.", nameof(base16Id));
        }

        return Convert.FromHexString(base16Id);
    }

    private static string ToBase16(byte[] value) =>
        Convert.ToHexStringLower(value);
}
