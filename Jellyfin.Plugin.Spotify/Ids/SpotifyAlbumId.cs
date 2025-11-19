using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Spotify.Ids;

/// <summary>
/// Spotify album id.
/// </summary>
public class SpotifyAlbumId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => Constants.Name;

    /// <inheritdoc />
    public string Key => $"{Constants.ProviderKey}:{Constants.AlbumKey}";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Album;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Audio or MusicAlbum;
}
