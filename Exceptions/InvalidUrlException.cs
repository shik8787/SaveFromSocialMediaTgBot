namespace SaveFromSocialMediaTgBot.Exceptions;

/// <summary>
/// The exception that is thrown when a url is invalid or not supported.
/// </summary>
public class InvalidUrlException() : Exception(MESSAGE)
{
    private const string MESSAGE = "Unsupported or error link";
}