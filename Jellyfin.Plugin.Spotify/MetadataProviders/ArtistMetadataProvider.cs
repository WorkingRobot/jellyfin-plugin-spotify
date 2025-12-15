using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Api;
using Jellyfin.Plugin.Spotify.ImageProviders;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Proto = Wavee.Protocol.Metadata;

namespace Jellyfin.Plugin.Spotify.MetadataProviders;

/// <summary>
/// Metadata provider for Spotify artists.
/// </summary>
public class ArtistMetadataProvider(ILoggerFactory loggerFactory, SessionManager sessionManager) : IRemoteMetadataProvider<MusicArtist, ArtistInfo>
{
    private readonly ILogger<ArtistMetadataProvider> _logger = loggerFactory.CreateLogger<ArtistMetadataProvider>();
    private readonly SessionManager _sessionManager = sessionManager;

    private static MetadataResult<MusicArtist> EmptyMetadata => new()
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
    public async Task<MetadataResult<MusicArtist>> GetMetadata(ArtistInfo info, CancellationToken cancellationToken)
    {
        Proto.Artist artistData;

        var spotifyId = info.GetProviderId(Constants.ProviderArtist) is { } id ? SpotifyId.TryFromBase62(id) : null;

        if (!spotifyId.HasValue)
        {
            foreach (var songInfo in info.SongInfos)
            {
                var songTags = TagHelper.ExtractSpotifyIds(songInfo.Path, _logger);
                spotifyId ??= songTags.GetArtistByName(info.Name);
                spotifyId ??= songTags.GetAlbumArtistByName(info.Name);
                if (spotifyId.HasValue)
                {
                    break;
                }
            }
        }

        if (!spotifyId.HasValue)
        {
            _logger.LogInformation("Spotify artist ID is not available, using first search result");

            var result = (await GetSearchResults(info, cancellationToken).ConfigureAwait(false))?.FirstOrDefault();
            if (result is null)
            {
                _logger.LogInformation("No search results found for artist {Name}", info.Name);
                return EmptyMetadata;
            }

            spotifyId = SpotifyId.FromBase62(result.GetProviderId(Constants.ProviderArtist)!);
        }

        var spotifyIdValue = spotifyId.Value;

        _logger.LogInformation("Using ID {Id} for artist metadata lookup", Constants.FormatArtistId(spotifyIdValue));
        artistData = await _sessionManager.GetArtistAsync(spotifyIdValue, cancellationToken).ConfigureAwait(false);
        if (artistData is null)
        {
            _logger.LogInformation("No artist data found using ID {Id}", Constants.FormatArtistId(spotifyIdValue));
            return EmptyMetadata;
        }

        var artistOverview = await _sessionManager.GetArtistOverviewAsync(spotifyIdValue, cancellationToken).ConfigureAwait(false);

        var metadataResult = new MetadataResult<MusicArtist>
        {
            Item = new MusicArtist
            {
                Name = artistData.Name,
                CommunityRating = artistData.HasPopularity ? artistData.Popularity / 10f : null,
                Overview = artistOverview.ArtistUnion.Profile.Biography.Text,
            },
            HasMetadata = true
        };

        if (artistData.GetRemoteImageInfo(_sessionManager)?.Url is { } imageUrl)
        {
            metadataResult.RemoteImages.Add((imageUrl, ImageType.Primary));
        }

        if (artistOverview.ArtistUnion.HeaderImage?.GetRemoteImageInfo()?.Url is { } headerUrl)
        {
            metadataResult.RemoteImages.Add((headerUrl, ImageType.Backdrop));
        }

        metadataResult.Item.SetProviderId(Constants.ProviderArtist, spotifyIdValue.Base62);

        return metadataResult;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(ArtistInfo searchInfo, CancellationToken cancellationToken)
    {
        var spotifyId = searchInfo.GetProviderId(Constants.ProviderArtist) is { } id ? SpotifyId.TryFromBase62(id) : null;

        if (!spotifyId.HasValue)
        {
            foreach (var songInfo in searchInfo.SongInfos)
            {
                var songTags = TagHelper.ExtractSpotifyIds(songInfo.Path, _logger);
                spotifyId ??= songTags.GetArtistByName(searchInfo.Name);
                spotifyId ??= songTags.GetAlbumArtistByName(searchInfo.Name);
                if (spotifyId.HasValue)
                {
                    break;
                }
            }
        }

        if (spotifyId is { } spotifyIdValue)
        {
            _logger.LogInformation("Looking up {ID}", Constants.FormatArtistId(spotifyIdValue));
            var artistData = await _sessionManager.GetArtistAsync(spotifyIdValue, cancellationToken).ConfigureAwait(false);
            return [artistData.GetRemoteSearchResult(_sessionManager)];
        }

        _logger.LogInformation("Spotify artist ID was not provided, using search");

        var searchTerm = searchInfo.Name;
        var searchResults = await _sessionManager.SearchArtistAsync(searchTerm, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Found {Count} search results using term {SearchTerm}", searchResults.Length, searchTerm);

        var allResults = new List<RemoteSearchResult>();
        foreach (var artistId in searchResults)
        {
            _logger.LogInformation("Processing search result: {ResultName}", Constants.FormatArtistId(artistId));
            var artistData = await _sessionManager.GetArtistAsync(artistId, cancellationToken).ConfigureAwait(false);
            allResults.Add(artistData.GetRemoteSearchResult(_sessionManager));
        }

        _logger.LogInformation("Total search results after processing: {Count}", allResults.Count);
        return allResults;
    }
}
