namespace SaveFromSocialMediaTgBot.Services;

internal static class HttpClientStreamExtensions
{
    public static Task<Stream> GetOwnedStreamAsync(
        this HttpClient client,
        string url,
        CancellationToken ct = default)
    {
        return client.GetOwnedStreamAsync(new HttpRequestMessage(HttpMethod.Get, url), ct);
    }

    public static async Task<Stream> GetOwnedStreamAsync(
        this HttpClient client,
        HttpRequestMessage request,
        CancellationToken ct = default)
    {
        HttpResponseMessage? response = null;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            var stream = await response.Content.ReadAsStreamAsync(ct);
            return new ResponseOwnedStream(stream, response);
        }
        catch
        {
            response?.Dispose();
            throw;
        }
        finally
        {
            request.Dispose();
        }
    }
}

internal sealed class ResponseOwnedStream(Stream inner, HttpResponseMessage response) : Stream
{
    public override bool CanRead => inner.CanRead;
    public override bool CanSeek => inner.CanSeek;
    public override bool CanWrite => inner.CanWrite;
    public override long Length => inner.Length;

    public override long Position
    {
        get => inner.Position;
        set => inner.Position = value;
    }

    public override void Flush() => inner.Flush();

    public override Task FlushAsync(CancellationToken cancellationToken) =>
        inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) =>
        inner.Read(buffer, offset, count);

    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        inner.ReadAsync(buffer, cancellationToken);

    public override Task<int> ReadAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken) =>
        inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) =>
        inner.Seek(offset, origin);

    public override void SetLength(long value) => inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) =>
        inner.Write(buffer, offset, count);

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default) =>
        inner.WriteAsync(buffer, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                inner.Dispose();
            }
            finally
            {
                response.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            await inner.DisposeAsync();
        }
        finally
        {
            response.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
