using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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
/// Metadata provider for Spotify albums.
/// </summary>
public class AlbumMetadataProvider(ILoggerFactory loggerFactory, SessionManager sessionManager) : IRemoteMetadataProvider<MusicAlbum, AlbumInfo>
{
    private readonly ILogger<AlbumMetadataProvider> _logger = loggerFactory.CreateLogger<AlbumMetadataProvider>();
    private readonly SessionManager _sessionManager = sessionManager;

    private static MetadataResult<MusicAlbum> EmptyMetadata => new()
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
    public async Task<MetadataResult<MusicAlbum>> GetMetadata(AlbumInfo info, CancellationToken cancellationToken)
    {
        Proto.Album albumData;

        var spotifyId = info.GetProviderId(Constants.ProviderAlbum) is { } id ? SpotifyId.TryFromBase62(id) : null;

        if (!spotifyId.HasValue)
        {
            foreach (var songInfo in info.SongInfos)
            {
                spotifyId ??= songInfo.GetProviderId(Constants.ProviderAlbum) is { } songAlbumId ? SpotifyId.TryFromBase62(songAlbumId) : null;
                spotifyId ??= TagHelper.ExtractSpotifyIds(songInfo.Path, _logger).Album;
                if (spotifyId.HasValue)
                {
                    break;
                }
            }
        }

        if (spotifyId is { } spotifyIdValue)
        {
            _logger.LogInformation("Using ID {Id} for album metadata lookup", Constants.FormatAlbumId(spotifyIdValue.Base62));
            albumData = await _sessionManager.GetAlbumAsync(spotifyIdValue, cancellationToken).ConfigureAwait(false);
            if (albumData is null)
            {
                _logger.LogInformation("No album data found using ID {Id}", Constants.FormatAlbumId(spotifyIdValue.Base62));
                return EmptyMetadata;
            }
        }
        else
        {
            _logger.LogInformation("Spotify album ID is not available, cannot continue");
            return EmptyMetadata;
        }

        var metadataResult = new MetadataResult<MusicAlbum>
        {
            Item = new MusicAlbum
            {
                Name = albumData.Name,
                Artists = [.. albumData.Artist.Select(a => a.Name)],
                AlbumArtists = [.. albumData.Artist.Select(a => a.Name)],
                CommunityRating = albumData.HasPopularity ? albumData.Popularity : null,
                Overview = albumData.Review.FirstOrDefault() ?? string.Empty,
                ProductionYear = albumData.Date.Year,
                PremiereDate = albumData.Date.ToDateTime(),
                OriginalTitle = albumData.OriginalTitle,
            },
            HasMetadata = true
        };

        foreach (var albumArtist in albumData.Artist)
        {
            metadataResult.AddPerson(new PersonInfo
            {
                Name = albumArtist.Name,
                Role = "Album Artist",
                Type = PersonKind.AlbumArtist,
            });
        }

        if (albumData.GetRemoteImageInfo(_sessionManager)?.Url is { } imageUrl)
        {
            metadataResult.RemoteImages.Add((imageUrl, ImageType.Primary));
        }

        metadataResult.Item.SetProviderId(Constants.ProviderAlbum, spotifyIdValue.Base62);

        if (albumData.Artist.FirstOrDefault() is { } artist)
        {
            metadataResult.Item.SetProviderId(Constants.ProviderArtist, SpotifyId.FromByteString(artist.Gid).Base62);
        }

        return metadataResult;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(AlbumInfo searchInfo, CancellationToken cancellationToken)
    {
        var spotifyId = searchInfo.GetProviderId(Constants.ProviderAlbum) is { } id ? SpotifyId.TryFromBase62(id) : null;

        if (!spotifyId.HasValue)
        {
            foreach (var songInfo in searchInfo.SongInfos)
            {
                spotifyId ??= songInfo.GetProviderId(Constants.ProviderAlbum) is { } songAlbumId ? SpotifyId.TryFromBase62(songAlbumId) : null;
                spotifyId ??= TagHelper.ExtractSpotifyIds(songInfo.Path, _logger).Album;
                if (spotifyId.HasValue)
                {
                    break;
                }
            }
        }

        if (spotifyId is { } spotifyIdValue)
        {
            _logger.LogInformation("Looking up {ID}", Constants.FormatAlbumId(spotifyIdValue.Base62));
            var albumData = await _sessionManager.GetAlbumAsync(spotifyIdValue, cancellationToken).ConfigureAwait(false);
            return [albumData.GetRemoteSearchResult(_sessionManager)];
        }

        _logger.LogInformation("Spotify album ID was not provided, using search");

        var searchTerm = searchInfo.Name;
        var searchResults = await _sessionManager.SearchAlbumAsync(searchTerm, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Found {Count} search results using term {SearchTerm}", searchResults.Length, searchTerm);

        var allResults = new List<RemoteSearchResult>();
        foreach (var albumId in searchResults)
        {
            _logger.LogInformation("Processing search result: {ResultName}", Constants.FormatAlbumId(albumId.Base62));
            var albumData = await _sessionManager.GetAlbumAsync(albumId, cancellationToken).ConfigureAwait(false);

            // Check year only if the year was specified in the search form
            if (searchInfo.Year is not null && searchInfo.Year != albumData.Date.Year)
            {
                _logger.LogInformation("Album {AlbumName} does not match specified year, ignoring", albumData.Name);
                continue;
            }

            var result = albumData.GetRemoteSearchResult(_sessionManager);
            var albumProvId = result.GetProviderId(Constants.ProviderAlbum);
            _logger.LogInformation("Provider ID for album: {AlbumProvId} for {AlbumId} with Image {ImageUrl}", albumProvId, albumId.Base62, result.ImageUrl);

            allResults.Add(result);
        }

        _logger.LogInformation("Total search results after processing: {Count}", allResults.Count);
        return allResults;
    }
}
