using Telegram.Bot;
using Telegram.Bot.Types;

namespace SaveFromSocialMediaTgBot.Interfaces;

public interface ITelegramBotService
{
    Task UpdateWorkflowAsync(ITelegramBotClient client, Update update, CancellationToken ct);
    Task CallbackWorkflowAsync(ITelegramBotClient client, Update update, CancellationToken ct);
}
