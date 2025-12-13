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
/// Image provider for Spotify albums.
/// </summary>
public class AlbumImageProvider(ILoggerFactory loggerFactory, SessionManager sessionManager) : IRemoteImageProvider
{
    private readonly ILogger<AlbumImageProvider> _logger = loggerFactory.CreateLogger<AlbumImageProvider>();
    private readonly SessionManager _sessionManager = sessionManager;

    /// <inheritdoc />
    public string Name => Constants.Name;

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _sessionManager.GetAsync(url, cancellationToken);
    }

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not MusicAlbum album)
        {
            return [];
        }

        var spotifyId = item.GetProviderId(Constants.ProviderAlbum) is { } id ? SpotifyId.TryFromBase62(id) : null;

        if (!spotifyId.HasValue)
        {
            foreach (var child in album.Children)
            {
                spotifyId ??= child.GetProviderId(Constants.ProviderAlbum) is { } songAlbumId ? SpotifyId.TryFromBase62(songAlbumId) : null;
                spotifyId ??= TagHelper.ExtractSpotifyIds(child.Path, _logger).Album;
                if (spotifyId.HasValue)
                {
                    break;
                }
            }
        }

        if (spotifyId is { } spotifyIdValue)
        {
            _logger.LogInformation("Using ID {Id} for album metadata lookup", Constants.FormatAlbumId(spotifyIdValue.Base62));
            var albumData = await _sessionManager.GetAlbumAsync(spotifyIdValue, cancellationToken).ConfigureAwait(false);
            if (albumData is null)
            {
                _logger.LogInformation("No album data found using ID {Id}", Constants.FormatAlbumId(spotifyIdValue.Base62));
                return [];
            }

            var albumImage = albumData.GetRemoteImageInfo(_sessionManager);
            if (albumImage != null)
            {
                return [albumImage];
            }
            else
            {
                _logger.LogInformation("No image found for album ID {ID}", Constants.FormatAlbumId(spotifyIdValue.Base62));
                return [];
            }
        }

        _logger.LogInformation("Spotify album ID was not provided, using search");

        var searchTerm = item.Name;
        var searchResults = await _sessionManager.SearchAlbumAsync(searchTerm, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Found {Count} search results using term {SearchTerm}", searchResults.Length, searchTerm);

        var allResults = new List<RemoteImageInfo>();
        foreach (var albumId in searchResults)
        {
            _logger.LogDebug("Processing search result: {ResultName}", Constants.FormatAlbumId(albumId.Base62));
            var albumData = await _sessionManager.GetAlbumAsync(albumId, cancellationToken).ConfigureAwait(false);

            // Check year only if the year was specified in the search form
            if (album.ProductionYear is not null && album.ProductionYear != albumData.Date.Year)
            {
                _logger.LogDebug("Album {AlbumName} does not match specified year, ignoring", albumData.Name);
                continue;
            }

            var albumImage = albumData.GetRemoteImageInfo(_sessionManager);
            if (albumImage != null)
            {
                allResults.Add(albumImage);
            }
            else
            {
                _logger.LogInformation("No image found for album ID {ID}", Constants.FormatAlbumId(albumId!.Base62));
            }
        }

        _logger.LogInformation("Total search results after processing: {Count}", allResults.Count);
        return allResults;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        return [ImageType.Primary];
    }

    public bool Supports(BaseItem item) =>
        item is MusicAlbum;
}
