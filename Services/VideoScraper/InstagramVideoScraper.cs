using System.Text.RegularExpressions;
using System.Web;
using PuppeteerSharp;
using PuppeteerSharp.Input;
using SaveFromSocialMediaTgBot.Data.Constants;
using SaveFromSocialMediaTgBot.Data.Models;
using SaveFromSocialMediaTgBot.Interfaces;

namespace SaveFromSocialMediaTgBot.Services.VideoScraper;

public class InstagramVideoScraper(
    ILogger<InstagramVideoScraper> logger,
    IConfiguration configuration,
    HttpClient client) : IVideoScraper
{
    private readonly Random random = new();
    private readonly Regex videoPattern = new(PatternConstants.INSTAGRAM, RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private readonly Regex photoPattern = new(PatternConstants.INSTAGRAM_PHOTO, RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private readonly Regex displayPhotoPattern = new(PatternConstants.INSTAGRAM_DISPLAY_PHOTO, RegexOptions.IgnoreCase | RegexOptions.Singleline);
    private readonly string login = configuration[EnvironmentConstants.INST_LOGIN] ?? "";
    private readonly string password = configuration[EnvironmentConstants.INST_PASSWORD] ?? "";
    private string sessionId = configuration[EnvironmentConstants.INST_COOKIE_SESSION_ID] ?? "";
    private const int NAVIGATION_TIMEOUT_MS = 90_000;
    private readonly NavigationOptions navigationOptions = new()
    {
        WaitUntil = [WaitUntilNavigation.DOMContentLoaded],
        Timeout = NAVIGATION_TIMEOUT_MS
    };
    private readonly WaitForSelectorOptions selectorOptions = new() { Timeout = 30_000 };
    private readonly TypeOptions typeOptions = new() { Delay = 150 };
    private static readonly SemaphoreSlim BrowserConcurrency = new(1, 1);

    private readonly LaunchOptions launchOptions = new()
    {
        Headless = true,
        ExecutablePath = "/usr/bin/chromium",
        Args =
        [
            "--no-sandbox",
            "--disable-setuid-sandbox",
            "--disable-background-networking",
            "--disable-component-update",
            "--disable-sync",
            "--disk-cache-size=0",
            "--media-cache-size=0"
        ]
    };

    private static CookieParam[]? Cookies { get; set; }

    public bool CanHandle(string url) => url.Contains("instagram.com", StringComparison.OrdinalIgnoreCase);

    private const int MAX_TELEGRAM_ALBUM_ITEMS = 10;

    public async Task<ScrapedMedia> GetMediaAsync(string url)
    {
        logger.LogDebug("Start processing {Url}", url);

        var media = await TryGetMediaUrlsAsync(url);
        if (media.Count == 0)
            throw new FormatException(MessageConstants.ERROR_EMPTY_URL);

        logger.LogDebug("{MediaCount} media URL(s) resolved for {Url}", media.Count, url);

        var items = new List<ScrapedMediaItem>();
        try
        {
            foreach (var item in media)
            {
                var stream = await client.GetOwnedStreamAsync(item.Url);
                items.Add(new ScrapedMediaItem(stream, item.Type));
            }
        }
        catch
        {
            await Task.WhenAll(items.Select(item => item.Stream.DisposeAsync().AsTask()));
            throw;
        }

        logger.LogDebug("{MediaCount} stream(s) opened successfully for {Url}", items.Count, url);

        return new ScrapedMedia(items);
    }

    private async Task<IReadOnlyList<(string Url, MediaType Type)>> TryGetMediaUrlsAsync(string pageUrl)
    {
        await BrowserConcurrency.WaitAsync();
        try
        {
            var linkType = GetInstagramLinkType(pageUrl);
            pageUrl = NormalizePageUrl(pageUrl);

            await using var browser = await Puppeteer.LaunchAsync(launchOptions);
            await using var page = await browser.NewPageAsync();

            try
            {
                await SetCookiesAsync(page);

                for (var attempt = 1; attempt <= 2; attempt++)
                {
                    logger.LogDebug("Fetching page (attempt {Attempt}) for {Url}", attempt, pageUrl);

                    await page.GoToAsync(pageUrl, navigationOptions);
                    if (linkType == InstagramLinkType.Unknown)
                        linkType = GetInstagramLinkType(page.Url);

                    var content = await page.GetContentAsync();
                    content = DecodeContent(content);

                    var media = ExtractMediaUrls(content, linkType);
                    if (media.Count > 0)
                    {
                        logger.LogDebug("{MediaCount} media item(s) extracted on attempt {Attempt} for {Url}",
                            media.Count, attempt, pageUrl);
                        return media;
                    }

                    if (attempt == 1)
                    {
                        var hasCredentials = !string.IsNullOrWhiteSpace(login) && !string.IsNullOrWhiteSpace(password);
                        var hasCookies = Cookies is { Length: > 0 };

                        if (!hasCredentials && !hasCookies)
                        {
                            logger.LogWarning(
                                "Media not found for {Url}, and Instagram credentials/cookies are not configured; skipping re-authorization",
                                pageUrl);
                            break;
                        }

                        logger.LogDebug("Media not found, re-authorizing for {Url}", pageUrl);
                        try
                        {
                            await page.SetCookieAsync(await AuthorizationAsync(page));
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Instagram re-authorization failed for {Url}", pageUrl);
                            break;
                        }
                    }
                }

                return [];
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Metadata fetch failed for {Url}", pageUrl);
                throw;
            }
        }
        finally
        {
            BrowserConcurrency.Release();
        }
    }

    private List<(string Url, MediaType Type)> ExtractMediaUrls(string content, InstagramLinkType linkType)
    {
        if (linkType == InstagramLinkType.Video)
            return ExtractSingleVideoUrl(content);

        return ExtractPostMediaUrls(content);
    }

    private List<(string Url, MediaType Type)> ExtractSingleVideoUrl(string content)
    {
        var match = videoPattern.Match(content);
        return match.Success
            ? [(match.Groups["url"].Value, MediaType.Video)]
            : [];
    }

    private List<(string Url, MediaType Type)> ExtractPostMediaUrls(string content)
    {
        if (IsCarouselContent(content))
        {
            var carousel = ExtractCarouselMediaUrls(content);
            if (carousel.Count > 0)
                return carousel;
        }

        var video = videoPattern.Match(content);
        if (video.Success)
            return [(video.Groups["url"].Value, MediaType.Video)];

        var photo = photoPattern.Match(content);
        if (!photo.Success)
            photo = displayPhotoPattern.Match(content);

        return photo.Success
            ? [(photo.Groups["url"].Value, MediaType.Photo)]
            : [];
    }

    private List<(string Url, MediaType Type)> ExtractCarouselMediaUrls(string content)
    {
        foreach (var marker in new[] { "\"carousel_media\"", "\"edge_sidecar_to_children\"" })
        {
            var markerIndex = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            while (markerIndex >= 0)
            {
                var arrayStart = content.IndexOf('[', markerIndex + marker.Length);
                if (arrayStart >= 0 && arrayStart - markerIndex <= 500)
                {
                    var result = ExtractMediaFromArray(content, arrayStart);
                    if (result.Count > 0)
                        return result;
                }

                markerIndex = content.IndexOf(marker, markerIndex + marker.Length,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        return [];
    }

    private List<(string Url, MediaType Type)> ExtractMediaFromArray(string content, int arrayStart)
    {
        var result = new List<(string Url, MediaType Type)>();
        var arrayDepth = 0;
        var objectDepth = 0;
        var objectStart = -1;
        var inString = false;
        var escaped = false;

        for (var i = arrayStart; i < content.Length; i++)
        {
            var ch = content[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (ch == '\\')
                {
                    escaped = true;
                }
                else if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (ch == '"')
            {
                inString = true;
                continue;
            }

            if (ch == '[')
            {
                arrayDepth++;
                continue;
            }

            if (ch == ']')
            {
                arrayDepth--;
                if (arrayDepth == 0)
                    break;
                continue;
            }

            if (ch == '{')
            {
                if (arrayDepth == 1 && objectDepth == 0)
                    objectStart = i;
                objectDepth++;
                continue;
            }

            if (ch != '}' || objectDepth == 0)
                continue;

            objectDepth--;
            if (arrayDepth != 1 || objectDepth != 0 || objectStart < 0)
                continue;

            var item = content.Substring(objectStart, i - objectStart + 1);
            var video = videoPattern.Match(item);
            if (video.Success)
            {
                result.Add((video.Groups["url"].Value, MediaType.Video));
            }
            else
            {
                var photo = photoPattern.Match(item);
                if (!photo.Success)
                    photo = displayPhotoPattern.Match(item);
                if (photo.Success)
                    result.Add((photo.Groups["url"].Value, MediaType.Photo));
            }

            objectStart = -1;
            if (result.Count == MAX_TELEGRAM_ALBUM_ITEMS)
                break;
        }

        return result
            .Where(item => !string.IsNullOrWhiteSpace(item.Url))
            .DistinctBy(item => item.Url)
            .ToList();
    }

    private static bool IsCarouselContent(string content)
    {
        return content.Contains("edge_sidecar_to_children", StringComparison.OrdinalIgnoreCase)
               || content.Contains("carousel_media", StringComparison.OrdinalIgnoreCase)
               || content.Contains("GraphSidecar", StringComparison.OrdinalIgnoreCase);
    }

    private static InstagramLinkType GetInstagramLinkType(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return InstagramLinkType.Unknown;

        var path = uri.AbsolutePath;

        if (path.StartsWith("/reel/", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/tv/", StringComparison.OrdinalIgnoreCase))
        {
            return InstagramLinkType.Video;
        }

        if (path.StartsWith("/p/", StringComparison.OrdinalIgnoreCase))
            return InstagramLinkType.Post;

        return InstagramLinkType.Unknown;
    }

    private async Task SetCookiesAsync(IPage page)
    {
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            Cookies =
            [
                new CookieParam
                {
                    Name = "sessionid",
                    Value = sessionId,
                    Domain = ".instagram.com"
                }
            ];

            sessionId = string.Empty;
        }
        if (Cookies is not null)
            await page.SetCookieAsync(Cookies);
    }

    private async Task<CookieParam[]> AuthorizationAsync(IPage page)
    {
        logger.LogInformation("Re-authorizing Instagram session");

        await page.GoToAsync("https://www.instagram.com/accounts/login/", navigationOptions);

        try
        {
            await page.WaitForSelectorAsync("input[name='username']", selectorOptions);
            await page.WaitForSelectorAsync("input[name='password']", selectorOptions);
        }
        catch (WaitTaskTimeoutException)
        {
            Cookies = await page.GetCookiesAsync();

            if (Cookies.Any(x => x.Name == "sessionid"))
            {
                logger.LogInformation("Instagram login form was not shown; existing session cookie is active");
                return Cookies;
            }

            throw;
        }

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Instagram login form was shown, but login/password are not configured");
            Cookies = await page.GetCookiesAsync();
            return Cookies;
        }

        await Task.Delay(random.Next(800, 1000));
        await page.TypeAsync("input[name='username']", login, typeOptions);
        await Task.Delay(random.Next(500, 1000));
        await page.TypeAsync("input[name='password']", password, typeOptions);
        await Task.Delay(random.Next(500, 1000));
        await page.ClickAsync("button[type='submit']");
        try
        {
            await page.WaitForNavigationAsync(navigationOptions);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Instagram login navigation timeout; using cookies collected after submit");
        }

        Cookies = await page.GetCookiesAsync();

        logger.LogInformation("Instagram re-authorization successful");

        return Cookies;
    }

    private static string NormalizePageUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.ToString();
    }

    private string DecodeContent(string rawContent)
    {
        var unescaped = Regex.Unescape(rawContent);
        var fullyDecoded = HttpUtility.HtmlDecode(unescaped);
        fullyDecoded = fullyDecoded.Replace("\\/", "/");
        return fullyDecoded;
    }

    private enum InstagramLinkType
    {
        Unknown,
        Video,
        Post
    }
}
