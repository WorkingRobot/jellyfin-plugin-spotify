using System;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Wavee.Core.Authentication;
using Wavee.OAuth;

namespace Jellyfin.Plugin.Spotify.Api;

[ApiController]
[Route("[controller]")]
[Authorize(Policy = Policies.RequiresElevation)]
[Produces(MediaTypeNames.Application.Json)]
public sealed class SpotifyAuthController(ILoggerFactory loggerFactory, SessionManager sessionManager) : ControllerBase, IDisposable
{
    private readonly ILogger<SpotifyAuthController> _logger = loggerFactory.CreateLogger<SpotifyAuthController>();
    private readonly SessionManager _sessionManager = sessionManager;

    private CancellationTokenSource? ActiveDeviceCodeRequest { get; set; }

    /// <summary>
    /// Creates an auth request URL and prepares the callback event.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The login request uri.</returns>
    [HttpGet("StartAuth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> StartAuth(
        CancellationToken cancellationToken)
    {
        var request = ActiveDeviceCodeRequest;
        if (request != null)
        {
            ActiveDeviceCodeRequest = null;
            await request.CancelAsync().ConfigureAwait(false);
        }

        var flow = new DeviceCodeFlow(
            _sessionManager.SessionConfig.GetClientId(),
            ["streaming", "user-read-playback-state", "user-modify-playback-state"],
            _logger);

        var deviceCode = await flow.RequestDeviceCodeAsync(cancellationToken).ConfigureAwait(false);

        ActiveDeviceCodeRequest = new CancellationTokenSource();

        var cbToken = ActiveDeviceCodeRequest.Token;
        var pollTask = flow.PollForTokenAsync(deviceCode, cbToken);
        var continueTask = pollTask.ContinueWith(
            async t =>
            {
                using var flow_ = flow;

                if (t.IsCanceled || cbToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Device code auth flow was cancelled.");
                    return;
                }

                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception.Flatten(), "Device code auth flow failed.");
                    return;
                }

                var token = Credentials.WithAccessToken((await t.ConfigureAwait(false)).AccessToken);

                await _sessionManager.ConnectAsync(token).ConfigureAwait(false);

                _logger.LogInformation("Successfully authenticated with Spotify via device code flow.");
            },
            scheduler: TaskScheduler.Default).ConfigureAwait(false);

        return Ok(deviceCode);
    }

    [HttpGet("RemoveAuth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> RemoveAuth()
    {
        var request = ActiveDeviceCodeRequest;
        if (request != null)
        {
            ActiveDeviceCodeRequest = null;
            await request.CancelAsync().ConfigureAwait(false);
        }

        _logger.LogInformation("Disconnecting");
        _ = _sessionManager.DisconnectAsync().ConfigureAwait(false);
        _logger.LogInformation("Clearing");
        await (Plugin.Instance!.Configuration as ICredentialsCache).ClearCredentialsAsync(null, default).ConfigureAwait(false);
        _logger.LogInformation("Returning");

        return Ok(JsonDocument.Parse("{}"));
    }

    public void Dispose()
    {
        ActiveDeviceCodeRequest?.Dispose();
    }
}
