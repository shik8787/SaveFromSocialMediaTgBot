namespace SaveFromSocialMediaTgBot.Data.Models;

public sealed record ScrapedMedia(IReadOnlyList<ScrapedMediaItem> Items)
{
    public ScrapedMedia(Stream stream, MediaType type)
        : this([new ScrapedMediaItem(stream, type)])
    {
    }
}

public sealed record ScrapedMediaItem(Stream Stream, MediaType Type);
