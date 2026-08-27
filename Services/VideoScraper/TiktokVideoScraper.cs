using System.Text.RegularExpressions;
using SaveFromSocialMediaTgBot.Data.Constants;
using SaveFromSocialMediaTgBot.Data.Models;
using SaveFromSocialMediaTgBot.Interfaces;

namespace SaveFromSocialMediaTgBot.Services.VideoScraper;

public class TiktokVideoScraper(
    ILogger<TiktokVideoScraper> logger,
    IConfiguration configuration,
    HttpClient client) : IVideoScraper
{
    private readonly int retryCount =
        int.TryParse(configuration[EnvironmentConstants.RETRY_COUNT], out var count) ? count : 1;

    private readonly Regex pattern = new(PatternConstants.TICKTOCK, RegexOptions.Compiled);

    public bool CanHandle(string url) => url.Contains("tiktok", StringComparison.OrdinalIgnoreCase);

    public async Task<ScrapedMedia> GetMediaAsync(string url)
    {
        logger.LogDebug("Start processing {Url}", url);

        var videoUrl = await GetVideoLinkAsync(client, url) ??
                       throw new FormatException(MessageConstants.ERROR_EMPTY_URL);
        
        logger.LogDebug("Video URL resolved for {Url}", url);

        var request = new HttpRequestMessage(HttpMethod.Get, videoUrl) { Headers = { Referrer = new Uri(url) } };
        var stream = await client.GetOwnedStreamAsync(request);

        logger.LogDebug("Stream opened successfully for {Url}", url);

        return new ScrapedMedia(stream, MediaType.Video);
    }

    private async Task<string?> GetVideoLinkAsync(HttpClient httpClient, string url)
    {
        for (var attempt = 1; attempt <= retryCount; attempt++)
        {
            logger.LogDebug("Fetching metadata (attempt {Attempt}) for {Url}", attempt, url);

            using var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            var match = pattern.Match(content);
            if (match.Success)
            {
                logger.LogDebug("Video extracted on attempt {Attempt} for {Url}", attempt, url);

                return match.Value.Replace("\\u002F", "/");
            }
        }

        return null;
    }
}
