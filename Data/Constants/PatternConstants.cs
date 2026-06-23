namespace SaveFromSocialMediaTgBot.Data.Constants;

/// <summary>
/// Pattern for searching video link inside html
/// </summary>
public static class PatternConstants
{
    public const string INSTAGRAM = @"""video_versions"":\s*\[\s*{[^}]*""url"":\s*""(?<url>https://[^""]+\.mp4[^""]*)""";
    public const string TICKTOCK = @"https?:\\u002F\\u002F[^""'\s]*?mime_type=video_mp4[^""'\s]*?tt_chain_token";
    public const string TWITTER = @"https?://(?:(?:www|m(?:obile)?)\.)?(?:twitter\.com|x\.com)/(?:(?:i/web|[^/]+)/status|statuses)/(\d+)(?:/(?:video|photo)/(\d+))?";
    public const string YOUTUBE = @"iPhone"",\S+""com.google.ios.youtube/";
}