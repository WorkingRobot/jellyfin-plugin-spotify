using System.Collections.Generic;
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
/// Image provider for Spotify tracks.
/// </summary>
public class TrackImageProvider(ILoggerFactory loggerFactory, SessionManager sessionManager) : IRemoteImageProvider
{
    private readonly ILogger<TrackImageProvider> _logger = loggerFactory.CreateLogger<TrackImageProvider>();
    private readonly SessionManager _sessionManager = sessionManager;

    /// <inheritdoc />
    public string Name => Constants.Name;

    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _sessionManager.GetAsync(url, cancellationToken);
    }

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is not Audio track)
        {
            return [];
        }

        var spotifyId = track.GetProviderId(Constants.ProviderTrack) is { } id ? SpotifyId.TryFromBase62(id) : null;

        if (!spotifyId.HasValue)
        {
            spotifyId ??= TagHelper.ExtractSpotifyIds(track.Path, _logger).Track;
        }

        if (spotifyId is { } spotifyIdValue)
        {
            _logger.LogInformation("Looking up {ID}", Constants.FormatTrackId(spotifyIdValue.Base62));
            var trackData = await _sessionManager.GetTrackAsync(spotifyIdValue, cancellationToken).ConfigureAwait(false);
            var trackImage = trackData.GetRemoteImageInfo(_sessionManager);
            if (trackImage != null)
            {
                return [trackImage];
            }
            else
            {
                _logger.LogInformation("No image found for track ID {ID}", Constants.FormatTrackId(spotifyIdValue.Base62));
                return [];
            }
        }

        _logger.LogInformation("Spotify track ID was not provided, using search");

        var searchTerm = item.Name;
        var searchResults = await _sessionManager.SearchTrackAsync(searchTerm, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Found {Count} search results using term {SearchTerm}", searchResults.Length, searchTerm);

        var allResults = new List<RemoteImageInfo>();
        foreach (var trackId in searchResults)
        {
            _logger.LogDebug("Processing search result: {ResultName}", Constants.FormatTrackId(trackId.Base62));
            var trackData = await _sessionManager.GetTrackAsync(trackId, cancellationToken).ConfigureAwait(false);

            var trackImage = trackData.GetRemoteImageInfo(_sessionManager);
            if (trackImage != null)
            {
                allResults.Add(trackImage);
            }
            else
            {
                _logger.LogInformation("No image found for track ID {ID}", Constants.FormatTrackId(trackId!.Base62));
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
        item is Audio;
}
