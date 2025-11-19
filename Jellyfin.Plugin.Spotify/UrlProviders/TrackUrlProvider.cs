using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Spotify.UrlProviders;

/// <summary>
/// External URL provider for Spotify tracks.
/// </summary>
public class TrackUrlProvider : IExternalUrlProvider
{
    /// <inheritdoc />
    public string Name => $"{Constants.Name} {Constants.TrackName}";

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item is not Audio track)
        {
            yield break;
        }

        if (track.TryGetProviderId($"{Constants.ProviderKey}:{Constants.TrackKey}", out var spotifyId))
        {
            yield return $"{Constants.OpenUrl}/{Constants.TrackKey}/{spotifyId}";
        }
    }
}
