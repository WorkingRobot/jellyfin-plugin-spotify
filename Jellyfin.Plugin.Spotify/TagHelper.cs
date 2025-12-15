using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ATL;
using Jellyfin.Extensions;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Spotify;

internal static class TagHelper
{
    public static EmbeddedIds ExtractSpotifyIds(string path, ILogger? logger = null)
    {
        var tagTrack = new Track(path);

        logger?.LogInformation("Attempting to extract Spotify ID from tags for file {Path}", path);

        var trackId = ExtractId(tagTrack, "SPOTIFY_ID", Constants.TrackKey, logger) ??
            ExtractIdCore(tagTrack, "URL", $"{Constants.OpenUrl}/{Constants.TrackKey}/", logger);
        var albumId = ExtractId(tagTrack, "SPOTIFY_ALBUM_ID", Constants.AlbumKey, logger);
        var artistIds = ExtractIds(tagTrack, "SPOTIFY_ARTIST_ID", Constants.ArtistKey, logger).ToList();
        var albumArtistIds = ExtractIds(tagTrack, "SPOTIFY_ALBUM_ARTIST_ID", Constants.ArtistKey, logger).ToList();

        var artistNames = tagTrack.Artist.Split('\x1f', StringSplitOptions.TrimEntries);
        var albumArtistNames = tagTrack.AlbumArtist.Split('\x1f', StringSplitOptions.TrimEntries);

        var artists = artistIds.Zip(artistNames, (id, name) => (Id: id, Name: name)).ToArray();
        var albumArtists = albumArtistIds.Zip(albumArtistNames, (id, name) => (Id: id, Name: name)).ToArray();

        return new EmbeddedIds
        {
            Track = trackId,
            Album = albumId,
            Artist = artists,
            AlbumArtist = albumArtists,
        };
    }

    private static SpotifyId? ExtractId(Track track, string field, string idType, ILogger? logger = null) =>
        ExtractIdCore(track, field, $"{Constants.ProviderKey}:{idType}:", logger);

    private static SpotifyId? ExtractIdCore(Track track, string field, string prefix, ILogger? logger = null)
    {
        if (ExtractTag(track, field) is { } id && id.StartsWith(prefix, StringComparison.Ordinal))
        {
            logger?.LogInformation("Found {FieldName} tag: {TagValue}", field, id);
            return SpotifyId.TryFromBase62(id[prefix.Length..]);
        }

        return null;
    }

    private static IEnumerable<SpotifyId> ExtractIds(Track track, string field, string idType, ILogger? logger = null)
    {
        var prefix = $"{Constants.ProviderKey}:{idType}:";
        if (ExtractTag(track, field) is { } ids)
        {
            foreach (var id in ids.Split('\x1f', StringSplitOptions.TrimEntries))
            {
                if (id.StartsWith(prefix, StringComparison.Ordinal))
                {
                    logger?.LogInformation("Found {FieldName} tag: {TagValue}", field, id);
                    if (SpotifyId.TryFromBase62(id[prefix.Length..]) is { } parsedId)
                    {
                        yield return parsedId;
                    }
                }
            }
        }
    }

    private static string? ExtractTag(Track track, string field)
    {
        if (TryGetSanitizedAdditionalFields(track, field, out var value))
        {
            return value;
        }

        return null;
    }

    private static string? GetSanitizedStringTag(string? tag)
    {
        if (string.IsNullOrEmpty(tag))
        {
            return null;
        }

        return tag.TruncateAtNull();
    }

    private static bool TryGetSanitizedAdditionalFields(Track track, string field, [NotNullWhen(true)] out string? value)
    {
        var hasField = TryGetAdditionalFieldWithFallback(track, field, out value);
        value = GetSanitizedStringTag(value);
        return hasField;
    }

    // Build the explicit mka-style fallback key (e.g., ARTISTS -> track.artists, "MusicBrainz Artist Id" -> track.musicbrainz_artist_id)
    private static string GetMkaFallbackKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        var normalized = key.Trim().Replace(' ', '_').ToLowerInvariant();
        return "track." + normalized;
    }

    // First try the normal key exactly; if missing, try the mka-style fallback key.
    private static bool TryGetAdditionalFieldWithFallback(Track track, string key, [NotNullWhen(true)] out string? value)
    {
        // Prefer the normal key (as-is, case-sensitive)
        if (track.AdditionalFields.TryGetValue(key, out value))
        {
            return true;
        }

        // Fallback to mka-style: "track." + lower-case(original key)
        var fallbackKey = GetMkaFallbackKey(key);
        if (track.AdditionalFields.TryGetValue(fallbackKey, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    internal readonly record struct EmbeddedIds
    {
        public SpotifyId? Track { get; init; }

        public SpotifyId? Album { get; init; }

        public (SpotifyId Id, string Name)[] Artist { get; init; }

        public (SpotifyId Id, string Name)[] AlbumArtist { get; init; }

        public SpotifyId? GetArtistByName(string artistName)
        {
            foreach (var (id, name) in Artist)
            {
                if (name.Equals(artistName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return id;
                }
            }

            return null;
        }

        public SpotifyId? GetAlbumArtistByName(string albumArtistName)
        {
            foreach (var (id, name) in AlbumArtist)
            {
                if (name.Equals(albumArtistName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return id;
                }
            }

            return null;
        }
    }
}
