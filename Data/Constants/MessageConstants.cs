namespace SaveFromSocialMediaTgBot.Data.Constants;

public static class MessageConstants
{
    public const string SUCCESS = "Success";
    public const string ACCESS_DENIED = "You don't have permisson to settings this chat";
    public const string ERROR_EMPTY_URL = "Generated invalid page URL";
    public const string ERROR_EMPTY_VISITOR_DATA = "Got bad visitor data";
    public const string ERROR_EMPTY_FETCH_FUNC = "The fetch function returned null";
    public const string ERROR_SERIALIZE_VALUE = "Error serializing value for key";
    public const string ERROR_DESERIALIZE_VALUE = "Error deserializing value for key";
    public const string SETTINGS = "Bot settings";
    public const string MENTION_MODE = "Mention mode";
    public const string MENTION_MODE_ON = "With mention (@)";
    public const string MENTION_MODE_OFF = "Without mention";
    public const string NOTIFICATION_MODE = "Notification mode";
    public const string NOTIFICATION_MODE_ON = "With notification";
    public const string NOTIFICATION_MODE_OFF = "Without notification";
    public const string DELETE_ORIGIN_MESSAGE = "Delete the original message";
    public const string DELETE_ORIGIN_MESSAGE_ON = "Enable";
    public const string DELETE_ORIGIN_MESSAGE_OFF = "Disable";
}