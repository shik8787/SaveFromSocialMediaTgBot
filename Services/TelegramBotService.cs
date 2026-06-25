using SaveFromSocialMediaTgBot.Data.Constants;
using SaveFromSocialMediaTgBot.Data.Models;
using SaveFromSocialMediaTgBot.Extensions;
using SaveFromSocialMediaTgBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SaveFromSocialMediaTgBot.Services;

public class TelegramBotService(
    ScraperService scraperService,
    ICacheService cacheService,
    ILogger<TelegramBotService> logger) : ITelegramBotService
{
    public async Task UpdateWorkflowAsync(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        var chatSettings = await cacheService.GetOrCreateAsync(update.Message!.Chat.Id.ToString(),
            async () => new ChatSettings(), ct);
        var botInfo = await client.GetMe(cancellationToken: ct);
        var message = new ParsedMessage(update.Message, botInfo.Username!, chatSettings);

        switch (message.Type)
        {
            case MessageEntityType.BotCommand:
                await ProcessBotCommandAsync(client, message, ct);
                return;
            case MessageEntityType.Url:
                await LinkHandlerAsync(client, message, ct);
                return;
            default:
                return;
        }
    }

    public async Task CallbackWorkflowAsync(ITelegramBotClient client, Update update, CancellationToken ct)
    {
        var data = update.CallbackQuery!.Data ?? string.Empty;
        var parts = data.Split(':', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var model = new
        {
            ChatId = update.CallbackQuery.Message!.Chat.Id,
            ChatType = update.CallbackQuery.Message!.Chat.Type,
            UserId = update.CallbackQuery.From.Id,
            Command = parts.Length > 0 ? parts[0] : string.Empty,
            Value = parts.Length > 1 ? parts[1] : string.Empty,
            MessageId = update.CallbackQuery.Message!.MessageId
        };

        if (!await IsAllowSettingsAsync(client, model.ChatId, model.UserId, model.ChatType, ct))
        {
            await client.AnswerCallbackQuery(callbackQueryId: update.CallbackQuery.Id,
                text: MessageConstants.ACCESS_DENIED,
                cancellationToken: ct);
            return;
        }

        var chatSettings = await cacheService.GetOrCreateAsync(model.ChatId.ToString(),
            async () => new ChatSettings(), ct);

        var changed = false;

        switch (model.Command)
        {
            case CommandConstants.CLOSE_SETTINGS:
            {
                await client.DeleteMessage(chatId: model.ChatId, messageId: model.MessageId,
                    cancellationToken: ct);
                break;
            }

            case CommandConstants.NOTIFICATION_MODE:
            {
                if (bool.TryParse(model.Value, out var result))
                {
                    chatSettings.Notification = result;
                    changed = true;
                }

                await cacheService.SetAsync(model.ChatId.ToString(), chatSettings, ct);
                await client.AnswerCallbackQuery(callbackQueryId: update.CallbackQuery.Id,
                    text: MessageConstants.SUCCESS, cancellationToken: ct);
                break;
            }

            case CommandConstants.MENTION_MODE:
            {
                if (bool.TryParse(model.Value, out var result))
                {
                    chatSettings.Mention = result;
                    changed = true;
                }

                await cacheService.SetAsync(model.ChatId.ToString(), chatSettings, ct);
                await client.AnswerCallbackQuery(callbackQueryId: update.CallbackQuery.Id,
                    text: MessageConstants.SUCCESS, cancellationToken: ct);
                break;
            }
            
            case CommandConstants.DELETE_ORIGIN_MESSAGE:
            {
                if (bool.TryParse(model.Value, out var result))
                {
                    chatSettings.DeleteOriginMessage = result;
                    changed = true;
                }

                await cacheService.SetAsync(model.ChatId.ToString(), chatSettings, ct);
                await client.AnswerCallbackQuery(callbackQueryId: update.CallbackQuery.Id,
                    text: MessageConstants.SUCCESS, cancellationToken: ct);
                break;
            }
        }

        if (changed)
        {
            await client.EditMessageText(
                chatId: model.ChatId,
                messageId: model.MessageId,
                text: chatSettings.BuildSettingsText(),
                replyMarkup: chatSettings.BuildSettingsKeyboard(),
                cancellationToken: ct);
        }
    }

    private async Task ProcessBotCommandAsync(ITelegramBotClient client, ParsedMessage message, CancellationToken ct)
    {
        if (!await IsAllowSettingsAsync(client, message.ChatId, message.UserId, message.ChatType, ct))
        {
            await client.DeleteMessage(message.ChatId, message.Id, ct);
            return;
        }

        var botInfo = await client.GetMe(cancellationToken: ct);

        switch (message.BotCommand)
        {
            case var command when command == $"{CommandConstants.SETTINGS}@{botInfo.Username}" ||
                                  command == $"{CommandConstants.SETTINGS}" & message.ChatType == ChatType.Private:
            {
                await client.SendMessage(
                    chatId: message.ChatId,
                    text: message.Settings.BuildSettingsText(),
                    replyMarkup: message.Settings.BuildSettingsKeyboard(),
                    cancellationToken: ct);

                await client.DeleteMessage(chatId: message.ChatId, messageId: message.Id,
                    cancellationToken: ct);

                break;
            }
        }
    }

    private async Task<bool> IsAllowSettingsAsync(ITelegramBotClient client, long chatId, long userId,
        ChatType chatType, CancellationToken ct)
    {
        var member = await client.GetChatMember(chatId, userId, ct);
        return member.Status is ChatMemberStatus.Creator or ChatMemberStatus.Administrator 
               || chatType == ChatType.Private;
    }

    private async Task LinkHandlerAsync(ITelegramBotClient client, ParsedMessage message, CancellationToken ct)
    {
        switch (message.ChatType)
        {
            case ChatType.Group:
            case ChatType.Supergroup:
            case ChatType.Private:
            {
                if (message.Settings.Mention && !message.IsBotMention)
                    break;

                await ProcessLinkAsync(client, message, ct);
                break;
            }
            default:
                return;
        }
    }

    private async Task ProcessLinkAsync(ITelegramBotClient client, ParsedMessage message, CancellationToken ct)
    {
        await client.SetMessageReaction(message.ChatId, message.Id, ["\ud83d\udc40"],
            cancellationToken: ct);

        var media = await scraperService.GetMediaAsync(message.VideoLink!);

        switch (media.Type)
        {
            case MediaType.Video:
                await client.SendVideo(
                    chatId: message.ChatId,
                    video: media.Stream,
                    messageThreadId: message.TreadId,
                    disableNotification: message.Settings.Notification,
                    cancellationToken: ct);
                break;

            case MediaType.Photo:
                await client.SendPhoto(
                    chatId: message.ChatId,
                    photo: media.Stream,
                    messageThreadId: message.TreadId,
                    disableNotification: message.Settings.Notification,
                    cancellationToken: ct);
                break;
        }

        await client.SetMessageReaction(message.ChatId, message.Id, ["\ud83d\udcaf"],
            cancellationToken: ct);
        
        if (message.Settings.DeleteOriginMessage)
            await client.DeleteMessage(message.ChatId, message.Id, ct);
    }
}
