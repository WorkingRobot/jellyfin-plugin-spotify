using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Spotify.Ids;

/// <summary>
/// Spotify artist id.
/// </summary>
public class SpotifyArtistId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => Constants.Name;

    /// <inheritdoc />
    public string Key => $"{Constants.ProviderKey}:{Constants.ArtistKey}";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Artist;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is MusicArtist;
}
