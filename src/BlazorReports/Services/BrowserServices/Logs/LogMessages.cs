using Microsoft.Extensions.Logging;

namespace BlazorReports.Services.BrowserServices.Logs;

/// <summary>
/// Log messages for browser services
/// </summary>
public static partial class LogMessages
{
  /// <summary>
  /// Log message for when a report failed to be generated in a browser
  /// </summary>
  /// <param name="logger"> The logger </param>
  /// <param name="error"> The error that occurred </param>
  /// <param name="browserProcessId"> The process id of the browser </param>
  /// <param name="browserDataDirectory"> The data directory of the browser </param>
  [LoggerMessage(
    EventId = 0,
    Level = LogLevel.Error,
    Message = "Failed to generate report for browser with process id {BrowserProcessId} and data directory {BrowserDataDirectory}"
  )]
  public static partial void BrowserGenerateReportFailed(
    ILogger logger,
    Exception error,
    int browserProcessId,
    string browserDataDirectory
  );

  /// <summary>
  /// Log message for when a browser page failed to be created
  /// </summary>
  /// <param name="logger"> The logger </param>
  /// <param name="error"> The error that occurred </param>
  /// <param name="browserProcessId"> The process id of the browser </param>
  /// <param name="browserDataDirectory"> The data directory of the browser </param>
  [LoggerMessage(
    EventId = 1,
    Level = LogLevel.Error,
    Message = "Failed to create browser page for browser with process id {BrowserProcessId} and data directory {BrowserDataDirectory}"
  )]
  public static partial void BrowserCreatePageFailed(
    ILogger logger,
    Exception error,
    int browserProcessId,
    string browserDataDirectory
  );

  /// <summary>
  /// Log message for when a browser is being disposed
  /// </summary>
  /// <param name="logger"> The logger </param>
  /// <param name="browserProcessId"> The process id of the browser </param>
  [LoggerMessage(
    EventId = 2,
    Level = LogLevel.Debug,
    Message = "Disposing of browser with process id: {BrowserProcessId}"
  )]
  public static partial void BrowserDispose(ILogger logger, int browserProcessId);

  // Browser Services Messages

  /// <summary>
  /// Log message for when the browser pool limit has been reached and the retry time has expired
  /// </summary>
  /// <param name="logger"> The logger </param>
  [LoggerMessage(
    EventId = 3,
    Level = LogLevel.Warning,
    Message = "Browser pool limit reached and retry time expired, server could not handle request"
  )]
  public static partial void BrowserPoolLimitReachedAndRetryTimeExpired(ILogger logger);

  /// <summary>
  /// Log message for when the browser failed to start
  /// </summary>
  /// <param name="logger"> The logger </param>
  [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Failed to start browser")]
  public static partial void FailedToStartBrowser(ILogger logger);

  /// <summary>
  /// Log message for when the browser failed to start
  /// </summary>
  /// <param name="logger"> The logger </param>
  /// <param name="error"> The error that occurred </param>
  [LoggerMessage(EventId = 5, Level = LogLevel.Error, Message = "Failed to start browser")]
  public static partial void FailedToStartBrowser(ILogger logger, Exception error);

  /// <summary>
  /// Log message for when a new browser instance is being created
  /// </summary>
  /// <param name="logger"> The logger </param>
  [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "Creating new browser instance")]
  public static partial void CreatingNewBrowserInstance(ILogger logger);

  /// <summary>
  /// Log message for when a browser failed to be created
  /// </summary>
  /// <param name="logger"> The logger </param>
  /// <param name="error"> The error that occurred </param>
  [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "Failed to create browser")]
  public static partial void FailedToCreateBrowser(ILogger logger, Exception error);

  /// <summary>
  /// Log message for when a browser failed to be created
  /// </summary>
  /// <param name="logger"> The logger </param>
  [LoggerMessage(EventId = 8, Level = LogLevel.Error, Message = "Failed to create browser")]
  public static partial void FailedToCreateBrowser(ILogger logger);

  /// <summary>
  /// Log message for disposing of a browser service
  /// </summary>
  /// <param name="logger"> The logger </param>
  [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "Disposing of browser service")]
  public static partial void DisposingOfBrowserService(ILogger logger);

  // Browser Factory Messages

  /// <summary>
  /// Log message for when the DevToolsActivePort file could not be read
  /// </summary>
  /// <param name="logger"> The logger </param>
  /// <param name="error"> The error that occurred </param>
  /// <param name="devToolsActivePortFile"> The dev tools active port file </param>
  [LoggerMessage(EventId = 10, Level = LogLevel.Error, Message = "Could not read DevToolsActivePort file '{DevToolsActivePortFile}'")]
  public static partial void CouldNotReadDevToolsActivePort(ILogger logger, Exception error, string devToolsActivePortFile);

  /// <summary>
  /// Log message for the browser data directory being used
  /// </summary>
  /// <param name="logger"> The logger </param>
  /// <param name="browserDataDirectory"> The browser data directory </param>
  [LoggerMessage(EventId = 11, Level = LogLevel.Debug, Message = "Data directory used: {BrowserDataDirectory}")]
  public static partial void BrowserDataDirectoryUsed(ILogger logger, string browserDataDirectory);

  /// <summary>
  /// Log message for when the chromium process is being started
  /// </summary>
  /// <param name="logger"> The logger </param>
  /// <param name="chromiumArguments"> The chromium arguments </param>
  [LoggerMessage(EventId = 12, Level = LogLevel.Debug, Message = "Starting Chromium process with arguments \'{ChromiumArguments}\'")]
  public static partial void StartingChromiumProcess(ILogger logger, string chromiumArguments);

  /// <summary>
  /// Log message for when the chromium process exited
  /// </summary>
  /// <param name="logger"> The logger </param>
  /// <param name="error"> The error that occurred </param>
  /// <param name="processExitCode"> The process exit code </param>
  [LoggerMessage(EventId = 13, Level = LogLevel.Error, Message = "Chromium process exited with code \'{ProcessExitCode}\'")]
  public static partial void ChromiumProcessCrashed(ILogger logger, Exception? error, int processExitCode);
}
