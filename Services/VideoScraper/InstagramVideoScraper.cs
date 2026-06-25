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

    public async Task<ScrapedMedia> GetMediaAsync(string url)
    {
        logger.LogInformation("Start processing {Url}", url);

        var media = await TryGetMediaUrlAsync(url) ?? throw new FormatException(MessageConstants.ERROR_EMPTY_URL);

        logger.LogInformation("{MediaType} URL resolved for {Url}", media.Type, url);

        var stream = await client.GetStreamAsync(media.Url);

        logger.LogInformation("Stream opened successfully for {Url}", url);

        return new ScrapedMedia(stream, media.Type);
    }

    private async Task<(string Url, MediaType Type)?> TryGetMediaUrlAsync(string pageUrl)
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

                var videoMatch = videoPattern.Match(content);
                if (videoMatch.Success)
                {
                    logger.LogInformation("Video extracted on attempt {Attempt} for {Url}", attempt, pageUrl);
                    return (videoMatch.Groups["url"].Value, MediaType.Video);
                }

                var photoMatch = photoPattern.Match(content);
                if (!photoMatch.Success)
                    photoMatch = displayPhotoPattern.Match(content);

                if (photoMatch.Success)
                {
                    logger.LogInformation("Photo extracted on attempt {Attempt} for {Url}", attempt, pageUrl);
                    return (photoMatch.Groups["url"].Value, MediaType.Photo);
                }

                if (attempt == 1)
                {
                    logger.LogDebug("Media not found, re-authorizing for {Url}", pageUrl);
                    await page.SetCookieAsync(await AuthorizationAsync(page));
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Metadata fetch failed for {Url}", pageUrl);
            throw;
        }
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
