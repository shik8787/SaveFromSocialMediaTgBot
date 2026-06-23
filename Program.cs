using SaveFromSocialMediaTgBot;
using SaveFromSocialMediaTgBot.Data.Constants;
using SaveFromSocialMediaTgBot.Extensions;
using SaveFromSocialMediaTgBot.Interfaces;
using SaveFromSocialMediaTgBot.Services;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.ConfigureLogging(configuration);
services.AddCache(configuration);
services.AddVideoScrapers();

services.AddTransient<ITelegramBotService, TelegramBotService>();
services.AddSingleton<ITelegramBotClient, TelegramBotClient>(_ =>
{
    var options = new TelegramBotClientOptions(configuration[EnvironmentConstants.BOT_TOKEN]!);
    return new TelegramBotClient(options);
});

services.AddHostedService<TelegramBotWorker>();

var host = builder.Build();
host.Run();