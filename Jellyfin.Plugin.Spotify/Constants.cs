using System;
using Proto = Wavee.Protocol.Metadata;

namespace Jellyfin.Plugin.Spotify;

/// <summary>
/// Constants for the plugin.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// The name of the plugin.
    /// </summary>
    public const string Name = "Spotify";
    public const string ApiPrefix = "Jellyfin.Plugin.Spotify";

    public const string OpenUrl = "https://open.spotify.com";

    public const string ProviderKey = "spotify";
    public const string AlbumKey = "album";
    public const string ArtistKey = "artist";
    public const string TrackKey = "track";

    public const string ProviderAlbum = $"{ProviderKey}:{AlbumKey}";
    public const string ProviderArtist = $"{ProviderKey}:{ArtistKey}";
    public const string ProviderTrack = $"{ProviderKey}:{TrackKey}";

    public const string AlbumName = "Album";
    public const string ArtistName = "Artist";
    public const string TrackName = "Track";

    /// <summary>
    /// The Guid of the plugin.
    /// </summary>
    public static readonly Guid Guid = Guid.Parse("8a586678-5b5f-4a40-afaa-5db100a21b34");

    public static string FormatAlbumId(SpotifyId id) => $"{ProviderAlbum}:{id.Base62}";

    public static string FormatArtistId(SpotifyId id) => $"{ProviderArtist}:{id.Base62}";

    public static string FormatTrackId(SpotifyId id) => $"{ProviderTrack}:{id.Base62}";

    public static DateTime ToDateTime(this Proto.Date date)
    {
        var year = date.Year;
        var month = date.HasMonth ? date.Month : 1;
        var day = date.HasDay ? date.Day : 1;
        var hour = date.Hour;
        var minute = date.Minute;
        var second = 0;

        return new DateTime(year, month, day, hour, minute, second, DateTimeKind.Utc);
    }
}
