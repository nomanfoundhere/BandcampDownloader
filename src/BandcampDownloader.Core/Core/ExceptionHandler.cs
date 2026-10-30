using System;
using System.Diagnostics;
using System.Threading.Tasks;
using BandcampDownloader.Core.Notifications;
using BandcampDownloader.Properties;
using NLog;

namespace BandcampDownloader.Core;

internal interface IExceptionHandler
{
    void RegisterUnhandledExceptionHandler();
}

internal sealed class ExceptionHandler : IExceptionHandler
{
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();
    private readonly IUserNotifier _userNotifier;

    public ExceptionHandler(IUserNotifier userNotifier)
    {
        _userNotifier = userNotifier;
    }

    public void RegisterUnhandledExceptionHandler()
    {
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        HandleException((Exception)e.ExceptionObject);
    }

    private void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
    {
        // Log only: an unobserved task exception is usually already reported by the operation that
        // failed, and showing a modal dialog from a background task would be disruptive. It is not a
        // fatal error: log it as an error, not as fatal.
        LogExceptionAndInnerExceptionsRecursively(e.Exception, LogLevel.Error);
        e.SetObserved();
    }

    private void HandleException(Exception exception)
    {
        LogExceptionAndInnerExceptionsRecursively(exception);

        if (Debugger.IsAttached)
        {
            Debugger.Break();
        }

        _userNotifier.ShowError(string.Format(Resources.messageBoxUnhandledException, Constants.URL_ISSUES));
    }

    /// <summary>
    /// Writes the specified <see cref="Exception" /> and all its InnerExceptions to the log.
    /// </summary>
    private void LogExceptionAndInnerExceptionsRecursively(Exception exception, LogLevel logLevel = null)
    {
        ArgumentNullException.ThrowIfNull(exception);

        logLevel ??= LogLevel.Fatal;
        _logger.Log(logLevel, $"{exception.GetType()} {exception.Message}");
        _logger.Log(logLevel, exception.StackTrace ?? "No stack trace available");

        if (exception.InnerException != null)
        {
            LogExceptionAndInnerExceptionsRecursively(exception.InnerException, logLevel);
        }
    }
}
