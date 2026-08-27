namespace SaveFromSocialMediaTgBot.Data.Models;

public sealed record ScrapedMedia(IReadOnlyList<ScrapedMediaItem> Items) : IAsyncDisposable
{
    public ScrapedMedia(Stream stream, MediaType type)
        : this([new ScrapedMediaItem(stream, type)])
    {
    }

    public async ValueTask DisposeAsync()
    {
        await Task.WhenAll(Items.Select(item => item.Stream.DisposeAsync().AsTask()));
    }
}

public sealed record ScrapedMediaItem(Stream Stream, MediaType Type);
