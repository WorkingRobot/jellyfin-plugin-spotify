using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Spotify.UrlProviders;

/// <summary>
/// External URL provider for Spotify artists.
/// </summary>
public class ArtistUrlProvider : IExternalUrlProvider
{
    /// <inheritdoc />
    public string Name => $"{Constants.Name} {Constants.ArtistName}";

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item is not MusicArtist)
        {
            yield break;
        }

        if (item.TryGetProviderId($"{Constants.ProviderKey}:{Constants.ArtistKey}", out var spotifyId))
        {
            yield return $"{Constants.OpenUrl}/{Constants.ArtistKey}/{spotifyId}";
        }
    }
}
