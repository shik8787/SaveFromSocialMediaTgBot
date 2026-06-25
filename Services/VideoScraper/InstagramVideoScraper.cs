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

    private readonly LaunchOptions launchOptions = new()
    {
        Headless = true,
        ExecutablePath = "/usr/bin/chromium",
        Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"]
    };

    private static CookieParam[]? Cookies { get; set; }

    public bool CanHandle(string url) => url.Contains("instagram.com", StringComparison.OrdinalIgnoreCase);

    private const int MAX_TELEGRAM_ALBUM_ITEMS = 10;

    public async Task<ScrapedMedia> GetMediaAsync(string url)
    {
        logger.LogInformation("Start processing {Url}", url);

        var media = await TryGetMediaUrlsAsync(url);
        if (media.Count == 0)
            throw new FormatException(MessageConstants.ERROR_EMPTY_URL);

        logger.LogInformation("{MediaCount} media URL(s) resolved for {Url}", media.Count, url);

        var items = new List<ScrapedMediaItem>();
        foreach (var item in media)
        {
            var stream = await client.GetStreamAsync(item.Url);
            items.Add(new ScrapedMediaItem(stream, item.Type));
        }

        logger.LogInformation("{MediaCount} stream(s) opened successfully for {Url}", items.Count, url);

        return new ScrapedMedia(items);
    }

    private async Task<IReadOnlyList<(string Url, MediaType Type)>> TryGetMediaUrlsAsync(string pageUrl)
    {
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
                var content = await page.GetContentAsync();
                content = DecodeContent(content);

                var media = ExtractMediaUrls(content);
                if (media.Count > 0)
                {
                    logger.LogInformation("{MediaCount} media item(s) extracted on attempt {Attempt} for {Url}",
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

    private List<(string Url, MediaType Type)> ExtractMediaUrls(string content)
    {
        var matches = new List<(int Index, string Url, MediaType Type)>();
        var isCarousel = IsCarouselContent(content);

        matches.AddRange(videoPattern.Matches(content)
            .Select(match => (match.Index, match.Groups["url"].Value, MediaType.Video)));

        if (!isCarousel && matches.Count > 0)
            return [matches.OrderBy(x => x.Index).Select(x => (x.Url, x.Type)).First()];

        matches.AddRange(photoPattern.Matches(content)
            .Select(match => (match.Index, match.Groups["url"].Value, MediaType.Photo)));

        if (matches.Count == 0)
        {
            matches.AddRange(displayPhotoPattern.Matches(content)
                .Select(match => (match.Index, match.Groups["url"].Value, MediaType.Photo)));
        }

        var result = matches
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .OrderBy(x => x.Index)
            .DistinctBy(x => x.Url)
            .Take(MAX_TELEGRAM_ALBUM_ITEMS)
            .Select(x => (x.Url, x.Type))
            .ToList();

        return isCarousel
            ? result
            : result.Take(1).ToList();
    }

    private static bool IsCarouselContent(string content)
    {
        return content.Contains("edge_sidecar_to_children", StringComparison.OrdinalIgnoreCase)
               || content.Contains("carousel_media", StringComparison.OrdinalIgnoreCase)
               || content.Contains("GraphSidecar", StringComparison.OrdinalIgnoreCase);
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
}
