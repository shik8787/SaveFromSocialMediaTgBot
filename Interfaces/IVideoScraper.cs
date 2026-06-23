namespace SaveFromSocialMediaTgBot.Interfaces;

public interface IVideoScraper
{
    bool CanHandle(string url);
    Task<Stream> GetVideoStreamAsync(string url);
}