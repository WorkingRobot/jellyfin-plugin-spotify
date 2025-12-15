using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ATL;
using ATL.Logging;
using Jellyfin.Data.Enums;
using Jellyfin.Extensions;
using Jellyfin.Plugin.Spotify.Api;
using Jellyfin.Plugin.Spotify.ImageProviders;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Proto = Wavee.Protocol.Metadata;

namespace Jellyfin.Plugin.Spotify.MetadataProviders;

/// <summary>
/// Metadata provider for Spotify tracks.
/// </summary>
public class TrackMetadataProvider(ILoggerFactory loggerFactory, SessionManager sessionManager) : IRemoteMetadataProvider<Audio, SongInfo>
{
    private readonly ILogger<TrackMetadataProvider> _logger = loggerFactory.CreateLogger<TrackMetadataProvider>();
    private readonly SessionManager _sessionManager = sessionManager;

    private static MetadataResult<Audio> EmptyMetadata => new()
    {
        HasMetadata = false,
    };

    /// <inheritdoc />
    public string Name => Constants.Name;

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _sessionManager.GetAsync(url, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MetadataResult<Audio>> GetMetadata(SongInfo info, CancellationToken cancellationToken)
    {
        Proto.Track trackData;

        var spotifyId = info.GetProviderId(Constants.ProviderTrack) is { } id ? SpotifyId.TryFromBase62(id) : null;

        if (!spotifyId.HasValue)
        {
            spotifyId ??= TagHelper.ExtractSpotifyIds(info.Path, _logger).Track;
        }

        if (spotifyId is { } spotifyIdValue)
        {
            _logger.LogInformation("Using ID {Id} for track metadata lookup", Constants.FormatTrackId(spotifyIdValue.Base62));
            trackData = await _sessionManager.GetTrackAsync(spotifyIdValue, cancellationToken).ConfigureAwait(false);
            if (trackData is null)
            {
                _logger.LogInformation("No track data found using ID {Id}", Constants.FormatTrackId(spotifyIdValue.Base62));
                return EmptyMetadata;
            }
        }
        else
        {
            _logger.LogInformation("Spotify track ID is not available, cannot continue");
            return EmptyMetadata;
        }

        var metadataResult = new MetadataResult<Audio>
        {
            Item = new Audio
            {
                Name = trackData.Name,
                Album = trackData.Album.Name,
                Artists = [.. trackData.Artist.Select(a => a.Name)],
                AlbumArtists = [.. trackData.Album.Artist.Select(a => a.Name)],
                IndexNumber = trackData.Number,
                ParentIndexNumber = trackData.DiscNumber,
                CommunityRating = trackData.HasPopularity ? trackData.Popularity / 10f : null,
                Tags = [.. trackData.Tags],
                ProductionYear = trackData.Album.Date.Year,
                PremiereDate = trackData.Album.Date.ToDateTime(),
                OriginalTitle = trackData.OriginalTitle,
                HasLyrics = trackData.HasLyrics,
                OfficialRating = trackData.Explicit ? "PG-13" : string.Empty,
            },
            HasMetadata = true
        };

        foreach (var roledArtist in trackData.ArtistWithRole)
        {
            var (personKind, roleName) = roledArtist.Role switch
            {
                Proto.ArtistWithRole.Types.ArtistRole.MainArtist => (PersonKind.Artist, "Main Artist"),
                Proto.ArtistWithRole.Types.ArtistRole.FeaturedArtist => (PersonKind.Artist, "Featured Artist"),
                Proto.ArtistWithRole.Types.ArtistRole.Remixer => (PersonKind.Remixer, "Remixer"),
                Proto.ArtistWithRole.Types.ArtistRole.Actor => (PersonKind.Actor, "Actor"),
                Proto.ArtistWithRole.Types.ArtistRole.Composer => (PersonKind.Composer, "Composer"),
                Proto.ArtistWithRole.Types.ArtistRole.Conductor => (PersonKind.Conductor, "Conductor"),
                Proto.ArtistWithRole.Types.ArtistRole.Orchestra => (PersonKind.Unknown, "Orchestra"),
                _ or Proto.ArtistWithRole.Types.ArtistRole.Unknown => (PersonKind.Unknown, "Unknown"),
            };
            metadataResult.AddPerson(new PersonInfo
            {
                Name = roledArtist.ArtistName,
                Role = roleName,
                Type = personKind,
            });
        }

        if (trackData.GetRemoteImageInfo(_sessionManager)?.Url is { } imageUrl)
        {
            metadataResult.RemoteImages.Add((imageUrl, ImageType.Primary));
        }

        metadataResult.Item.SetProviderId(Constants.ProviderTrack, spotifyId.Value.Base62);
        metadataResult.Item.SetProviderId(Constants.ProviderAlbum, SpotifyId.FromByteString(trackData.Album.Gid).Base62);

        if (trackData.Artist.FirstOrDefault() is { } artist)
        {
            metadataResult.Item.SetProviderId(Constants.ProviderArtist, SpotifyId.FromByteString(artist.Gid).Base62);
        }

        return metadataResult;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SongInfo searchInfo, CancellationToken cancellationToken)
    {
        var spotifyId = searchInfo.GetProviderId(Constants.ProviderTrack) is { } id ? SpotifyId.TryFromBase62(id) : null;

        if (!spotifyId.HasValue)
        {
            spotifyId ??= TagHelper.ExtractSpotifyIds(searchInfo.Path, _logger).Track;
        }

        if (spotifyId is { } spotifyIdValue)
        {
            _logger.LogInformation("Looking up {ID}", Constants.FormatTrackId(spotifyIdValue.Base62));
            var trackData = await _sessionManager.GetTrackAsync(spotifyIdValue, cancellationToken).ConfigureAwait(false);
            return [trackData.GetRemoteSearchResult(_sessionManager)];
        }

        _logger.LogInformation("Spotify track ID was not provided, using search");

        var searchTerm = searchInfo.Name;
        var searchResults = await _sessionManager.SearchTrackAsync(searchTerm, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Found {Count} search results using term {SearchTerm}", searchResults.Length, searchTerm);

        var allResults = new List<RemoteSearchResult>();
        foreach (var trackId in searchResults)
        {
            _logger.LogInformation("Processing search result: {ResultName}", Constants.FormatTrackId(trackId.Base62));
            var trackData = await _sessionManager.GetTrackAsync(trackId, cancellationToken).ConfigureAwait(false);

            // Check year only if the year was specified in the search form
            if (searchInfo.Year is not null && searchInfo.Year != trackData.Album.Date.Year)
            {
                _logger.LogInformation("Track {TrackName} does not match specified year, ignoring", trackData.Name);
                continue;
            }

            allResults.Add(trackData.GetRemoteSearchResult(_sessionManager));
        }

        _logger.LogInformation("Total search results after processing: {Count}", allResults.Count);
        return allResults;
    }
}
