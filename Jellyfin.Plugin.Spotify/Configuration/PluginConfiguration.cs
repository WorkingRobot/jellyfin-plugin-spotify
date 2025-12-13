using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Plugins;
using Wavee.Core.Authentication;

namespace Jellyfin.Plugin.Spotify.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public sealed class PluginConfiguration : BasePluginConfiguration, ICredentialsCache
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        DeviceId = Guid.NewGuid();
    }

    /// <summary>
    /// Gets or sets the Spotify Device ID.
    /// </summary>
    public Guid DeviceId { get; set; }

    public Credentials? SpotifyCredentials { get; set; }

    Task ICredentialsCache.ClearCredentialsAsync(string? username, CancellationToken cancellationToken)
    {
        SpotifyCredentials = null;
        Plugin.Instance!.SaveConfiguration();
        return Task.CompletedTask;
    }

    Task<Credentials?> ICredentialsCache.LoadCredentialsAsync(string? username, CancellationToken cancellationToken)
    {
        return Task.FromResult(username == SpotifyCredentials?.Username ? SpotifyCredentials : null);
    }

    Task<string?> ICredentialsCache.LoadLastUsernameAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(SpotifyCredentials?.Username);
    }

    Task ICredentialsCache.SaveCredentialsAsync(Credentials credentials, CancellationToken cancellationToken)
    {
        SpotifyCredentials = credentials;
        Plugin.Instance!.SaveConfiguration();
        return Task.CompletedTask;
    }
}
