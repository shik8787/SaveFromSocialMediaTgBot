using SaveFromSocialMediaTgBot.Exceptions;
using SaveFromSocialMediaTgBot.Data.Models;
using SaveFromSocialMediaTgBot.Interfaces;

namespace SaveFromSocialMediaTgBot.Services;

public class ScraperService(IEnumerable<IVideoScraper> videoScrapers)
{
    public async Task<ScrapedMedia> GetMediaAsync(string url)
    {
        var scrapper = videoScrapers.FirstOrDefault(x => x.CanHandle(url)) ?? throw new InvalidUrlException();
        return await scrapper.GetMediaAsync(url);
    }
}
