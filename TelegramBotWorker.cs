using SaveFromSocialMediaTgBot.Exceptions;
using SaveFromSocialMediaTgBot.Interfaces;
using SaveFromSocialMediaTgBot.Logging;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SaveFromSocialMediaTgBot;

public class TelegramBotWorker(
    ILogger<TelegramBotWorker> logger,
    ITelegramBotClient client,
    ITelegramBotService telegramBotService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        client.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandleErrorAsync,
            receiverOptions: new ReceiverOptions { AllowedUpdates = [] },
            cancellationToken: stoppingToken
        );
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        using var _ = RequestContext.Push(update);
        try
        {
            switch (update)
            {
                case { Type: UpdateType.CallbackQuery }:
                    logger.LogInformation("Processing callback query");
                    await telegramBotService.CallbackWorkflowAsync(botClient, update, ct);
                    return;

                case { Type: UpdateType.Message, Message.Type: MessageType.Text }:
                    await telegramBotService.UpdateWorkflowAsync(botClient, update, ct);
                    return;

                default:
                    logger.LogWarning("Received unsupported update type: {UpdateType}", update.Type);
                    return;
            }
        }
        catch (InvalidUrlException ex)
        {
            logger.LogError(ex, ex.Message);
            await botClient.SetMessageReaction(update.Message!.Chat.Id, update.Message.Id, [],
                cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, ex.Message);
            await botClient.SetMessageReaction(update.Message!.Chat.Id, update.Message.Id, ["\ud83d\udca9"],
                cancellationToken: ct);
        }
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        logger.LogError(exception, exception.Message);
        return Task.CompletedTask;
    }
}
