using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Spotify.Ids;

/// <summary>
/// Spotify track id.
/// </summary>
public class SpotifyTrackId : IExternalId
{
    /// <inheritdoc />
    public string ProviderName => Constants.Name;

    /// <inheritdoc />
    public string Key => $"{Constants.ProviderKey}:{Constants.TrackKey}";

    /// <inheritdoc />
    public ExternalIdMediaType? Type => ExternalIdMediaType.Track;

    /// <inheritdoc />
    public bool Supports(IHasProviderIds item) => item is Audio;
}
