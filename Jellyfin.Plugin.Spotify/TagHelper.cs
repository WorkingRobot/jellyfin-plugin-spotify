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
    internal readonly record struct EmbeddedIds
    {
        public SpotifyId? Track { get; init; }

        public SpotifyId? Album { get; init; }

        public SpotifyId[] Artist { get; init; }

        public SpotifyId[] AlbumArtist { get; init; }
    }

    public static EmbeddedIds ExtractSpotifyIds(string path, ILogger? logger = null)
    {
        var tagTrack = new Track(path);

        logger?.LogInformation("Attempting to extract Spotify ID from tags for file {Path}", path);

        SpotifyId? trackId = null;
        SpotifyId? albumId = null;
        List<SpotifyId> artistIds = [];
        List<SpotifyId> albumArtistIds = [];

        if (TryGetSanitizedAdditionalFields(tagTrack, "SPOTIFY_ID", out var prefixedSpotifyId) && prefixedSpotifyId.StartsWith(Constants.ProviderTrack + ":", StringComparison.Ordinal))
        {
            logger?.LogInformation("Found SPOTIFY_ID tag: {TagValue}", prefixedSpotifyId);
            trackId = SpotifyId.TryFromBase62(prefixedSpotifyId[(Constants.ProviderTrack.Length + 1)..]);
        }

        if (trackId != null && TryGetSanitizedAdditionalFields(tagTrack, "URL", out var prefixedSpotifyUrl) && prefixedSpotifyUrl.StartsWith(Constants.OpenUrl + "/track/", StringComparison.Ordinal))
        {
            logger?.LogInformation("Found URL tag: {TagValue}", prefixedSpotifyUrl);
            trackId = SpotifyId.TryFromBase62(prefixedSpotifyUrl[(Constants.OpenUrl.Length + "/track/".Length)..]);
        }

        if (TryGetSanitizedAdditionalFields(tagTrack, "SPOTIFY_ALBUM_ID", out prefixedSpotifyId) && prefixedSpotifyId.StartsWith(Constants.ProviderAlbum + ":", StringComparison.Ordinal))
        {
            logger?.LogInformation("Found SPOTIFY_ALBUM_ID tag: {TagValue}", prefixedSpotifyId);
            albumId = SpotifyId.TryFromBase62(prefixedSpotifyId[(Constants.ProviderAlbum.Length + 1)..]);
        }

        if (TryGetSanitizedAdditionalFields(tagTrack, "SPOTIFY_ARTIST_ID", out prefixedSpotifyId))
        {
            if (prefixedSpotifyId.Contains('\x1f'))
            {
                logger?.LogInformation("Found {TagCount} SPOTIFY_ARTIST_ID potential tags", prefixedSpotifyId.Count(c => c == '\x1f'));
                var tags = prefixedSpotifyId.Split('\x1f', StringSplitOptions.TrimEntries);
                foreach (var tag in tags)
                {
                    if (tag.StartsWith(Constants.ProviderArtist + ":", StringComparison.Ordinal))
                    {
                        logger?.LogInformation("Found SPOTIFY_ARTIST_ID tag: {TagValue}", tag);
                        if (SpotifyId.TryFromBase62(tag[(Constants.ProviderArtist.Length + 1)..]) is { } artistId)
                        {
                            artistIds.Add(artistId);
                        }
                    }
                }
            }
            else
            {
                if (prefixedSpotifyId.StartsWith(Constants.ProviderArtist + ":", StringComparison.Ordinal))
                {
                    logger?.LogInformation("Found SPOTIFY_ARTIST_ID tag: {TagValue}", prefixedSpotifyId);
                    if (SpotifyId.TryFromBase62(prefixedSpotifyId[(Constants.ProviderArtist.Length + 1)..]) is { } artistId)
                    {
                        artistIds.Add(artistId);
                    }
                }
            }
        }

        if (TryGetSanitizedAdditionalFields(tagTrack, "SPOTIFY_ALBUM_ARTIST_ID", out prefixedSpotifyId))
        {
            if (prefixedSpotifyId.Contains('\x1f'))
            {
                logger?.LogInformation("Found {TagCount} SPOTIFY_ALBUM_ARTIST_ID potential tags", prefixedSpotifyId.Count(c => c == '\x1f'));
                var tags = prefixedSpotifyId.Split('\x1f', StringSplitOptions.TrimEntries);
                foreach (var tag in tags)
                {
                    if (tag.StartsWith(Constants.ProviderArtist + ":", StringComparison.Ordinal))
                    {
                        logger?.LogInformation("Found SPOTIFY_ALBUM_ARTIST_ID tag: {TagValue}", tag);
                        if (SpotifyId.TryFromBase62(tag[(Constants.ProviderArtist.Length + 1)..]) is { } artistId)
                        {
                            albumArtistIds.Add(artistId);
                        }
                    }
                }
            }
            else
            {
                if (prefixedSpotifyId.StartsWith(Constants.ProviderArtist + ":", StringComparison.Ordinal))
                {
                    logger?.LogInformation("Found SPOTIFY_ALBUM_ARTIST_ID tag: {TagValue}", prefixedSpotifyId);
                    if (SpotifyId.TryFromBase62(prefixedSpotifyId[(Constants.ProviderArtist.Length + 1)..]) is { } artistId)
                    {
                        albumArtistIds.Add(artistId);
                    }
                }
            }
        }

        return new EmbeddedIds
        {
            Track = trackId,
            Album = albumId,
            Artist = artistIds.ToArray(),
            AlbumArtist = albumArtistIds.ToArray(),
        };
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
}
