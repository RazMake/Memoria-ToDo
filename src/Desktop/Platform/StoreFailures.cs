using System.IO;
using Memoria.ToDo.Shared.Platform;
using Serilog;

namespace Memoria.ToDo.Desktop.Platform;

/// <summary>The messages for failures any store call can throw, and a way to run a call that reports them.</summary>
public static class StoreFailures
{
    /// <summary>
    /// "There is no repository folder yet. Open Configure and choose one." for NoRepositoryFolderException;
    /// the failure's own message for IOException, UnauthorizedAccessException, InvalidDataException,
    /// and ArgumentException; null for any other failure.
    /// </summary>
    public static string? MessageOf(Exception failure) => failure switch
    {
        NoRepositoryFolderException => "There is no repository folder yet. Open Configure and choose one.",
        IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException => failure.Message,
        _ => null,
    };

    /// <summary>
    /// "Saved, but the app couldn't finish updating: " followed by the message of the first failure inside
    /// <paramref name="failure"/>, the one message every window shows for a change a store saved before a notice failed.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static string NoticeFailed(NoticeFailedException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return "Saved, but the app couldn't finish updating: " + failure.InnerExceptions[0].Message;
    }

    /// <summary>
    /// Runs the call and returns true. When it throws a failure <paramref name="messageOf"/> gives a message for, logs
    /// the failure at Error, passes the message to <paramref name="report"/> and returns false. When it throws
    /// <see cref="NoticeFailedException"/>, the change was saved: logs it at Error, passes <see cref="NoticeFailed"/>'s message to
    /// <paramref name="report"/> and returns true, without asking <paramref name="messageOf"/>. Any other failure passes
    /// on, unlogged, since whoever ends it logs it.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any argument is null.</exception>
    public static bool Run(Action call, Func<Exception, string?> messageOf, Action<string> report, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(call);
        ArgumentNullException.ThrowIfNull(messageOf);
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(logger);
        try
        {
            call();
            return true;
        }
        catch (NoticeFailedException failure)
        {
            var message = NoticeFailed(failure);
            logger.Error(failure, "A change was saved, but a notice failed: {Reason}", message);
            report(message);
            return true;
        }
        catch (Exception failure) when (messageOf(failure) is { } message)
        {
            logger.Error(failure, "A store call failed: {Reason}", message);
            report(message);
            return false;
        }
    }
}
