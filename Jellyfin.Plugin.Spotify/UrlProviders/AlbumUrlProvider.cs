using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Spotify.UrlProviders;

/// <summary>
/// External URL provider for Spotify albums.
/// </summary>
public class AlbumUrlProvider : IExternalUrlProvider
{
    /// <inheritdoc />
    public string Name => $"{Constants.Name} {Constants.AlbumName}";

    /// <inheritdoc />
    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item is not MusicAlbum album)
        {
            yield break;
        }

        if (album.TryGetProviderId($"{Constants.ProviderKey}:{Constants.AlbumKey}", out var spotifyId))
        {
            yield return $"{Constants.OpenUrl}/{Constants.AlbumKey}/{spotifyId}";
        }
    }
}
