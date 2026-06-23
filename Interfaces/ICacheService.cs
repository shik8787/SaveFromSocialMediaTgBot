
namespace SaveFromSocialMediaTgBot.Interfaces;

public interface ICacheService
{
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> fetchFunction, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, CancellationToken ct = default);
}