using System.Diagnostics;
using Telegram.Bot.Types;

namespace SaveFromSocialMediaTgBot.Logging;

public static class RequestContext
{
    private static readonly AsyncLocal<State?> _current = new();

    public static IDisposable Push(Update update)
    {
        var prev = _current.Value;
        var state = BuildFrom(update);

        _current.Value = state;
        var activity = Activity.Current ?? new Activity("TelegramUpdate").SetIdFormat(ActivityIdFormat.W3C).Start();

        return new Popper(prev, activity);
    }

    public static State? Current => _current.Value;

    private static State BuildFrom(Update update)
    {
        var msg = update.Message ?? update.CallbackQuery?.Message;
        var from = update.Message?.From ?? update.CallbackQuery?.From;

        return new State
        {
            TraceId = Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("n"),
            ChatId = msg?.Chat.Id,
            UserName = from?.Username,
            UpdateType = update.Type.ToString()
        };
    }

    public sealed class State
    {
        public string? TraceId { get; init; }
        public long? ChatId { get; init; }
        public string? UserName { get; init; }
        public string? UpdateType { get; init; }
    }

    private sealed class Popper(State? prev, Activity? activity) : IDisposable
    {
        public void Dispose()
        {
            _current.Value = prev;
            activity?.Stop();
        }
    }
}