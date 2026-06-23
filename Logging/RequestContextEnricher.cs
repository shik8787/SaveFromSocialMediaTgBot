using Serilog.Core;
using Serilog.Events;

namespace SaveFromSocialMediaTgBot.Logging;

public sealed class RequestContextEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory f)
    {
        var s = RequestContext.Current;
        if (s is null) return;

        Add("TraceId", s.TraceId);
        Add("ChatId", s.ChatId);
        Add("UserName", s.UserName);
        Add("UpdateType", s.UpdateType);
        return;

        void Add(string name, object? value)
        {
            if (value is null || logEvent.Properties.ContainsKey(name)) return;
            logEvent.AddPropertyIfAbsent(f.CreateProperty(name, value));
        }
    }
}