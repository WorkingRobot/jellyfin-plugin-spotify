using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Spotify.Api;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Spotify.ImageProviders;

/// <summary>
/// Image provider for Spotify artists.
/// </summary>
public class ArtistImageProvider(ILoggerFactory loggerFactory, SessionManager sessionManager) : IRemoteImageProvider
{
    private readonly ILogger<ArtistImageProvider> _logger = loggerFactory.CreateLogger<ArtistImageProvider>();
    private readonly SessionManager _sessionManager = sessionManager;

    /// <inheritdoc />
    public string Name => Constants.Name;

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _sessionManager.GetAsync(url, cancellationToken);
    }

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not MusicArtist artist)
        {
            return [];
        }

        var spotifyId = item.GetProviderId(Constants.ProviderArtist) is { } id ? SpotifyId.TryFromBase62(id) : null;

        if (!spotifyId.HasValue)
        {
            foreach (var path in artist.Children.Select(s => s.Path).Concat(MetadataProviders.Helpers.GetChildPaths(artist.Path)).Distinct())
            {
                var songTags = TagHelper.ExtractSpotifyIds(path, _logger);
                spotifyId ??= songTags.GetArtistByName(item.Name);
                spotifyId ??= songTags.GetAlbumArtistByName(item.Name);
                if (spotifyId.HasValue)
                {
                    break;
                }
            }
        }

        if (spotifyId is { } spotifyIdValue)
        {
            _logger.LogInformation("Using ID {Id} for artist metadata lookup", Constants.FormatArtistId(spotifyIdValue));
            var artistData = await _sessionManager.GetArtistAsync(spotifyIdValue, cancellationToken).ConfigureAwait(false);
            if (artistData is null)
            {
                _logger.LogInformation("No artist data found using ID {Id}", Constants.FormatArtistId(spotifyIdValue));
                return [];
            }

            var artistOverview = await _sessionManager.GetArtistOverviewAsync(spotifyIdValue, cancellationToken).ConfigureAwait(false);

            var artistImage = artistData.GetRemoteImageInfo(_sessionManager);
            var artistHeader = artistOverview.ArtistUnion?.HeaderImage?.GetRemoteImageInfo();
            if (artistImage != null)
            {
                return new[] { artistImage, artistHeader }.Where(i => i != null)!;
            }
            else
            {
                _logger.LogInformation("No image found for artist ID {ID}", Constants.FormatArtistId(spotifyIdValue));
                return [];
            }
        }

        _logger.LogInformation("Spotify artist ID was not provided, using search");

        var searchTerm = item.Name;
        var searchResults = await _sessionManager.SearchArtistAsync(searchTerm, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Found {Count} search results using term {SearchTerm}", searchResults.Length, searchTerm);

        var allResults = new List<RemoteImageInfo>();
        foreach (var artistId in searchResults)
        {
            _logger.LogDebug("Processing search result: {ResultName}", Constants.FormatArtistId(artistId));
            var artistData = await _sessionManager.GetArtistAsync(artistId, cancellationToken).ConfigureAwait(false);
            var artistOverview = await _sessionManager.GetArtistOverviewAsync(artistId, cancellationToken).ConfigureAwait(false);

            var artistImage = artistData.GetRemoteImageInfo(_sessionManager);
            if (artistImage != null)
            {
                allResults.Add(artistImage);
                var artistHeader = artistOverview.ArtistUnion?.HeaderImage.GetRemoteImageInfo();
                if (artistHeader != null)
                {
                    allResults.Add(artistHeader!);
                }
            }
            else
            {
                _logger.LogInformation("No image found for artist ID {ID}", Constants.FormatArtistId(artistId));
            }
        }

        _logger.LogInformation("Total search results after processing: {Count}", allResults.Count);
        return allResults;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return [ImageType.Primary, ImageType.Backdrop];
    }

    public bool Supports(BaseItem item) =>
        item is MusicArtist;
}
