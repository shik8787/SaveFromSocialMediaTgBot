using SaveFromSocialMediaTgBot.Exceptions;
using SaveFromSocialMediaTgBot.Interfaces;

namespace SaveFromSocialMediaTgBot.Services;

public class ScraperService(IEnumerable<IVideoScraper> videoScrapers)
{
    public async Task<Stream> GetVideoStreamAsync(string url)
    {
        var scrapper = videoScrapers.FirstOrDefault(x => x.CanHandle(url)) ?? throw new InvalidUrlException();
        return await scrapper.GetVideoStreamAsync(url);
    }
}