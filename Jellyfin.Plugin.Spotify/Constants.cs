using System;
using System.Collections.Generic;
using System.Globalization;
using Jellyfin.Plugin.Spotify.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.Spotify;

/// <summary>
/// Constants for the plugin.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// The name of the plugin.
    /// </summary>
    public const string Name = "Spotify";

    public const string OpenUrl = "https://open.spotify.com";

    public const string ProviderKey = "spotify";
    public const string AlbumKey = "album";
    public const string ArtistKey = "artist";
    public const string TrackKey = "track";

    public const string AlbumName = "Album";
    public const string ArtistName = "Artist";
    public const string TrackName = "Track";

    /// <summary>
    /// The Guid of the plugin.
    /// </summary>
    public static readonly Guid Guid = Guid.Parse("8a586678-5b5f-4a40-afaa-5db100a21b34");
}
