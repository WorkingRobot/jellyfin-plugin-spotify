using System.Linq;
using Jellyfin.Plugin.Spotify.Api;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Proto = Wavee.Protocol.Metadata;

namespace Jellyfin.Plugin.Spotify.ImageProviders;

internal static class Helpers
{
    public static RemoteImageInfo? GetRemoteImageInfo(this Proto.Track me, SessionManager sessionManager) =>
        me.Album.GetRemoteImageInfo(sessionManager);

    public static RemoteImageInfo? GetRemoteImageInfo(this Proto.Artist me, SessionManager sessionManager) =>
        me.PortraitGroup?.GetRemoteImageInfo(sessionManager) ?? me.Portrait?.GetRemoteImageInfo(sessionManager);

    public static RemoteImageInfo? GetRemoteImageInfo(this Proto.Album me, SessionManager sessionManager) =>
        me.CoverGroup.GetRemoteImageInfo(sessionManager) ?? me.Cover?.GetRemoteImageInfo(sessionManager);

    public static RemoteImageInfo? GetRemoteImageInfo(this Proto.ImageGroup? me, SessionManager sessionManager) =>
        me?.Image?.GetRemoteImageInfo(sessionManager);

    public static RemoteImageInfo? GetRemoteImageInfo(this Google.Protobuf.Collections.RepeatedField<Proto.Image> me, SessionManager sessionManager)
    {
        var bestImage = me.GetBestImage();
        var worstImage = me.GetWorstImage();
        if (bestImage == null || worstImage == null)
        {
            return null;
        }

        int? resolution = bestImage.Size switch
        {
            Proto.Image.Types.Size.Default => 64,
            Proto.Image.Types.Size.Small => 160,
            Proto.Image.Types.Size.Large => 300,
            Proto.Image.Types.Size.Xlarge => 640,
            _ => null,
        };

        return new RemoteImageInfo
        {
            Url = sessionManager.FormatImageUrl(new FileId(bestImage.FileId)),
            ThumbnailUrl = sessionManager.FormatImageUrl(new FileId(worstImage.FileId)),
            Type = ImageType.Primary,
            Width = resolution,
            Height = resolution,
        };
    }

    public static RemoteImageInfo? GetRemoteImageInfo(this Wavee.Protocol.Partner.HeaderImage me)
    {
        var bestImage = me.GetBestImage();
        var worstImage = me.GetWorstImage();
        if (bestImage == null || worstImage == null)
        {
            return null;
        }

        return new RemoteImageInfo
        {
            Url = bestImage.Url,
            ThumbnailUrl = worstImage.Url,
            Type = ImageType.Backdrop,
            Width = bestImage.MaxWidth,
            Height = bestImage.MaxHeight,
        };
    }

    public static Proto.Image? GetBestImage(this Proto.ImageGroup me) =>
        me.Image.GetBestImage();

    public static Proto.Image? GetWorstImage(this Proto.ImageGroup me) =>
        me.Image.GetWorstImage();

    public static Proto.Image? GetBestImage(this Google.Protobuf.Collections.RepeatedField<Proto.Image> me) =>
        me.MaxBy(i => i.Size);

    public static Proto.Image? GetWorstImage(this Google.Protobuf.Collections.RepeatedField<Proto.Image> me) =>
        me.MinBy(i => i.Size);

    public static Wavee.Protocol.Partner.DataSource? GetBestImage(this Wavee.Protocol.Partner.HeaderImage me) =>
        me.Data?.Sources?.MaxBy(i => i.MaxWidth);

    public static Wavee.Protocol.Partner.DataSource? GetWorstImage(this Wavee.Protocol.Partner.HeaderImage me) =>
        me.Data?.Sources?.MinBy(i => i.MaxWidth);
}
