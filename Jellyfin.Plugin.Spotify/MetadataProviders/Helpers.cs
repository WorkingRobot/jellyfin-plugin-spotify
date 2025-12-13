using System.Linq;
using Jellyfin.Plugin.Spotify.Api;
using Jellyfin.Plugin.Spotify.ImageProviders;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Proto = Wavee.Protocol.Metadata;

namespace Jellyfin.Plugin.Spotify.MetadataProviders;

internal static class Helpers
{
    public static RemoteSearchResult GetRemoteSearchResult(this Proto.Track me, SessionManager sessionManager)
    {
        var imageUrl = me.GetRemoteImageInfo(sessionManager)?.Url;
        var ret = new RemoteSearchResult
        {
            Name = me.Name,
            ProductionYear = me.Album.Date.Year,
            PremiereDate = me.Album.Date.ToDateTime(),
            ImageUrl = imageUrl,
            Overview = me.Album.Label,
            AlbumArtist = me.Album.Artist.FirstOrDefault()?.GetRemoteSearchResult(sessionManager),
            Artists = [.. me.Artist.Select(a => a.GetRemoteSearchResult(sessionManager))],
        };

        ret.SetProviderId(Constants.ProviderTrack, SpotifyId.FromByteString(me.Gid).Base62);
        ret.SetProviderId(Constants.ProviderAlbum, SpotifyId.FromByteString(me.Album.Gid).Base62);
        if (me.Artist.FirstOrDefault() is { } artist)
        {
            ret.SetProviderId(Constants.ProviderArtist, SpotifyId.FromByteString(artist.Gid).Base62);
        }

        return ret;
    }

    public static RemoteSearchResult GetRemoteSearchResult(this Proto.Artist me, SessionManager sessionManager)
    {
        var imageUrl = me.GetRemoteImageInfo(sessionManager)?.Url;
        var ret = new RemoteSearchResult
        {
            Name = me.Name,
            ImageUrl = imageUrl,
            Overview = me.Biography.FirstOrDefault(b => b.HasText)?.Text ?? string.Empty,
        };
        ret.SetProviderId(Constants.ProviderArtist, SpotifyId.FromByteString(me.Gid).Base62);
        return ret;
    }

    public static RemoteSearchResult GetRemoteSearchResult(this Proto.Album me, SessionManager sessionManager)
    {
        var imageUrl = me.GetRemoteImageInfo(sessionManager)?.Url;
        var ret = new RemoteSearchResult
        {
            Name = me.Name,
            ProductionYear = me.Date.Year,
            PremiereDate = me.Date.ToDateTime(),
            ImageUrl = imageUrl,
            Overview = me.Label,
            AlbumArtist = me.Artist.FirstOrDefault()?.GetRemoteSearchResult(sessionManager),
            Artists = [.. me.Artist.Select(a => a.GetRemoteSearchResult(sessionManager))],
        };

        ret.SetProviderId(Constants.ProviderAlbum, SpotifyId.FromByteString(me.Gid).Base62);
        if (me.Artist.FirstOrDefault() is { } artist)
        {
            ret.SetProviderId(Constants.ProviderArtist, SpotifyId.FromByteString(artist.Gid).Base62);
        }

        return ret;
    }
}
