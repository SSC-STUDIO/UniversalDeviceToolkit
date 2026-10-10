using System.Collections.Generic;

namespace UniversalDeviceToolkit.Lib.Network;

/// <summary>
/// Audited built-in domain groups (Steam, GitHub, etc.).
/// Disabled by default — user must explicitly enable groups before they affect PAC/proxy.
/// No remote script download; static lists only.
/// </summary>
public static class BuiltinDomainGroups
{
    public static List<NetworkDomainGroup> CreateDefaults() =>
    [
        new NetworkDomainGroup
        {
            Id = "steam",
            DisplayName = "Steam",
            Description = "Steam store, community and CDN resources",
            IconKey = "SteamLogo",
            Enabled = false,
            Domains =
            [
                "steampowered.com",
                "steamcommunity.com",
                "steamstatic.com",
                "steamserver.net",
                "steamcontent.com",
                "steam-api.com",
                "steamstat.us"
            ],
            SubItems =
            [
                new() { Id = "steam-images",      DisplayName = "Steam 图片",              Domain = "steamcdn-a.akamaihd.net" },
                new() { Id = "steam-static",      DisplayName = "Steam 静态资源",          Domain = "community.steamstatic.com" },
                new() { Id = "steam-update",      DisplayName = "Steam 更新",              Domain = "media.steampowered.com" },
                new() { Id = "steam-community",   DisplayName = "Steam 社区",              Domain = "steamcommunity.com" },
                new() { Id = "steam-store",       DisplayName = "Steam 商店",              Domain = "store.steampowered.com" },
                new() { Id = "steam-discussion",  DisplayName = "Steam 讨论/留言 修复项",  Domain = "配合 Steam 讨论/留言 (IPv4) 使用", IsBeta = true },
                new() { Id = "steam-video-cover", DisplayName = "Steam 社区视频封面加载",  Domain = "img.youtube.com", IsBeta = true },
                new() { Id = "steam-cdn-fix",     DisplayName = "Steam 白山云CDN 修复",    Domain = "*.st.dl.eccdnx.com", Enabled = false },
            ]
        },
        new NetworkDomainGroup
        {
            Id = "github",
            DisplayName = "GitHub",
            Description = "GitHub code hosting and package registry",
            IconKey = "GitHubLogo",
            Enabled = false,
            Domains =
            [
                "github.com",
                "githubusercontent.com",
                "githubassets.com",
                "github.io",
                "ghcr.io",
                "npm.pkg.github.com",
                "api.github.com"
            ],
            SubItems =
            [
                new() { Id = "github-huggingface",  DisplayName = "huggingface.co",          Domain = "huggingface.co", IsBeta = true },
                new() { Id = "github-dev",          DisplayName = "Github Dev",              Domain = "github.dev" },
                new() { Id = "github-api",          DisplayName = "Github Api",              Domain = "api.github.com" },
                new() { Id = "github-assets",       DisplayName = "Github Assets",           Domain = "github.githubassets.com" },
                new() { Id = "github-education",    DisplayName = "Github Education",        Domain = "education.github.com" },
                new() { Id = "github-resources",    DisplayName = "Github Resources",        Domain = "resources.github.com" },
                new() { Id = "github-uploads",      DisplayName = "Github Uploads",          Domain = "uploads.github.com" },
                new() { Id = "github-archive",      DisplayName = "Github Archivprogram",    Domain = "archiveprogram.github.com" },
                new() { Id = "github-usercontent",  DisplayName = "Github UserContent",      Domain = "githubusercontent.com" },
                new() { Id = "github-website",      DisplayName = "Github 网站 (Git Push)",  Domain = "github.com" },
                new() { Id = "github-app",          DisplayName = "Github App",              Domain = "githubapp.com" },
                new() { Id = "github-dockerhub",    DisplayName = "Docker Hub",              Domain = "hub.docker.com" },
                new() { Id = "github-greasyfork",   DisplayName = "greasyfork",              Domain = "greasyfork.org", IsBeta = true },
                new() { Id = "github-io",           DisplayName = "Github.io",               Domain = "github.io" },
            ]
        },
        new NetworkDomainGroup
        {
            Id = "twitch",
            DisplayName = "Twitch",
            Description = "Twitch live streaming and related services",
            IconKey = "TwitchLogo",
            Enabled = false,
            Domains =
            [
                "twitch.tv",
                "ttvnw.net",
                "jtvnw.net"
            ],
            SubItems =
            [
                new() { Id = "twitch-chat",    DisplayName = "Twitch Chat",    Domain = "irc-ws.chat.twitch.tv", Enabled = false },
                new() { Id = "twitch-login",   DisplayName = "Twitch Login",   Domain = "passport.twitch.tv", Enabled = false },
                new() { Id = "twitch-assets",  DisplayName = "Twitch Assets",  Domain = "assets.twitch.tv", Enabled = false },
                new() { Id = "twitch-drops",   DisplayName = "Twitch Drops",   Domain = "pubsub-edge.twitch.tv", Enabled = false },
                new() { Id = "twitch-service", DisplayName = "Twitch Service", Domain = "supervisor.ext-twitch.tv", Enabled = false },
            ]
        },
        new NetworkDomainGroup
        {
            Id = "public-cdn",
            DisplayName = "Public CDN",
            Description = "Common public CDN and translation services",
            IconKey = "CdnLogo",
            Enabled = false,
            Domains =
            [
                "translate.googleapis.com",
                "open.spotify.com",
                "fonts.gstatic.com",
                "gravatar.com",
                "themes.googleusercontent.com",
                "ajax.googleapis.com",
                "fonts.googleapis.com",
                "maxcdn.bootstrapcdn.com",
                "cdn.jsdelivr.net",
                "cdnjs.cloudflare.com",
                "unpkg.com"
            ],
            SubItems =
            [
                new() { Id = "cdn-fonts-gstatic",  DisplayName = "fonts.gstatic.com",            Domain = "fonts.gstatic.com", Enabled = false },
                new() { Id = "cdn-gravatar",       DisplayName = "Gravatar 头像",                Domain = "gravatar.com", Enabled = false },
                new() { Id = "cdn-themes-google",  DisplayName = "themes.googleusercontent.com", Domain = "themes.googleusercontent.com", Enabled = false },
                new() { Id = "cdn-ajax-google",    DisplayName = "ajax.googleapis.com",          Domain = "ajax.googleapis.com", Enabled = false },
                new() { Id = "cdn-fonts-google",   DisplayName = "fonts.googleapis.com",         Domain = "fonts.googleapis.com", Enabled = false },
                new() { Id = "cdn-bootstrap",      DisplayName = "BootStrap CDN",                Domain = "maxcdn.bootstrapcdn.com/bootstrap", Enabled = false },
                new() { Id = "cdn-jsdelivr", DisplayName = "jsDelivr CDN",  Domain = "cdn.jsdelivr.net",     Enabled = false },
                new() { Id = "cdn-cdnjs",    DisplayName = "CDNJS",         Domain = "cdnjs.cloudflare.com", Enabled = false },
                new() { Id = "cdn-unpkg",    DisplayName = "UNPKG",         Domain = "unpkg.com",            Enabled = false },
            ]
        },
        new NetworkDomainGroup
        {
            Id = "roblox",
            DisplayName = "Roblox",
            Description = "Roblox game platform and CDN",
            IconKey = "RobloxLogo",
            Enabled = false,
            Domains =
            [
                "roblox.com",
                "rbxcdn.com",
                "roblox.plus"
            ],
            SubItems =
            [
                new() { Id = "roblox-main", DisplayName = "Roblox Main", Domain = "www.roblox.com", Enabled = false },
                new() { Id = "roblox-cdn",  DisplayName = "Roblox CDN",  Domain = "thumbnails.roblox.com", Enabled = false },
            ]
        }
    ];
}
