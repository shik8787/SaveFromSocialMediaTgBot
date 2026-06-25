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
    private readonly NavigationOptions navigationOptions = new() { WaitUntil = [WaitUntilNavigation.DOMContentLoaded] };
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
                    logger.LogDebug("Media not found, re-authorizing for {Url}", pageUrl);
                    await page.SetCookieAsync(await AuthorizationAsync(page));
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

        matches.AddRange(videoPattern.Matches(content)
            .Select(match => (match.Index, match.Groups["url"].Value, MediaType.Video)));

        matches.AddRange(photoPattern.Matches(content)
            .Select(match => (match.Index, match.Groups["url"].Value, MediaType.Photo)));

        if (matches.Count == 0)
        {
            matches.AddRange(displayPhotoPattern.Matches(content)
                .Select(match => (match.Index, match.Groups["url"].Value, MediaType.Photo)));
        }

        return matches
            .Where(x => !string.IsNullOrWhiteSpace(x.Url))
            .OrderBy(x => x.Index)
            .DistinctBy(x => x.Url)
            .Take(MAX_TELEGRAM_ALBUM_ITEMS)
            .Select(x => (x.Url, x.Type))
            .ToList();
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
        await page.SetCookieAsync(Cookies);
    }

    private async Task<CookieParam[]> AuthorizationAsync(IPage page)
    {
        logger.LogInformation("Re-authorizing Instagram session");

        await page.GoToAsync("https://www.instagram.com/accounts/login/");
        await page.WaitForSelectorAsync("input[name='username']");
        await page.WaitForSelectorAsync("input[name='password']");
        await Task.Delay(random.Next(800, 1000));
        await page.TypeAsync("input[name='username']", login, typeOptions);
        await Task.Delay(random.Next(500, 1000));
        await page.TypeAsync("input[name='password']", password, typeOptions);
        await Task.Delay(random.Next(500, 1000));
        await page.ClickAsync("button[type='submit']");
        await page.WaitForNavigationAsync(navigationOptions);
        Cookies = await page.GetCookiesAsync();

        logger.LogInformation("Instagram re-authorization successful");

        return Cookies;
    }

    private string DecodeContent(string rawContent)
    {
        var unescaped = Regex.Unescape(rawContent);
        var fullyDecoded = HttpUtility.HtmlDecode(unescaped);
        fullyDecoded = fullyDecoded.Replace("\\/", "/");
        return fullyDecoded;
    }
}
