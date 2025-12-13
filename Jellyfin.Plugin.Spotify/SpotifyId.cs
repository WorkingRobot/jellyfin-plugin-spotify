using System;
using System.Text;

namespace Jellyfin.Plugin.Spotify;

public readonly record struct SpotifyId(UInt128 Id)
{
    private const string Base62Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

    private const int Size = 16;
    private const int SizeBase62 = 22;

    public string Base62 => ToBase62(Id);

    public string Base16 => ToBase16(Id);

    public override string ToString() => ToBase62(Id);

    public static SpotifyId? TryFromByteArray(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return FromByteArray(bytes);
        }
        catch
        {
            return null;
        }
    }

    public static SpotifyId? TryFromBase62(string base62Id)
    {
        try
        {
            return FromBase62(base62Id);
        }
        catch
        {
            return null;
        }
    }

    public static SpotifyId? TryFromByteString(Google.Protobuf.ByteString byteString)
    {
        try
        {
            return FromByteString(byteString);
        }
        catch
        {
            return null;
        }
    }

    public static SpotifyId FromByteArray(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Size)
        {
            throw new ArgumentException($"Byte array must be exactly {Size} bytes long to convert to UInt128.");
        }

        if (BitConverter.IsLittleEndian)
        {
            Span<byte> reversed = stackalloc byte[Size];
            bytes.CopyTo(reversed);
            reversed.Reverse();
            return new(BitConverter.ToUInt128(reversed));
        }
        else
        {
            return new(BitConverter.ToUInt128(bytes));
        }
    }

    public static SpotifyId FromBase62(string base62Id)
    {
        if (string.IsNullOrEmpty(base62Id))
        {
            throw new ArgumentException("Base62 string cannot be null or empty.", nameof(base62Id));
        }

        if (base62Id.Length != SizeBase62)
        {
            throw new ArgumentException($"Base62 string must be exactly {SizeBase62} characters long.", nameof(base62Id));
        }

        UInt128 result = 0;
        foreach (char c in base62Id)
        {
            var p = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'z' => c - 'a' + 10,
                >= 'A' and <= 'Z' => c - 'A' + 36,
                _ => throw new ArgumentException($"Invalid character '{c}' in Base62 string.", nameof(base62Id)),
            };
            result = checked((result * 62) + (UInt128)p);
        }

        return new(result);
    }

    public static SpotifyId FromByteString(Google.Protobuf.ByteString byteString) =>
        FromByteArray(byteString.Span);

    private static string ToBase62(UInt128 value)
    {
        Span<byte> tmp = stackalloc byte[SizeBase62];
        int i = 0;

        for (var shift = 96; shift >= 0; shift -= 32)
        {
            ulong carry = (uint)(value >> shift);

            foreach (ref var b in tmp[..i])
            {
                carry += (ulong)b << 32;
                b = (byte)(carry % 62);
                carry /= 62;
            }

            while (carry > 0)
            {
                tmp[i] = (byte)(carry % 62);
                carry /= 62;
                i += 1;
            }
        }

        for (int j = 0; j < SizeBase62; j++)
        {
            tmp[j] = (byte)Base62Alphabet[tmp[j]];
        }

        tmp.Reverse();

        return Encoding.ASCII.GetString(tmp);
    }

    private static string ToBase16(UInt128 value)
    {
        var bytes = BitConverter.GetBytes(value);

        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(bytes);
        }

        return Convert.ToHexStringLower(bytes);
    }
}
