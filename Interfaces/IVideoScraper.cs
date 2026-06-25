using SaveFromSocialMediaTgBot.Data.Models;

namespace SaveFromSocialMediaTgBot.Interfaces;

public interface IVideoScraper
{
    bool CanHandle(string url);
    Task<ScrapedMedia> GetMediaAsync(string url);
}
