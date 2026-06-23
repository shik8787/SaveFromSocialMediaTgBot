using System.Net;
using PuppeteerSharp;
using SaveFromSocialMediaTgBot.Data.Constants;
using SaveFromSocialMediaTgBot.Interfaces;
using SaveFromSocialMediaTgBot.Logging;
using SaveFromSocialMediaTgBot.Services;
using SaveFromSocialMediaTgBot.Services.VideoScraper;
using Serilog;

namespace SaveFromSocialMediaTgBot.Extensions;

public static class AppBuilderExtension
{
    internal static void AddCache(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration[EnvironmentConstants.REDIS_CONNECTION_STRING];
            options.InstanceName = "Chat-settings:";
        });

        services.AddSingleton<ICacheService, CacheService>();
    }

    internal static void AddVideoScrapers(this IServiceCollection services)
    {
        // Puppeteer client for instagram
        new BrowserFetcher().DownloadAsync();

        services.AddHttpClient<IVideoScraper, InstagramVideoScraper>();
        services.AddHttpClient<IVideoScraper, TwitterVideoScraper>();
        services.AddHttpClient<IVideoScraper, YoutubeVideoScraper>();
        services.AddHttpClient<IVideoScraper, TiktokVideoScraper>(client =>
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true,
                AllowAutoRedirect = true
            });

        services.AddSingleton<ScraperService>();
    }

    internal static void ConfigureLogging(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Filter.ByIncludingOnly(evt =>
            {
                if (evt.Properties.TryGetValue("SourceContext", out var ctx))
                {
                    var source = ctx.ToString().Trim('"');
                    if (source.StartsWith("System.Net.Http.HttpClient.IVideoScraper.LogicalHandler") ||
                        source.StartsWith("System.Net.Http.HttpClient.IVideoScraper.ClientHandler"))
                    {
                        return evt.Level >= Serilog.Events.LogEventLevel.Warning;
                    }
                }

                return true;
            })
            .Enrich.With<RequestContextEnricher>()
            .CreateLogger();

        services.AddSerilog();
    }
}