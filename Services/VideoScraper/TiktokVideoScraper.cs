using System.Text.RegularExpressions;
using SaveFromSocialMediaTgBot.Data.Constants;
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

    public async Task<Stream> GetVideoStreamAsync(string url)
    {
        logger.LogInformation("Start processing {Url}", url);

        var videoUrl = await GetVideoLinkAsync(client, url) ??
                       throw new FormatException(MessageConstants.ERROR_EMPTY_URL);
        
        logger.LogInformation("Video URL resolved for {Url}", url);

        var request = new HttpRequestMessage(HttpMethod.Get, videoUrl) { Headers = { Referrer = new Uri(url) } };

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync();

        logger.LogInformation("Stream opened successfully for {Url}", url);

        return stream;
    }

    private async Task<string?> GetVideoLinkAsync(HttpClient httpClient, string url)
    {
        for (var attempt = 1; attempt <= retryCount; attempt++)
        {
            logger.LogDebug("Fetching metadata (attempt {Attempt}) for {Url}", attempt, url);

            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            var match = pattern.Match(content);
            if (match.Success)
            {
                logger.LogInformation("Video extracted on attempt {Attempt} for {Url}", attempt, url);

                return match.Value.Replace("\\u002F", "/");
            }
        }

        return null;
    }
}