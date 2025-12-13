using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Wavee.Core.Authentication;
using Wavee.Core.Session;
using Wavee.Protocol.Metadata;
using Wavee.Protocol.Partner;

namespace Jellyfin.Plugin.Spotify.Api;

public sealed class SessionManager : IAsyncDisposable
{
    private readonly ILogger<SessionManager> _logger;
    private readonly ILogger<Session> _sessionLogger;
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SessionManager"/> class.
    /// </summary>
    /// <param name="loggerFactory">Instance of the <see cref="ILoggerFactory"/> interface.</param>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    public SessionManager(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
    {
        _logger = loggerFactory.CreateLogger<SessionManager>();
        _sessionLogger = loggerFactory.CreateLogger<Session>();
        _httpClientFactory = httpClientFactory;

        if (Plugin.Instance?.Configuration.SpotifyCredentials is { } creds)
        {
            _ = ConnectAsync(creds).ConfigureAwait(false);
        }
    }

    public Session? ActiveSession { get; private set; }

    public SessionConfig SessionConfig => new()
    {
        DeviceType = DeviceType.Computer,
        DeviceName = "Jellyfin Spotify Plugin",
        DeviceId = Plugin.Instance!.Configuration.DeviceId.ToString(),
        EnableConnect = false,
    };

    public async Task ConnectAsync(Credentials credentials)
    {
        await DisconnectAsync().ConfigureAwait(false);

        ActiveSession = Session.Create(SessionConfig, _httpClientFactory, _sessionLogger);
        var t = ActiveSession.ConnectAsync(credentials, Plugin.Instance!.Configuration);
        _ = t.ContinueWith(
            t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception.Flatten(), "Failed to connect session with provided Spotify credentials.");
                }

                if (t.IsCompletedSuccessfully)
                {
                    _logger.LogInformation("Successfully connected session with provided Spotify credentials.");
                    var userData = ActiveSession.GetUserData();
                    if (userData == null)
                    {
                        _logger.LogWarning("User data is null after successful connection.");
                    }
                    else
                    {
                        _logger.LogInformation("  Username: {Username}", userData.Username);
                    }
                }
            },
            TaskScheduler.Default);
        await t.ConfigureAwait(false);
    }

    public async Task DisconnectAsync()
    {
        if (ActiveSession is { } session)
        {
            ActiveSession = null;
            await session.DisposeAsync().ConfigureAwait(false);

            _logger.LogInformation("Disconnected active session.");
        }
    }

    public async Task<Track> GetTrackAsync(SpotifyId spotifyId, CancellationToken cancellationToken)
    {
        if (ActiveSession is null)
        {
            throw new InvalidOperationException("No active session. Please connect first.");
        }

        var msg = await ActiveSession.SpClient.GetTrackMetadataAsync(spotifyId.Base16, cancellationToken).ConfigureAwait(false);
        return Track.Parser.ParseFrom(msg);
    }

    public async Task<Album> GetAlbumAsync(SpotifyId spotifyId, CancellationToken cancellationToken)
    {
        if (ActiveSession is null)
        {
            throw new InvalidOperationException("No active session. Please connect first.");
        }

        var msg = await ActiveSession.SpClient.GetAlbumMetadataAsync(spotifyId.Base16, cancellationToken).ConfigureAwait(false);
        return Album.Parser.ParseFrom(msg);
    }

    public async Task<Artist> GetArtistAsync(SpotifyId spotifyId, CancellationToken cancellationToken)
    {
        if (ActiveSession is null)
        {
            throw new InvalidOperationException("No active session. Please connect first.");
        }

        var msg = await ActiveSession.SpClient.GetArtistMetadataAsync(spotifyId.Base16, cancellationToken).ConfigureAwait(false);
        return Artist.Parser.ParseFrom(msg);
    }

    public async Task<ArtistOverview> GetArtistOverviewAsync(SpotifyId spotifyId, CancellationToken cancellationToken)
    {
        if (ActiveSession is null)
        {
            throw new InvalidOperationException("No active session. Please connect first.");
        }

        return await ActiveSession.PartnerClient.QueryArtistOverviewAsync(spotifyId.Base62, cancellationToken).ConfigureAwait(false);
    }

    // Truncate search results to first 5 to limit API usage

    public async Task<SpotifyId[]> SearchTrackAsync(string name, CancellationToken cancellationToken)
    {
        if (ActiveSession is null)
        {
            throw new InvalidOperationException("No active session. Please connect first.");
        }

        var results = await ActiveSession.SpClient.GetJsonAsync<ApiSearchResults>(
            $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(name)}&type=track",
            cancellationToken).ConfigureAwait(false);

        return [.. results?.Tracks?.Items?.Select(i => SpotifyId.FromBase62(i.Id!)).Take(5) ?? []];
    }

    public async Task<SpotifyId[]> SearchAlbumAsync(string name, CancellationToken cancellationToken, ILogger? logger = null)
    {
        if (ActiveSession is null)
        {
            throw new InvalidOperationException("No active session. Please connect first.");
        }

        var results = await ActiveSession.SpClient.GetJsonAsync<ApiSearchResults>(
            $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(name)}&type=album",
            cancellationToken).ConfigureAwait(false);

        return [.. results?.Albums?.Items?.Select(i => SpotifyId.FromBase62(i.Id!)).Take(5) ?? []];
    }

    public async Task<SpotifyId[]> SearchArtistAsync(string name, CancellationToken cancellationToken)
    {
        if (ActiveSession is null)
        {
            throw new InvalidOperationException("No active session. Please connect first.");
        }

        var results = await ActiveSession.SpClient.GetJsonAsync<ApiSearchResults>(
            $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(name)}&type=artist",
            cancellationToken).ConfigureAwait(false);

        return [.. results?.Artists?.Items?.Select(i => SpotifyId.FromBase62(i.Id!)).Take(5) ?? []];
    }

    public Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken)
    {
        if (ActiveSession is null)
        {
            throw new InvalidOperationException("No active session. Please connect first.");
        }

        return ActiveSession.SpClient.GetAsync(url, cancellationToken);
    }

    public string FormatImageUrl(FileId imageFile)
    {
        if (ActiveSession is null)
        {
            throw new InvalidOperationException("No active session. Please connect first.");
        }

        var imageUrl = ActiveSession.GetUserData()?.ImageUrl ?? "https://i.scdn.co/image/{file_id}";
        return imageUrl.Replace("{file_id}", imageFile.Base16, StringComparison.Ordinal);
    }

    public async ValueTask DisposeAsync()
    {
        if (ActiveSession is { } session)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class ApiSearchResults
    {
        [JsonPropertyName("tracks")]
        public ReturnType? Tracks { get; set; }

        [JsonPropertyName("albums")]
        public ReturnType? Albums { get; set; }

        [JsonPropertyName("artists")]
        public ReturnType? Artists { get; set; }

        public sealed class ReturnType
        {
            [JsonPropertyName("items")]
            public Item[]? Items { get; set; }
        }

        public sealed class Item
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }
        }
    }
}
