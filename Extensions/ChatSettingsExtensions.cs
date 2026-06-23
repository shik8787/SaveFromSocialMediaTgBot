using SaveFromSocialMediaTgBot.Data.Constants;
using SaveFromSocialMediaTgBot.Data.Models;
using Telegram.Bot.Types.ReplyMarkups;

namespace SaveFromSocialMediaTgBot.Extensions;

public static class ChatSettingsExtensions
{
    public static string BuildSettingsText(this ChatSettings s) =>
        $"⚙️ {MessageConstants.SETTINGS}\n" +
        $"• {MessageConstants.MENTION_MODE}: {(s.Mention
            ? MessageConstants.MENTION_MODE_ON
            : MessageConstants.MENTION_MODE_OFF)}\n" +
        $"• {MessageConstants.NOTIFICATION_MODE}: {(s.Notification
            ? MessageConstants.NOTIFICATION_MODE_ON
            : MessageConstants.NOTIFICATION_MODE_OFF)}\n" +
        $"• {MessageConstants.DELETE_ORIGIN_MESSAGE}: {(s.DeleteOriginMessage
            ? MessageConstants.DELETE_ORIGIN_MESSAGE_ON
            : MessageConstants.DELETE_ORIGIN_MESSAGE_OFF)}\n";

    public static InlineKeyboardMarkup BuildSettingsKeyboard(this ChatSettings s) =>
        new([
            [
                InlineKeyboardButton.WithCallbackData(
                    s.Mention
                        ? ButtonConstants.MENTION_MODE_OFF
                        : ButtonConstants.MENTION_MODE_ON,
                    $"{CommandConstants.MENTION_MODE}:{(!s.Mention).ToString().ToLower()}")
            ],
            [
                InlineKeyboardButton.WithCallbackData(
                    s.Notification
                        ? ButtonConstants.NOTIFICATION_MODE_OFF
                        : ButtonConstants.NOTIFICATION_MODE_ON,
                    $"{CommandConstants.NOTIFICATION_MODE}:{(!s.Notification).ToString().ToLower()}")
            ],
            [
                InlineKeyboardButton.WithCallbackData(
                    s.DeleteOriginMessage
                        ? ButtonConstants.DELETE_ORIGIN_MESSAGE_OFF
                        : ButtonConstants.DELETE_ORIGIN_MESSAGE_ON,
                    $"{CommandConstants.DELETE_ORIGIN_MESSAGE}:{(!s.DeleteOriginMessage).ToString().ToLower()}")
            ],
            [
                InlineKeyboardButton.WithCallbackData(ButtonConstants.CLOSE_SETTINGS,
                    CommandConstants.CLOSE_SETTINGS)
            ]
        ]);
}